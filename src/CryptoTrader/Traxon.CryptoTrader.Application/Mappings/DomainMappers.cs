using Traxon.CryptoTrader.Application.DTOs;
using Traxon.CryptoTrader.Domain.Market;
using Traxon.CryptoTrader.Domain.Trading;

namespace Traxon.CryptoTrader.Application.Mappings;

/// <summary>Domain entity → Application DTO donusturuculer.</summary>
public static class DomainMappers
{
    public static SignalDto ToDto(this Signal s) => new(
        s.SignalId,
        s.Asset.Symbol,
        s.TimeFrame.Value,
        s.Direction == SignalDirection.Up ? "UP" : "DOWN",
        s.FairValue,
        s.MarketPrice,
        s.Edge,
        s.KellyFraction,
        s.Regime == MarketRegime.HighVolatility ? "HIGH_VOL" : "LOW_VOL",
        s.Indicators.Rsi.Value,
        s.Indicators.Macd.Histogram,
        s.Indicators.BullishCount(),
        s.GeneratedAt);

    public static TradeDto ToDto(this Trade t) => new(
        t.Id,
        t.Engine,
        t.Asset.Symbol,
        t.TimeFrame.Value,
        t.Direction == SignalDirection.Up ? "UP" : "DOWN",
        t.EntryPrice,
        t.ExitPrice,
        t.FairValue,
        t.Edge,
        t.PositionSize,
        t.Status == TradeStatus.Open ? "OPEN" : "CLOSED",
        t.Outcome?.ToString().ToUpperInvariant(),
        t.PnL,
        t.OpenedAt,
        t.ClosedAt);

    public static PortfolioDto ToDto(this Portfolio p) => new(
        p.Engine,
        p.Balance,
        p.InitialBalance,
        p.TotalPnL,
        p.WinCount,
        p.LossCount,
        p.TotalTradeCount,
        p.WinRate,
        p.TotalExposure,
        p.OpenPositions.Count);

    public static CandleDto ToCandleDto(this Candle c) => new(
        c.Asset.Symbol,
        c.TimeFrame.Value,
        c.OpenTime,
        c.Open,
        c.High,
        c.Low,
        c.Close,
        c.Volume,
        c.IsClosed);
}
