﻿// <copyright file="GameServerRegistry.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.ConnectServer.Host;

using System.Net;
using System.Threading;
using Microsoft.Extensions.Logging;
using MUnique.OpenMU.Interfaces;

/// <summary>
/// A registry which keeps track of available <see cref="IGameServer"/>s.
/// </summary>
/// <seealso cref="System.IDisposable" />
public sealed class GameServerRegistry : IDisposable
{
    private readonly TimeSpan _timeout = TimeSpan.FromSeconds(10);
    private readonly CancellationTokenSource _disposeCts = new();
    private readonly IConnectServer _connectServer;
    private readonly ILogger<GameServerRegistry> _logger;
    private readonly Dictionary<ushort, DateTime> _entries = new();
    private readonly SemaphoreSlim _semaphore = new(1);

    /// <summary>
    /// Initializes a new instance of the <see cref="GameServerRegistry"/> class.
    /// </summary>
    /// <param name="connectServer">The connect server.</param>
    /// <param name="logger">The logger.</param>
    public GameServerRegistry(IConnectServer connectServer, ILogger<GameServerRegistry> logger)
    {
        this._connectServer = connectServer;
        this._logger = logger;

        Task.Run(async () =>
        {
            try
            {
                await this.CleanupLoopAsync(this._disposeCts.Token);
            }
            catch (Exception ex)
            {
                this._logger.LogError(ex, "Error in cleanup loop");
            }
        });
    }

    /// <inheritdoc />
    public void Dispose()
    {
        this._disposeCts.Cancel();
        this._disposeCts.Dispose();
        this._semaphore.Dispose();
    }

    /// <summary>
    /// Updates the registration.
    /// </summary>
    /// <param name="serverInfo">The server information.</param>
    /// <param name="publicEndPoint">The public end point.</param>
    public async Task UpdateRegistrationAsync(ServerInfo serverInfo, IPEndPoint publicEndPoint)
    {
        await this._semaphore.WaitAsync();
        try
        {
            var isNew = !this._entries.ContainsKey(serverInfo.Id);
            this._entries[serverInfo.Id] = DateTime.UtcNow;
            if (isNew)
            {
                this._connectServer.RegisterGameServer(serverInfo, publicEndPoint);
            }
            else
            {
                this._connectServer.CurrentConnectionsChanged(serverInfo.Id, serverInfo.CurrentConnections);
            }
        }
        finally
        {
            this._semaphore.Release(1);
        }
    }

    private async Task CleanupLoopAsync(CancellationToken cancellationToken)
    {
        var tempRemoved = new List<ushort>();
        while (!this._disposeCts.IsCancellationRequested)
        {
            await Task.Delay(2000, cancellationToken);
            await this._semaphore.WaitAsync(cancellationToken);
            try
            {
                foreach (var serverId in this._entries.Keys)
                {
                    var lastUpdate = this._entries[serverId];
                    var diff = DateTime.UtcNow - lastUpdate;
                    if (diff > this._timeout)
                    {
                        this._logger.LogInformation("Difference of {0} higher than timeout for server {1}", diff, serverId);
                        this._connectServer.UnregisterGameServer(serverId);
                        tempRemoved.Add(serverId);
                    }
                }

                foreach (var serverId in tempRemoved)
                {
                    this._entries.Remove(serverId, out _);
                }

                tempRemoved.Clear();
            }
            finally
            {
                this._semaphore.Release(1);
            }
        }
    }
}