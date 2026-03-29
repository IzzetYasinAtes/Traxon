namespace Traxon.CryptoTrader.Domain.Trading;

/// <summary>
/// V2 sinyal motoru skoru — agirlikli teknik gosterge skoru ile sinyal karari.
/// </summary>
public sealed record SignalScore(
    /// <summary>Agirlikli final skor (0.0 - 1.0).</summary>
    decimal FinalScore,
    /// <summary>|FairValue - MarketPrice| edge.</summary>
    decimal Edge,
    /// <summary>1h trend dogrulamasi yapildi mi?</summary>
    bool HourlyTrendConfirmed,
    /// <summary>Hacim dogrulamasi yapildi mi?</summary>
    bool VolumeConfirmed,
    /// <summary>Skor > 0.60 ise UP sinyal.</summary>
    bool IsUpSignal,
    /// <summary>Skor &lt; 0.40 ise DOWN sinyal.</summary>
    bool IsDownSignal);
