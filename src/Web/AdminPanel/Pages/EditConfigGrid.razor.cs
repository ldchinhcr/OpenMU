// <copyright file="EditConfigGrid.razor.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Web.AdminPanel.Pages;

using System.Collections;
using System.ComponentModel;
using System.Threading;
using Blazored.Modal;
using Blazored.Modal.Services;
using Blazored.Toast.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.QuickGrid;
using Microsoft.Extensions.Logging;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.Persistence;
using MUnique.OpenMU.Web.AdminPanel.Components.Form;

/// <summary>
/// Trang Razor hiển thị các đối tượng của loại đã chỉ định trong một lưới.
/// </summary>
public partial class EditConfigGrid : ComponentBase, IAsyncDisposable
{
    private readonly PaginationState _pagination = new() { ItemsPerPage = 20 };

    private Task? _loadTask;
    private CancellationTokenSource? _disposeCts;

    private List<ViewModel>? _viewModels;

    /// <summary>
    /// Lấy hoặc thiết lập <see cref="Type.FullName"/> của đối tượng mà nên được chỉnh sửa.
    /// </summary>
    [Parameter]
    public string TypeString { get; set; } = string.Empty;

    /// <summary>
    /// Lấy hoặc thiết lập nguồn dữ liệu.
    /// </summary>
    [Inject]
    public IDataSource<GameConfiguration> DataSource { get; set; } = null!;

    /// <summary>
    /// Lấy hoặc thiết lập trình quản lý điều hướng.
    /// </summary>
    [Inject]
    public NavigationManager NavigationManager { get; set; } = null!;

    /// <summary>
    /// Lấy hoặc thiết lập nhà cung cấp ngữ cảnh lưu trữ, người sẽ tải và lưu đối tượng.
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
    /// Lấy hoặc thiết lập logger.
    /// </summary>
    [Inject]
    public ILogger<EditConfigGrid> Logger { get; set; } = null!;

    /// <summary>
    /// Lấy hoặc thiết lập loại.
    /// </summary>
    private Type? Type { get; set; }

    private IQueryable<ViewModel>? ViewModels
    {
        get
        {
            if (string.IsNullOrWhiteSpace(this.NameFilter))
            {
                return this._viewModels?.AsQueryable();
            }

            return this._viewModels?
                .Where(vm => vm.Name.Contains(this.NameFilter.Trim(), StringComparison.InvariantCultureIgnoreCase))
                .AsQueryable();
        }
    }

    private string? NameFilter { get; set; }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await (this._disposeCts?.CancelAsync() ?? Task.CompletedTask).ConfigureAwait(false);
        this._disposeCts?.Dispose();
        this._disposeCts = null;

        try
        {
            await (this._loadTask ?? Task.CompletedTask).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // chúng ta có thể bỏ qua điều đó ...
        }
        catch
        {
            // và chúng ta không nên ném ngoại lệ trong phương thức dispose ...
        }
    }

    /// <inheritdoc />
    protected override async Task OnParametersSetAsync()
    {
        this.NameFilter = string.Empty;
        this.Type = this.DetermineTypeByTypeString();
        var cts = new CancellationTokenSource();
        this._disposeCts = cts;
        this._loadTask = Task.Run(() => this.LoadDataAsync(cts.Token), cts.Token);
        await base.OnParametersSetAsync().ConfigureAwait(true);
    }

    private async Task LoadDataAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (this.Type is null)
        {
            throw new InvalidOperationException($"Chỉ các loại trong không gian tên {nameof(MUnique)} có thể được chỉnh sửa trên trang này.");
        }

        IEnumerable data;
        var gameConfiguration = await this.DataSource.GetOwnerAsync(default, cancellationToken).ConfigureAwait(true);
        if (this.DataSource.IsSupporting(this.Type))
        {
            cancellationToken.ThrowIfCancellationRequested();
            data = this.DataSource.GetAll(this.Type!);
        }
        else
        {
            var createContextMethod = typeof(IPersistenceContextProvider).GetMethod(nameof(IPersistenceContextProvider.CreateNewTypedContext))!.MakeGenericMethod(this.Type);
            using var context = (IContext)createContextMethod.Invoke(this.PersistenceContextProvider, new object[] { true, gameConfiguration })!;
            data = await context.GetAsync(this.Type, cancellationToken);
        }

        this._viewModels = data.OfType<object>()
            .Select(o => new ViewModel(o))
            .OrderBy(o => o.Name)
            .ToList();

        await this.InvokeAsync(async () =>
        {
            await this._pagination.SetCurrentPageIndexAsync(0).ConfigureAwait(true);
            this.StateHasChanged();
        }).ConfigureAwait(false);
    }

    private Type? DetermineTypeByTypeString()
    {
        return AppDomain.CurrentDomain.GetAssemblies().Where(assembly => assembly.FullName?.StartsWith(nameof(MUnique)) ?? false)
            .Select(assembly => assembly.GetType(this.TypeString)).FirstOrDefault(t => t != null);
    }

    private async Task OnDeleteButtonClickAsync(ViewModel viewModel)
    {
        try
        {
            var dialogResult = await this.ModalService.ShowQuestionAsync("Bạn có chắc chắn không?", $"Bạn sắp xóa '{viewModel.Name}'. Bạn có chắc chắn không?");
            if (!dialogResult)
            {
                return;
            }

            var cancellationToken = this._disposeCts?.Token ?? default;
            var gameConfiguration = await this.DataSource.GetOwnerAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
            using var deleteContext = this.PersistenceContextProvider.CreateNewContext(gameConfiguration);
            deleteContext.Attach(viewModel.Parent);
            await deleteContext.DeleteAsync(viewModel.Parent).ConfigureAwait(false);
            await deleteContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            this.ToastService.ShowSuccess($"Đã xóa '{viewModel.Name}' thành công.");
            this._viewModels = null;
            this._loadTask = Task.Run(() => this.LoadDataAsync(cancellationToken), cancellationToken);
        }
        catch (Exception ex)
        {
            this.Logger.LogError(ex, $"Không thể xóa '{viewModel.Name}', có thể vì nó được tham chiếu bởi một đối tượng khác.");
            this.ToastService.ShowError($"Không thể xóa '{viewModel.Name}', có thể vì nó được tham chiếu bởi một đối tượng khác. Để biết thêm chi tiết, xem nhật ký");
        }
    }

    private async Task OnCreateButtonClickAsync()
    {
        var cancellationToken = this._disposeCts?.Token ?? default;
        var gameConfiguration = await this.DataSource.GetOwnerAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        using var creationContext = this.PersistenceContextProvider.CreateNewContext(gameConfiguration);
        var newObject = creationContext.CreateNew(this.Type!);
        var parameters = new ModalParameters();
        var modalType = typeof(ModalCreateNew<>).MakeGenericType(this.Type!);

        parameters.Add(nameof(ModalCreateNew<object>.Item), newObject);
        var options = new ModalOptions
        {
            DisableBackgroundCancel = true,
        };

        var modal = this.ModalService.Show(modalType, $"Tạo mới", parameters, options);
        var result = await modal.Result.ConfigureAwait(false);
        if (!result.Cancelled)
        {
            await creationContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            this.ToastService.ShowSuccess("Đối tượng mới đã được tạo thành công.");
            this._viewModels = null;
            this._loadTask = Task.Run(() => this.LoadDataAsync(cancellationToken), cancellationToken);
        }
    }

    /// <summary>
    /// Mô hình xem cho lưới.
    /// Chúng tôi sử dụng điều này thay vì các đối tượng, vì nó làm cho mã đơn giản hơn.
    /// Tạo các thành phần tổng quát là một chút phức tạp khi bạn không
    /// có loại như tham số loại tổng quát.
    /// </summary>
    public class ViewModel
    {
        /// <summary>
        /// Khởi tạo một thể hiện mới của lớp <see cref="ViewModel"/>.
        /// </summary>
        /// <param name="parent">Đối tượng cha.</param>
        public ViewModel(object parent)
        {
            this.Parent = parent;
            this.Id = parent.GetId();
            this.Name = parent.GetName();
        }

        /// <summary>
        /// Lấy đối tượng cha, đối tượng này được hiển thị.
        /// </summary>
        [Browsable(false)]
        public object Parent { get; }

        /// <summary>
        /// Lấy hoặc thiết lập định danh của đối tượng.
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// Lấy hoặc thiết lập tên của đối tượng.
        /// </summary>
        public string Name { get; set; }
    }
}