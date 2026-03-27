using Traxon.CryptoTrader.Domain.Common;
using Traxon.CryptoTrader.Domain.Market;
using Traxon.CryptoTrader.Domain.Trading;

namespace Traxon.CryptoTrader.Application.Abstractions;

public interface ITradingEngine
{
    string EngineName { get; }

    Task<Result<Trade>>                OpenPositionAsync(Signal signal, CancellationToken ct = default);
    Task<Result<Trade>>                ClosePositionAsync(Guid tradeId, string reason, CancellationToken ct = default);
    Task<Result<IReadOnlyList<Trade>>> GetOpenTradesAsync(CancellationToken ct = default);
    Task<Result<Portfolio>>            GetPortfolioAsync(CancellationToken ct = default);
    Task<Result<bool>>                 IsReadyAsync(CancellationToken ct = default);
    /// <summary>
    /// Her candle kapanisinda cagrilir.
    /// PaperBinanceEngine: SL/TP kontrolu.
    /// PaperPolymarketEngine: Resolution suresi dolmus mu kontrolu.
    /// </summary>
    Task CheckPositionsAsync(Candle candle, CancellationToken ct = default);
}
