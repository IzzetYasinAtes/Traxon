using Traxon.CryptoTrader.Domain.Assets;
using Traxon.CryptoTrader.Domain.Common;
using Traxon.CryptoTrader.Domain.Indicators;
using Traxon.CryptoTrader.Domain.Market;
using Traxon.CryptoTrader.Domain.Trading;

namespace Traxon.CryptoTrader.Application.Abstractions;

public interface ISignalGenerator
{
    /// <summary>
    /// Candle listesinden sinyal uretir.
    /// Minimum 50 candle gerektirir.
    /// marketPrice: Polymarket YES token fiyati (paper trading icin 0.50m gecirilebilir).
    /// </summary>
    Result<Signal> Generate(
        Asset asset,
        TimeFrame timeFrame,
        IReadOnlyList<Candle> candles,
        decimal marketPrice);

    /// <summary>
    /// Onceden hesaplanmis indicator'lari kullanarak sinyal uretir — cift hesaplamay onler.
    /// </summary>
    Result<Signal> Generate(
        Asset asset,
        TimeFrame timeFrame,
        IReadOnlyList<Candle> candles,
        decimal marketPrice,
        TechnicalIndicators precomputedIndicators);

}
