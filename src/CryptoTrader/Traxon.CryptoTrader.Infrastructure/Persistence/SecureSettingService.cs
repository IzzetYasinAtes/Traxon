using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Traxon.CryptoTrader.Application.Abstractions;
using Traxon.CryptoTrader.Infrastructure.Persistence.Models;

namespace Traxon.CryptoTrader.Infrastructure.Persistence;

/// <summary>DataProtection API ile şifreli ayar yönetimi.</summary>
public sealed class SecureSettingService : ISecureSettingService
{
    private const string Purpose = "Traxon.SecureSettings.V1";

    private readonly IDbContextFactory<AppDbContext> _dbFactory;
    private readonly IDataProtector _protector;

    public SecureSettingService(
        IDbContextFactory<AppDbContext> dbFactory,
        IDataProtectionProvider dataProtectionProvider)
    {
        _dbFactory = dbFactory;
        _protector = dataProtectionProvider.CreateProtector(Purpose);
    }

    public async Task SetAsync(string key, string plainValue)
    {
        var encrypted = _protector.Protect(plainValue);

        await using var db = await _dbFactory.CreateDbContextAsync();

        var existing = await db.SecureSettings.FindAsync(key);
        if (existing is not null)
        {
            existing.EncryptedValue = encrypted;
            existing.UpdatedAt = DateTime.UtcNow;
        }
        else
        {
            db.SecureSettings.Add(new SecureSetting
            {
                Key = key,
                EncryptedValue = encrypted,
                UpdatedAt = DateTime.UtcNow
            });
        }

        await db.SaveChangesAsync();
    }

    public async Task<string?> GetAsync(string key)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var setting = await db.SecureSettings.FindAsync(key);
        if (setting is null) return null;

        return _protector.Unprotect(setting.EncryptedValue);
    }

    public async Task<bool> HasValueAsync(string key)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        return await db.SecureSettings.AnyAsync(s => s.Key == key);
    }

    public async Task<Dictionary<string, bool>> GetStatusAsync(IEnumerable<string> keys)
    {
        var keyList = keys.ToList();
        await using var db = await _dbFactory.CreateDbContextAsync();

        var existingKeys = await db.SecureSettings
            .Where(s => keyList.Contains(s.Key))
            .Select(s => s.Key)
            .ToListAsync();

        return keyList.ToDictionary(k => k, k => existingKeys.Contains(k));
    }
}
