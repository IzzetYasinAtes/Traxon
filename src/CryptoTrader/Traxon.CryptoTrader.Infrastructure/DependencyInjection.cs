using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Traxon.CryptoTrader.Application.Abstractions;
using Traxon.CryptoTrader.Infrastructure.Buffers;
using Traxon.CryptoTrader.Infrastructure.Calculators;
using Traxon.CryptoTrader.Infrastructure.Configuration;
using Traxon.CryptoTrader.Infrastructure.Engines;
using Traxon.CryptoTrader.Infrastructure.Patterns;
using Traxon.CryptoTrader.Infrastructure.Persistence;
using Traxon.CryptoTrader.Application.Options;
using Traxon.CryptoTrader.Infrastructure.Signals;
using Traxon.CryptoTrader.Polymarket.Engines;
using Traxon.CryptoTrader.Polymarket.Options;

namespace Traxon.CryptoTrader.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Core services — TF bazli kapasite: 1m→120, 5m→500, 15m→500, 1h→48
        var candleCapacities = new Dictionary<string, int>
        {
            ["1m"]  = 120,
            ["5m"]  = 500,
            ["15m"] = 500,
            ["1h"]  = 48
        };
        services.AddSingleton<ICandleBuffer>(_ => new InMemoryCandleBuffer(candleCapacities, defaultCapacity: 200));
        services.AddSingleton<IPatternRecognizer, PatternRecognizer>();
        services.AddSingleton<IIndicatorCalculator, IndicatorCalculator>();
        services.AddSingleton<IFairValueCalculator, FairValueCalculator>();
        services.AddSingleton<IPositionSizer, PositionSizer>();
        services.AddSingleton<ISignalGenerator, SignalGenerator>();

        // DataProtection + Secure Settings
        services.AddDataProtection()
            .SetApplicationName("Traxon");
        services.AddSingleton<ISecureSettingService, SecureSettingService>();

        // DB'deki şifreli credential'ları PolymarketOptions'a override eder
        services.AddSingleton<IPostConfigureOptions<PolymarketOptions>, PolymarketCredentialConfigurer>();

        // EF Core — IDbContextFactory pattern
        var connectionString = configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            services.AddDbContextFactory<AppDbContext>(opts =>
                opts.UseInMemoryDatabase("TraxonDev"));
        }
        else
        {
            services.AddDbContextFactory<AppDbContext>(opts =>
                opts.UseSqlServer(connectionString));
        }

        // Persistence
        services.AddSingleton<ITradeLogger, SqlTradeLogger>();
        services.AddSingleton<ICandleWriter, SqlCandleWriter>();

        // Buffer warmup — DB'deki mumları startup'ta buffer'a yükler (MarketDataWorker'dan önce çalışır)
        services.AddSingleton<IHostedService, BufferWarmupService>();

        // Trading Engines — only register engines listed in EnabledEngines config
        var enabled = configuration
            .GetSection($"{TradingEngineOptions.SectionName}:EnabledEngines")
            .GetChildren()
            .Select(c => c.Value)
            .Where(v => v is not null)
            .ToList();

        if (enabled.Contains("PaperPoly"))
        {
            services.AddSingleton<PaperPolymarketEngine>();
            services.AddSingleton<ITradingEngine>(sp => sp.GetRequiredService<PaperPolymarketEngine>());
        }

        if (enabled.Contains("PaperBinance"))
        {
            services.AddSingleton<PaperBinanceEngine>();
            services.AddSingleton<ITradingEngine>(sp => sp.GetRequiredService<PaperBinanceEngine>());
        }

        if (enabled.Contains("LivePoly"))
        {
            services.AddSingleton<ITradingEngine>(sp => sp.GetRequiredService<PolymarketEngine>());
        }

        return services;
    }
}
