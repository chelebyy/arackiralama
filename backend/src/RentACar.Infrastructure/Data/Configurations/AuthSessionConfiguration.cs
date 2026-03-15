using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RentACar.Core.Entities;

namespace RentACar.Infrastructure.Data.Configurations;

public sealed class AuthSessionConfiguration : IEntityTypeConfiguration<AuthSession>
{
    public void Configure(EntityTypeBuilder<AuthSession> builder)
    {
        builder.ToTable("auth_sessions");
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
        builder.Property(x => x.RefreshTokenHash).HasColumnName("refresh_token_hash").HasMaxLength(256).IsRequired();
        builder.Property(x => x.RefreshTokenExpiresAtUtc).HasColumnName("refresh_token_expires_at_utc");
        builder.Property(x => x.LastSeenAtUtc).HasColumnName("last_seen_at_utc");
        builder.Property(x => x.RevokedAtUtc).HasColumnName("revoked_at_utc");
        builder.Property(x => x.ReplacedBySessionId).HasColumnName("replaced_by_session_id");
        builder.Property(x => x.CreatedByIp).HasColumnName("created_by_ip").HasMaxLength(64);
        builder.Property(x => x.UserAgent).HasColumnName("user_agent").HasMaxLength(512);

        builder.HasIndex(x => new { x.PrincipalType, x.PrincipalId })
            .HasDatabaseName("idx_auth_sessions_principal");
        builder.HasIndex(x => x.RefreshTokenHash)
            .HasDatabaseName("ux_auth_sessions_refresh_token_hash")
            .IsUnique();
        builder.HasIndex(x => x.RefreshTokenExpiresAtUtc)
            .HasDatabaseName("idx_auth_sessions_refresh_token_expires_at_utc");
        builder.HasIndex(x => x.RevokedAtUtc)
            .HasDatabaseName("idx_auth_sessions_revoked_at_utc");
    }
}
