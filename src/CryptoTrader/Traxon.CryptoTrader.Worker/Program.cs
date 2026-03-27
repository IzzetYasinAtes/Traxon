using Serilog;
using Traxon.CryptoTrader.Application;
using Traxon.CryptoTrader.Binance;
using Traxon.CryptoTrader.Infrastructure;
using Traxon.CryptoTrader.Worker.Workers;

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
            services.AddHostedService<MarketDataWorker>();
        })
        .Build();

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
