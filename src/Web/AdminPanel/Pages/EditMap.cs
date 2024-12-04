// <copyright file="EditMap.cs" company="MUnique">
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
using MUnique.OpenMU.Web.AdminPanel.Components;

/// <summary>
/// Một trang, hiển thị <see cref="MapEditor"/> cho tất cả <see cref="GameConfiguration.Maps"/>.
/// </summary>
[Route("/map-editor")]
[Route("/map-editor/{SelectedMapId:guid}")]
public sealed class EditMap : ComponentBase, IDisposable
{
    private List<GameMapDefinition>? _maps;
    private CancellationTokenSource? _disposeCts;
    private IContext? _context;
    private IDisposable? _navigationLockDisposable;

    /// <summary>
    /// Lấy hoặc thiết lập định danh bản đồ đã chọn.
    /// </summary>
    [Parameter]
    public Guid SelectedMapId { get; set; }

    /// <summary>
    /// Lấy hoặc thiết lập dịch vụ modal.
    /// </summary>
    [Inject]
    private IModalService ModalService { get; set; } = null!;

    /// <summary>
    /// Lấy hoặc thiết lập dịch vụ thông báo.
    /// </summary>
    [Inject]
    private IToastService ToastService { get; set; } = null!;

    /// <summary>
    /// Lấy hoặc thiết lập cấu hình trò chơi.
    /// </summary>
    [Inject]
    private IDataSource<GameConfiguration> GameConfigurationSource { get; set; } = null!;

    [Inject]
    private ILogger<EditMap> Logger { get; set; } = null!;

    /// <summary>
    /// Lấy hoặc thiết lập trình quản lý điều hướng.
    /// </summary>
    [Inject]
    public NavigationManager NavigationManager { get; set; } = null!;

    /// <summary>
    /// Lấy hoặc thiết lập runtime java script.
    /// </summary>
    [Inject]
    public IJSRuntime JavaScript { get; set; } = null!;

    /// <inheritdoc />
    public void Dispose()
    {
        this._disposeCts?.Cancel();
        this._disposeCts?.Dispose();
        this._disposeCts = null;

        this._navigationLockDisposable?.Dispose();
        this._navigationLockDisposable = null;
    }

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        if (this._maps is { })
        {
            builder.OpenComponent<CascadingValue<IContext>>(1);
            builder.AddAttribute(2, nameof(CascadingValue<IContext>.Value), this._context);
            builder.AddAttribute(3, nameof(CascadingValue<IContext>.IsFixed), false);
            builder.AddAttribute(4, nameof(CascadingValue<IContext>.ChildContent), (RenderFragment)(builder2 =>
            {
                builder2.OpenComponent(5, typeof(MapEditor));
                builder2.AddAttribute(6, nameof(MapEditor.Maps), this._maps);
                builder2.AddAttribute(7, nameof(MapEditor.SelectedMapId), this.SelectedMapId);
                builder2.AddAttribute(8, nameof(MapEditor.OnValidSubmit), EventCallback.Factory.Create(this, this.SaveChangesAsync));
                builder2.AddAttribute(9, nameof(MapEditor.SelectedMapChanging), EventCallback.Factory.Create<MapEditor.MapChangingArgs>(this, this.OnSelectedMapChanging));
                builder2.CloseComponent();
            }));

            builder.CloseComponent();
        }
    }

    /// <inheritdoc />
    protected override async Task OnParametersSetAsync()
    {
        await (this._disposeCts?.CancelAsync() ?? Task.CompletedTask).ConfigureAwait(false);
        this._disposeCts?.Dispose();
        this._disposeCts = new CancellationTokenSource();

        this._context = await this.GameConfigurationSource.GetContextAsync(this._disposeCts.Token).ConfigureAwait(false);

        await base.OnParametersSetAsync().ConfigureAwait(false);
    }

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        await base.OnAfterRenderAsync(firstRender).ConfigureAwait(false);
        if (this._maps is null)
        {
            this._disposeCts ??= new CancellationTokenSource();
            var cts = this._disposeCts.Token;
            _ = Task.Run(() => this.LoadDataAsync(cts), cts);
        }
    }

    /// <inheritdoc />
    protected override Task OnInitializedAsync()
    {
        this._navigationLockDisposable = this.NavigationManager.RegisterLocationChangingHandler(this.OnBeforeInternalNavigation);
        return base.OnInitializedAsync();
    }

    private async ValueTask OnBeforeInternalNavigation(LocationChangingContext context)
    {
        if (!await this.AllowChangeAsync().ConfigureAwait(false))
        {
            context.PreventNavigation();
        }
    }

    private async Task OnSelectedMapChanging(MapEditor.MapChangingArgs eventArgs)
    {
        eventArgs.Cancel = !await this.AllowChangeAsync().ConfigureAwait(true);
        if (!eventArgs.Cancel)
        {
            this.SelectedMapId = eventArgs.NextMap;
        }
    }

    private async ValueTask<bool> AllowChangeAsync()
    {
        var cancellationToken = this._disposeCts?.Token ?? default;
        var persistenceContext = await this.GameConfigurationSource.GetContextAsync(cancellationToken).ConfigureAwait(true);
        if (persistenceContext?.HasChanges is not true)
        {
            return true;
        }

        var isConfirmed = await this.JavaScript.InvokeAsync<bool>(
                "window.confirm",
                cancellationToken,
                "Có thay đổi chưa được lưu. Bạn có chắc chắn muốn bỏ qua chúng không?")
            .ConfigureAwait(true);

        if (!isConfirmed)
        {
            return false;
        }

        await this.GameConfigurationSource.DiscardChangesAsync().ConfigureAwait(true);
        this._maps = null;

        // OnAfterRender sẽ tải lại các bản đồ ...
        return true;
    }

    private async Task LoadDataAsync(CancellationToken cancellationToken)
    {
        IDisposable? modal = null;
        var showModalTask = this.InvokeAsync(() => modal = this.ModalService.ShowLoadingIndicator());

        try
        {
            if (!cancellationToken.IsCancellationRequested)
            {
                var gameConfig = await this.GameConfigurationSource.GetOwnerAsync(Guid.Empty, cancellationToken).ConfigureAwait(false);
                try
                {
                    this._maps = gameConfig.Maps.OrderBy(c => c.Number).ToList();
                }
                catch (Exception ex)
                {
                    this.Logger.LogError(ex, $"Không thể tải các bản đồ trò chơi: {ex.Message}{Environment.NewLine}{ex.StackTrace}");
                    await this.ModalService.ShowMessageAsync("Lỗi", "Không thể tải dữ liệu bản đồ. Kiểm tra nhật ký để biết chi tiết.").ConfigureAwait(false);
                }

                await showModalTask.ConfigureAwait(false);
                modal?.Dispose();
                await this.InvokeAsync(this.StateHasChanged).ConfigureAwait(false);
            }
        }
        catch (TargetInvocationException ex) when (ex.InnerException is ObjectDisposedException)
        {
            // Xem ObjectDisposedException.
        }
        catch (ObjectDisposedException)
        {
            // Xảy ra khi người dùng điều hướng đi nơi khác (không nên xảy ra với chỉ báo tải modal, nhưng chúng tôi kiểm tra nó để đảm bảo).
            // Sẽ thật tuyệt nếu có một api async với hỗ trợ token hủy trong lớp lưu trữ
            // Hiện tại, chúng tôi sẽ bỏ qua ngoại lệ
        }
    }

    private async Task SaveChangesAsync()
    {
        try
        {
            var context = await this.GameConfigurationSource.GetContextAsync().ConfigureAwait(true);
            var success = await context.SaveChangesAsync().ConfigureAwait(true);
            var text = success ? "Các thay đổi đã được lưu." : "Không có thay đổi nào để lưu.";
            this.ToastService.ShowSuccess(text);
        }
        catch (Exception ex)
        {
            this.Logger.LogError(ex, $"Lỗi trong quá trình lưu");
            this.ToastService.ShowError($"Đã xảy ra lỗi không mong muốn: {ex.Message}. Xem nhật ký để biết thêm chi tiết.");
        }
    }
}