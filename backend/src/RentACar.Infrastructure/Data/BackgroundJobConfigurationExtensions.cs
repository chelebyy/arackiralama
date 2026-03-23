using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RentACar.Core.Entities;

namespace RentACar.Infrastructure.Data;

// Data/Configurations dizinine yazma sorunu nedeniyle ek map burada tutuluyor.
// ApplyConfigurationsFromAssembly ile otomatik uygulanır.
public sealed class BackgroundJobFailureFieldsConfiguration : IEntityTypeConfiguration<BackgroundJob>
{
    public void Configure(EntityTypeBuilder<BackgroundJob> builder)
    {
        builder.Property(x => x.LastError)
            .HasColumnName("last_error")
            .HasColumnType("text");

        builder.Property(x => x.FailedAt)
            .HasColumnName("failed_at");
    }
}
