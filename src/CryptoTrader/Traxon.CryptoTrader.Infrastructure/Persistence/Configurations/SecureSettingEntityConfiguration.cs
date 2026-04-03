using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Traxon.CryptoTrader.Infrastructure.Persistence.Models;

namespace Traxon.CryptoTrader.Infrastructure.Persistence.Configurations;

public sealed class SecureSettingEntityConfiguration : IEntityTypeConfiguration<SecureSetting>
{
    public void Configure(EntityTypeBuilder<SecureSetting> builder)
    {
        builder.ToTable("SecureSettings");
        builder.HasKey(s => s.Key);

        builder.Property(s => s.Key)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(s => s.EncryptedValue)
            .HasColumnType("nvarchar(max)")
            .IsRequired();

        builder.Property(s => s.UpdatedAt)
            .HasColumnType("datetime2")
            .IsRequired();
    }
}
