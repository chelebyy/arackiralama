using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RentACar.Core.Entities;

namespace RentACar.Infrastructure.Data.Configurations;

public sealed class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        builder.ToTable("customers");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        builder.Property(x => x.FullName).HasColumnName("full_name").HasMaxLength(140).IsRequired();
        builder.Property(x => x.Phone).HasColumnName("phone").HasMaxLength(40).IsRequired();
        builder.Property(x => x.Email).HasColumnName("email").HasMaxLength(160).IsRequired();
        builder.Property(x => x.BirthDate).HasColumnName("birth_date");
        builder.Property(x => x.LicenseYear).HasColumnName("license_year");
        builder.Property(x => x.IdentityNumber).HasColumnName("identity_number").HasMaxLength(32).IsRequired();
        builder.Property(x => x.Nationality).HasColumnName("nationality").HasMaxLength(80).IsRequired();

        builder.HasIndex(x => x.Email);
        builder.HasIndex(x => x.IdentityNumber);
    }
}
