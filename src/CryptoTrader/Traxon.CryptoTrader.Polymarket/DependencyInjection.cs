using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Traxon.CryptoTrader.Application.Abstractions;
using Traxon.CryptoTrader.Polymarket.Authentication;
using Traxon.CryptoTrader.Polymarket.Engines;
using Traxon.CryptoTrader.Polymarket.Http;
using Traxon.CryptoTrader.Polymarket.Options;
using Traxon.CryptoTrader.Polymarket.Services;
using Traxon.CryptoTrader.Polymarket.WebSocket;

namespace Traxon.CryptoTrader.Polymarket;

public static class DependencyInjection
{
    public static IServiceCollection AddPolymarketServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<PolymarketOptions>(
            configuration.GetSection(PolymarketOptions.SectionName));

        services.AddTransient<PolymarketAuthHandler>();

        services.AddHttpClient<IPolymarketClient, PolymarketClient>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(15);
        })
        .AddHttpMessageHandler<PolymarketAuthHandler>();

        services.AddHttpClient<IGammaApiClient, GammaApiClient>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(15);
        });

        services.AddSingleton<IMarketDiscoveryService, MarketDiscoveryService>();

        services.AddHttpClient<IPolymarketSigningClient, PolymarketSigningClient>(client =>
        {
            client.BaseAddress = new Uri("http://127.0.0.1:5099");
            client.Timeout = TimeSpan.FromSeconds(15);
        });

        services.AddSingleton<PolymarketWebSocketClient>();

        services.AddSingleton<PolymarketEngine>();
        // ITradingEngine kaydı Infrastructure DI'da yapılır (EnabledEngines kontrolü ile)

        return services;
    }
}
