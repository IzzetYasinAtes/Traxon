using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Traxon.CryptoTrader.Application.Abstractions;
using Traxon.CryptoTrader.Binance.Adapters;
using Traxon.CryptoTrader.Binance.Options;

namespace Traxon.CryptoTrader.Binance;

public static class DependencyInjection
{
    public static IServiceCollection AddBinanceServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var opts = new BinanceOptions();
        configuration.GetSection(BinanceOptions.SectionName).Bind(opts);

        services.Configure<BinanceOptions>(configuration.GetSection(BinanceOptions.SectionName));

        // Binance.Net client registration — credentials only when enabled AND key provided
        if (opts.Enabled && !string.IsNullOrWhiteSpace(opts.ApiKey))
        {
            services.AddBinance(binanceOpts =>
            {
                binanceOpts.Rest.ApiCredentials = new global::Binance.Net.BinanceCredentials(
                    opts.ApiKey, opts.ApiSecret);
            });
        }
        else
        {
            services.AddBinance();
        }

        services.AddSingleton<IMarketDataProvider, BinanceMarketDataProvider>();

        return services;
    }
}
