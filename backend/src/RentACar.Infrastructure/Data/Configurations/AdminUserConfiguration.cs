using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RentACar.Core.Entities;

namespace RentACar.Infrastructure.Data.Configurations;

public sealed class AdminUserConfiguration : IEntityTypeConfiguration<AdminUser>
{
    public void Configure(EntityTypeBuilder<AdminUser> builder)
    {
        builder.ToTable("admin_users");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        builder.Property(x => x.Email).HasColumnName("email").HasMaxLength(160).IsRequired();
        builder.Property(x => x.NormalizedEmail).HasColumnName("normalized_email").HasMaxLength(160).IsRequired();
        builder.Property(x => x.PasswordHash).HasColumnName("password_hash").HasMaxLength(256).IsRequired();
        builder.Property(x => x.FullName).HasColumnName("full_name").HasMaxLength(140).IsRequired();
        builder.Property(x => x.Role).HasColumnName("role").HasMaxLength(40).IsRequired();
        builder.Property(x => x.IsActive).HasColumnName("is_active");
        builder.Property(x => x.FailedLoginCount).HasColumnName("failed_login_count").HasDefaultValue(0);
        builder.Property(x => x.LockoutEndUtc).HasColumnName("lockout_end_utc");
        builder.Property(x => x.LastLoginAtUtc).HasColumnName("last_login_at_utc");
        builder.Property(x => x.TokenVersion).HasColumnName("token_version").HasDefaultValue(0);

        builder.HasIndex(x => x.Email).HasDatabaseName("idx_admin_users_email");
        builder.HasIndex(x => x.NormalizedEmail).HasDatabaseName("ux_admin_users_normalized_email").IsUnique();
    }
}
