using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Traxon.CryptoTrader.Infrastructure.Persistence.Models;

namespace Traxon.CryptoTrader.Infrastructure.Persistence.Configurations;

public sealed class PortfolioSnapshotEntityConfiguration : IEntityTypeConfiguration<PortfolioSnapshot>
{
    public void Configure(EntityTypeBuilder<PortfolioSnapshot> builder)
    {
        builder.ToTable("PortfolioSnapshots");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Id)
            .ValueGeneratedOnAdd();

        builder.Property(p => p.Engine)
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(p => p.Timestamp)
            .HasColumnType("datetime2")
            .IsRequired();

        builder.Property(p => p.Balance)
            .HasColumnType("decimal(18,4)")
            .IsRequired();

        builder.Property(p => p.TotalExposure)
            .HasColumnType("decimal(18,4)")
            .IsRequired();

        builder.Property(p => p.TotalPnL)
            .HasColumnType("decimal(18,4)")
            .IsRequired();

        builder.Property(p => p.WinRate)
            .HasColumnType("decimal(5,2)");

        builder.HasIndex(p => new { p.Engine, p.Timestamp })
            .HasDatabaseName("IX_PortfolioSnapshots_Engine_Timestamp");
    }
}
