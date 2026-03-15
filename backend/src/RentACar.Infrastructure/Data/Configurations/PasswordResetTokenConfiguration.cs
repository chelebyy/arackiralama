using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RentACar.Core.Entities;

namespace RentACar.Infrastructure.Data.Configurations;

public sealed class PasswordResetTokenConfiguration : IEntityTypeConfiguration<PasswordResetToken>
{
    public void Configure(EntityTypeBuilder<PasswordResetToken> builder)
    {
        builder.ToTable("password_reset_tokens");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        builder.Property(x => x.PrincipalType)
            .HasColumnName("principal_type")
            .HasConversion<string>()
            .HasMaxLength(24)
            .IsRequired();
        builder.Property(x => x.PrincipalId).HasColumnName("principal_id");
        builder.Property(x => x.TokenHash).HasColumnName("token_hash").HasMaxLength(256).IsRequired();
        builder.Property(x => x.ExpiresAtUtc).HasColumnName("expires_at_utc");
        builder.Property(x => x.ConsumedAtUtc).HasColumnName("consumed_at_utc");

        builder.HasIndex(x => new { x.PrincipalType, x.PrincipalId })
            .HasDatabaseName("idx_password_reset_tokens_principal");
        builder.HasIndex(x => x.TokenHash)
            .HasDatabaseName("ux_password_reset_tokens_token_hash")
            .IsUnique();
        builder.HasIndex(x => x.ExpiresAtUtc)
            .HasDatabaseName("idx_password_reset_tokens_expires_at_utc");
        builder.HasIndex(x => x.ConsumedAtUtc)
            .HasDatabaseName("idx_password_reset_tokens_consumed_at_utc");
    }
}
