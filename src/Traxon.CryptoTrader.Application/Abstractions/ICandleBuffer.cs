using Traxon.CryptoTrader.Domain.Assets;
using Traxon.CryptoTrader.Domain.Common;
using Traxon.CryptoTrader.Domain.Market;

namespace Traxon.CryptoTrader.Application.Abstractions;

public interface ICandleBuffer
{
    int Capacity { get; }

    void Add(Candle candle);

    /// <summary>Son N candle'ı döndürür. Yeterliyse Success, değilse Failure.</summary>
    Result<IReadOnlyList<Candle>> GetLast(Asset asset, TimeFrame timeFrame, int count);

    /// <summary>Tüm candle'ları döndürür (son kapanan en sonda).</summary>
    Result<IReadOnlyList<Candle>> GetAll(Asset asset, TimeFrame timeFrame);

    int Count(Asset asset, TimeFrame timeFrame);

    bool IsWarmedUp(Asset asset, TimeFrame timeFrame, int minimumCandles = 50);
}
