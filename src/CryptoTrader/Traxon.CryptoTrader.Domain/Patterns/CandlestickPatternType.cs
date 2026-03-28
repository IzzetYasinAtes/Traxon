namespace Traxon.CryptoTrader.Domain.Patterns;

/// <summary>Desteklenen candlestick pattern türleri.</summary>
public enum CandlestickPatternType
{
    // Single candle
    Hammer,
    InvertedHammer,
    ShootingStar,
    HangingMan,
    Doji,
    DragonflyDoji,
    GravestoneDoji,
    BullishMarubozu,
    BearishMarubozu,
    SpinningTop,

    // Two candle
    BullishEngulfing,
    BearishEngulfing,
    BullishHarami,
    BearishHarami,
    PiercingLine,
    DarkCloudCover,
    TweezerTop,
    TweezerBottom,
    BullishKicker,
    BearishKicker,

    // Three+ candle
    MorningStar,
    EveningStar,
    ThreeWhiteSoldiers,
    ThreeBlackCrows,
    MorningDojiStar,
    EveningDojiStar,
    ThreeInsideUp,
    ThreeInsideDown,
    ThreeOutsideUp,
    ThreeOutsideDown
}
