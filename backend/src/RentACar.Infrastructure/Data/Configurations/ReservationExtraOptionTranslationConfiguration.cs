using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RentACar.Core.Entities;

namespace RentACar.Infrastructure.Data.Configurations;

public sealed class ReservationExtraOptionTranslationConfiguration : IEntityTypeConfiguration<ReservationExtraOptionTranslation>
{
    public void Configure(EntityTypeBuilder<ReservationExtraOptionTranslation> builder)
    {
        builder.ToTable("reservation_extra_option_translations", tableBuilder =>
            tableBuilder.HasCheckConstraint("ck_reservation_extra_option_translations_locale", "locale IN ('tr', 'en', 'de', 'ru', 'ar')"));
        builder.HasKey(x => new { x.OptionId, x.Locale });
        builder.Property(x => x.OptionId).HasColumnName("option_id");
        builder.Property(x => x.Locale).HasColumnName("locale").HasMaxLength(5).IsRequired();
        builder.Property(x => x.Name).HasColumnName("name").HasMaxLength(100).IsRequired();
        builder.Property(x => x.Description).HasColumnName("description").HasMaxLength(300).IsRequired();

        builder.HasIndex(x => x.Locale).HasDatabaseName("idx_reservation_extra_option_translations_locale");
        builder.HasOne(x => x.Option)
            .WithMany(x => x.Translations)
            .HasForeignKey(x => x.OptionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasData(
            TranslationRows.All);
    }

    private static class TranslationRows
    {
        internal static readonly ReservationExtraOptionTranslation[] All =
        [
            Row(SeedDataConstants.ChildSeatExtraOptionId, "tr", "Çocuk Koltuğu", "9-36 kg arası çocuklar için uygundur"),
            Row(SeedDataConstants.ChildSeatExtraOptionId, "en", "Child Seat", "Suitable for children 9-36 kg"),
            Row(SeedDataConstants.ChildSeatExtraOptionId, "de", "Kindersitz", "Geeignet für Kinder 9-36 kg"),
            Row(SeedDataConstants.ChildSeatExtraOptionId, "ru", "Детское кресло", "Подходит для детей 9-36 кг"),
            Row(SeedDataConstants.ChildSeatExtraOptionId, "ar", "مقعد أطفال", "مناسب للأطفال 9-36 كجم"),
            Row(SeedDataConstants.AdditionalDriverExtraOptionId, "tr", "Ek Sürücü", "Araçta ek sürücü de kullanabilir"),
            Row(SeedDataConstants.AdditionalDriverExtraOptionId, "en", "Additional Driver", "An additional driver can also drive the vehicle"),
            Row(SeedDataConstants.AdditionalDriverExtraOptionId, "de", "Zusätzlicher Fahrer", "Ein zusätzlicher Fahrer kann das Fahrzeug auch fahren"),
            Row(SeedDataConstants.AdditionalDriverExtraOptionId, "ru", "Дополнительный водитель", "Дополнительный водитель также может управлять автомобилем"),
            Row(SeedDataConstants.AdditionalDriverExtraOptionId, "ar", "سائق إضافي", "يمكن لسائق إضافي قيادة السيارة أيضاً"),
            Row(SeedDataConstants.GpsExtraOptionId, "tr", "GPS Navigasyon", "Navigasyon cihazıyla asla kaybolmayın"),
            Row(SeedDataConstants.GpsExtraOptionId, "en", "GPS Navigation", "Never get lost with navigation device"),
            Row(SeedDataConstants.GpsExtraOptionId, "de", "GPS-Navigation", "Mit Navigationsgerät nie verloren gehen"),
            Row(SeedDataConstants.GpsExtraOptionId, "ru", "GPS-навигатор", "Никогда не теряйтесь с навигационным устройством"),
            Row(SeedDataConstants.GpsExtraOptionId, "ar", "جهاز ملاحة GPS", "لا تضل الطريق مع جهاز الملاحة"),
            Row(SeedDataConstants.WifiExtraOptionId, "tr", "WiFi", "Araç içinde internetle bağlı kalın"),
            Row(SeedDataConstants.WifiExtraOptionId, "en", "WiFi", "Stay connected with in-car internet"),
            Row(SeedDataConstants.WifiExtraOptionId, "de", "WiFi", "Bleiben Sie mit Internet im Auto verbunden"),
            Row(SeedDataConstants.WifiExtraOptionId, "ru", "WiFi", "Оставайтесь на связи с интернетом в автомобиле"),
            Row(SeedDataConstants.WifiExtraOptionId, "ar", "واي فاي", "ابقَ متصلاً بالإنترنت داخل السيارة")
        ];

        private static ReservationExtraOptionTranslation Row(Guid optionId, string locale, string name, string description) => new()
        {
            OptionId = optionId,
            Locale = locale,
            Name = name,
            Description = description
        };
    }
}
