using Microsoft.EntityFrameworkCore;
using RentACar.Core.Entities;
using RentACar.Core.Interfaces;

namespace RentACar.Infrastructure.Data;

public class RentACarDbContext(DbContextOptions<RentACarDbContext> options)
    : DbContext(options), IApplicationDbContext
{
    public DbSet<Vehicle> Vehicles => Set<Vehicle>();
    public DbSet<VehicleGroup> VehicleGroups => Set<VehicleGroup>();
    public DbSet<Office> Offices => Set<Office>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Reservation> Reservations => Set<Reservation>();
    public DbSet<ReservationExtraOption> ReservationExtraOptions => Set<ReservationExtraOption>();
    public DbSet<ReservationExtraOptionTranslation> ReservationExtraOptionTranslations => Set<ReservationExtraOptionTranslation>();
    public DbSet<ReservationExtraOptionVehicleGroup> ReservationExtraOptionVehicleGroups => Set<ReservationExtraOptionVehicleGroup>();
    public DbSet<ReservationSelectedExtra> ReservationSelectedExtras => Set<ReservationSelectedExtra>();
    public DbSet<PaymentIntent> PaymentIntents => Set<PaymentIntent>();
    public DbSet<PaymentWebhookEvent> PaymentWebhookEvents => Set<PaymentWebhookEvent>();
    public DbSet<PricingRule> PricingRules => Set<PricingRule>();
    public DbSet<Campaign> Campaigns => Set<Campaign>();
    public DbSet<ReservationHold> ReservationHolds => Set<ReservationHold>();
    public DbSet<AdminUser> AdminUsers => Set<AdminUser>();
    public DbSet<AuthSession> AuthSessions => Set<AuthSession>();
    public DbSet<PasswordResetToken> PasswordResetTokens => Set<PasswordResetToken>();
    public DbSet<CustomerAccountClaimToken> CustomerAccountClaimTokens => Set<CustomerAccountClaimToken>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<BackgroundJob> BackgroundJobs => Set<BackgroundJob>();
    public DbSet<FeatureFlag> FeatureFlags => Set<FeatureFlag>();
    public DbSet<PublicSiteSettings> PublicSiteSettings => Set<PublicSiteSettings>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(RentACarDbContext).Assembly);
    }
}
