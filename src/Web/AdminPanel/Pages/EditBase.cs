// <copyright file="EditBase.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Web.AdminPanel.Pages;

using System.Reflection;
using System.Threading;
using Blazored.Modal.Services;
using Blazored.Toast.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.Persistence;
using MUnique.OpenMU.Web.AdminPanel;
using MUnique.OpenMU.Web.AdminPanel.Services;

/// <summary>
/// Lớp cơ sở trừu tượng chung cho một trang chỉnh sửa.
/// </summary>
public abstract class EditBase : ComponentBase, IAsyncDisposable
{
    private object? _model;
    private Type? _type;
    private bool _isOwningContext;
    private IContext? _persistenceContext;
    private CancellationTokenSource? _disposeCts;
    private DataLoadingState _loadingState;
    private Task? _loadTask;
    private IDisposable? _modalDisposable;
    private IDisposable? _navigationLockDisposable;

    private enum DataLoadingState
    {
        NotLoadedYet,

        LoadingStarted,

        Loading,

        Loaded,

        NotFound,

        Error,

        Cancelled,
    }

    /// <summary>
    /// Lấy hoặc thiết lập định danh của đối tượng mà nên được chỉnh sửa.
    /// </summary>
    [Parameter]
    public Guid Id { get; set; }

    /// <summary>
    /// Lấy hoặc thiết lập <see cref="Type.FullName"/> của đối tượng mà nên được chỉnh sửa.
    /// </summary>
    [Parameter]
    public string TypeString { get; set; } = string.Empty;

    /// <summary>
    /// Lấy hoặc thiết lập nhà cung cấp ngữ cảnh lưu trữ mà tải và lưu đối tượng.
    /// </summary>
    [Inject]
    public IPersistenceContextProvider PersistenceContextProvider { get; set; } = null!;

    /// <summary>
    /// Lấy hoặc thiết lập dịch vụ modal.
    /// </summary>
    [Inject]
    public IModalService ModalService { get; set; } = null!;

    /// <summary>
    /// Lấy hoặc thiết lập dịch vụ thông báo.
    /// </summary>
    [Inject]
    public IToastService ToastService { get; set; } = null!;

    /// <summary>
    /// Lấy hoặc thiết lập nguồn dữ liệu cấu hình.
    /// </summary>
    [Inject]
    public IDataSource<GameConfiguration> ConfigDataSource { get; set; } = null!;

    /// <summary>
    /// Lấy hoặc thiết lập quản lý điều hướng.
    /// </summary>
    [Inject]
    public NavigationManager NavigationManager { get; set; } = null!;

    /// <summary>
    /// Lấy hoặc thiết lập runtime java script.
    /// </summary>
    [Inject]
    public IJSRuntime JavaScript { get; set; } = null!;

    /// <summary>
    /// Lấy hoặc thiết lập logger.
    /// </summary>
    [Inject]
    public ILogger<EditBase>? Logger { get; set; }

    /// <summary>
    /// Lấy nguồn dữ liệu của loại mà đang được chỉnh sửa.
    /// </summary>
    protected virtual IDataSource EditDataSource => this.ConfigDataSource;

    /// <summary>
    /// Lấy mô hình mà nên được chỉnh sửa.
    /// </summary>
    protected object? Model => this._model;

    /// <summary>
    /// Lấy loại.
    /// </summary>
    protected virtual Type? Type => this._type ??= this.DetermineTypeByTypeString();

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        this._navigationLockDisposable?.Dispose();
        this._navigationLockDisposable = null;

        await (this._disposeCts?.CancelAsync() ?? Task.CompletedTask).ConfigureAwait(false);
        this._disposeCts?.Dispose();
        this._disposeCts = null;

        await (this._loadTask ?? Task.CompletedTask).ConfigureAwait(false);
        await this.EditDataSource.DiscardChangesAsync().ConfigureAwait(true);

        if (this._isOwningContext)
        {
            this._persistenceContext?.Dispose();
        }

        this._persistenceContext = null;
    }

    /// <inheritdoc />
    public override async Task SetParametersAsync(ParameterView parameters)
    {
        this._model = null;
        await (this._disposeCts?.CancelAsync() ?? Task.CompletedTask).ConfigureAwait(false);
        this._disposeCts?.Dispose();
        await (this._loadTask ?? Task.CompletedTask).ConfigureAwait(true);
        await base.SetParametersAsync(parameters).ConfigureAwait(true);
    }

    /// <inheritdoc />
    protected override async Task OnParametersSetAsync()
    {
        this._loadingState = DataLoadingState.LoadingStarted;
        var cts = new CancellationTokenSource();
        this._disposeCts = cts;
        this._type = null;
        this._loadTask = Task.Run(() => this.LoadDataAsync(cts.Token), cts.Token);

        await base.OnParametersSetAsync().ConfigureAwait(true);
    }

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        if (this.Model is null)
        {
            return;
        }

        var downloadMarkup = this.GetDownloadMarkup();
        var editorsMarkup = this.GetEditorsMarkup();
        builder.AddMarkupContent(0, $"<h1>Chỉnh sửa {CaptionHelper.GetTypeCaption(this.Type!)}</h1>{downloadMarkup}{editorsMarkup}\r\n");
        builder.OpenComponent<CascadingValue<IContext>>(1);
        builder.AddAttribute(2, nameof(CascadingValue<IContext>.Value), this._persistenceContext);
        builder.AddAttribute(3, nameof(CascadingValue<IContext>.IsFixed), this._isOwningContext);
        builder.AddAttribute(4, nameof(CascadingValue<IContext>.ChildContent), (RenderFragment)(builder2 =>
        {
            var sequence = 4;
            this.AddFormToRenderTree(builder2, ref sequence);
        }));

        builder.CloseComponent();
    }

    /// <inheritdoc />
    protected override Task OnInitializedAsync()
    {
        this._navigationLockDisposable = this.NavigationManager.RegisterLocationChangingHandler(this.OnBeforeInternalNavigationAsync);
        return base.OnInitializedAsync();
    }

    /// <summary>
    /// Thêm biểu mẫu vào cây render.
    /// </summary>
    /// <param name="builder">Trình xây dựng.</param>
    /// <param name="currentSequence">Chuỗi hiện tại.</param>
    protected abstract void AddFormToRenderTree(RenderTreeBuilder builder, ref int currentSequence);
    
    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (this._loadingState is not DataLoadingState.Loading && this._modalDisposable is { } modal)
        {
            modal.Dispose();
            this._modalDisposable = null;
        }

        if (this._loadingState == DataLoadingState.LoadingStarted)
        {
            this._loadingState = DataLoadingState.Loading;

            await this.InvokeAsync(() =>
            {
                if (this._loadingState != DataLoadingState.Loaded)
                {
                    this._modalDisposable = this.ModalService.ShowLoadingIndicator();
                    this.StateHasChanged();
                }
            }).ConfigureAwait(false);
        }

        await base.OnAfterRenderAsync(firstRender).ConfigureAwait(true);
    }

    /// <summary>
    /// Lưu các thay đổi.
    /// </summary>
    protected async Task SaveChangesAsync()
    {
        try
        {
            if (this._persistenceContext is { } context)
            {
                var success = await context.SaveChangesAsync().ConfigureAwait(true);
                var text = success ? "Các thay đổi đã được lưu." : "Không có thay đổi nào để lưu.";
                this.ToastService.ShowSuccess(text);
            }
            else
            {
                this.ToastService.ShowError("Thất bại, ngữ cảnh chưa được khởi tạo");
            }
        }
        catch (Exception ex)
        {
            this.Logger?.LogError(ex, $"Lỗi trong quá trình lưu {this.Id}");
            var text = $"Đã xảy ra lỗi không mong muốn: {ex.Message}.";
            this.ToastService.ShowError(text);
        }
    }

    /// <summary>
    /// Lấy mã HTML cho các trình chỉnh sửa tùy chọn cho loại hiện tại.
    /// </summary>
    /// <returns>Mã HTML cho các trình chỉnh sửa tùy chọn cho loại hiện tại.</returns>
    protected virtual string? GetEditorsMarkup()
    {
        return null;
    }

    /// <summary>
    /// Tải chủ sở hữu của <see cref="EditDataSource" />.
    /// </summary>
    /// <param name="cancellationToken">Mã hủy.</param>
    protected virtual async ValueTask LoadOwnerAsync(CancellationToken cancellationToken)
    {
        await this.EditDataSource.GetOwnerAsync(Guid.Empty, cancellationToken).ConfigureAwait(true);
    }

    private async ValueTask OnBeforeInternalNavigationAsync(LocationChangingContext context)
    {
        if (this._persistenceContext?.HasChanges is true)
        {
            var isConfirmed = await this.JavaScript.InvokeAsync<bool>("window.confirm",
                    "Có thay đổi chưa được lưu. Bạn có chắc chắn muốn bỏ qua chúng không?")
                .ConfigureAwait(true);

            if (!isConfirmed)
            {
                context.PreventNavigation();
            }
            else if (this._isOwningContext)
            {
                this._persistenceContext.Dispose();
                this._persistenceContext = null;
            }
            else
            {
                await this.EditDataSource.DiscardChangesAsync().ConfigureAwait(true);
            }
        }
    }

    private string? GetDownloadMarkup()
    {
        if (this.Type is not null && GenericControllerFeatureProvider.SupportedTypes.Any(t => t.Item1 == this.Type))
        {
            var uri = $"/download/{this.Type.Name}/{this.Type.Name}_{this.Id}.json";
            return $"<p>Tải xuống dưới dạng json: <a href=\"{uri}\" download><span class=\"oi oi-data-transfer-download\"></span></a></p>";
        }

        return null;
    }

    private Type? DetermineTypeByTypeString()
    {
        return AppDomain.CurrentDomain.GetAssemblies().Where(assembly => assembly.FullName?.StartsWith(nameof(MUnique)) ?? false)
            .Select(assembly => assembly.GetType(this.TypeString)).FirstOrDefault(t => t != null);
    }

    private async Task LoadDataAsync(CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (this.Type is null)
            {
                throw new InvalidOperationException($"Chỉ các loại trong namespace {nameof(MUnique)} có thể được chỉnh sửa trên trang này.");
            }

            await this.LoadOwnerAsync(cancellationToken).ConfigureAwait(true);
            cancellationToken.ThrowIfCancellationRequested();
            if (this.EditDataSource.IsSupporting(this.Type))
            {
                this._isOwningContext = false;
                this._persistenceContext = await this.EditDataSource.GetContextAsync(cancellationToken).ConfigureAwait(true);
            }
            else
            {
                this._isOwningContext = true;
                var gameConfiguration = await this.ConfigDataSource.GetOwnerAsync(Guid.Empty, cancellationToken).ConfigureAwait(true);
                var createContextMethod = typeof(IPersistenceContextProvider).GetMethod(nameof(IPersistenceContextProvider.CreateNewTypedContext))!.MakeGenericMethod(this.Type);
                this._persistenceContext = (IContext)createContextMethod.Invoke(this.PersistenceContextProvider, new object[] { true, gameConfiguration})!;
            }

            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                if (this.EditDataSource.IsSupporting(this.Type))
                {
                    this._model = this.Id == default
                        ? this.EditDataSource.GetAll(this.Type).OfType<object>().FirstOrDefault()
                        : this.EditDataSource.Get(this.Id);
                }
                else
                {
                    this._model = this.Id == default
                        ? (await this._persistenceContext.GetAsync(this.Type, cancellationToken).ConfigureAwait(true)).OfType<object>().FirstOrDefault()
                        : await this._persistenceContext.GetByIdAsync(this.Id, this.Type, cancellationToken).ConfigureAwait(true);
                }

                this._loadingState = this.Model is not null
                    ? DataLoadingState.Loaded
                    : DataLoadingState.NotFound;
            }
            catch (OperationCanceledException)
            {
                this._loadingState = DataLoadingState.Cancelled;
                throw;
            }
            catch (Exception ex)
            {
                this._loadingState = DataLoadingState.Error;
                this.Logger?.LogError(ex, $"Không thể tải {this.Type.FullName} với {this.Id}: {ex.Message}{Environment.NewLine}{ex.StackTrace}");
                await this.InvokeAsync(() => this.ModalService.ShowMessageAsync("Lỗi", "Không thể tải dữ liệu. Kiểm tra nhật ký (logs) để biết chi tiết.")).ConfigureAwait(false);
            }

            cancellationToken.ThrowIfCancellationRequested();
            await this.InvokeAsync(this.StateHasChanged).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            // expected when the page is getting disposed.
        }
        catch (TargetInvocationException ex) when (ex.InnerException is ObjectDisposedException)
        {
            // See ObjectDisposedException.
        }
        catch (ObjectDisposedException)
        {
            // Xảy ra khi người dùng điều hướng đi (không nên xảy ra với chỉ báo tải modal, nhưng chúng tôi kiểm tra nó để đảm bảo).
            // Sẽ thật tuyệt nếu có một api async với hỗ trợ mã hủy trong lớp lưu trữ
            // Hiện tại, chúng tôi sẽ bỏ qua ngoại lệ
        }
        catch (Exception ex)
        {
            this.Logger?.LogError(ex, "Lỗi không mong đợi khi tải dữ liệu: {ex}", ex);
        }
    }
}