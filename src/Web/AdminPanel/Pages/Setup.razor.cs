// <copyright file="Setup.razor.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Web.AdminPanel.Pages;

using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

using MUnique.OpenMU.Network.PlugIns;
using MUnique.OpenMU.Web.AdminPanel.Components;
using MUnique.OpenMU.Web.AdminPanel.Services;

/// <summary>
/// Trang thiết lập.
/// </summary>
public partial class Setup
{
    private bool _isDataInitialized;

    private ClientVersion? _gameClientVersion;

    /// <summary>
    /// Lấy hoặc thiết lập giá trị cho biết có hiển thị thành phần <see cref="Install"/> hay không.
    /// </summary>
    public bool ShowInstall { get; set; }

    /// <summary>
    /// Lấy hoặc thiết lập dịch vụ thiết lập.
    /// </summary>
    [Inject]
    public SetupService SetupService { get; set; } = null!;

    /// <summary>
    /// Lấy hoặc thiết lập runtime javascript.
    /// </summary>
    [Inject]
    public IJSRuntime JsRuntime { get; set; } = null!;

    /// <inheritdoc />
    protected override async Task OnInitializedAsync()
    {
        this._isDataInitialized = await this.SetupService.IsDataInitializedAsync().ConfigureAwait(false);
        if (this._isDataInitialized)
        {
            this._gameClientVersion = await this.SetupService.GetCurrentGameClientVersionAsync().ConfigureAwait(false);
        }
    }

    private Task OnUpdateClickAsync()
    {
        return this.SetupService.InstallUpdatesAsync(default);
    }

    private void OnInstallClick()
    {
        this.ShowInstall = true;
    }

    private async Task OnReInstallClickAsync()
    {
        if (await this.JsRuntime.InvokeAsync<bool>("confirm", "Bạn có chắc chắn không? Tất cả dữ liệu hiện tại sẽ bị xóa và được cài đặt lại từ đầu.").ConfigureAwait(false))
        {
            this.ShowInstall = true;
        }
    }
}