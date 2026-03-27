using Microsoft.EntityFrameworkCore;
using Traxon.CryptoTrader.Domain.Market;
using Traxon.CryptoTrader.Domain.Trading;
using Traxon.CryptoTrader.Infrastructure.Persistence.Configurations;
using Traxon.CryptoTrader.Infrastructure.Persistence.Models;

namespace Traxon.CryptoTrader.Infrastructure.Persistence;

public sealed class AppDbContext : DbContext
{
    public DbSet<Trade>             Trades             { get; set; } = null!;
    public DbSet<Candle>            Candles            { get; set; } = null!;
    public DbSet<PortfolioSnapshot> PortfolioSnapshots { get; set; } = null!;

    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new TradeEntityConfiguration());
        modelBuilder.ApplyConfiguration(new CandleEntityConfiguration());
        modelBuilder.ApplyConfiguration(new PortfolioSnapshotEntityConfiguration());
    }
}
