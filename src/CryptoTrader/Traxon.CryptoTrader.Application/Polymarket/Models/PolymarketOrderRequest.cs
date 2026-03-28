namespace Traxon.CryptoTrader.Application.Polymarket.Models;

/// <summary>
/// Polymarket CLOB order request. EIP-712 signature stub olarak bırakılmıştır.
/// Gerçek signing için Nethereum gerekir — Faz 7'ye ertelendi.
/// </summary>
public sealed record PolymarketOrderRequest
{
    public string  TokenId   { get; init; } = string.Empty;
    public decimal Price     { get; init; }
    public decimal Size      { get; init; }
    public string  Side      { get; init; } = "BUY";
    public string  OrderType { get; init; } = "GTC";
    public string  Signature { get; init; } = "0x_stub_eip712_sign_not_implemented";
}
