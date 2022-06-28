﻿// <copyright file="BaseTradeAction.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlayerActions.Trade;

using System.Diagnostics.Metrics;
using MUnique.OpenMU.GameLogic.Views;
using MUnique.OpenMU.Interfaces;

/// <summary>
/// Action to cancel a trade.
/// </summary>
public class BaseTradeAction
{
    private static readonly Meter TradeMeter = new(MeterName);

    /// <summary>
    /// Gets the name of the meter of this class.
    /// </summary>
    internal static string MeterName => typeof(BaseTradeAction).Namespace ?? nameof(BaseTradeAction);

    /// <summary>
    /// Gets the counter for finished trades.
    /// </summary>
    protected static Counter<int> FinishedTrades { get; } = TradeMeter.CreateCounter<int>("FinishedTradesCount");

    /// <summary>
    /// Gets the counter for cancelled trades.
    /// </summary>
    private static Counter<int> CancelledTrades { get; } = TradeMeter.CreateCounter<int>("CancelledTradesCount");

    /// <summary>
    /// Cancels the trade.
    /// </summary>
    /// <param name="trader">The trader.</param>
    protected void CancelTrade(ITrader trader)
    {
        CancelledTrades.Add(1);
        using (var context = trader.PlayerState.TryBeginAdvanceTo(PlayerState.EnteredWorld))
        {
            if (!context.Allowed)
            {
                return;
            }

            trader.TradingMoney = 0;
            if (trader.BackupInventory != null)
            {
                trader.Inventory!.Clear();
                trader.BackupInventory.RestoreItemStates();
                trader.BackupInventory.Items.ForEach(item => trader.Inventory.AddItem(item.ItemSlot, item));
                trader.Inventory.ItemStorage.Money = trader.BackupInventory.Money;
            }
        }

        this.ResetTradeState(trader);
    }

    /// <summary>
    /// Resets the trade.
    /// </summary>
    /// <param name="trader">The trader.</param>
    protected void ResetTradeState(ITrader trader)
    {
        trader.TradingPartner = null;
        trader.BackupInventory = null;
        trader.TemporaryStorage!.Clear();
    }

    /// <summary>
    /// Sends the message to the trader.
    /// </summary>
    /// <param name="trader">The trader.</param>
    /// <param name="message">The message.</param>
    protected void SendMessage(ITrader trader, string message)
    {
        var player = trader as Player;
        player?.ViewPlugIns.GetPlugIn<IShowMessagePlugIn>()?.ShowMessage(message, MessageType.BlueNormal);
    }
}