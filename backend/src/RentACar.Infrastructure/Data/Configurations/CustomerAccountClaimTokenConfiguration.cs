using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RentACar.Core.Entities;

namespace RentACar.Infrastructure.Data.Configurations;

public sealed class CustomerAccountClaimTokenConfiguration : IEntityTypeConfiguration<CustomerAccountClaimToken>
{
    public void Configure(EntityTypeBuilder<CustomerAccountClaimToken> builder)
    {
        builder.ToTable("customer_account_claim_tokens");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        builder.Property(x => x.CustomerId).HasColumnName("customer_id");
        builder.Property(x => x.TokenHash).HasColumnName("token_hash").HasMaxLength(256).IsRequired();
        builder.Property(x => x.ExpiresAtUtc).HasColumnName("expires_at_utc");
        builder.Property(x => x.ConsumedAtUtc).HasColumnName("consumed_at_utc");
        builder.Property(x => x.SupersededAtUtc).HasColumnName("superseded_at_utc");
        builder.Property(x => x.IssuedFromIp).HasColumnName("issued_from_ip").HasMaxLength(64);
        builder.Property(x => x.IssuedUserAgent).HasColumnName("issued_user_agent").HasMaxLength(512);

        builder.HasIndex(x => x.CustomerId)
            .HasDatabaseName("idx_customer_account_claim_tokens_customer");
        builder.HasIndex(x => x.TokenHash)
            .HasDatabaseName("ux_customer_account_claim_tokens_token_hash")
            .IsUnique();
        builder.HasIndex(x => x.ExpiresAtUtc)
            .HasDatabaseName("idx_customer_account_claim_tokens_expires_at_utc");
        builder.HasIndex(x => new { x.CustomerId, x.SupersededAtUtc, x.ConsumedAtUtc })
            .HasDatabaseName("idx_customer_account_claim_tokens_active_lookup");
    }
}