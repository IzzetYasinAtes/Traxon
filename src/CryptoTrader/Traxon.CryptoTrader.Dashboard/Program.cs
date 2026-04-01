using ApexCharts;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Traxon.CryptoTrader.Application;
using Traxon.CryptoTrader.Application.Abstractions;
using Traxon.CryptoTrader.Application.Workers;
using Traxon.CryptoTrader.Binance;
using Traxon.CryptoTrader.Dashboard.Components;
using Traxon.CryptoTrader.Dashboard.Hubs;
using Traxon.CryptoTrader.Dashboard.Services;
using Traxon.CryptoTrader.Infrastructure;
using Traxon.CryptoTrader.Infrastructure.Persistence;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .WriteTo.File("logs/dashboard-.log", rollingInterval: RollingInterval.Day)
    .CreateLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);
    builder.Host.UseSerilog();

    // Infrastructure + Application
    builder.Services.AddApplication();
    builder.Services.AddInfrastructure(builder.Configuration);
    builder.Services.AddBinanceServices(builder.Configuration);

    // Dashboard only shows live feed — Worker handles DB persistence.
    // Override SQL writers with no-ops to prevent duplicate candle writes.
    builder.Services.AddSingleton<ITradeLogger, NullTradeLogger>();
    builder.Services.AddSingleton<ICandleWriter, NullCandleWriter>();

    // Dashboard trade ACMAMALI — sadece goruntuleme yapar.
    // AddInfrastructure'in kaydettigi ITradingEngine'leri kaldir:
    // MarketDataWorker signal uretmeye devam eder ama hicbir engine'e iletmez.
    var engineDescriptors = builder.Services
        .Where(d => d.ServiceType == typeof(ITradingEngine))
        .ToList();
    foreach (var descriptor in engineDescriptors)
        builder.Services.Remove(descriptor);

    // Admin data service (DB queries for performance, calibration, trades, engines, logs)
    builder.Services.AddSingleton<AdminDataService>();

    // LiveFeedService: ILiveFeedService + IMarketEventPublisher (same singleton)
    builder.Services.AddSingleton<LiveFeedService>();
    builder.Services.AddSingleton<ILiveFeedService>(sp => sp.GetRequiredService<LiveFeedService>());
    builder.Services.AddSingleton<IMarketEventPublisher>(sp => sp.GetRequiredService<LiveFeedService>());

    // Background services
    builder.Services.AddHostedService<LiveFeedSeederService>();   // DB'den son trade'leri yukle
    builder.Services.AddHostedService<MarketDataWorker>();
    builder.Services.AddHostedService<PortfolioRefreshService>();

    // Blazor Server + SignalR
    builder.Services.AddRazorComponents()
        .AddInteractiveServerComponents();
    builder.Services.AddSignalR();

    // Chart libraries
    builder.Services.AddApexCharts();

    var app = builder.Build();

    // Auto-migrate on startup
    using (var scope = app.Services.CreateScope())
    {
        var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
        await using var context = await factory.CreateDbContextAsync();
        await context.Database.MigrateAsync();
    }

    if (!app.Environment.IsDevelopment())
    {
        app.UseExceptionHandler("/Error", createScopeForErrors: true);
        app.UseHsts();
    }

    app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
    app.UseHttpsRedirection();
    app.UseAntiforgery();
    app.MapStaticAssets();
    app.MapHub<TradingHub>("/trading-hub");
    app.MapRazorComponents<App>()
        .AddInteractiveServerRenderMode();

    await app.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Dashboard terminated unexpectedly");
}
finally
{
    await Log.CloseAndFlushAsync();
}
