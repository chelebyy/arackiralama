using Microsoft.EntityFrameworkCore;
using RentACar.Core.Entities;

namespace RentACar.Core.Interfaces;

public interface IApplicationDbContext
{
    DbSet<Vehicle> Vehicles { get; }
    DbSet<VehicleGroup> VehicleGroups { get; }
    DbSet<Office> Offices { get; }
    DbSet<Customer> Customers { get; }
    DbSet<Reservation> Reservations { get; }
    DbSet<PaymentIntent> PaymentIntents { get; }
    DbSet<PaymentWebhookEvent> PaymentWebhookEvents { get; }
    DbSet<PricingRule> PricingRules { get; }
    DbSet<Campaign> Campaigns { get; }
    DbSet<ReservationHold> ReservationHolds { get; }
    DbSet<AdminUser> AdminUsers { get; }
    DbSet<AuthSession> AuthSessions { get; }
    DbSet<PasswordResetToken> PasswordResetTokens { get; }
    DbSet<AuditLog> AuditLogs { get; }
    DbSet<BackgroundJob> BackgroundJobs { get; }
    DbSet<FeatureFlag> FeatureFlags { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
