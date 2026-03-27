using Microsoft.Extensions.DependencyInjection;
using Traxon.CryptoTrader.Application.Abstractions;
using Traxon.CryptoTrader.Infrastructure.Buffers;
using Traxon.CryptoTrader.Infrastructure.Calculators;

namespace Traxon.CryptoTrader.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton<ICandleBuffer>(_ => new InMemoryCandleBuffer(capacity: 200));
        services.AddSingleton<IIndicatorCalculator, IndicatorCalculator>();
        return services;
    }
}
