namespace Traxon.CryptoTrader.Application.Abstractions;

/// <summary>Şifreli ayar yönetimi — credential'ları güvenli saklar ve okur.</summary>
public interface ISecureSettingService
{
    /// <summary>Değeri şifrele ve kaydet (upsert).</summary>
    Task SetAsync(string key, string plainValue);

    /// <summary>Şifreli değeri oku ve çöz. Yoksa null döner.</summary>
    Task<string?> GetAsync(string key);

    /// <summary>Belirtilen key için değer var mı?</summary>
    Task<bool> HasValueAsync(string key);

    /// <summary>Birden fazla key için toplu durum sorgula (true = dolu).</summary>
    Task<Dictionary<string, bool>> GetStatusAsync(IEnumerable<string> keys);
}
