using Traxon.CryptoTrader.Domain.Market;

namespace Traxon.CryptoTrader.Application.Abstractions;

public interface ICandleWriter
{
    /// <summary>
    /// Kapanan candle'i arka planda SQL'e yazar (fire-and-forget tarzı).
    /// </summary>
    Task WriteAsync(Candle candle, CancellationToken ct = default);
}
