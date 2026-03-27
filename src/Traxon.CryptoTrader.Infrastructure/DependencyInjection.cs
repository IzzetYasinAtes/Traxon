using Microsoft.Extensions.DependencyInjection;
using Traxon.CryptoTrader.Application.Abstractions;
using Traxon.CryptoTrader.Infrastructure.Buffers;
using Traxon.CryptoTrader.Infrastructure.Calculators;
using Traxon.CryptoTrader.Infrastructure.Signals;

namespace Traxon.CryptoTrader.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton<ICandleBuffer>(_ => new InMemoryCandleBuffer(capacity: 200));
        services.AddSingleton<IIndicatorCalculator, IndicatorCalculator>();
        services.AddSingleton<IFairValueCalculator, FairValueCalculator>();
        services.AddSingleton<IPositionSizer, PositionSizer>();
        services.AddSingleton<ISignalGenerator, SignalGenerator>();
        return services;
    }
}
