namespace Traxon.CryptoTrader.Infrastructure.Persistence.Models;

/// <summary>Şifreli ayar deposu — credential'lar AES korumalı saklanır.</summary>
public sealed class SecureSetting
{
    public string Key { get; set; } = string.Empty;           // PK: "Polymarket:ApiKey" vb.
    public string EncryptedValue { get; set; } = string.Empty; // DataProtection ile şifreli
    public DateTime UpdatedAt { get; set; }
}
