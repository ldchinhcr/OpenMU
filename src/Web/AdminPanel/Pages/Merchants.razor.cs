// <copyright file="Merchants.razor.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Web.AdminPanel.Pages;

using System.ComponentModel;
using System.Threading;
using Blazored.Toast.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.QuickGrid;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.DataModel.Entities;
using MUnique.OpenMU.Persistence;

/// <summary>
/// Trang Razor hiển thị các đối tượng của loại đã chỉ định trong một lưới.
/// </summary>
public partial class Merchants : ComponentBase, IAsyncDisposable
{
    private readonly PaginationState _merchantPagination = new() { ItemsPerPage = 20 };
    private readonly PaginationState _itemPagination = new() { ItemsPerPage = 20 };

    private Task? _loadTask;
    private CancellationTokenSource? _disposeCts;
    private List<MerchantStorageViewModel>? _viewModels;
    private MerchantStorageViewModel? _selectedMerchant;
    private IContext? _persistenceContext;
    private IDisposable? _navigationLockDisposable;

    /// <summary>
    /// Lấy hoặc thiết lập nguồn dữ liệu.
    /// </summary>
    [Inject]
    public IDataSource<GameConfiguration> DataSource { get; set; } = null!;

    /// <summary>
    /// Lấy hoặc thiết lập nhà cung cấp ngữ cảnh.
    /// </summary>
    [Inject]
    public IPersistenceContextProvider ContextProvider { get; set; } = null!;

    /// <summary>
    /// Lấy hoặc thiết lập dịch vụ thông báo.
    /// </summary>
    [Inject]
    public IToastService ToastService { get; set; } = null!;

    /// <summary>
    /// Lấy hoặc thiết lập quản lý điều hướng.
    /// </summary>
    [Inject]
    public NavigationManager NavigationManager { get; set; } = null!;

    /// <summary>
    /// Lấy hoặc thiết lập thời gian chạy java script.
    /// </summary>
    [Inject]
    public IJSRuntime JavaScript { get; set; } = null!;

    /// <summary>
    /// Lấy hoặc thiết lập logger.
    /// </summary>
    [Inject]
    public ILogger<Merchants> Logger { get; set; } = null!;

    private IQueryable<MerchantStorageViewModel>? ViewModels => this._viewModels?.AsQueryable();

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        this._navigationLockDisposable?.Dispose();
        this._navigationLockDisposable = null;

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
    protected override Task OnInitializedAsync()
    {
        this._navigationLockDisposable = this.NavigationManager.RegisterLocationChangingHandler(this.OnBeforeInternalNavigationAsync);
        return base.OnInitializedAsync();
    }

    /// <inheritdoc />
    protected override async Task OnParametersSetAsync()
    {
        var cts = new CancellationTokenSource();
        this._disposeCts = cts;
        this._loadTask = Task.Run(() => this.LoadDataAsync(cts.Token), cts.Token);
        await base.OnParametersSetAsync().ConfigureAwait(true);
    }

    private async ValueTask OnBeforeInternalNavigationAsync(LocationChangingContext context)
    {
        if (this._persistenceContext?.HasChanges is not true)
        {
            return;
        }

        var isConfirmed = await this.JavaScript.InvokeAsync<bool>(
                "window.confirm",
                "Có thay đổi chưa được lưu. Bạn có chắc chắn muốn bỏ qua chúng không?")
            .ConfigureAwait(true);

        if (!isConfirmed)
        {
            context.PreventNavigation();
        }
        else
        {
            await this.DataSource.DiscardChangesAsync().ConfigureAwait(true);
        }
    }

    private async Task LoadDataAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        this._persistenceContext = await this.DataSource.GetContextAsync(cancellationToken).ConfigureAwait(true);
        await this.DataSource.GetOwnerAsync(default, cancellationToken).ConfigureAwait(true);
        cancellationToken.ThrowIfCancellationRequested();
        var data = this.DataSource.GetAll<MonsterDefinition>()
            .Where(m => m is { ObjectKind: NpcObjectKind.PassiveNpc, MerchantStore: { } });
        this._viewModels = data
            .Select(o => new MerchantStorageViewModel(o))
            .OrderBy(o => o.Merchant.Designation)
            .ToList();

        await this.InvokeAsync(this.StateHasChanged).ConfigureAwait(false);
    }

    private async Task OnMerchantEditClickAsync(MerchantStorageViewModel context)
    {
        this._selectedMerchant = context;
        await this.InvokeAsync(async () => await this._itemPagination.SetCurrentPageIndexAsync(0).ConfigureAwait(true)).ConfigureAwait(true);
    }

    private async Task OnSaveButtonClickAsync()
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
                this.ToastService.ShowError("Thất bại, ngữ cảnh chưa được khởi tạo.");
            }
        }
        catch (Exception ex)
        {
            this.Logger.LogError(ex, $"Đã xảy ra lỗi không mong muốn khi lưu: {ex.Message}");
            this.ToastService.ShowError($"Đã xảy ra lỗi không mong muốn: {ex.Message}");
        }
    }

    private async Task OnCancelButtonClickAsync()
    {
        if (this._persistenceContext?.HasChanges is true)
        {
            await this.DataSource.DiscardChangesAsync().ConfigureAwait(true);
            await this.LoadDataAsync(this._disposeCts?.Token ?? default).ConfigureAwait(true);
        }
    }

    private async Task OnBackButtonClickAsync()
    {
        if (this._persistenceContext?.HasChanges is true)
        {
            var isConfirmed = await this.JavaScript.InvokeAsync<bool>(
                    "window.confirm",
                    "Có thay đổi chưa được lưu. Bạn có chắc chắn muốn bỏ qua chúng không?")
                .ConfigureAwait(true);

            if (!isConfirmed)
            {
                return;
            }

            await this.OnCancelButtonClickAsync().ConfigureAwait(true);
        }

        this._selectedMerchant = null;
    }

    /// <summary>
    /// Mô hình xem cho một cửa hàng thương nhân.
    /// </summary>
    public class MerchantStorageViewModel
    {
        /// <summary>
        /// Khởi tạo một thể hiện mới của lớp <see cref="MerchantStorageViewModel"/>.
        /// </summary>
        /// <param name="merchant">Thương nhân.</param>
        public MerchantStorageViewModel(MonsterDefinition merchant)
        {
            this.Merchant = merchant;
            this.Id = merchant.GetId();
        }

        /// <summary>
        /// Lấy định danh.
        /// </summary>
        [Browsable(false)]
        public Guid Id { get; }

        /// <summary>
        /// Lấy định nghĩa thương nhân.
        /// </summary>
        [Browsable(false)]
        public MonsterDefinition Merchant { get; }

        /// <summary>
        /// Lấy tên của thương nhân.
        /// </summary>
        [Browsable(false)]
        public string Name => this.Merchant.Designation;

        /// <summary>
        /// Lấy các mặt hàng của thương nhân.
        /// </summary>
        public ICollection<Item> Items => this.Merchant.MerchantStore!.Items;
    }
}