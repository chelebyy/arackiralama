using Microsoft.EntityFrameworkCore;
using RentACar.Core.Entities;
using RentACar.Core.Enums;
using RentACar.Core.Interfaces;

namespace RentACar.Infrastructure.Data;

public class RentACarDbContext(DbContextOptions<RentACarDbContext> options)
    : DbContext(options), IApplicationDbContext
{
    private static readonly Guid AlanyaCenterOfficeId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid GazipasaAirportOfficeId = Guid.Parse("11111111-1111-1111-1111-111111111112");
    private static readonly Guid EconomyGroupId = Guid.Parse("22222222-2222-2222-2222-222222222221");
    private static readonly Guid SuvGroupId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    public DbSet<Vehicle> VehicleSet => Set<Vehicle>();
    public DbSet<VehicleGroup> VehicleGroupSet => Set<VehicleGroup>();
    public DbSet<Office> OfficeSet => Set<Office>();
    public DbSet<Customer> CustomerSet => Set<Customer>();
    public DbSet<Reservation> ReservationSet => Set<Reservation>();
    public DbSet<PaymentIntent> PaymentIntentSet => Set<PaymentIntent>();
    public DbSet<PaymentWebhookEvent> PaymentWebhookEventSet => Set<PaymentWebhookEvent>();
    public DbSet<PricingRule> PricingRuleSet => Set<PricingRule>();
    public DbSet<Campaign> CampaignSet => Set<Campaign>();
    public DbSet<ReservationHold> ReservationHoldSet => Set<ReservationHold>();
    public DbSet<AdminUser> AdminUserSet => Set<AdminUser>();
    public DbSet<AuditLog> AuditLogSet => Set<AuditLog>();
    public DbSet<BackgroundJob> BackgroundJobSet => Set<BackgroundJob>();
    public DbSet<FeatureFlag> FeatureFlagSet => Set<FeatureFlag>();

    public IQueryable<Vehicle> Vehicles => VehicleSet.AsNoTracking();
    public IQueryable<VehicleGroup> VehicleGroups => VehicleGroupSet.AsNoTracking();
    public IQueryable<Office> Offices => OfficeSet.AsNoTracking();
    public IQueryable<Customer> Customers => CustomerSet.AsNoTracking();
    public IQueryable<Reservation> Reservations => ReservationSet.AsNoTracking();
    public IQueryable<PaymentIntent> PaymentIntents => PaymentIntentSet.AsNoTracking();
    public IQueryable<PaymentWebhookEvent> PaymentWebhookEvents => PaymentWebhookEventSet.AsNoTracking();
    public IQueryable<PricingRule> PricingRules => PricingRuleSet.AsNoTracking();
    public IQueryable<Campaign> Campaigns => CampaignSet.AsNoTracking();
    public IQueryable<ReservationHold> ReservationHolds => ReservationHoldSet.AsNoTracking();
    public IQueryable<AdminUser> AdminUsers => AdminUserSet.AsNoTracking();
    public IQueryable<AuditLog> AuditLogs => AuditLogSet.AsNoTracking();
    public IQueryable<BackgroundJob> BackgroundJobs => BackgroundJobSet.AsNoTracking();
    public IQueryable<FeatureFlag> FeatureFlags => FeatureFlagSet.AsNoTracking();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        ConfigureVehicleGroup(modelBuilder);
        ConfigureOffice(modelBuilder);
        ConfigureVehicle(modelBuilder);
        ConfigureCustomer(modelBuilder);
        ConfigureReservation(modelBuilder);
        ConfigurePaymentIntent(modelBuilder);
        ConfigurePaymentWebhookEvent(modelBuilder);
        ConfigurePricingRule(modelBuilder);
        ConfigureCampaign(modelBuilder);
        ConfigureReservationHold(modelBuilder);
        ConfigureAdminUser(modelBuilder);
        ConfigureAuditLog(modelBuilder);
        ConfigureBackgroundJob(modelBuilder);
        ConfigureFeatureFlag(modelBuilder);
        SeedData(modelBuilder);
    }

    private static void ConfigureVehicleGroup(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<VehicleGroup>();
        entity.ToTable("vehicle_groups");
        entity.HasKey(x => x.Id);
        entity.Property(x => x.Id).HasColumnName("id");
        entity.Property(x => x.CreatedAt).HasColumnName("created_at");
        entity.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        entity.Property(x => x.NameTr).HasColumnName("name_tr").HasMaxLength(120).IsRequired();
        entity.Property(x => x.NameEn).HasColumnName("name_en").HasMaxLength(120).IsRequired();
        entity.Property(x => x.NameRu).HasColumnName("name_ru").HasMaxLength(120).IsRequired();
        entity.Property(x => x.NameAr).HasColumnName("name_ar").HasMaxLength(120).IsRequired();
        entity.Property(x => x.NameDe).HasColumnName("name_de").HasMaxLength(120).IsRequired();
        entity.Property(x => x.DepositAmount).HasColumnName("deposit_amount").HasPrecision(18, 2);
        entity.Property(x => x.MinAge).HasColumnName("min_age");
        entity.Property(x => x.MinLicenseYears).HasColumnName("min_license_years");
    }

    private static void ConfigureOffice(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<Office>();
        entity.ToTable("offices");
        entity.HasKey(x => x.Id);
        entity.Property(x => x.Id).HasColumnName("id");
        entity.Property(x => x.CreatedAt).HasColumnName("created_at");
        entity.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        entity.Property(x => x.Name).HasColumnName("name").HasMaxLength(120).IsRequired();
        entity.Property(x => x.Address).HasColumnName("address").HasMaxLength(250).IsRequired();
        entity.Property(x => x.Phone).HasColumnName("phone").HasMaxLength(40).IsRequired();
        entity.Property(x => x.IsAirport).HasColumnName("is_airport");
        entity.Property(x => x.OpeningHours).HasColumnName("opening_hours").HasMaxLength(120).IsRequired();
    }

    private static void ConfigureVehicle(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<Vehicle>();
        entity.ToTable("vehicles");
        entity.HasKey(x => x.Id);
        entity.Property(x => x.Id).HasColumnName("id");
        entity.Property(x => x.CreatedAt).HasColumnName("created_at");
        entity.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        entity.Property(x => x.Plate).HasColumnName("plate").HasMaxLength(32).IsRequired();
        entity.Property(x => x.Brand).HasColumnName("brand").HasMaxLength(80).IsRequired();
        entity.Property(x => x.Model).HasColumnName("model").HasMaxLength(80).IsRequired();
        entity.Property(x => x.Year).HasColumnName("year");
        entity.Property(x => x.Color).HasColumnName("color").HasMaxLength(40).IsRequired();
        entity.Property(x => x.GroupId).HasColumnName("group_id");
        entity.Property(x => x.OfficeId).HasColumnName("office_id");
        entity.Property(x => x.Status).HasColumnName("status").HasMaxLength(40).IsRequired();

        entity.HasIndex(x => x.Plate).IsUnique();
        entity.HasIndex(x => new { x.OfficeId, x.Status, x.GroupId }).HasDatabaseName("idx_vehicles_office_status_group");
        entity.HasIndex(x => new { x.OfficeId, x.GroupId, x.Status })
            .HasDatabaseName("idx_vehicles_available")
            .HasFilter("status = 'Available'");

        entity.HasOne(x => x.Group)
            .WithMany(x => x.Vehicles)
            .HasForeignKey(x => x.GroupId)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasOne(x => x.Office)
            .WithMany(x => x.Vehicles)
            .HasForeignKey(x => x.OfficeId)
            .OnDelete(DeleteBehavior.Restrict);
    }

    private static void ConfigureCustomer(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<Customer>();
        entity.ToTable("customers");
        entity.HasKey(x => x.Id);
        entity.Property(x => x.Id).HasColumnName("id");
        entity.Property(x => x.CreatedAt).HasColumnName("created_at");
        entity.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        entity.Property(x => x.FullName).HasColumnName("full_name").HasMaxLength(140).IsRequired();
        entity.Property(x => x.Phone).HasColumnName("phone").HasMaxLength(40).IsRequired();
        entity.Property(x => x.Email).HasColumnName("email").HasMaxLength(160).IsRequired();
        entity.Property(x => x.BirthDate).HasColumnName("birth_date");
        entity.Property(x => x.LicenseYear).HasColumnName("license_year");
        entity.Property(x => x.IdentityNumber).HasColumnName("identity_number").HasMaxLength(32).IsRequired();
        entity.Property(x => x.Nationality).HasColumnName("nationality").HasMaxLength(80).IsRequired();

        entity.HasIndex(x => x.Email);
        entity.HasIndex(x => x.IdentityNumber);
    }

    private static void ConfigureReservation(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<Reservation>();
        entity.ToTable("reservations");
        entity.HasKey(x => x.Id);
        entity.Property(x => x.Id).HasColumnName("id");
        entity.Property(x => x.CreatedAt).HasColumnName("created_at");
        entity.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        entity.Property(x => x.PublicCode).HasColumnName("public_code").HasMaxLength(24).IsRequired();
        entity.Property(x => x.CustomerId).HasColumnName("customer_id");
        entity.Property(x => x.VehicleId).HasColumnName("vehicle_id");
        entity.Property(x => x.PickupDateTime).HasColumnName("pickup_datetime");
        entity.Property(x => x.ReturnDateTime).HasColumnName("return_datetime");
        entity.Property(x => x.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();
        entity.Property(x => x.TotalAmount).HasColumnName("total_amount").HasPrecision(18, 2);

        entity.HasIndex(x => x.PublicCode).IsUnique();
        entity.HasIndex(x => new { x.VehicleId, x.PickupDateTime, x.ReturnDateTime })
            .HasDatabaseName("idx_reservations_vehicle_dates");
        entity.HasIndex(x => new { x.VehicleId, x.PickupDateTime, x.ReturnDateTime })
            .HasDatabaseName("idx_reservations_active_dates")
            .HasFilter("status IN ('Paid','Active')");
        entity.HasIndex(x => new { x.Status, x.CreatedAt })
            .HasDatabaseName("idx_reservations_status_created");

        entity.HasOne(x => x.Customer)
            .WithMany(x => x.Reservations)
            .HasForeignKey(x => x.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasOne(x => x.Vehicle)
            .WithMany()
            .HasForeignKey(x => x.VehicleId)
            .OnDelete(DeleteBehavior.Restrict);
    }

    private static void ConfigurePaymentIntent(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<PaymentIntent>();
        entity.ToTable("payment_intents");
        entity.HasKey(x => x.Id);
        entity.Property(x => x.Id).HasColumnName("id");
        entity.Property(x => x.CreatedAt).HasColumnName("created_at");
        entity.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        entity.Property(x => x.ReservationId).HasColumnName("reservation_id");
        entity.Property(x => x.Amount).HasColumnName("amount").HasPrecision(18, 2);
        entity.Property(x => x.Status).HasColumnName("status").HasMaxLength(40).IsRequired();
        entity.Property(x => x.Provider).HasColumnName("provider").HasMaxLength(50).IsRequired();
        entity.Property(x => x.IdempotencyKey).HasColumnName("idempotency_key").HasMaxLength(120).IsRequired();

        entity.HasIndex(x => x.IdempotencyKey)
            .HasDatabaseName("idx_payment_idempotency")
            .IsUnique();

        entity.HasOne(x => x.Reservation)
            .WithMany(x => x.PaymentIntents)
            .HasForeignKey(x => x.ReservationId)
            .OnDelete(DeleteBehavior.Cascade);
    }

    private static void ConfigurePaymentWebhookEvent(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<PaymentWebhookEvent>();
        entity.ToTable("payment_webhook_events");
        entity.HasKey(x => x.Id);
        entity.Property(x => x.Id).HasColumnName("id");
        entity.Property(x => x.CreatedAt).HasColumnName("created_at");
        entity.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        entity.Property(x => x.ProviderEventId).HasColumnName("provider_event_id").HasMaxLength(120).IsRequired();
        entity.Property(x => x.Payload).HasColumnName("payload").HasColumnType("text").IsRequired();
        entity.Property(x => x.Processed).HasColumnName("processed");

        entity.HasIndex(x => x.ProviderEventId)
            .HasDatabaseName("idx_webhook_provider_event")
            .IsUnique();
    }

    private static void ConfigurePricingRule(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<PricingRule>();
        entity.ToTable("pricing_rules");
        entity.HasKey(x => x.Id);
        entity.Property(x => x.Id).HasColumnName("id");
        entity.Property(x => x.CreatedAt).HasColumnName("created_at");
        entity.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        entity.Property(x => x.VehicleGroupId).HasColumnName("vehicle_group_id");
        entity.Property(x => x.StartDate).HasColumnName("start_date");
        entity.Property(x => x.EndDate).HasColumnName("end_date");
        entity.Property(x => x.DailyPrice).HasColumnName("daily_price").HasPrecision(18, 2);
        entity.Property(x => x.Multiplier).HasColumnName("multiplier").HasPrecision(8, 4);

        entity.HasIndex(x => new { x.StartDate, x.EndDate }).HasDatabaseName("idx_pricing_date_range");

        entity.HasOne(x => x.VehicleGroup)
            .WithMany(x => x.PricingRules)
            .HasForeignKey(x => x.VehicleGroupId)
            .OnDelete(DeleteBehavior.Cascade);
    }

    private static void ConfigureCampaign(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<Campaign>();
        entity.ToTable("campaigns");
        entity.HasKey(x => x.Id);
        entity.Property(x => x.Id).HasColumnName("id");
        entity.Property(x => x.CreatedAt).HasColumnName("created_at");
        entity.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        entity.Property(x => x.Code).HasColumnName("code").HasMaxLength(64).IsRequired();
        entity.Property(x => x.DiscountType).HasColumnName("discount_type").HasMaxLength(32).IsRequired();
        entity.Property(x => x.DiscountValue).HasColumnName("discount_value").HasPrecision(18, 2);
        entity.Property(x => x.MinDays).HasColumnName("min_days");
        entity.Property(x => x.ValidFrom).HasColumnName("valid_from");
        entity.Property(x => x.ValidUntil).HasColumnName("valid_until");

        entity.HasIndex(x => x.Code).IsUnique();
    }

    private static void ConfigureReservationHold(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<ReservationHold>();
        entity.ToTable("reservation_holds");
        entity.HasKey(x => x.Id);
        entity.Property(x => x.Id).HasColumnName("id");
        entity.Property(x => x.CreatedAt).HasColumnName("created_at");
        entity.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        entity.Property(x => x.ReservationId).HasColumnName("reservation_id");
        entity.Property(x => x.VehicleId).HasColumnName("vehicle_id");
        entity.Property(x => x.ExpiresAt).HasColumnName("expires_at");
        entity.Property(x => x.SessionId).HasColumnName("session_id").HasMaxLength(120).IsRequired();

        entity.HasIndex(x => x.ExpiresAt).HasDatabaseName("idx_holds_expires");
        entity.HasIndex(x => x.SessionId).HasDatabaseName("idx_holds_session");

        entity.HasOne(x => x.Reservation)
            .WithMany(x => x.Holds)
            .HasForeignKey(x => x.ReservationId)
            .OnDelete(DeleteBehavior.Cascade);

        entity.HasOne(x => x.Vehicle)
            .WithMany()
            .HasForeignKey(x => x.VehicleId)
            .OnDelete(DeleteBehavior.Restrict);
    }

    private static void ConfigureAdminUser(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<AdminUser>();
        entity.ToTable("admin_users");
        entity.HasKey(x => x.Id);
        entity.Property(x => x.Id).HasColumnName("id");
        entity.Property(x => x.CreatedAt).HasColumnName("created_at");
        entity.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        entity.Property(x => x.Email).HasColumnName("email").HasMaxLength(160).IsRequired();
        entity.Property(x => x.PasswordHash).HasColumnName("password_hash").HasMaxLength(256).IsRequired();
        entity.Property(x => x.FullName).HasColumnName("full_name").HasMaxLength(140).IsRequired();
        entity.Property(x => x.Role).HasColumnName("role").HasMaxLength(40).IsRequired();
        entity.Property(x => x.IsActive).HasColumnName("is_active");

        entity.HasIndex(x => x.Email).IsUnique();
    }

    private static void ConfigureAuditLog(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<AuditLog>();
        entity.ToTable("audit_logs");
        entity.HasKey(x => x.Id);
        entity.Property(x => x.Id).HasColumnName("id");
        entity.Property(x => x.CreatedAt).HasColumnName("created_at");
        entity.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        entity.Property(x => x.Action).HasColumnName("action").HasMaxLength(120).IsRequired();
        entity.Property(x => x.EntityType).HasColumnName("entity_type").HasMaxLength(120).IsRequired();
        entity.Property(x => x.EntityId).HasColumnName("entity_id").HasMaxLength(120).IsRequired();
        entity.Property(x => x.UserId).HasColumnName("user_id").HasMaxLength(120);
        entity.Property(x => x.Timestamp).HasColumnName("timestamp");
        entity.Property(x => x.Details).HasColumnName("details").HasColumnType("text").IsRequired();
    }

    private static void ConfigureBackgroundJob(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<BackgroundJob>();
        entity.ToTable("background_jobs");
        entity.HasKey(x => x.Id);
        entity.Property(x => x.Id).HasColumnName("id");
        entity.Property(x => x.CreatedAt).HasColumnName("created_at");
        entity.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        entity.Property(x => x.Type).HasColumnName("type").HasMaxLength(120).IsRequired();
        entity.Property(x => x.Payload).HasColumnName("payload").HasColumnType("text").IsRequired();
        entity.Property(x => x.Status).HasColumnName("status").HasMaxLength(40).IsRequired();
        entity.Property(x => x.RetryCount).HasColumnName("retry_count");
        entity.Property(x => x.ScheduledAt).HasColumnName("scheduled_at");
    }

    private static void ConfigureFeatureFlag(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<FeatureFlag>();
        entity.ToTable("feature_flags");
        entity.HasKey(x => x.Id);
        entity.Property(x => x.Id).HasColumnName("id");
        entity.Property(x => x.CreatedAt).HasColumnName("created_at");
        entity.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        entity.Property(x => x.Name).HasColumnName("name").HasMaxLength(120).IsRequired();
        entity.Property(x => x.Enabled).HasColumnName("enabled");
        entity.Property(x => x.Description).HasColumnName("description").HasMaxLength(500).IsRequired();

        entity.HasIndex(x => x.Name).IsUnique();
    }

    private static void SeedData(ModelBuilder modelBuilder)
    {
        var now = new DateTime(2026, 3, 2, 0, 0, 0, DateTimeKind.Utc);

        modelBuilder.Entity<Office>().HasData(
            new Office
            {
                Id = AlanyaCenterOfficeId,
                Name = "Alanya Merkez",
                Address = "Sekerhane Mah. Ataturk Blv. No:10 Alanya/Antalya",
                Phone = "+90 242 000 00 01",
                IsAirport = false,
                OpeningHours = "08:00-22:00",
                CreatedAt = now,
                UpdatedAt = now
            },
            new Office
            {
                Id = GazipasaAirportOfficeId,
                Name = "Gazipasa Airport",
                Address = "Gazipasa-Alanya Havalimani Terminal Ici",
                Phone = "+90 242 000 00 02",
                IsAirport = true,
                OpeningHours = "24/7",
                CreatedAt = now,
                UpdatedAt = now
            });

        modelBuilder.Entity<VehicleGroup>().HasData(
            new VehicleGroup
            {
                Id = EconomyGroupId,
                NameTr = "Ekonomi",
                NameEn = "Economy",
                NameRu = "Economy",
                NameAr = "Economy",
                NameDe = "Economy",
                DepositAmount = 2000m,
                MinAge = 21,
                MinLicenseYears = 2,
                CreatedAt = now,
                UpdatedAt = now
            },
            new VehicleGroup
            {
                Id = SuvGroupId,
                NameTr = "SUV",
                NameEn = "SUV",
                NameRu = "SUV",
                NameAr = "SUV",
                NameDe = "SUV",
                DepositAmount = 3500m,
                MinAge = 25,
                MinLicenseYears = 3,
                CreatedAt = now,
                UpdatedAt = now
            });

        modelBuilder.Entity<FeatureFlag>().HasData(
            new FeatureFlag
            {
                Id = Guid.Parse("33333333-3333-3333-3333-333333333331"),
                Name = "EnableOnlinePayment",
                Enabled = false,
                Description = "Online payment provider integration toggle",
                CreatedAt = now,
                UpdatedAt = now
            },
            new FeatureFlag
            {
                Id = Guid.Parse("33333333-3333-3333-3333-333333333332"),
                Name = "EnableCampaigns",
                Enabled = true,
                Description = "Campaign and discount rules toggle",
                CreatedAt = now,
                UpdatedAt = now
            });
    }
}
