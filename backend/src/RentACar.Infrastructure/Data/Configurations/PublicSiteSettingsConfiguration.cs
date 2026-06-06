using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RentACar.Core.Entities;

namespace RentACar.Infrastructure.Data.Configurations;

public sealed class PublicSiteSettingsConfiguration : IEntityTypeConfiguration<PublicSiteSettings>
{
    public void Configure(EntityTypeBuilder<PublicSiteSettings> builder)
    {
        builder.ToTable("public_site_settings");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        builder.Property(x => x.Key).HasColumnName("key").HasMaxLength(80).IsRequired();
        builder.Property(x => x.CompanyName).HasColumnName("company_name").HasMaxLength(160).IsRequired();
        builder.Property(x => x.CompanyAddress).HasColumnName("company_address").HasMaxLength(500).IsRequired();
        builder.Property(x => x.CompanyPhone).HasColumnName("company_phone").HasMaxLength(80).IsRequired();
        builder.Property(x => x.CompanyEmail).HasColumnName("company_email").HasMaxLength(160).IsRequired();
        builder.Property(x => x.WorkingHours).HasColumnName("working_hours").HasMaxLength(160).IsRequired();
        builder.Property(x => x.HeaderLinksJson).HasColumnName("header_links_json").HasColumnType("jsonb").IsRequired();
        builder.Property(x => x.HeroLinksJson).HasColumnName("hero_links_json").HasColumnType("jsonb").IsRequired();
        builder.Property(x => x.QuickLinksJson).HasColumnName("quick_links_json").HasColumnType("jsonb").IsRequired();
        builder.Property(x => x.SocialLinksJson).HasColumnName("social_links_json").HasColumnType("jsonb").IsRequired();
        builder.Property(x => x.FooterBottomLinksJson).HasColumnName("footer_bottom_links_json").HasColumnType("jsonb").IsRequired();
        builder.Property(x => x.ContactPageChannelsJson).HasColumnName("contact_page_channels_json").HasColumnType("jsonb").IsRequired();
        builder.Property(x => x.ContactPageOfficesJson).HasColumnName("contact_page_offices_json").HasColumnType("jsonb").IsRequired();
        builder.Property(x => x.ContactPageWorkingHoursJson).HasColumnName("contact_page_working_hours_json").HasColumnType("jsonb").IsRequired();
        builder.Property(x => x.ContactPageMapTitle).HasColumnName("contact_page_map_title").HasMaxLength(160).IsRequired();
        builder.Property(x => x.ContactPageMapEmbedUrl).HasColumnName("contact_page_map_embed_url").HasMaxLength(1200).IsRequired();
        builder.Property(x => x.ContactPageMapIsVisible).HasColumnName("contact_page_map_is_visible").IsRequired();
        builder.Property(x => x.PagesJson).HasColumnName("pages_json").HasColumnType("jsonb").IsRequired();

        builder.HasIndex(x => x.Key).IsUnique();
    }
}
