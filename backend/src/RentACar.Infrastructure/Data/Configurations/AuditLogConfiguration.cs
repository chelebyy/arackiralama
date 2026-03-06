using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RentACar.Core.Entities;

namespace RentACar.Infrastructure.Data.Configurations;

public sealed class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("audit_logs");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        builder.Property(x => x.Action).HasColumnName("action").HasMaxLength(120).IsRequired();
        builder.Property(x => x.EntityType).HasColumnName("entity_type").HasMaxLength(120).IsRequired();
        builder.Property(x => x.EntityId).HasColumnName("entity_id").HasMaxLength(120).IsRequired();
        builder.Property(x => x.UserId).HasColumnName("user_id").HasMaxLength(120);
        builder.Property(x => x.Timestamp).HasColumnName("timestamp");
        builder.Property(x => x.Details).HasColumnName("details").HasColumnType("text").IsRequired();
    }
}
