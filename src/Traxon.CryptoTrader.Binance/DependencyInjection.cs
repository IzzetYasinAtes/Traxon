using Binance.Net.Clients;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Traxon.CryptoTrader.Application.Abstractions;
using Traxon.CryptoTrader.Binance.Adapters;
using Traxon.CryptoTrader.Binance.Buffers;
using Traxon.CryptoTrader.Binance.Options;

namespace Traxon.CryptoTrader.Binance;

public static class DependencyInjection
{
    public static IServiceCollection AddBinanceServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<BinanceOptions>(configuration.GetSection(BinanceOptions.SectionName));

        services.AddBinance();

        services.AddSingleton<ICandleBuffer>(_ => new InMemoryCandleBuffer(capacity: 200));

        services.AddSingleton<IMarketDataProvider, BinanceMarketDataProvider>();

        return services;
    }
}
