using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Traxon.CryptoTrader.Application.Abstractions;
using Traxon.CryptoTrader.Infrastructure.Buffers;
using Traxon.CryptoTrader.Infrastructure.Calculators;
using Traxon.CryptoTrader.Infrastructure.Engines;
using Traxon.CryptoTrader.Infrastructure.Patterns;
using Traxon.CryptoTrader.Infrastructure.Persistence;
using Traxon.CryptoTrader.Application.Options;
using Traxon.CryptoTrader.Infrastructure.Signals;

namespace Traxon.CryptoTrader.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Core services
        services.AddSingleton<ICandleBuffer>(_ => new InMemoryCandleBuffer(capacity: 200));
        services.AddSingleton<IPatternRecognizer, PatternRecognizer>();
        services.AddSingleton<IIndicatorCalculator, IndicatorCalculator>();
        services.AddSingleton<IFairValueCalculator, FairValueCalculator>();
        services.AddSingleton<IPositionSizer, PositionSizer>();
        services.AddSingleton<ISignalGenerator, SignalGenerator>();

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

        return services;
    }
}
