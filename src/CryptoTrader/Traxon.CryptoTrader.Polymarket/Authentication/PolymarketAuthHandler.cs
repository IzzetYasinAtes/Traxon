using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Traxon.CryptoTrader.Polymarket.Options;

namespace Traxon.CryptoTrader.Polymarket.Authentication;

public sealed class PolymarketAuthHandler : DelegatingHandler
{
    private readonly PolymarketOptions _options;

    public PolymarketAuthHandler(IOptions<PolymarketOptions> options)
    {
        _options = options.Value;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken ct)
    {
        if (!_options.Enabled || string.IsNullOrWhiteSpace(_options.ApiKey))
            return await base.SendAsync(request, ct);

        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        var method    = request.Method.Method.ToUpperInvariant();
        var path      = request.RequestUri?.PathAndQuery ?? "/";
        var body      = string.Empty;

        if (request.Content is not null)
            body = await request.Content.ReadAsStringAsync(ct);

        var message   = timestamp + method + path + body;
        var signature = ComputeHmac(_options.ApiSecret, message);

        request.Headers.Add("POLY_ADDRESS",    _options.WalletAddress);
        request.Headers.Add("POLY_API_KEY",    _options.ApiKey);
        request.Headers.Add("POLY_PASSPHRASE", _options.Passphrase);
        request.Headers.Add("POLY_TIMESTAMP",  timestamp);
        request.Headers.Add("POLY_SIGNATURE",  signature);

        return await base.SendAsync(request, ct);
    }

    private static string ComputeHmac(string secret, string message)
    {
        var keyBytes  = Encoding.UTF8.GetBytes(secret);
        var msgBytes  = Encoding.UTF8.GetBytes(message);
        var hashBytes = HMACSHA256.HashData(keyBytes, msgBytes);
        return Convert.ToHexStringLower(hashBytes);
    }
}
