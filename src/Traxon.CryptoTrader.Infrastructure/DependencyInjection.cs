using Microsoft.Extensions.DependencyInjection;
using Traxon.CryptoTrader.Application.Abstractions;
using Traxon.CryptoTrader.Infrastructure.Calculators;

namespace Traxon.CryptoTrader.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton<IIndicatorCalculator, IndicatorCalculator>();
        return services;
    }
}
