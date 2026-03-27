using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Traxon.CryptoTrader.Domain.Trading;

namespace Traxon.CryptoTrader.Infrastructure.Persistence.Configurations;

public sealed class TradeEntityConfiguration : IEntityTypeConfiguration<Trade>
{
    public void Configure(EntityTypeBuilder<Trade> builder)
    {
        builder.ToTable("Trades");
        builder.HasKey(t => t.Id);

        builder.Property(t => t.Id)
            .ValueGeneratedNever();

        builder.Property(t => t.Engine)
            .HasMaxLength(20)
            .IsRequired();

        builder.OwnsOne(t => t.Asset, a =>
        {
            a.Property(x => x.Symbol)
                .HasColumnName("Symbol")
                .HasMaxLength(20)
                .IsRequired();
        });

        builder.OwnsOne(t => t.TimeFrame, tf =>
        {
            tf.Property(x => x.Value)
                .HasColumnName("TimeFrame")
                .HasMaxLength(5)
                .IsRequired();
        });

        builder.Property(t => t.Direction)
            .HasConversion<string>()
            .HasMaxLength(10)
            .IsRequired();

        builder.Property(t => t.EntryPrice)
            .HasColumnType("decimal(18,8)")
            .IsRequired();

        builder.Property(t => t.ExitPrice)
            .HasColumnType("decimal(18,8)");

        builder.Property(t => t.FairValue)
            .HasColumnType("decimal(18,8)")
            .IsRequired();

        builder.Property(t => t.Edge)
            .HasColumnType("decimal(18,8)")
            .IsRequired();

        builder.Property(t => t.PositionSize)
            .HasColumnType("decimal(18,4)")
            .IsRequired();

        builder.Property(t => t.KellyFraction)
            .HasColumnType("decimal(18,8)")
            .IsRequired();

        builder.Property(t => t.MuEstimate)
            .HasColumnType("decimal(18,12)")
            .IsRequired();

        builder.Property(t => t.SigmaEstimate)
            .HasColumnType("decimal(18,12)")
            .IsRequired();

        builder.Property(t => t.Regime)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(t => t.IndicatorSnapshot)
            .HasColumnType("nvarchar(max)")
            .IsRequired();

        builder.Property(t => t.EntryReason)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(t => t.OpenedAt)
            .HasColumnType("datetime2")
            .IsRequired();

        builder.Property(t => t.ClosedAt)
            .HasColumnType("datetime2");

        builder.Property(t => t.Status)
            .HasConversion<string>()
            .HasMaxLength(10)
            .IsRequired();

        builder.Property(t => t.Outcome)
            .HasConversion<string>()
            .HasMaxLength(10);

        builder.Property(t => t.PnL)
            .HasColumnType("decimal(18,4)");

        builder.HasIndex(t => new { t.Engine, t.OpenedAt })
            .HasDatabaseName("IX_Trades_Engine_OpenedAt");
    }
}
