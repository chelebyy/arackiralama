using RentACar.Core.Entities;

namespace RentACar.Core.Interfaces;

public interface IApplicationDbContext
{
    IQueryable<Vehicle> Vehicles { get; }
    IQueryable<VehicleGroup> VehicleGroups { get; }
    IQueryable<Office> Offices { get; }
    IQueryable<Customer> Customers { get; }
    IQueryable<Reservation> Reservations { get; }
    IQueryable<PaymentIntent> PaymentIntents { get; }
    IQueryable<PaymentWebhookEvent> PaymentWebhookEvents { get; }
    IQueryable<PricingRule> PricingRules { get; }
    IQueryable<Campaign> Campaigns { get; }
    IQueryable<ReservationHold> ReservationHolds { get; }
    IQueryable<AdminUser> AdminUsers { get; }
    IQueryable<AuditLog> AuditLogs { get; }
    IQueryable<BackgroundJob> BackgroundJobs { get; }
    IQueryable<FeatureFlag> FeatureFlags { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
