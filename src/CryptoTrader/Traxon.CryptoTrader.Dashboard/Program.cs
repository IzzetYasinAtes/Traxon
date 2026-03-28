using ApexCharts;
using Microsoft.AspNetCore.Localization;
using Microsoft.EntityFrameworkCore;
using Serilog;
using System.Globalization;
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
    // Override SQL writers with no-ops to prevent duplicate trades/candles.
    builder.Services.AddSingleton<ITradeLogger, NullTradeLogger>();
    builder.Services.AddSingleton<ICandleWriter, NullCandleWriter>();

    // LiveFeedService: ILiveFeedService + IMarketEventPublisher (same singleton)
    builder.Services.AddSingleton<LiveFeedService>();
    builder.Services.AddSingleton<ILiveFeedService>(sp => sp.GetRequiredService<LiveFeedService>());
    builder.Services.AddSingleton<IMarketEventPublisher>(sp => sp.GetRequiredService<LiveFeedService>());

    // Background services
    builder.Services.AddHostedService<MarketDataWorker>();
    builder.Services.AddHostedService<PortfolioRefreshService>();

    // Blazor Server + SignalR
    builder.Services.AddRazorComponents()
        .AddInteractiveServerComponents();
    builder.Services.AddSignalR();

    // Chart libraries
    builder.Services.AddApexCharts();

    // Localization
    builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");

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

    var supportedCultures = new[] { new CultureInfo("en"), new CultureInfo("tr") };
    app.UseRequestLocalization(new RequestLocalizationOptions
    {
        DefaultRequestCulture = new RequestCulture("en"),
        SupportedCultures = supportedCultures,
        SupportedUICultures = supportedCultures
    });

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
