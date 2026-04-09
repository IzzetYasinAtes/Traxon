using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Traxon.CryptoTrader.Domain.Market;

namespace Traxon.CryptoTrader.Infrastructure.Persistence.Configurations;

public sealed class FuturesSnapshotEntityConfiguration : IEntityTypeConfiguration<FuturesSnapshot>
{
    public void Configure(EntityTypeBuilder<FuturesSnapshot> builder)
    {
        builder.ToTable("FuturesSnapshots");
        builder.HasKey(f => f.Id);
        builder.Property(f => f.Id).ValueGeneratedOnAdd();
        builder.Property(f => f.Symbol).HasMaxLength(20).IsRequired();
        builder.Property(f => f.FundingRate).HasColumnType("decimal(18,10)");
        builder.Property(f => f.OpenInterest).HasColumnType("decimal(18,4)");
        builder.Property(f => f.OrderBookImbalance).HasColumnType("decimal(18,8)");
        builder.Property(f => f.ObiPersistence).HasColumnType("decimal(5,2)");
        builder.Property(f => f.BidVolume).HasColumnType("decimal(18,4)");
        builder.Property(f => f.AskVolume).HasColumnType("decimal(18,4)");
        builder.HasIndex(f => new { f.Symbol, f.Timestamp });
    }
}
