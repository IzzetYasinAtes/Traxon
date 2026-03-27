using Binance.Net.Interfaces;
using Traxon.CryptoTrader.Domain.Assets;
using Traxon.CryptoTrader.Domain.Market;

namespace Traxon.CryptoTrader.Binance.Mappers;

internal static class BinanceMapper
{
    /// <summary>Maps a WebSocket stream kline (IBinanceStreamKlineData) to a Candle.</summary>
    public static Candle ToCandle(IBinanceStreamKlineData klineData, Asset asset, TimeFrame timeFrame)
    {
        var k = klineData.Data;
        return new Candle(
            id: k.OpenTime.Ticks,
            asset: asset,
            timeFrame: timeFrame,
            openTime: k.OpenTime,
            closeTime: k.CloseTime,
            open: k.OpenPrice,
            high: k.HighPrice,
            low: k.LowPrice,
            close: k.ClosePrice,
            volume: k.Volume,
            quoteVolume: k.QuoteVolume,
            tradeCount: (int)k.TradeCount,
            isClosed: k.Final);
    }

    /// <summary>Maps a REST kline (IBinanceKline) to a Candle.</summary>
    public static Candle ToCandle(IBinanceKline kline, Asset asset, TimeFrame timeFrame)
    {
        return new Candle(
            id: kline.OpenTime.Ticks,
            asset: asset,
            timeFrame: timeFrame,
            openTime: kline.OpenTime,
            closeTime: kline.CloseTime,
            open: kline.OpenPrice,
            high: kline.HighPrice,
            low: kline.LowPrice,
            close: kline.ClosePrice,
            volume: kline.Volume,
            quoteVolume: kline.QuoteVolume,
            tradeCount: (int)kline.TradeCount,
            isClosed: true);
    }
}
