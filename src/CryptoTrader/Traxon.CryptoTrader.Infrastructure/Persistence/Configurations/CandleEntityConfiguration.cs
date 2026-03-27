using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Traxon.CryptoTrader.Domain.Market;

namespace Traxon.CryptoTrader.Infrastructure.Persistence.Configurations;

public sealed class CandleEntityConfiguration : IEntityTypeConfiguration<Candle>
{
    public void Configure(EntityTypeBuilder<Candle> builder)
    {
        builder.ToTable("Candles");
        builder.HasKey(c => c.Id);

        // Candle.Id = Binance OpenTime Unix ms — domain tarafindan uretilir
        builder.Property(c => c.Id)
            .ValueGeneratedNever();

        builder.OwnsOne(c => c.Asset, a =>
        {
            a.Property(x => x.Symbol)
                .HasColumnName("Symbol")
                .HasMaxLength(20)
                .IsRequired();
        });

        builder.OwnsOne(c => c.TimeFrame, tf =>
        {
            tf.Property(x => x.Value)
                .HasColumnName("Interval")
                .HasMaxLength(5)
                .IsRequired();
        });

        builder.Property(c => c.OpenTime)
            .HasColumnType("datetime2")
            .IsRequired();

        builder.Property(c => c.CloseTime)
            .HasColumnType("datetime2")
            .IsRequired();

        builder.Property(c => c.Open)
            .HasColumnType("decimal(18,8)")
            .IsRequired();

        builder.Property(c => c.High)
            .HasColumnType("decimal(18,8)")
            .IsRequired();

        builder.Property(c => c.Low)
            .HasColumnType("decimal(18,8)")
            .IsRequired();

        builder.Property(c => c.Close)
            .HasColumnType("decimal(18,8)")
            .IsRequired();

        builder.Property(c => c.Volume)
            .HasColumnType("decimal(18,8)")
            .IsRequired();

        builder.Property(c => c.QuoteVolume)
            .HasColumnType("decimal(18,8)")
            .IsRequired();

        builder.Property(c => c.TradeCount)
            .IsRequired();

        builder.Property(c => c.IsClosed)
            .IsRequired();

        builder.HasIndex(c => c.OpenTime)
            .HasDatabaseName("IX_Candles_OpenTime");
    }
}
