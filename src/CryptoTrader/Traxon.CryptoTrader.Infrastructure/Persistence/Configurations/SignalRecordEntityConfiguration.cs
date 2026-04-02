using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Traxon.CryptoTrader.Domain.Trading;

namespace Traxon.CryptoTrader.Infrastructure.Persistence.Configurations;

public sealed class SignalRecordEntityConfiguration : IEntityTypeConfiguration<SignalRecord>
{
    public void Configure(EntityTypeBuilder<SignalRecord> builder)
    {
        builder.ToTable("SignalRecords");
        builder.HasKey(s => s.Id);

        builder.Property(s => s.Id)
            .ValueGeneratedNever();

        builder.Property(s => s.Symbol)
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(s => s.TimeFrame)
            .HasMaxLength(5)
            .IsRequired();

        builder.Property(s => s.Direction)
            .HasMaxLength(10)
            .IsRequired();

        builder.Property(s => s.FairValue)
            .HasColumnType("decimal(18,8)")
            .IsRequired();

        builder.Property(s => s.MarketPrice)
            .HasColumnType("decimal(18,8)")
            .IsRequired();

        builder.Property(s => s.Edge)
            .HasColumnType("decimal(18,8)")
            .IsRequired();

        builder.Property(s => s.KellyFraction)
            .HasColumnType("decimal(18,8)")
            .IsRequired();

        builder.Property(s => s.MuEstimate)
            .HasColumnType("decimal(18,12)")
            .IsRequired();

        builder.Property(s => s.SigmaEstimate)
            .HasColumnType("decimal(18,12)")
            .IsRequired();

        builder.Property(s => s.Regime)
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(s => s.SignalScore)
            .HasColumnType("decimal(18,8)");

        builder.Property(s => s.Rsi)
            .HasColumnType("decimal(18,8)")
            .IsRequired();

        builder.Property(s => s.MacdHistogram)
            .HasColumnType("decimal(18,8)")
            .IsRequired();

        builder.Property(s => s.BullishCount)
            .IsRequired();

        builder.Property(s => s.GeneratedAt)
            .HasColumnType("datetime2")
            .IsRequired();

        builder.HasMany(s => s.EngineResults)
            .WithOne()
            .HasForeignKey(e => e.SignalRecordId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(s => new { s.Symbol, s.GeneratedAt })
            .IsDescending(false, true)
            .HasDatabaseName("IX_SignalRecords_Symbol_GeneratedAt");

        builder.HasIndex(s => s.GeneratedAt)
            .IsDescending(true)
            .HasDatabaseName("IX_SignalRecords_GeneratedAt");
    }
}
