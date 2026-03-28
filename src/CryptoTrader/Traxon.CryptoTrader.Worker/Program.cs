using Microsoft.EntityFrameworkCore;
using Serilog;
using Traxon.CryptoTrader.Application;
using Traxon.CryptoTrader.Application.Abstractions;
using Traxon.CryptoTrader.Application.Workers;
using Traxon.CryptoTrader.Binance;
using Traxon.CryptoTrader.Infrastructure;
using Traxon.CryptoTrader.Infrastructure.Persistence;
using Traxon.CryptoTrader.Polymarket;
using Traxon.CryptoTrader.Worker.Publishers;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .WriteTo.File("logs/traxon-.log", rollingInterval: RollingInterval.Day)
    .CreateLogger();

try
{
    var host = Host.CreateDefaultBuilder(args)
        .UseSerilog()
        .ConfigureServices((context, services) =>
        {
            services.AddApplication();
            services.AddInfrastructure(context.Configuration);
            services.AddBinanceServices(context.Configuration);
            services.AddPolymarketServices(context.Configuration);
            services.AddSingleton<IMarketEventPublisher, NullMarketEventPublisher>();
            services.AddHostedService<MarketDataWorker>();
        })
        .Build();

    // Auto-migrate on startup
    using (var scope = host.Services.CreateScope())
    {
        var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
        await using var dbContext = await factory.CreateDbContextAsync();
        await dbContext.Database.MigrateAsync();
    }

    await host.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Host terminated unexpectedly");
}
finally
{
    await Log.CloseAndFlushAsync();
}
