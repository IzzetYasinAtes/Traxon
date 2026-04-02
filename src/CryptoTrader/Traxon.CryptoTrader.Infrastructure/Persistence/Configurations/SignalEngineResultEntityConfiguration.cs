using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Traxon.CryptoTrader.Domain.Trading;

namespace Traxon.CryptoTrader.Infrastructure.Persistence.Configurations;

public sealed class SignalEngineResultEntityConfiguration : IEntityTypeConfiguration<SignalEngineResult>
{
    public void Configure(EntityTypeBuilder<SignalEngineResult> builder)
    {
        builder.ToTable("SignalEngineResults");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .ValueGeneratedNever();

        builder.Property(e => e.SignalRecordId)
            .IsRequired();

        builder.Property(e => e.EngineName)
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(e => e.Accepted)
            .IsRequired();

        builder.Property(e => e.RejectionReason)
            .HasMaxLength(100);

        builder.Property(e => e.TradeId);

        builder.Property(e => e.EvaluatedAt)
            .HasColumnType("datetime2")
            .IsRequired();

        builder.HasIndex(e => e.SignalRecordId)
            .HasDatabaseName("IX_SignalEngineResults_SignalRecordId");
    }
}
