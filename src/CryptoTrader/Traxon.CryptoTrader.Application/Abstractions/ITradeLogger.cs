using Traxon.CryptoTrader.Domain.Trading;

namespace Traxon.CryptoTrader.Application.Abstractions;

public interface ITradeLogger
{
    Task LogSignalAsync(Signal signal, CancellationToken ct = default);
    Task LogTradeOpenedAsync(Trade trade, CancellationToken ct = default);
    Task LogTradeClosedAsync(Trade trade, CancellationToken ct = default);
}
