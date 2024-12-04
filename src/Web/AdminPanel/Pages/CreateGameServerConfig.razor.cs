// <copyright file="CreateGameServerConfig.razor.cs" company="MUnique">
// Được cấp phép theo Giấy phép MIT. Xem tệp LICENSE trong thư mục gốc của dự án để biết thông tin cấp phép đầy đủ.
// </copyright>

using MUnique.OpenMU.Interfaces;

namespace MUnique.OpenMU.Web.AdminPanel.Pages;

using System.ComponentModel.DataAnnotations;
using System.Threading;
using Blazored.Modal.Services;
using Blazored.Toast.Services;
using Microsoft.AspNetCore.Components;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.Persistence;

/// <summary>
/// Trang Razor hiển thị các đối tượng của loại đã chỉ định trong một lưới.
/// </summary>
public partial class CreateGameServerConfig : ComponentBase, IAsyncDisposable
{
    private Task? _loadTask;
    private CancellationTokenSource? _disposeCts;

    private GameServerViewModel? _viewModel;
    private string? _initState;

    /// <summary>
    /// Lấy hoặc thiết lập nhà cung cấp ngữ cảnh.
    /// </summary>
    [Inject]
    public IPersistenceContextProvider ContextProvider { get; set; } = null!;

    /// <summary>
    /// Lấy hoặc thiết lập trình khởi tạo máy chủ.
    /// </summary>
    [Inject]
    public IGameServerInstanceManager ServerInstanceManager { get; set; } = null!;

    /// <summary>
    /// Lấy hoặc thiết lập nguồn dữ liệu.
    /// </summary>
    [Inject]
    public IDataSource<GameConfiguration> DataSource { get; set; } = null!;

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
    /// Lấy hoặc thiết lập trình quản lý điều hướng.
    /// </summary>
    [Inject]
    public NavigationManager NavigationManager { get; set; } = null!;

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
        var cts = new CancellationTokenSource();
        this._disposeCts = cts;
        this._loadTask = Task.Run(() => this.LoadDataAsync(cts.Token), cts.Token);
        await base.OnParametersSetAsync().ConfigureAwait(true);
    }

    private async Task LoadDataAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var gameConfiguration = await this.DataSource.GetOwnerAsync(default, cancellationToken).ConfigureAwait(true);
        using var persistenceContext = this.ContextProvider.CreateNewContext(gameConfiguration);

        var serverConfigs = await persistenceContext.GetAsync<GameServerConfiguration>(cancellationToken).ConfigureAwait(false);
        var clients = await persistenceContext.GetAsync<GameClientDefinition>(cancellationToken).ConfigureAwait(false);
        var existingServerDefinitions = (await persistenceContext.GetAsync<GameServerDefinition>(cancellationToken).ConfigureAwait(false)).ToList();


        var nextServerId = 0;
        var networkPort = 55901;
        if (existingServerDefinitions.Count > 0)
        {
            nextServerId = existingServerDefinitions.Max(s => s.ServerID) + 1;
            networkPort = existingServerDefinitions.Max(s => s.Endpoints.FirstOrDefault()?.NetworkPort ?? 55900) + 1;
        }

        this._viewModel = new GameServerViewModel
        {
            ServerConfiguration = serverConfigs.FirstOrDefault(),
            ServerId = (byte)nextServerId,
            ExperienceRate = 1.0f,
            PvpEnabled = true,
            NetworkPort = networkPort,
            Client = clients.FirstOrDefault(),
        };

        await this.InvokeAsync(this.StateHasChanged).ConfigureAwait(false);
    }

    private async ValueTask<GameServerDefinition> CreateDefinitionByViewModelAsync(IContext context)
    {
        if (this._viewModel is null)
        {
            throw new InvalidOperationException("Mô hình xem chưa được khởi tạo.");
        }

        var result = context.CreateNew<GameServerDefinition>();
        result.ServerID = this._viewModel.ServerId;
        result.Description = this._viewModel.Description;
        result.PvpEnabled = this._viewModel.PvpEnabled;
        result.ExperienceRate = this._viewModel.ExperienceRate;
        result.GameConfiguration = await this.DataSource.GetOwnerAsync();
        result.ServerConfiguration = this._viewModel.ServerConfiguration!;

        var endpoint = context.CreateNew<GameServerEndpoint>();
        endpoint.NetworkPort = (ushort)this._viewModel.NetworkPort;
        endpoint.Client = this._viewModel.Client!;
        result.Endpoints.Add(endpoint);

        return result;
    }

    private async Task OnSaveButtonClickAsync()
    {
        string text;
        try
        {
            var gameConfiguration = await this.DataSource.GetOwnerAsync().ConfigureAwait(false);

            using var saveContext = this.ContextProvider.CreateNewTypedContext<DataModel.Configuration.GameServerDefinition>(true, gameConfiguration);

            var existingServerDefinitions = (await saveContext.GetAsync<GameServerDefinition>().ConfigureAwait(false)).ToList();
            if (existingServerDefinitions.Any(def => def.ServerID == this._viewModel?.ServerId))
            {
                this.ToastService.ShowError($"Máy chủ với Id {this._viewModel?.ServerId} đã tồn tại. Vui lòng sử dụng giá trị khác.");
                return;
            }

            if (existingServerDefinitions.Any(def => def.Endpoints.Any(endpoint => endpoint.NetworkPort == this._viewModel?.NetworkPort)))
            {
                this.ToastService.ShowError($"Một máy chủ với cổng tcp {this._viewModel?.NetworkPort} đã tồn tại. Vui lòng sử dụng cổng tcp khác.");
                return;
            }

            this._initState = "Đang tạo cấu hình ...";
            await this.InvokeAsync(this.StateHasChanged);
            var gameServerDefinition = await this.CreateDefinitionByViewModelAsync(saveContext).ConfigureAwait(false);
            this._initState = "Đang lưu cấu hình ...";
            await this.InvokeAsync(this.StateHasChanged);
            var success = await saveContext.SaveChangesAsync().ConfigureAwait(true);

            // nếu thành công, khởi tạo phiên bản máy chủ trò chơi mới
            if (success)
            {
                this.ToastService.ShowSuccess("Cấu hình máy chủ trò chơi đã được lưu. Đang khởi tạo máy chủ trò chơi ...");
                this._initState = "Đang khởi tạo máy chủ trò chơi ...";
                await this.InvokeAsync(this.StateHasChanged);
                await this.ServerInstanceManager.InitializeGameServerAsync(gameServerDefinition.ServerID);
                this.NavigationManager.NavigateTo("servers");
                return;
            }

            this.ToastService.ShowError("Không có thay đổi nào đã được lưu.");
        }
        catch (Exception ex)
        {
            this.ToastService.ShowError($"Đã xảy ra lỗi không mong muốn: {ex.Message}.");
        }

        this._initState = null;
    }

    /// <summary>
    /// Mô hình xem cho một <see cref="GameServerDefinition"/>.
    /// </summary>
    public class GameServerViewModel
    {
        /// <summary>
        /// Lấy hoặc thiết lập định danh máy chủ.
        /// </summary>
        public byte ServerId { get; set; }

        /// <summary>
        /// Lấy hoặc thiết lập mô tả.
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Lấy hoặc thiết lập tỷ lệ kinh nghiệm.
        /// </summary>
        /// <value>
        /// Tỷ lệ kinh nghiệm.
        /// </value>
        [Range(0, float.MaxValue)]
        public float ExperienceRate { get; set; }

        /// <summary>
        /// Lấy hoặc thiết lập giá trị cho biết liệu PVP có được kích hoạt trên máy chủ này hay không.
        /// </summary>
        public bool PvpEnabled { get; set; }

        /// <summary>
        /// Lấy hoặc thiết lập cấu hình máy chủ.
        /// </summary>
        [Required]
        public GameServerConfiguration? ServerConfiguration { get; set; }

        /// <summary>
        /// Lấy hoặc thiết lập khách hàng mà dự kiến sẽ kết nối.
        /// </summary>
        [Required]
        public GameClientDefinition? Client { get; set; }

        /// <summary>
        /// Lấy hoặc thiết lập cổng mạng mà máy chủ đang lắng nghe.
        /// </summary>
        [Range(1, ushort.MaxValue)]
        public int NetworkPort { get; set; }
    }
}