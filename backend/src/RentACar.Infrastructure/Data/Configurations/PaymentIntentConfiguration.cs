using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RentACar.Core.Entities;

namespace RentACar.Infrastructure.Data.Configurations;

public sealed class PaymentIntentConfiguration : IEntityTypeConfiguration<PaymentIntent>
{
    public void Configure(EntityTypeBuilder<PaymentIntent> builder)
    {
        builder.ToTable("payment_intents");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        builder.Property(x => x.ReservationId).HasColumnName("reservation_id");
        builder.Property(x => x.Amount).HasColumnName("amount").HasPrecision(18, 2);
        builder.Property(x => x.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(40)
            .IsRequired();
        builder.Property(x => x.Provider).HasColumnName("provider").HasMaxLength(50).IsRequired();
        builder.Property(x => x.IdempotencyKey).HasColumnName("idempotency_key").HasMaxLength(120).IsRequired();
        builder.Property(x => x.ProviderIntentId).HasColumnName("provider_intent_id").HasMaxLength(120);
        builder.Property(x => x.ProviderTransactionId).HasColumnName("provider_transaction_id").HasMaxLength(160);
        builder.Property(x => x.RefundIdempotencyKey).HasColumnName("refund_idempotency_key").HasMaxLength(120);
        builder.Property(x => x.RefundReferenceId).HasColumnName("refund_reference_id").HasMaxLength(160);
        builder.Property(x => x.RefundedAmount).HasColumnName("refunded_amount").HasPrecision(18, 2);
        builder.Property(x => x.RefundReason).HasColumnName("refund_reason").HasMaxLength(500);

        builder.HasIndex(x => new { x.Provider, x.IdempotencyKey })
            .HasDatabaseName("idx_payment_provider_idempotency")
            .IsUnique();
        builder.HasIndex(x => new { x.ReservationId, x.RefundIdempotencyKey })
            .HasDatabaseName("idx_payment_reservation_refund_idempotency");
        builder.HasIndex(x => x.ProviderIntentId)
            .HasDatabaseName("idx_payment_provider_intent_id");
        builder.HasIndex(x => x.ProviderTransactionId)
            .HasDatabaseName("idx_payment_provider_transaction_id");

        builder.HasOne(x => x.Reservation)
            .WithMany(x => x.PaymentIntents)
            .HasForeignKey(x => x.ReservationId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
