using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RentACar.Core.Entities;

namespace RentACar.Infrastructure.Data.Configurations;

public sealed class BackgroundJobConfiguration : IEntityTypeConfiguration<BackgroundJob>
{
    public void Configure(EntityTypeBuilder<BackgroundJob> builder)
    {
        builder.ToTable("background_jobs");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        builder.Property(x => x.Type).HasColumnName("type").HasMaxLength(120).IsRequired();
        builder.Property(x => x.Payload).HasColumnName("payload").HasColumnType("text").IsRequired();
        builder.Property(x => x.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(40)
            .IsRequired();
        builder.Property(x => x.RetryCount).HasColumnName("retry_count");
        builder.Property(x => x.ScheduledAt).HasColumnName("scheduled_at");

        builder.HasIndex(x => new { x.Type, x.Payload })
            .IsUnique()
            .HasFilter("\"status\" IN ('Pending', 'Processing')");
    }
}
