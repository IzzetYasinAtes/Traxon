using ApexCharts;
using Microsoft.AspNetCore.Localization;
using Microsoft.EntityFrameworkCore;
using Serilog;
using System.Globalization;
using Traxon.CryptoTrader.Admin.Components;
using Traxon.CryptoTrader.Admin.Services;
using Traxon.CryptoTrader.Infrastructure;
using Traxon.CryptoTrader.Infrastructure.Persistence;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .WriteTo.File("logs/admin-.log", rollingInterval: RollingInterval.Day, shared: true)
    .CreateLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);
    builder.Host.UseSerilog();

    builder.Services.AddInfrastructure(builder.Configuration);
    builder.Services.AddSingleton<AdminDataService>();
    builder.Services.AddSingleton<ConfigService>();

    builder.Services.AddRazorComponents()
        .AddInteractiveServerComponents();
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
    app.MapRazorComponents<App>()
        .AddInteractiveServerRenderMode();

    await app.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Admin terminated unexpectedly");
}
finally
{
    await Log.CloseAndFlushAsync();
}
