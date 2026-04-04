namespace Traxon.CryptoTrader.Application.Options;

/// <summary>
/// Configuration for which trading engines are active.
/// Maps to the "TradingEngine" section in appsettings.json.
/// </summary>
public sealed class TradingEngineOptions
{
    public const string SectionName = "TradingEngine";

    /// <summary>
    /// List of engine names to register (e.g. "PaperPoly", "LivePoly").
    /// Only engines in this list will be activated.
    /// </summary>
    public List<string> EnabledEngines { get; init; } = [];
}
