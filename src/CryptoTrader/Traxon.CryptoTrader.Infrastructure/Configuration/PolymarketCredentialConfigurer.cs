using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Traxon.CryptoTrader.Application.Abstractions;
using Traxon.CryptoTrader.Polymarket.Options;

namespace Traxon.CryptoTrader.Infrastructure.Configuration;

/// <summary>
/// appsettings.json'daki placeholder değerleri DB'deki şifreli credential'larla override eder.
/// IConfigureOptions pattern'i sayesinde DI resolve sırasında otomatik çalışır.
/// </summary>
public sealed class PolymarketCredentialConfigurer : IPostConfigureOptions<PolymarketOptions>
{
    private readonly ISecureSettingService _settings;
    private readonly ILogger<PolymarketCredentialConfigurer> _logger;

    public PolymarketCredentialConfigurer(
        ISecureSettingService settings,
        ILogger<PolymarketCredentialConfigurer> logger)
    {
        _settings = settings;
        _logger = logger;
    }

    public void PostConfigure(string? name, PolymarketOptions options)
    {
        _logger.LogInformation("[CredentialConfigurer] Running — current Enabled={Enabled}", options.Enabled);

        // Sync wrapper — Configure is sync, GetAsync is async
        var apiKey = _settings.GetAsync("Polymarket:ApiKey").GetAwaiter().GetResult();
        if (!string.IsNullOrEmpty(apiKey)) options.ApiKey = apiKey;

        var apiSecret = _settings.GetAsync("Polymarket:ApiSecret").GetAwaiter().GetResult();
        if (!string.IsNullOrEmpty(apiSecret)) options.ApiSecret = apiSecret;

        var passphrase = _settings.GetAsync("Polymarket:Passphrase").GetAwaiter().GetResult();
        if (!string.IsNullOrEmpty(passphrase)) options.Passphrase = passphrase;

        var walletAddress = _settings.GetAsync("Polymarket:WalletAddress").GetAwaiter().GetResult();
        if (!string.IsNullOrEmpty(walletAddress)) options.WalletAddress = walletAddress;

        var privateKey = _settings.GetAsync("Polymarket:PrivateKey").GetAwaiter().GetResult();
        if (!string.IsNullOrEmpty(privateKey)) options.PrivateKey = privateKey;

        var sigType = _settings.GetAsync("Polymarket:SignatureType").GetAwaiter().GetResult();
        if (int.TryParse(sigType, out var st)) options.SignatureType = st;

        // Credential varsa auto-enable
        if (!string.IsNullOrEmpty(apiKey) && !string.IsNullOrEmpty(apiSecret))
        {
            options.Enabled = true;
            _logger.LogInformation("[CredentialConfigurer] Credentials found — Enabled=true (ApiKey={HasKey}, ApiSecret={HasSecret})",
                !string.IsNullOrEmpty(apiKey), !string.IsNullOrEmpty(apiSecret));
        }
        else
        {
            _logger.LogWarning("[CredentialConfigurer] Credentials missing — ApiKey={HasKey}, ApiSecret={HasSecret}",
                !string.IsNullOrEmpty(apiKey), !string.IsNullOrEmpty(apiSecret));
        }
    }
}
