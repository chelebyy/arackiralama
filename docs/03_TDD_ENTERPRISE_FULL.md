# Technical Design Document (TDD)

Date: 2026-02-25

## 1. Domain Modules

- Reservation
- Fleet
- Pricing
- Payment
- Notification
- Feature Management
- Identity & Access

## 2. Reservation Concurrency Strategy

Overlap Rule: (start \< existingEnd) AND (end \> existingStart)

Mechanisms: - DB-level transaction - Indexed time range queries - Redis
hold TTL (15 minutes) - Idempotency enforcement

## 3. Payment Flow

- PaymentIntent creation (unique idempotency key)
- 3D Secure redirect
- Webhook verification (signature)
- Background worker processes payment confirmation
- Reservation status updated atomically

## 4. Idempotency Enforcement

Unique constraints: - payment_intents.idempotency_key -
payment_webhook_events.provider_event_id

## 5. Error Model

Standardized response: { "errorCode": "RESERVATION_CONFLICT", "message":
"Vehicle unavailable", "correlationId": "uuid" }

## 6. Logging

Structured JSON logs Correlation ID per request PII masking enabled

## 7. Background Job Processing Architecture

The system implements a persistent job processing pattern.

## Job Table Design

**Table:** `background_jobs`

**Fields:**

- `id` (UUID, PK)
- `type` (string)
- `payload` (JSONB)
- `status` (Pending | Processing | Completed | Failed)
- `retry_count` (int)
- `max_retries` (int)
- `scheduled_at` (timestamp)
- `created_at` (timestamp)
- `processed_at` (timestamp nullable)

## Flow

1. Business event occurs (e.g., PaymentConfirmed).
2. Within the same DB transaction:
   - Core entity updated.
   - Job inserted into `background_jobs` table.
3. Worker continuously polls:
   ```sql
   SELECT * FROM background_jobs WHERE status = 'Pending'
   ```
4. Worker marks job as Processing.
5. Executes logic (SMS, email, audit).
6. On success → status = Completed.
7. On failure → retry_count++.
8. If retry_count >= max_retries → status = Failed.

## Guarantees

- Crash-safe processing
- Idempotent execution required
- No event loss
- Retry mechanism built-in

---

# 8. Core Service Interfaces

This section defines the enterprise-grade service contracts that form the
backbone of the rental platform's domain layer. All interfaces follow SOLID
principles with single responsibility and dependency inversion.

## 8.1 IPaymentProvider Interface

Payment provider abstraction enabling seamless integration with multiple
payment gateways (Halkbank, Stripe, etc.) without changing core business logic.

```csharp
using System.Threading;
using System.Threading.Tasks;

namespace RentACar.Core.Interfaces.Payment;

/// <summary>
/// Abstraction for payment gateway integrations.
/// Supports 3D Secure, pre-authorization, and refund operations.
/// </summary>
public interface IPaymentProvider
{
    /// <summary>
    /// Creates a payment intent for reservation checkout.
    /// </summary>
    /// <param name="request">Payment intent creation parameters</param>
    /// <param name="cancellationToken">Cancellation token for async operation</param>
    /// <returns>Payment intent with redirect URL for 3D Secure</returns>
    Task<PaymentIntentResult> CreatePaymentIntentAsync(
        CreatePaymentIntentRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Verifies 3D Secure callback and completes payment authorization.
    /// </summary>
    /// <param name="callbackData">Callback data from payment provider</param>
    /// <param name="cancellationToken">Cancellation token for async operation</param>
    /// <returns>Verification result with transaction details</returns>
    Task<PaymentVerificationResult> VerifyPaymentAsync(
        PaymentCallbackData callbackData,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a pre-authorization hold for deposit.
    /// </summary>
    /// <param name="request">Pre-authorization parameters including amount</param>
    /// <param name="cancellationToken">Cancellation token for async operation</param>
    /// <returns>Pre-authorization result with hold reference</returns>
    Task<PreAuthorizationResult> CreatePreAuthorizationAsync(
        CreatePreAuthorizationRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Captures a previously authorized deposit amount.
    /// </summary>
    /// <param name="holdReference">Reference ID from pre-authorization</param>
    /// <param name="amount">Amount to capture (may be partial)</param>
    /// <param name="cancellationToken">Cancellation token for async operation</param>
    /// <returns>Capture result with transaction ID</returns>
    Task<CaptureResult> CapturePreAuthorizationAsync(
        string holdReference,
        decimal amount,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Releases a pre-authorization hold without charging.
    /// </summary>
    /// <param name="holdReference">Reference ID from pre-authorization</param>
    /// <param name="cancellationToken">Cancellation token for async operation</param>
    /// <returns>Release confirmation result</returns>
    Task<ReleaseResult> ReleasePreAuthorizationAsync(
        string holdReference,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Processes a refund for a completed payment.
    /// </summary>
    /// <param name="originalTransactionId">Original payment transaction ID</param>
    /// <param name="amount">Refund amount (partial or full)</param>
    /// <param name="reason">Refund reason for audit trail</param>
    /// <param name="cancellationToken">Cancellation token for async operation</param>
    /// <returns>Refund result with new transaction ID</returns>
    Task<RefundResult> RefundAsync(
        string originalTransactionId,
        decimal amount,
        string reason,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates webhook signature from payment provider.
    /// </summary>
    /// <param name="payload">Raw request body</param>
    /// <param name="signature">Signature header value</param>
    /// <param name="timestamp">Timestamp header for replay protection</param>
    /// <returns>True if signature is valid</returns>
    bool VerifyWebhookSignature(string payload, string signature, string timestamp);

    /// <summary>
    /// Gets provider-specific transaction status.
    /// </summary>
    /// <param name="transactionId">Transaction ID to query</param>
    /// <param name="cancellationToken">Cancellation token for async operation</param>
    /// <returns>Current transaction status</returns>
    Task<TransactionStatus> GetTransactionStatusAsync(
        string transactionId,
        CancellationToken cancellationToken = default);
}
```

## 8.2 ISmsProvider Interface

SMS notification abstraction for multi-provider failover support
(Twilio, MessageBird, Netgsm for Turkey).

```csharp
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace RentACar.Core.Interfaces.Notification;

/// <summary>
/// SMS provider abstraction for reservation notifications and alerts.
/// Supports multi-language templates and failover providers.
/// </summary>
public interface ISmsProvider
{
    /// <summary>
    /// Sends SMS to a single recipient.
    /// </summary>
    /// <param name="phoneNumber">E.164 formatted phone number</param>
    /// <param name="templateKey">Template identifier from resource files</param>
    /// <param name="parameters">Template parameter replacements</param>
    /// <param name="language">ISO 639-1 language code (tr, en, ru, ar, de)</param>
    /// <param name="cancellationToken">Cancellation token for async operation</param>
    /// <returns>SMS delivery result with provider message ID</returns>
    Task<SmsResult> SendAsync(
        string phoneNumber,
        string templateKey,
        Dictionary<string, string> parameters,
        string language = "tr",
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends bulk SMS to multiple recipients (for admin broadcasts).
    /// </summary>
    /// <param name="recipients">List of phone numbers and personalized parameters</param>
    /// <param name="templateKey">Template identifier</param>
    /// <param name="language">ISO 639-1 language code</param>
    /// <param name="cancellationToken">Cancellation token for async operation</param>
    /// <returns>Bulk send result with individual status per recipient</returns>
    Task<BulkSmsResult> SendBulkAsync(
        List<SmsRecipient> recipients,
        string templateKey,
        string language = "tr",
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates phone number format for target country.
    /// </summary>
    /// <param name="phoneNumber">Phone number to validate</param>
    /// <param name="countryCode">ISO 3166-1 alpha-2 country code</param>
    /// <returns>Validation result with normalized format</returns>
    PhoneValidationResult ValidatePhoneNumber(string phoneNumber, string countryCode = "TR");

    /// <summary>
    /// Gets delivery status of a previously sent SMS.
    /// </summary>
    /// <param name="providerMessageId">Message ID from SendAsync result</param>
    /// <param name="cancellationToken">Cancellation token for async operation</param>
    /// <returns>Current delivery status</returns>
    Task<DeliveryStatus> GetDeliveryStatusAsync(
        string providerMessageId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks provider health and credit balance.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for async operation</param>
    /// <returns>Provider health status with remaining credits</returns>
    Task<ProviderHealthStatus> GetHealthStatusAsync(
        CancellationToken cancellationToken = default);
}
```

## 8.3 IReservationService Interface

Core reservation domain service handling availability, holds, and lifecycle.

```csharp
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace RentACar.Core.Interfaces.Reservation;

/// <summary>
/// Core reservation domain service.
/// Manages availability queries, holds, and reservation lifecycle.
/// </summary>
public interface IReservationService
{
    /// <summary>
    /// Searches available vehicles for given date range and office.
    /// </summary>
    /// <param name="pickupOfficeId">Pickup office identifier</param>
    /// <param name="returnOfficeId">Return office identifier (one-way support)</param>
    /// <param name="pickupDateTime">Requested pickup datetime</param>
    /// <param name="returnDateTime">Requested return datetime</param>
    /// <param name="vehicleGroupId">Optional specific vehicle group filter</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of available vehicle groups with pricing</returns>
    Task<List<AvailableVehicleGroup>> SearchAvailabilityAsync(
        int pickupOfficeId,
        int returnOfficeId,
        DateTime pickupDateTime,
        DateTime returnDateTime,
        int? vehicleGroupId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a temporary hold on a vehicle group.
    /// </summary>
    /// <param name="request">Hold creation parameters</param>
    /// <param name="sessionId">User session identifier for idempotency</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Hold result with expiration timestamp</returns>
    Task<HoldResult> CreateHoldAsync(
        CreateHoldRequest request,
        string sessionId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Extends an existing hold (user still on payment page).
    /// </summary>
    /// <param name="holdId">Existing hold identifier</param>
    /// <param name="extensionMinutes">Minutes to extend (max 15)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Updated hold result</returns>
    Task<HoldResult> ExtendHoldAsync(
        string holdId,
        int extensionMinutes,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Releases a hold manually (user cancelled).
    /// </summary>
    /// <param name="holdId">Hold identifier to release</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Release confirmation</returns>
    Task<bool> ReleaseHoldAsync(
        string holdId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a reservation from an existing hold after payment.
    /// </summary>
    /// <param name="holdId">Confirmed hold identifier</param>
    /// <param name="paymentIntentId">Payment provider intent ID</param>
    /// <param name="customerInfo">Customer personal details</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Created reservation with confirmation code</returns>
    Task<Reservation> CreateReservationAsync(
        string holdId,
        string paymentIntentId,
        CustomerInfo customerInfo,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets reservation by confirmation code (public tracking).
    /// </summary>
    /// <param name="confirmationCode">Public confirmation code (e.g., A8X9K2)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Reservation details or null if not found</returns>
    Task<Reservation?> GetByConfirmationCodeAsync(
        string confirmationCode,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Cancels a reservation with refund calculation.
    /// </summary>
    /// <param name="reservationId">Internal reservation ID</param>
    /// <param name="reason">Cancellation reason</param>
    /// <param name="cancelledBy">User/admin identifier</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Cancellation result with refund amount</returns>
    Task<CancelResult> CancelAsync(
        Guid reservationId,
        string reason,
        string cancelledBy,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks for overlapping reservations (internal validation).
    /// Overlap Rule: (start < existingEnd) AND (end > existingStart)
    /// </summary>
    /// <param name="vehicleId">Vehicle to check</param>
    /// <param name="startDateTime">Proposed start</param>
    /// <param name="endDateTime">Proposed end</param>
    /// <param name="excludeReservationId">Optional reservation to exclude (for updates)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if overlapping reservation exists</returns>
    Task<bool> HasOverlappingReservationAsync(
        int vehicleId,
        DateTime startDateTime,
        DateTime endDateTime,
        Guid? excludeReservationId = null,
        CancellationToken cancellationToken = default);
}
```

## 8.4 IFleetService Interface

Vehicle fleet management service for administrators.

```csharp
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace RentACar.Core.Interfaces.Fleet;

/// <summary>
/// Fleet management service for vehicle CRUD and status management.
/// </summary>
public interface IFleetService
{
    /// <summary>
    /// Gets paginated vehicle list with filtering.
    /// </summary>
    /// <param name="filter">Filter parameters (office, status, group, etc.)</param>
    /// <param name="page">Page number (1-based)</param>
    /// <param name="pageSize">Items per page</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Paginated vehicle list</returns>
    Task<PaginatedResult<Vehicle>> GetVehiclesAsync(
        VehicleFilter filter,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets single vehicle by ID with full details.
    /// </summary>
    /// <param name="vehicleId">Vehicle identifier</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Vehicle details or null</returns>
    Task<Vehicle?> GetByIdAsync(
        int vehicleId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new vehicle in the fleet.
    /// </summary>
    /// <param name="request">Vehicle creation data</param>
    /// <param name="createdBy">Admin user ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Created vehicle with generated ID</returns>
    Task<Vehicle> CreateAsync(
        CreateVehicleRequest request,
        string createdBy,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates vehicle information.
    /// </summary>
    /// <param name="vehicleId">Vehicle to update</param>
    /// <param name="request">Update data</param>
    /// <param name="updatedBy">Admin user ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Updated vehicle</returns>
    Task<Vehicle> UpdateAsync(
        int vehicleId,
        UpdateVehicleRequest request,
        string updatedBy,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Changes vehicle status (Available, Maintenance, Retired).
    /// </summary>
    /// <param name="vehicleId">Vehicle identifier</param>
    /// <param name="newStatus">Target status</param>
    /// <param name="reason">Status change reason</param>
    /// <param name="changedBy">Admin user ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Success result with validation errors if any</returns>
    Task<StatusChangeResult> ChangeStatusAsync(
        int vehicleId,
        VehicleStatus newStatus,
        string reason,
        string changedBy,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Transfers vehicle to different office (one-way rental aftermath).
    /// </summary>
    /// <param name="vehicleId">Vehicle to transfer</param>
    /// <param name="targetOfficeId">Destination office</param>
    /// <param name="reason">Transfer reason</param>
    /// <param name="transferredBy">Admin user ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Transfer result</returns>
    Task<TransferResult> TransferToOfficeAsync(
        int vehicleId,
        int targetOfficeId,
        string reason,
        string transferredBy,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets vehicle maintenance history.
    /// </summary>
    /// <param name="vehicleId">Vehicle identifier</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of maintenance records</returns>
    Task<List<MaintenanceRecord>> GetMaintenanceHistoryAsync(
        int vehicleId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Schedules vehicle for maintenance.
    /// </summary>
    /// <param name="vehicleId">Vehicle identifier</param>
    /// <param name="startDate">Maintenance start date</param>
    /// <param name="endDate">Expected completion date</param>
    /// <param name="description">Maintenance description</param>
    /// <param name="scheduledBy">Admin user ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Maintenance record</returns>
    Task<MaintenanceRecord> ScheduleMaintenanceAsync(
        int vehicleId,
        DateTime startDate,
        DateTime endDate,
        string description,
        string scheduledBy,
        CancellationToken cancellationToken = default);
}
```

## 8.5 IPricingService Interface

Dynamic pricing engine with seasonal rules and campaign support.

```csharp
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace RentACar.Core.Interfaces.Pricing;

/// <summary>
/// Pricing engine for calculating rental costs with seasonal rules,
/// campaigns, and additional fees.
/// </summary>
public interface IPricingService
{
    /// <summary>
    /// Calculates total price for a rental period.
    /// Priority Order: Campaign -> Seasonal -> Base Price
    /// </summary>
    /// <param name="vehicleGroupId">Vehicle group identifier</param>
    /// <param name="pickupDateTime">Rental start</param>
    /// <param name="returnDateTime">Rental end</param>
    /// <param name="pickupOfficeId">Pickup office (for location fees)</param>
    /// <param name="returnOfficeId">Return office (for one-way fees)</param>
    /// <param name="campaignCode">Optional discount code</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Detailed price breakdown</returns>
    Task<PriceBreakdown> CalculatePriceAsync(
        int vehicleGroupId,
        DateTime pickupDateTime,
        DateTime returnDateTime,
        int pickupOfficeId,
        int returnOfficeId,
        string? campaignCode = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates and applies campaign code.
    /// </summary>
    /// <param name="campaignCode">Discount code entered by user</param>
    /// <param name="vehicleGroupId">Vehicle group for restrictions</param>
    /// <param name="rentalDays">Number of rental days</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Validation result with discount amount if valid</returns>
    Task<CampaignValidationResult> ValidateCampaignAsync(
        string campaignCode,
        int vehicleGroupId,
        int rentalDays,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets applicable seasonal pricing rules for date range.
    /// </summary>
    /// <param name="startDate">Range start</param>
    /// <param name="endDate">Range end</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Active seasonal rules</returns>
    Task<List<SeasonalPricingRule>> GetSeasonalRulesAsync(
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Calculates deposit amount for vehicle group.
    /// </summary>
    /// <param name="vehicleGroupId">Vehicle group</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Deposit amount in base currency</returns>
    Task<decimal> GetDepositAmountAsync(
        int vehicleGroupId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets additional fees for pickup/return locations.
    /// </summary>
    /// <param name="pickupOfficeId">Pickup office</param>
    /// <param name="returnOfficeId">Return office</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of applicable fees</returns>
    Task<List<LocationFee>> GetLocationFeesAsync(
        int pickupOfficeId,
        int returnOfficeId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Admin: Creates seasonal pricing rule.
    /// </summary>
    /// <param name="rule">Seasonal rule definition</param>
    /// <param name="createdBy">Admin user ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Created rule with ID</returns>
    Task<SeasonalPricingRule> CreateSeasonalRuleAsync(
        CreateSeasonalRuleRequest rule,
        string createdBy,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Admin: Creates campaign code.
    /// </summary>
    /// <param name="campaign">Campaign definition</param>
    /// <param name="createdBy">Admin user ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Created campaign</returns>
    Task<Campaign> CreateCampaignAsync(
        CreateCampaignRequest campaign,
        string createdBy,
        CancellationToken cancellationToken = default);
}
```

## 8.6 IReportsService Interface

Read-only admin reporting service for operational dashboard/report screens.

```csharp
public interface IReportsService
{
    Task<RevenueReportResponse> GetRevenueReportAsync(
        string period,
        CancellationToken cancellationToken = default);

    Task<OccupancyReportResponse> GetOccupancyReportAsync(
        string period,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PopularVehicleReportItemResponse>> GetPopularVehiclesAsync(
        string period,
        CancellationToken cancellationToken = default);
}
```

### Implementation Notes

- Admin reports are exposed under `/api/admin/v1/reports/*` and require the existing `AdminOnly` authorization policy.
- `ReportsService` uses `IApplicationDbContext` with no-tracking reads and in-memory aggregation for the launch scope.
- Supported period inputs are `daily`, `weekly`, `monthly`, `quarterly`, and `yearly`; invalid period input returns an empty report payload instead of throwing.
- Revenue aggregation counts succeeded payments connected to revenue-eligible reservations (`Paid`, `Active`, `Completed`).
- Occupancy and popular-vehicle reports are launch-scope operational views, not accounting ledgers.

## 8.7 ICacheService Interface

Unified caching abstraction with Redis and in-memory fallback.

```csharp
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace RentACar.Core.Interfaces.Caching;

/// <summary>
/// Distributed caching service with local fallback.
/// Implements Cache-Aside pattern with automatic invalidation.
/// </summary>
public interface ICacheService
{
    /// <summary>
    /// Gets item from cache or returns null.
    /// </summary>
    /// <typeparam name="T">Expected type</typeparam>
    /// <param name="key">Cache key</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Cached item or null</returns>
    Task<T?> GetAsync<T>(
        string key,
        CancellationToken cancellationToken = default) where T : class;

    /// <summary>
    /// Sets item in cache with expiration.
    /// </summary>
    /// <typeparam name="T">Item type</typeparam>
    /// <param name="key">Cache key</param>
    /// <param name="value">Value to cache</param>
    /// <param name="expiration">Cache duration</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task completion</returns>
    Task SetAsync<T>(
        string key,
        T value,
        TimeSpan expiration,
        CancellationToken cancellationToken = default) where T : class;

    /// <summary>
    /// Gets or creates cache entry using factory.
    /// Implements Cache-Aside pattern.
    /// </summary>
    /// <typeparam name="T">Item type</typeparam>
    /// <param name="key">Cache key</param>
    /// <param name="factory">Async factory for cache miss</param>
    /// <param name="expiration">Cache duration</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Cached or factory-created item</returns>
    Task<T> GetOrCreateAsync<T>(
        string key,
        Func<Task<T>> factory,
        TimeSpan expiration,
        CancellationToken cancellationToken = default) where T : class;

    /// <summary>
    /// Removes item from cache.
    /// </summary>
    /// <param name="key">Cache key to remove</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if item existed and was removed</returns>
    Task<bool> RemoveAsync(
        string key,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes multiple items by pattern (Redis key scan).
    /// </summary>
    /// <param name="pattern">Key pattern to match (e.g., "vehicle:*")</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Number of items removed</returns>
    Task<int> RemoveByPatternAsync(
        string pattern,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if key exists in cache.
    /// </summary>
    /// <param name="key">Cache key</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if exists</returns>
    Task<bool> ExistsAsync(
        string key,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Increments counter atomically.
    /// </summary>
    /// <param name="key">Counter key</param>
    /// <param name="value">Amount to increment</param>
    /// <param name="expiration">Key expiration</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>New counter value</returns>
    Task<long> IncrementAsync(
        string key,
        long value = 1,
        TimeSpan? expiration = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets expiration on existing key.
    /// </summary>
    /// <param name="key">Cache key</param>
    /// <param name="expiration">New expiration</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if expiration was set</returns>
    Task<bool> SetExpirationAsync(
        string key,
        TimeSpan expiration,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets cache statistics for monitoring.
    /// </summary>
    /// <returns>Hit rate, miss rate, total keys</returns>
    Task<CacheStatistics> GetStatisticsAsync();

    /// <summary>
    /// Flushes all cached data (emergency use only).
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task completion</returns>
    Task FlushAsync(CancellationToken cancellationToken = default);
}
```

---

# 9. Redis Caching Strategy

Enterprise-grade distributed caching architecture with Cache-Aside pattern,
automatic failover to database, and intelligent invalidation strategies.

## 9.1 Cache-Aside Pattern Implementation

The Cache-Aside (Lazy Loading) pattern loads data on-demand:

```csharp
public class CacheAsideService<T> where T : class
{
    private readonly ICacheService _cache;
    private readonly IDbContext _dbContext;
    private readonly ILogger<CacheAsideService<T>> _logger;

    public async Task<T?> GetAsync(
        string cacheKey,
        Func<Task<T?>> databaseQuery,
        TimeSpan expiration,
        CancellationToken ct = default)
    {
        // Step 1: Check cache
        var cached = await _cache.GetAsync<T>(cacheKey, ct);
        if (cached is not null)
        {
            _logger.LogDebug("Cache hit for key: {Key}", cacheKey);
            return cached;
        }

        // Step 2: Cache miss - query database
        _logger.LogDebug("Cache miss for key: {Key}", cacheKey);
        var data = await databaseQuery();

        // Step 3: Store in cache (only if data exists)
        if (data is not null)
        {
            await _cache.SetAsync(cacheKey, data, expiration, ct);
        }

        return data;
    }
}
```

## 9.2 Cache Key Naming Convention

| Data Type            | Key Pattern                         | Example                         |
| -------------------- | ----------------------------------- | ------------------------------- |
| Vehicle Availability | `avail:{officeId}:{date}:{groupId}` | `avail:1:20260315:5`            |
| Vehicle Details      | `vehicle:{vehicleId}`               | `vehicle:42`                    |
| Pricing Rules        | `pricing:seasonal:{year}`           | `pricing:seasonal:2026`         |
| Campaign Code        | `campaign:{code}`                   | `campaign:SUMMER25`             |
| User Session         | `session:{sessionId}`               | `session:a8x9k2m3`              |
| Reservation Hold     | `hold:{holdId}`                     | `hold:hold_abc123`              |
| Rate Limit           | `ratelimit:{endpoint}:{ip}`         | `ratelimit:payment:192.168.1.1` |

## 9.3 Time-To-Live (TTL) Strategy

| Cache Category           | TTL                | Rationale             |
| ------------------------ | ------------------ | --------------------- |
| **Vehicle Availability** | 5 minutes          | High change frequency |
| **Vehicle Details**      | 1 hour             | Rarely changes        |
| **Pricing Rules**        | 30 minutes         | Seasonal changes      |
| **Campaign Codes**       | 15 minutes         | Promotion updates     |
| **User Sessions**        | 20 minutes         | Security + UX balance |
| **Reservation Holds**    | 15 minutes (exact) | Business requirement  |
| **Location/Offices**     | 24 hours           | Static data           |
| **Vehicle Groups**       | 6 hours            | Configuration data    |

## 9.4 Cache Invalidation Rules

### Automatic Invalidation Triggers

```csharp
public enum CacheInvalidationTrigger
{
    VehicleStatusChanged,
    PricingRuleUpdated,
    CampaignActivated,
    CampaignExpired,
    ReservationConfirmed,
    ReservationCancelled,
    VehicleTransferCompleted,
    MaintenanceScheduled
}

public static class CacheInvalidationRules
{
    public static Dictionary<CacheInvalidationTrigger, string[]> Patterns = new()
    {
        [CacheInvalidationTrigger.VehicleStatusChanged] =
            new[] { "avail:*", "vehicle:*" },

        [CacheInvalidationTrigger.PricingRuleUpdated] =
            new[] { "pricing:*", "avail:*" },

        [CacheInvalidationTrigger.CampaignActivated] =
            new[] { "campaign:*", "avail:*" },

        [CacheInvalidationTrigger.ReservationConfirmed] =
            new[] { "avail:*", "hold:*" },

        [CacheInvalidationTrigger.ReservationCancelled] =
            new[] { "avail:*" },

        [CacheInvalidationTrigger.VehicleTransferCompleted] =
            new[] { "vehicle:*", "avail:*" }
    };
}
```

### Implementation

```csharp
public async Task InvalidateAsync(CacheInvalidationTrigger trigger)
{
    if (CacheInvalidationRules.Patterns.TryGetValue(trigger, out var patterns))
    {
        foreach (var pattern in patterns)
        {
            await _cache.RemoveByPatternAsync(pattern);
        }
    }
}
```

## 9.5 Redis Degraded Mode

When Redis is unavailable, system falls back to DB-only operation:

```csharp
public class ResilientCacheService : ICacheService
{
    private readonly ICacheService _redisCache;
    private readonly IDbContext _dbContext;
    private readonly ILogger _logger;
    private static bool _redisAvailable = true;
    private static DateTime _lastCheck = DateTime.MinValue;

    public async Task<T?> GetAsync<T>(string key, CancellationToken ct = default)
        where T : class
    {
        if (await IsRedisAvailableAsync())
        {
            try
            {
                return await _redisCache.GetAsync<T>(key, ct);
            }
            catch (RedisConnectionException)
            {
                _redisAvailable = false;
                _logger.LogWarning("Redis unavailable, falling back to DB");
            }
        }

        // Degraded mode: Skip cache, query directly
        return null; // Signals cache miss
    }

    private async Task<bool> IsRedisAvailableAsync()
    {
        // Check every 30 seconds
        if (DateTime.UtcNow - _lastCheck < TimeSpan.FromSeconds(30))
            return _redisAvailable;

        _lastCheck = DateTime.UtcNow;
        _redisAvailable = await _redisCache.ExistsAsync("health:check");
        return _redisAvailable;
    }
}
```

## 9.6 Cache Warm-Up Strategy

Pre-populate cache after deployment:

```csharp
public class CacheWarmUpService : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Wait for app startup
        await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);

        _logger.LogInformation("Starting cache warm-up...");

        // Warm up static data
        await WarmUpVehicleGroupsAsync(stoppingToken);
        await WarmUpOfficesAsync(stoppingToken);
        await WarmUpPricingRulesAsync(stoppingToken);

        _logger.LogInformation("Cache warm-up completed");
    }

    private async Task WarmUpVehicleGroupsAsync(CancellationToken ct)
    {
        var groups = await _dbContext.VehicleGroups
            .AsNoTracking()
            .ToListAsync(ct);

        foreach (var group in groups)
        {
            await _cache.SetAsync(
                $"vehiclegroup:{group.Id}",
                group,
                TimeSpan.FromHours(6), ct);
        }
    }
}
```

---

# 10. Internationalization (i18n) Implementation

Multi-language support for 5 languages: Turkish (TR), English (EN),
Russian (RU), Arabic (AR), German (DE).

## 10.1 Resource File Structure

```
Resources/
├── SharedResources.resx          (Default - Turkish)
├── SharedResources.en.resx       (English)
├── SharedResources.ru.resx       (Russian)
├── SharedResources.ar.resx       (Arabic)
├── SharedResources.de.resx       (German)
├── ValidationMessages.resx
├── ValidationMessages.en.resx
├── ValidationMessages.ru.resx
├── ValidationMessages.ar.resx
└── ValidationMessages.de.resx
```

## 10.2 Resource Organization

### SharedResources (UI Texts)

| Key                    | TR                    | EN                    | RU                        | AR                 | DE                       |
| ---------------------- | --------------------- | --------------------- | ------------------------- | ------------------ | ------------------------ |
| `WelcomeMessage`       | Hoş geldiniz          | Welcome               | Добро пожаловать          | أهلاً وسهلاً       | Willkommen               |
| `ReservationConfirmed` | Rezervasyon onaylandı | Reservation confirmed | Бронирование подтверждено | تم تأكيد الحجز     | Reservierung bestätigt   |
| `PaymentFailed`        | Ödeme başarısız       | Payment failed        | Оплата не прошла          | فشل الدفع          | Zahlung fehlgeschlagen   |
| `VehicleNotAvailable`  | Araç müsait değil     | Vehicle not available | Автомобиль недоступен     | السيارة غير متوفرة | Fahrzeug nicht verfügbar |

### ValidationMessages (Error Texts)

| Key                | TR                              | EN                  | RU                            | AR                               | DE                         |
| ------------------ | ------------------------------- | ------------------- | ----------------------------- | -------------------------------- | -------------------------- |
| `RequiredField`    | Zorunlu alan                    | Required field      | Обязательное поле             | حقل مطلوب                        | Pflichtfeld                |
| `InvalidPhone`     | Geçersiz telefon                | Invalid phone       | Неверный телефон              | رقم هاتف غير صالح                | Ungültige Telefonnummer    |
| `InvalidDateRange` | Geçersiz tarih aralığı          | Invalid date range  | Неверный диапазон дат         | نطاق تاريخ غير صالح              | Ungültiger Zeitraum        |
| `MinAgeNotMet`     | Minimum yaş şartı karşılanmıyor | Minimum age not met | Минимальный возраст не достиг | لم يتم استيفاء الحد الأدنى للعمر | Mindestalter nicht erfüllt |

## 10.3 Culture Middleware

```csharp
public class CultureMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<CultureMiddleware> _logger;

    // Supported cultures
    private static readonly string[] SupportedCultures =
        new[] { "tr", "en", "ru", "ar", "de" };

    public async Task InvokeAsync(HttpContext context)
    {
        var culture = DetermineCulture(context);

        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;

        // Set RTL for Arabic
        if (culture.Name == "ar")
        {
            context.Items["IsRTL"] = true;
        }

        _logger.LogDebug("Culture set to: {Culture}", culture.Name);

        await _next(context);
    }

    private CultureInfo DetermineCulture(HttpContext context)
    {
        // Priority 1: Query string (?lang=en)
        if (context.Request.Query.TryGetValue("lang", out var queryLang) &&
            IsSupported(queryLang.ToString()))
        {
            return new CultureInfo(queryLang.ToString());
        }

        // Priority 2: Route prefix (/en/api/...)
        var path = context.Request.Path.Value?.Split('/');
        if (path?.Length > 1 && IsSupported(path[1]))
        {
            return new CultureInfo(path[1]);
        }

        // Priority 3: Accept-Language header
        var acceptLang = context.Request.Headers.AcceptLanguage.ToString();
        if (!string.IsNullOrEmpty(acceptLang))
        {
            var preferred = acceptLang.Split(',')[0].Trim().Substring(0, 2);
            if (IsSupported(preferred))
                return new CultureInfo(preferred);
        }

        // Default: Turkish
        return new CultureInfo("tr");
    }

    private bool IsSupported(string lang) =>
        SupportedCultures.Contains(lang.ToLowerInvariant());
}
```

## 10.4 Registration in Program.cs

```csharp
// Add localization services
builder.Services.AddLocalization(options =>
{
    options.ResourcesPath = "Resources";
});

builder.Services.Configure<RequestLocalizationOptions>(options =>
{
    var supportedCultures = new[]
    {
        new CultureInfo("tr"),
        new CultureInfo("en"),
        new CultureInfo("ru"),
        new CultureInfo("ar"),
        new CultureInfo("de")
    };

    options.DefaultRequestCulture = new RequestCulture("tr");
    options.SupportedCultures = supportedCultures;
    options.SupportedUICultures = supportedCultures;
});

// Add middleware (before MVC)
app.UseMiddleware<CultureMiddleware>();
app.UseRequestLocalization();
```

## 10.5 String Localizer Usage

```csharp
public class ReservationController : ControllerBase
{
    private readonly IStringLocalizer<SharedResources> _localizer;

    public ReservationController(IStringLocalizer<SharedResources> localizer)
    {
        _localizer = localizer;
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(new
            {
                Error = _localizer["InvalidRequest"]
            });
        }

        // ... create logic

        return Ok(new
        {
            Message = _localizer["ReservationConfirmed"],
            ConfirmationCode = code
        });
    }
}
```

## 10.6 Date/Number Localization

```csharp
public static class LocalizationExtensions
{
    // Format date according to culture
    public static string ToLocalizedDate(this DateTime date, string culture)
    {
        return culture switch
        {
            "tr" => date.ToString("dd.MM.yyyy"),
            "en" => date.ToString("MM/dd/yyyy"),
            "de" => date.ToString("dd.MM.yyyy"),
            "ru" => date.ToString("dd.MM.yyyy"),
            "ar" => date.ToString("yyyy/MM/dd"),
            _ => date.ToString("dd.MM.yyyy")
        };
    }

    // Format currency
    public static string ToLocalizedCurrency(this decimal amount, string culture)
    {
        var cultureInfo = new CultureInfo(culture);
        return amount.ToString("C", cultureInfo);
    }
}
```

## 10.7 RTL Support for Arabic

```csharp
// In view/component
@if (Context.Items["IsRTL"] as bool? == true)
{
    <html dir="rtl" lang="ar">
    <link rel="stylesheet" href="/css/rtl.css" />
}
else
{
    <html dir="ltr">
}
```

## 10.8 Admin-Managed Public Content Localization

Public legal/contact/navigation content can be managed from the admin dashboard without redeploying message files.

- `PublicSiteSettings` remains a singleton JSON-backed configuration entity. The JSON columns keep the schema migration-free for managed content additions.
- Base fields (`label`, `value`, `description`, `name`, `address`, `hours`, `day`) remain the Turkish/default fallback for older records and for incomplete translations.
- Optional `translations` maps are supported on:
  - `PublicSiteLinkDto` for header, hero CTA, quick links, and footer bottom links.
  - `PublicContactChannelDto` for contact label/value/description.
  - `PublicContactOfficeDto` for office name/address/hours.
  - `PublicContactWorkingHourDto` for day/hours rows.
- Supported managed-content locale keys are limited to `tr`, `en`, `ru`, `ar`, and `de`. Unsupported keys are rejected during `PublicSiteSettingsService.UpdateAsync`.
- Public consumers resolve content with locale-specific override first, then base field fallback. This keeps existing records backward-compatible while allowing complete five-language content entry.
- Dashboard editing is split by ownership:
  - `/dashboard/settings/system` keeps operational and technical settings.
  - `/dashboard/settings/public-content` provides a dedicated authoring surface for managed pages, contact information, navigation links, hero CTA, map embed, and public payment-method display.
- The admin authoring UI must expose state close to the content it affects:
  - managed page editors show the active locale, saved/dirty/draft/published state, and publish/unpublish availability near the editing controls;
  - contact editors separate global contact/map fields from locale override fields and keep hidden rows visible to admins with explicit public visibility labels;
  - settings navigation is overflow-contained on narrow mobile viewports so tab labels can scroll without widening the page.
- `privacy`, `terms`, and contact/`iletisim` routes remain normal public Next.js routes. They consume `GET /api/v1/public-site-settings`, render managed records when published, and fall back to bundled locale messages when a managed block is missing or unpublished.
- Browser smoke coverage is expected for the dedicated admin authoring route and the public managed-content routes after Docker rebuilds. The 2026-07-08 Admin Public Site & Contact UX pass is recorded in `docs/13_Local_Docker_Browser_Test_Checklist.md` and `docs/test-evidence/local-docker-2026-07-08-admin-ux/`.

Example link payload:

```json
{
  "id": "vehicles",
  "label": "Araçlar",
  "href": "/vehicles",
  "isVisible": true,
  "sortOrder": 0,
  "translations": {
    "en": { "label": "Vehicles" },
    "de": { "label": "Fahrzeuge" }
  }
}
```

---

# 11. Database Indexing Strategy

YW|Performance-optimized indexing for PostgreSQL 18.3 with composite indexes,
partial indexes, and GIN indexes for JSON fields.

## 11.1 Index Overview by Table

| Table            | Index Name                     | Type   | Columns                          | Purpose              |
| ---------------- | ------------------------------ | ------ | -------------------------------- | -------------------- |
| reservations     | idx_reservations_vehicle_dates | B-tree | vehicle_id, start_date, end_date | Overlap detection    |
| reservations     | idx_reservations_status        | B-tree | status                           | Status filtering     |
| reservations     | idx_reservations_confirmation  | B-tree | confirmation_code                | Public lookup        |
| reservations     | idx_reservations_created_at    | B-tree | created_at DESC                  | Admin listing        |
| vehicles         | idx_vehicles_office_status     | B-tree | current_office_id, status        | Availability queries |
| vehicles         | idx_vehicles_group             | B-tree | vehicle_group_id                 | Group filtering      |
| holds            | idx_holds_expires              | B-tree | expires_at                       | Cleanup job          |
| holds            | idx_holds_session              | B-tree | session_id                       | Session lookup       |
| pricing_rules    | idx_pricing_date_range         | B-tree | start_date, end_date             | Seasonal queries     |
| payment_intents  | idx_payment_idempotency        | B-tree | idempotency_key                  | Duplicate prevention |
| payment_webhooks | idx_webhook_provider_event     | B-tree | provider_event_id                | Duplicate detection  |

## 11.2 Critical Composite Indexes

### Reservation Overlap Prevention

```sql
-- Primary index for overlap detection
CREATE INDEX idx_reservations_vehicle_dates
ON reservations (vehicle_id, start_date, end_date);

-- Partial index for active reservations only
CREATE INDEX idx_reservations_active_dates
ON reservations (vehicle_id, start_date, end_date)
WHERE status IN ('Paid', 'Active');
```

### Vehicle Availability Query

```sql
-- Composite index for office + status queries
CREATE INDEX idx_vehicles_office_status_group
ON vehicles (current_office_id, status, vehicle_group_id);

-- Include frequently accessed columns
CREATE INDEX idx_vehicles_available
ON vehicles (current_office_id, vehicle_group_id, status)
WHERE status = 'Available';
```

## 11.3 Migration Scripts

```csharp
// EF Core Migration - Add Indexes
public partial class AddPerformanceIndexes : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Reservations - overlap detection
        migrationBuilder.CreateIndex(
            name: "IX_reservations_vehicle_dates",
            table: "reservations",
            columns: new[] { "vehicle_id", "start_date", "end_date" });

        // Reservations - status filtering
        migrationBuilder.CreateIndex(
            name: "IX_reservations_status_created",
            table: "reservations",
            columns: new[] { "status", "created_at" });

        // Vehicles - availability queries
        migrationBuilder.CreateIndex(
            name: "IX_vehicles_office_status",
            table: "vehicles",
            columns: new[] { "current_office_id", "status" });

        // Holds - expiration cleanup
        migrationBuilder.CreateIndex(
            name: "IX_holds_expires_at",
            table: "holds",
            column: "expires_at");

        // Payment intents - idempotency
        migrationBuilder.CreateIndex(
            name: "IX_payment_intents_idempotency",
            table: "payment_intents",
            column: "idempotency_key",
            unique: true);

        // Webhook events - deduplication
        migrationBuilder.CreateIndex(
            name: "IX_webhook_events_provider",
            table: "payment_webhook_events",
            column: "provider_event_id",
            unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(name: "IX_reservations_vehicle_dates");
        migrationBuilder.DropIndex(name: "IX_reservations_status_created");
        migrationBuilder.DropIndex(name: "IX_vehicles_office_status");
        migrationBuilder.DropIndex(name: "IX_holds_expires_at");
        migrationBuilder.DropIndex(name: "IX_payment_intents_idempotency");
        migrationBuilder.DropIndex(name: "IX_webhook_events_provider");
    }
}
```

## 11.4 Index Maintenance

```sql
-- Analyze query performance
EXPLAIN ANALYZE
SELECT * FROM reservations
WHERE vehicle_id = 42
  AND start_date < '2026-03-20'
  AND end_date > '2026-03-15';

-- Check index usage
SELECT
    schemaname, tablename, indexname, idx_scan, idx_tup_read
FROM pg_stat_user_indexes
WHERE tablename IN ('reservations', 'vehicles', 'holds')
ORDER BY idx_scan DESC;

-- Reindex if needed (maintenance window)
REINDEX INDEX CONCURRENTLY idx_reservations_vehicle_dates;
```

## 11.5 Reservation Extra Options Persistence

The reservation-extra catalog uses explicit EF Core configurations and PostgreSQL constraints rather than convention-only mapping. The Phase 1 schema is introduced by `20260709204616_AddReservationExtraOptions` and consists of:

- `reservation_extra_options` with unique immutable code, price/quantity/order checks, catalog ordering index, and `xmin` row version;
- `reservation_extra_option_translations` with `option_id + locale` composite key and the supported five-locale constraint;
- `reservation_extra_option_vehicle_groups` with a composite assignment key and vehicle-group lookup index;
- `reservation_selected_extras` with immutable localized/monetary snapshots, unique `reservation_id + extra_option_id`, cascading reservation delete, and restricted option delete;
- nullable unique `reservations.quote_id` for replay protection;
- nullable `reservations.pricing_snapshot` stored as versioned `jsonb` for exact new-format pricing history.

The built-in seed and assignment backfill are deliberately bounded. Migration-time SQL assigns every built-in to groups already present. Startup backfill runs only while all four built-ins have zero assignments, and a second run must not change `UpdatedAt` or `xmin`. Groups created by normal admin workflows are not auto-assigned.

Database-focused verification uses both EF metadata tests and the real PostgreSQL integration fixture. Tests cover keys, precision, indexes, delete behavior, seed/translation/assignment shape, late backfill idempotence, hard-delete restriction, duplicate selected snapshots, and duplicate quote IDs. Detailed product rules and phase gates remain in `docs/16_Reservation_Extra_Options_Plan.md` and `docs/17_Reservation_Extra_Options_Implementation.md`.

### Phase 2 Catalog Service and API Boundary

`IReservationExtraOptionCatalogService` and `ReservationExtraOptionCatalogService` live in the API application-service layer and operate through `IApplicationDbContext`. The service owns normalization, validation, code generation, paginated admin queries, localized public group queries, full replacement of translation/group children, lifecycle transitions, audit events, and optimistic concurrency. Controllers translate domain outcomes to the existing `ApiResponse<T>` envelope without exposing EF exceptions.

The implemented HTTP surface is:

- `GET /api/v1/reservation-extra-options?vehicleGroupId={guid}&locale={locale}` for active, non-archived, group-assigned localized items;
- admin list/create/full-update/status/restore/delete operations under `/api/admin/v1/reservation-extra-options`.

Admin routes require `AuthPolicyNames.AdminOnly`; public reads are anonymous. Both controller types use `RateLimitPolicyNames.Standard` and `ResponseCache(NoStore = true)`. Public ordering is `SortOrder`, then localized name. Enum values cross the wire as `PER_DAY` and `PER_RENTAL`. Admin DTOs expose `Version` from PostgreSQL `xmin` separately from display-only `UpdatedAt`.

Write validation enforces price `0..1,000,000`, quantity `1..20`, sort order `0..9,999`, the server icon allowlist, distinct existing group IDs, supported unique locales, and translation storage limits. Incomplete inactive drafts are valid; activation requires non-empty name/description values for `tr`, `en`, `de`, `ru`, and `ar`, plus at least one assignment. Codes use `extra-{guid:N}` and are omitted from update contracts so they remain immutable.

Translation and assignment replacement occurs in one `SaveChangesAsync` boundary with an explicit parent `UpdatedAt` mutation, ensuring PostgreSQL advances the parent `xmin`. The loaded `Version` is checked before mutation and EF concurrency exceptions map to `409`. Used options archive; unused options are deleted. If a concurrent selected-extra reference causes PostgreSQL foreign-key/restrict failure during delete, the tracker is cleared, the option is reloaded, and the operation completes as archive. Real two-context and delete-race tests cover both paths.

Audit entries are added to `AuditLogs` in the same save boundary as each catalog mutation. Stable actions cover create, update, activation, deactivation, assignment changes, delete, archive, and restore. `NewValue` contains only option code and changed field names; localized bodies and customer data are excluded.

### Phase 3 Generic Quote and Reservation Boundary

`ReservationExtraPricingService` owns authoritative generic-extra validation and calculation. Quote issuance loads selected options with their requested locale and vehicle-group assignment, rejects duplicate/stale/inactive/archived/unassigned/over-limit inputs, and snapshots localized text, pricing mode, unit price, quantity, rental days, and total. Submission revalidates structural availability while deliberately honoring the issued price when only price or `xmin` changed.

`ReservationQuoteService` combines the existing base/campaign/fee engine with generic extras and stores `ReservationQuoteV1` through `IReservationQuoteStore`. The Redis implementation keeps the quote for 15 minutes, stores only normalized price-driving data and a one-way session hash, acquires claims atomically, releases/finalizes only the owning claim token, and records consumed state separately so the retained quote can authenticate a database replay.

Draft and unpaid reservation creation first validate quote/session/input equality and current option structure, then persist the reservation, nullable unique `QuoteId`, `ReservationSelectedExtra` rows, and `ReservationPricingSnapshotV1` in one relational transaction. A retry that finds the unique database `QuoteId` must still validate the retained quote session and booking inputs before returning the existing DTO; it then reconciles Redis consumed state. Snapshot-backed reads never reconstruct current catalog prices, while pre-migration rows remain explicit total-only fallbacks.

### Phase 4-5 Admin, Public Booking, and Browser Acceptance Boundary

The admin settings surface uses explicit reservation-extra API contracts and stable SWR keys for list filters and lifecycle mutations. Its editor permits incomplete inactive drafts, derives readiness from all five locale name/description pairs plus at least one vehicle-group assignment, and submits activation to the server as the final authority. Public Step 3 stores only option identity, version, quantity, and display-only catalog fields; it removes newly generated legacy `extras` URL state and clears incompatible selections when the vehicle group changes. Step 4 renders the server quote, sends the quote ID with the matching session and non-price inputs, retries one recoverable conflict after refreshing the catalog/quote, and never creates a payment intent before reservation success.

The Docker acceptance target is the production web image at `http://localhost:3001`, not the development server. `frontend/e2e/tests/reservation-extra-options.spec.ts` retains a self-cleaning authoring scenario that verifies incomplete activation is disabled, activates complete TR/EN/DE/RU/AR content assigned to exactly one group, asserts localized `no-store` public catalog results for the assigned group, rejects visibility for an unassigned group, and deactivates/deletes its unused test option in `finally`. Its route-controlled Step 3 scenarios cover loading, per-day/per-rental quantity and total rules, retry/empty behavior, legacy warnings, and generated-URL cleanup. Its route-controlled Step 4 scenarios cover server quote/campaign/paid/unpaid ordering plus price-only quote preservation and bounded availability-conflict recovery without creating server-side reservation data or calling a payment provider. A real unpaid browser flow persists a child-seat row and full pricing snapshot, proves no double counting, preserves immutable history after a catalog edit, and renders a pre-migration `LEGACY_TOTAL_ONLY` warning. The real quote-lifecycle scenario reuses the browser-issued quote contract against Docker Redis/PostgreSQL: an expired key and mismatched session return `409` with zero rows, while replay returns the original reservation and the unique `quote_id` count remains one. Current-source proof passed as 9/9 existing scenarios plus focused quote-lifecycle 1/1 in separate clean API rate-limit windows. These checks close the first seven rows in checklist section 6.6; extra-specific responsive/network/screenshot evidence remains the browser acceptance gate.

The current comprehensive continuation artifact for this boundary is `C:\Users\muham\AppData\Local\Temp\2026-07-11-203021-reservation-extra-options-final-implementation-handoff.md`; canonical architecture and acceptance remain in this TDD, ADR 12.8, plan 16, implementation 17, and checklist 6.6.

---

# 12. Rate Limiting Configuration

Multi-tier rate limiting protecting API endpoints from abuse while
allowing legitimate traffic.

## 12.1 Rate Limit Tiers

| Tier         | Endpoint Pattern         | Limit | Window   | Burst |
| ------------ | ------------------------ | ----- | -------- | ----- |
| **Strict**   | `/api/v1/auth/*`         | 5     | 1 minute | 2     |
| **Strict**   | `/api/v1/payment/*`      | 10    | 1 minute | 5     |
| **Standard** | `/api/v1/reservations/*` | 30    | 1 minute | 10    |
| **Standard** | `/api/v1/search/*`       | 60    | 1 minute | 20    |
| **Relaxed**  | `/api/v1/public/*`       | 100   | 1 minute | 50    |
| **Health**   | `/health`                | 10    | 1 minute | 0     |

## 12.2 ASP.NET Core Rate Limiter

```csharp
// Program.cs configuration
builder.Services.AddRateLimiter(options =>
{
    // Global limiter
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(
        context => RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.User.Identity?.Name ??
                          context.Request.Headers["X-Forwarded-For"].ToString() ??
                          context.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 100,
                Window = TimeSpan.FromMinutes(1),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 2
            }));

    // Endpoint-specific policies
    options.AddPolicy("Strict", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: GetClientId(context),
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));

    options.AddPolicy("Payment", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: GetClientId(context),
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 2
            }));

    options.AddPolicy("Standard", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: GetClientId(context),
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 30,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 5
            }));

    // Custom rejection response
    options.OnRejected = async (context, token) =>
    {
        context.HttpContext.Response.StatusCode = 429;
        await context.HttpContext.Response.WriteAsJsonAsync(
            new { Error = "Rate limit exceeded. Try again later." }, token);
    };
});

// Helper method
static string GetClientId(HttpContext context)
{
    return context.User.Identity?.Name ??
           context.Request.Headers["X-Api-Key"].ToString() ??
           context.Connection.RemoteIpAddress?.ToString() ?? "anonymous";
}
```

## 12.3 Endpoint Attributes

```csharp
[ApiController]
[Route("api/v1/[controller]")]
public class PaymentController : ControllerBase
{
    [HttpPost("intent")]
    [EnableRateLimiting("Payment")]
    public async Task<IActionResult> CreateIntent(...)

    [HttpPost("webhook")]
    [EnableRateLimiting("Strict")]
    public async Task<IActionResult> HandleWebhook(...)
}

[ApiController]
[Route("api/v1/[controller]")]
public class AuthController : ControllerBase
{
    [HttpPost("login")]
    [EnableRateLimiting("Strict")]
    public async Task<IActionResult> Login(...)
}
```

## 12.4 Redis-Based Distributed Rate Limiting

```csharp
public class RedisRateLimiter : IRateLimiter
{
    private readonly IDatabase _redis;
    private readonly ILogger<RedisRateLimiter> _logger;

    public async Task<RateLimitResult> CheckAsync(
        string key,
        int limit,
        TimeSpan window)
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var windowStart = now - (long)window.TotalSeconds;

        // Lua script for atomic operation
        var script = @"
            redis.call('ZREMRANGEBYSCORE', KEYS[1], '-inf', ARGV[1])
            local current = redis.call('ZCARD', KEYS[1])
            if current < tonumber(ARGV[2]) then
                redis.call('ZADD', KEYS[1], ARGV[3], ARGV[3])
                redis.call('EXPIRE', KEYS[1], ARGV[4])
                return {0, current + 1}
            else
                return {1, current}
            end";

        var result = await _redis.ScriptEvaluateAsync(
            script,
            new RedisKey[] { $"ratelimit:{key}" },
            new RedisValue[] { windowStart, limit, now, (long)window.TotalSeconds });

        var values = (RedisResult[])result;
        bool isAllowed = (long)values[0] == 0;
        long currentCount = (long)values[1];

        return new RateLimitResult
        {
            IsAllowed = isAllowed,
            Limit = limit,
            Remaining = isAllowed ? limit - (int)currentCount : 0,
            ResetAfter = window
        };
    }
}
```

---

# 13. API Versioning Strategy

URL Path-based versioning ensuring backward compatibility and
smooth migration paths.

## 13.1 Versioning Approach

**Selected Strategy: URL Path Versioning**

Format: `/api/v{major}/[controller]`

Examples:

- `/api/v1/reservations` - Current stable
- `/api/v2/reservations` - Future enhancements

## 13.2 Configuration

```csharp
// Program.cs
builder.Services.AddApiVersioning(options =>
{
    // Default version when not specified
    options.DefaultApiVersion = new ApiVersion(1, 0);

    // Assume default version if not specified
    options.AssumeDefaultVersionWhenUnspecified = true;

    // Report supported versions in headers
    options.ReportApiVersions = true;

    // Read version from URL path
    options.ApiVersionReader = new UrlSegmentApiVersionReader();
});

builder.Services.AddVersionedApiExplorer(options =>
{
    options.GroupNameFormat = "'v'VVV";
    options.SubstituteApiVersionInUrl = true;
});

// Swagger configuration for versioning
builder.Services.AddSwaggerGen(options =>
{
    var provider = app.Services.GetRequiredService<IApiVersionDescriptionProvider>();

    foreach (var description in provider.ApiVersionDescriptions)
    {
        options.SwaggerDoc(
            description.GroupName,
            new OpenApiInfo
            {
                Title = "RentACar API",
                Version = description.ApiVersion.ToString(),
                Description = description.IsDeprecated ? "DEPRECATED" : "Active"
            });
    }
});
```

## 13.3 Controller Implementation

```csharp
// V1 Controller (Current)
[ApiController]
[Route("api/v{version:apiVersion}/[controller]")]
[ApiVersion("1.0")]
public class ReservationsController : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<ReservationV1>>> GetAll()
    {
        // V1 implementation
    }
}

// V2 Controller (Future)
[ApiController]
[Route("api/v{version:apiVersion}/[controller]")]
[ApiVersion("2.0")]
public class ReservationsController : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResult<ReservationV2>>> GetAll(
        [FromQuery] PaginationParams pagination)
    {
        // V2 with pagination and enhanced fields
    }
}
```

## 13.4 Deprecation Strategy

```csharp
[ApiController]
[Route("api/v{version:apiVersion}/[controller]")]
[ApiVersion("1.0", Deprecated = true)]  // Mark as deprecated
[ApiVersion("2.0")]
public class VehiclesController : ControllerBase
{
    [HttpGet]
    [MapToApiVersion("1.0")]
    public async Task<IActionResult> GetV1()
    {
        Response.Headers.Add("Deprecation", "true");
        Response.Headers.Add("Sunset", DateTime.UtcNow.AddMonths(6).ToString("R"));

        return Ok(await _service.GetV1Async());
    }

    [HttpGet]
    [MapToApiVersion("2.0")]
    public async Task<IActionResult> GetV2()
    {
        return Ok(await _service.GetV2Async());
    }
}
```

## 13.5 Version Compatibility Matrix

| Version | Status  | Sunset Date | Breaking Changes       |
| ------- | ------- | ----------- | ---------------------- |
| v1.0    | Active  | -           | Baseline               |
| v2.0    | Planned | -           | Pagination, new fields |

---

# 14. Security Implementation

## 14.1 JWT Authentication

```csharp
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Secret"]))
        };
    });
```

## 14.2 Authorization Policies

```csharp
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy =>
        policy.RequireRole("Admin", "SuperAdmin"));

    options.AddPolicy("ReservationAccess", policy =>
        policy.RequireClaim("Permission", "reservations.read"));

    options.AddPolicy("PaymentProcess", policy =>
        policy.RequireRole("Admin")
              .RequireClaim("Permission", "payments.process"));
});
```

## 14.3 CORS Configuration

```csharp
builder.Services.AddCors(options =>
{
    options.AddPolicy("PublicApi", policy =>
    {
        policy.WithOrigins(
                "https://alanyarentacar.com",
                "https://www.alanyarentacar.com")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });

    options.AddPolicy("AdminApi", policy =>
    {
        policy.WithOrigins("https://admin.alanyarentacar.com")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});
```

## 14.4 Dependency Vulnerability Verification

Backend dependency-security work must verify both direct and transitive NuGet packages:

```bash
dotnet list backend\RentACar.sln package --include-transitive --vulnerable
```

When a vulnerable transitive package is pulled by a framework integration package, prefer the narrowest compatible override before making broad framework upgrades. The 8 July 2026 `Microsoft.OpenApi` fix follows this pattern: `Microsoft.AspNetCore.OpenApi` stays on the current .NET 10 line, while `RentACar.API.csproj` explicitly references patched `Microsoft.OpenApi` 2.7.5.

Verification expectations for dependency-security slices:

- Restore the solution with the repo NuGet config.
- Re-run transitive vulnerability scanning and require 0 critical/high vulnerable backend packages.
- Build the backend solution with 0 warnings/errors.
- Run the backend unit and integration tests.
- If the repo-required Aikido MCP scanner is unavailable, record the scanner availability blocker separately from NuGet vulnerability status.

## 14.5 Time-Stable Test Data

Tests that exercise reservation editability rules must avoid fixed calendar dates for future reservations. A date that was safely in the future when the test was written can become historical and trigger the production rule that blocks updates after pickup has started.

Use `DateTime.UtcNow.Date.AddDays(...)` or an injected clock/test clock pattern for future reservation fixtures. Keep assertions focused on behavior, not on a hardcoded calendar day.

---

END OF DOCUMENT
