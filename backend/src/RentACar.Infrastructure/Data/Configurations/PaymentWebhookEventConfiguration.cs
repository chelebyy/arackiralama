using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RentACar.Core.Entities;

namespace RentACar.Infrastructure.Data.Configurations;

public sealed class PaymentWebhookEventConfiguration : IEntityTypeConfiguration<PaymentWebhookEvent>
{
    public void Configure(EntityTypeBuilder<PaymentWebhookEvent> builder)
    {
        builder.ToTable("payment_webhook_events");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        builder.Property(x => x.ProviderEventId).HasColumnName("provider_event_id").HasMaxLength(120).IsRequired();
        builder.Property(x => x.Payload).HasColumnName("payload").HasColumnType("text").IsRequired();
        builder.Property(x => x.Processed).HasColumnName("processed");

        builder.HasIndex(x => x.ProviderEventId)
            .HasDatabaseName("idx_webhook_provider_event")
            .IsUnique();
    }
}
