using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Primitives;
using Npgsql;
using RentACar.API.Contracts.Pricing;
using RentACar.API.Contracts.Reservations;
using RentACar.Core.Entities;
using RentACar.Core.Enums;
using RentACar.Core.Interfaces;
using RentACar.Core.Interfaces.Notifications;
using StackExchange.Redis;

namespace RentACar.API.Services;

public sealed class ReservationService : IReservationService
{
    private readonly IReservationRepository _reservationRepository;
    private readonly IRepository<Customer> _customerRepository;
    private readonly IRepository<Vehicle> _vehicleRepository;
    private readonly IVehicleRepository _vehicleRepositorySpecific;
    private readonly IRepository<Office> _officeRepository;
    private readonly IReservationHoldService _holdService;
    private readonly IApplicationDbContext _applicationDbContext;
    private readonly IFleetService _fleetService;
    private readonly IPricingService _pricingService;
    private readonly IPaymentService _paymentService;
    private readonly INotificationQueueService _notificationQueueService;
    private readonly IMemoryCache _memoryCache;
    private readonly AvailabilityCacheInvalidationSignal _availabilityCacheInvalidationSignal;
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<ReservationService> _logger;
    private readonly IReservationQuoteStore? _quoteStore;
    private readonly IReservationExtraPricingService? _extraPricingService;
    private readonly bool _allowLoadTestSessionPartition;
    private readonly TimeSpan _defaultHoldDuration = TimeSpan.FromMinutes(15);
    private readonly TimeSpan _maxHoldDuration = TimeSpan.FromMinutes(15);
    private readonly TimeSpan _availabilityCacheTtl = TimeSpan.FromMinutes(5);
    private readonly TimeSpan _holdCreationLockTtl = TimeSpan.FromSeconds(30);
    private readonly TimeSpan _quoteClaimTtl = TimeSpan.FromMinutes(2);

    public ReservationService(
        IReservationRepository reservationRepository,
        IRepository<Customer> customerRepository,
        IRepository<Vehicle> vehicleRepository,
        IVehicleRepository vehicleRepositorySpecific,
        IRepository<Office> officeRepository,
        IReservationHoldService holdService,
        IApplicationDbContext applicationDbContext,
        IFleetService fleetService,
        IPricingService pricingService,
        IPaymentService paymentService,
        INotificationQueueService notificationQueueService,
        IMemoryCache memoryCache,
        AvailabilityCacheInvalidationSignal availabilityCacheInvalidationSignal,
        IConnectionMultiplexer redis,
        IConfiguration configuration,
        ILogger<ReservationService> logger,
        IReservationQuoteStore? quoteStore = null,
        IReservationExtraPricingService? extraPricingService = null)
    {
        _reservationRepository = reservationRepository;
        _customerRepository = customerRepository;
        _vehicleRepository = vehicleRepository;
        _vehicleRepositorySpecific = vehicleRepositorySpecific;
        _officeRepository = officeRepository;
        _holdService = holdService;
        _applicationDbContext = applicationDbContext;
        _fleetService = fleetService;
        _pricingService = pricingService;
        _paymentService = paymentService;
        _notificationQueueService = notificationQueueService;
        _memoryCache = memoryCache;
        _availabilityCacheInvalidationSignal = availabilityCacheInvalidationSignal;
        _redis = redis;
        _allowLoadTestSessionPartition = configuration.GetValue<bool>("RateLimiting:LoadTestSessionPartition");
        _logger = logger;
        _quoteStore = quoteStore;
        _extraPricingService = extraPricingService;
    }

    public async Task<IReadOnlyList<AvailableVehicleGroupDto>> SearchAvailabilityAsync(
        Guid pickupOfficeId,
        Guid? returnOfficeId,
        DateTime pickupDateTimeUtc,
        DateTime returnDateTimeUtc,
        Guid? vehicleGroupId = null,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var actualReturnOfficeId = returnOfficeId ?? pickupOfficeId;
        var cacheKey = BuildAvailabilityCacheKey(
            pickupOfficeId,
            actualReturnOfficeId,
            pickupDateTimeUtc,
            returnDateTimeUtc,
            vehicleGroupId,
            page,
            pageSize);

        if (_memoryCache.TryGetValue(cacheKey, out IReadOnlyList<AvailableVehicleGroupDto>? cachedResult) && cachedResult != null)
        {
            return cachedResult;
        }

        var availabilityCacheInvalidationToken = _availabilityCacheInvalidationSignal.Token;

        // Get available vehicle groups from fleet service
        var fleetGroups = (await _fleetService.SearchAvailableVehicleGroupsAsync(
            pickupOfficeId,
            pickupDateTimeUtc,
            returnDateTimeUtc,
            vehicleGroupId,
            cancellationToken))
            .OrderBy(g => g.GroupName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        var result = new List<AvailableVehicleGroupDto>();

        foreach (var group in fleetGroups)
        {
            // Calculate pricing for each group
            var pricing = await _pricingService.CalculateBreakdownAsync(
                group.GroupId,
                pickupOfficeId,
                actualReturnOfficeId,
                pickupDateTimeUtc,
                returnDateTimeUtc,
                null, // no campaign code
                0, 0, null, false,
                cancellationToken);

            var rentalDays = (int)Math.Ceiling((returnDateTimeUtc - pickupDateTimeUtc).TotalDays);

            if (pricing != null)
            {
                var priceBreakdown = new List<PriceBreakdownItemDto>
                {
                    new() { Description = "Günlük Kiralama", Amount = pricing.BaseTotal, Type = "base" },
                    new() { Description = "Ekstralar", Amount = pricing.ExtrasTotal, Type = "fee" }
                };

                if (pricing.CampaignDiscount > 0)
                {
                    priceBreakdown.Add(new PriceBreakdownItemDto
                    {
                        Description = "Kampanya İndirimi",
                        Amount = -pricing.CampaignDiscount,
                        Type = "discount"
                    });
                }

                result.Add(new AvailableVehicleGroupDto
                {
                    Id = group.GroupId,
                    Name = group.GroupName,
                    NameTr = group.GroupName,
                    NameEn = group.GroupNameEn,
                    DailyPrice = pricing.DailyRate,
                    TotalPrice = pricing.FinalTotal,
                    DepositAmount = pricing.DepositAmount,
                    AvailableVehicleCount = group.AvailableCount,
                    MinAge = group.MinAge,
                    MinLicenseYears = group.MinLicenseYears,
                    Features = group.Features.ToList(),
                    PhotoUrl = group.ImageUrl,
                    RentalDays = rentalDays,
                    IsAvailable = group.AvailableCount > 0,
                    PriceBreakdown = priceBreakdown
                });
            }
            else
            {
                // Use default pricing from fleet group
                result.Add(new AvailableVehicleGroupDto
                {
                    Id = group.GroupId,
                    Name = group.GroupName,
                    NameTr = group.GroupName,
                    NameEn = group.GroupNameEn,
                    DailyPrice = group.DailyPrice,
                    TotalPrice = group.DailyPrice * rentalDays,
                    DepositAmount = group.DepositAmount,
                    AvailableVehicleCount = group.AvailableCount,
                    MinAge = group.MinAge,
                    MinLicenseYears = group.MinLicenseYears,
                    Features = group.Features.ToList(),
                    PhotoUrl = group.ImageUrl,
                    RentalDays = rentalDays,
                    IsAvailable = group.AvailableCount > 0,
                    PriceBreakdown = new List<PriceBreakdownItemDto>()
                });
            }
        }

        if (!availabilityCacheInvalidationToken.IsCancellationRequested)
        {
            _memoryCache.Set(cacheKey, result, new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = _availabilityCacheTtl,
                ExpirationTokens = { new CancellationChangeToken(availabilityCacheInvalidationToken) }
            });
        }

        return result;
    }

    public async Task<bool> IsVehicleGroupAvailableAsync(
        Guid vehicleGroupId,
        Guid pickupOfficeId,
        DateTime pickupDateTimeUtc,
        DateTime returnDateTimeUtc,
        CancellationToken cancellationToken = default)
    {
        var availableGroups = await _fleetService.SearchAvailableVehicleGroupsAsync(
            pickupOfficeId,
            pickupDateTimeUtc,
            returnDateTimeUtc,
            vehicleGroupId,
            cancellationToken);

        return availableGroups.Any(g => g.GroupId == vehicleGroupId && g.AvailableCount > 0);
    }

    public async Task<ReservationDto?> GetReservationByPublicCodeAsync(
        string publicCode,
        CancellationToken cancellationToken = default)
    {
        var reservation = await _reservationRepository.GetByPublicCodeAsync(publicCode, cancellationToken);
        return reservation != null ? MapToDto(reservation) : null;
    }

    public async Task<ReservationDto?> GetReservationByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var reservation = await _reservationRepository.GetByIdAsync(id, cancellationToken);
        return reservation != null ? MapToDto(reservation) : null;
    }

    public async Task<ReservationDto> CreateDraftReservationAsync(
        CreateReservationRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateQuoteAndLegacyCombination(request);
        var existingReservation = await ResolveExistingQuoteReservationAsync(request, cancellationToken);
        if (existingReservation is not null)
        {
            return MapToDto(existingReservation);
        }

        // Validate vehicle group availability
        var isAvailable = await IsVehicleGroupAvailableAsync(
            request.VehicleGroupId,
            request.PickupOfficeId,
            request.PickupDateTimeUtc,
            request.ReturnDateTimeUtc,
            cancellationToken);

        if (!isAvailable)
        {
            throw new InvalidOperationException("Vehicle group is not available for the selected dates");
        }

        // Get or create customer
        var customer = await GetOrCreateCustomerAsync(request, cancellationToken);

        var returnOfficeId = request.ReturnOfficeId == Guid.Empty
            ? request.PickupOfficeId
            : request.ReturnOfficeId;
        var pricingContext = await ResolveReservationPricingAsync(request, returnOfficeId, cancellationToken);

        var vehicle = await FindAvailableVehicleAsync(
            request.VehicleGroupId,
            request.PickupOfficeId,
            request.PickupDateTimeUtc,
            request.ReturnDateTimeUtc,
            request.SessionId,
            cancellationToken);

        if (vehicle is null)
        {
            await ReleaseQuoteClaimAsync(pricingContext, cancellationToken);
            throw new InvalidOperationException("Vehicle group is not available for the selected dates");
        }

        await using var transaction = await TryBeginTransactionAsync(cancellationToken);
        var reservation = new Reservation
        {
            PublicCode = GeneratePublicCode(),
            CustomerId = customer.Id,
            Customer = customer, // Set navigation property for mapping
            VehicleId = vehicle.Id,
            Vehicle = vehicle,
            PickupOfficeId = request.PickupOfficeId,
            PickupOffice = await _officeRepository.GetByIdAsync(request.PickupOfficeId, cancellationToken),
            ReturnOfficeId = returnOfficeId,
            ReturnOffice = await _officeRepository.GetByIdAsync(returnOfficeId, cancellationToken),
            PickupDateTime = request.PickupDateTimeUtc,
            ReturnDateTime = request.ReturnDateTimeUtc,
            Status = ReservationStatus.Draft,
            TotalAmount = pricingContext.Pricing.FinalTotal,
            Notes = request.Notes,
            QuoteId = pricingContext.QuoteId,
            PricingSnapshot = pricingContext.Snapshot
        };
        ApplyDriverSnapshot(reservation, request.Customer, request.Driver);
        AddSelectedExtraSnapshots(reservation, pricingContext.QuotedExtras);

        try
        {
            await _reservationRepository.AddAsync(reservation, cancellationToken);
            await SaveChangesWithConcurrencyHandlingAsync(cancellationToken);
            if (transaction != null)
            {
                await transaction.CommitAsync(cancellationToken);
            }

            InvalidateAvailabilityCache();
        }
        catch
        {
            if (transaction != null)
            {
                await transaction.RollbackAsync(cancellationToken);
            }
            await ReleaseQuoteClaimAsync(pricingContext, cancellationToken);
            throw;
        }

        await FinalizeQuoteAsync(pricingContext, reservation.Id, cancellationToken);

        _logger.LogInformation(
            "Created draft reservation {PublicCode} for customer {CustomerId}",
            reservation.PublicCode, customer.Id);

        return MapToDto(reservation);
    }

    public async Task<ReservationDto> CreateUnpaidRequestAsync(
        CreateReservationRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateQuoteAndLegacyCombination(request);
        var existingReservation = await ResolveExistingQuoteReservationAsync(request, cancellationToken);
        if (existingReservation is not null)
        {
            return MapToDto(existingReservation);
        }

        var paymentMethods = await PaymentMethodFeatureFlags.GetAvailabilityAsync(_applicationDbContext, cancellationToken);
        if (!paymentMethods.UnpaidRequestEnabled)
        {
            throw new InvalidOperationException("Odeme yapmadan rezervasyon talebi su anda aktif degil.");
        }

        var isAvailable = await IsVehicleGroupAvailableAsync(
            request.VehicleGroupId,
            request.PickupOfficeId,
            request.PickupDateTimeUtc,
            request.ReturnDateTimeUtc,
            cancellationToken);

        if (!isAvailable)
        {
            throw new InvalidOperationException("Vehicle group is not available for the selected dates");
        }

        var customer = await GetOrCreateCustomerAsync(request, cancellationToken);
        var returnOfficeId = request.ReturnOfficeId == Guid.Empty
            ? request.PickupOfficeId
            : request.ReturnOfficeId;
        var pricingContext = await ResolveReservationPricingAsync(request, returnOfficeId, cancellationToken);

        var vehicle = await FindAvailableVehicleAsync(
            request.VehicleGroupId,
            request.PickupOfficeId,
            request.PickupDateTimeUtc,
            request.ReturnDateTimeUtc,
            request.SessionId,
            cancellationToken);

        if (vehicle is null)
        {
            await ReleaseQuoteClaimAsync(pricingContext, cancellationToken);
            throw new InvalidOperationException("Vehicle group is not available for the selected dates");
        }

        await using var transaction = await TryBeginTransactionAsync(cancellationToken);
        var now = DateTime.UtcNow;
        var reservation = new Reservation
        {
            PublicCode = GeneratePublicCode(),
            CustomerId = customer.Id,
            Customer = customer,
            VehicleId = vehicle.Id,
            Vehicle = vehicle,
            PickupOfficeId = request.PickupOfficeId,
            PickupOffice = await _officeRepository.GetByIdAsync(request.PickupOfficeId, cancellationToken),
            ReturnOfficeId = returnOfficeId,
            ReturnOffice = await _officeRepository.GetByIdAsync(returnOfficeId, cancellationToken),
            PickupDateTime = request.PickupDateTimeUtc,
            ReturnDateTime = request.ReturnDateTimeUtc,
            Status = ReservationStatus.UnpaidRequest,
            TotalAmount = pricingContext.Pricing.FinalTotal,
            Notes = request.Notes,
            QuoteId = pricingContext.QuoteId,
            PricingSnapshot = pricingContext.Snapshot,
            UnpaidRequestExpiresAtUtc = now.AddHours(24),
            CreatedAt = now,
            UpdatedAt = now
        };
        ApplyDriverSnapshot(reservation, request.Customer, request.Driver);
        AddSelectedExtraSnapshots(reservation, pricingContext.QuotedExtras);

        try
        {
            await _reservationRepository.AddAsync(reservation, cancellationToken);
            await SaveChangesWithConcurrencyHandlingAsync(cancellationToken);

            if (transaction != null)
            {
                await transaction.CommitAsync(cancellationToken);
            }

            InvalidateAvailabilityCache();
        }
        catch (DbUpdateException ex) when (IsReservationOverlapViolation(ex))
        {
            if (transaction != null)
            {
                await transaction.RollbackAsync(cancellationToken);
            }

            await ReleaseQuoteClaimAsync(pricingContext, cancellationToken);
            throw new InvalidOperationException("Selected vehicle is no longer available for these dates.", ex);
        }
        catch
        {
            if (transaction != null)
            {
                await transaction.RollbackAsync(cancellationToken);
            }
            await ReleaseQuoteClaimAsync(pricingContext, cancellationToken);
            throw;
        }

        await FinalizeQuoteAsync(pricingContext, reservation.Id, cancellationToken);

        _logger.LogInformation(
            "Created unpaid reservation request {PublicCode} for customer {CustomerId}",
            reservation.PublicCode,
            customer.Id);

        return MapToDto(reservation);
    }

    public async Task<ReservationDto> CreateManualReservationAsync(
        AdminManualReservationRequest request,
        CancellationToken cancellationToken = default)
    {
        var vehicle = await _vehicleRepository
            .GetQueryable()
            .Include(x => x.Group)
            .Include(x => x.Office)
            .FirstOrDefaultAsync(x => x.Id == request.VehicleId, cancellationToken);

        if (vehicle is null)
        {
            throw new InvalidOperationException("Vehicle not found.");
        }

        var pickupOffice = await _officeRepository.GetByIdAsync(request.PickupOfficeId, cancellationToken)
            ?? throw new InvalidOperationException("Pickup office not found.");
        var returnOffice = await _officeRepository.GetByIdAsync(request.ReturnOfficeId, cancellationToken)
            ?? throw new InvalidOperationException("Return office not found.");

        var hasOverlap = await _reservationRepository.HasOverlappingReservationsAsync(
            request.VehicleId,
            request.PickupDateTimeUtc,
            request.ReturnDateTimeUtc,
            null,
            cancellationToken);

        if (hasOverlap)
        {
            throw new InvalidOperationException("Vehicle has overlapping reservations");
        }

        var customer = await GetOrCreateManualCustomerAsync(request, cancellationToken);
        var totalAmount = request.TotalAmount;
        if (!totalAmount.HasValue)
        {
            var pricing = await _pricingService.CalculateBreakdownAsync(
                vehicle.GroupId,
                request.PickupOfficeId,
                request.ReturnOfficeId,
                request.PickupDateTimeUtc,
                request.ReturnDateTimeUtc,
                null,
                0,
                0,
                null,
                false,
                cancellationToken);

            totalAmount = pricing?.FinalTotal
                ?? throw new InvalidOperationException("Could not calculate pricing for the reservation");
        }

        await using var transaction = await TryBeginTransactionAsync(cancellationToken);
        var now = DateTime.UtcNow;
        var reservation = new Reservation
        {
            PublicCode = GeneratePublicCode(),
            CustomerId = customer.Id,
            Customer = customer,
            VehicleId = vehicle.Id,
            Vehicle = vehicle,
            PickupOfficeId = request.PickupOfficeId,
            PickupOffice = pickupOffice,
            ReturnOfficeId = request.ReturnOfficeId,
            ReturnOffice = returnOffice,
            PickupDateTime = request.PickupDateTimeUtc,
            ReturnDateTime = request.ReturnDateTimeUtc,
            Status = ReservationStatus.Confirmed,
            TotalAmount = totalAmount.Value,
            Notes = request.Notes,
            CreatedAt = now,
            UpdatedAt = now
        };
        ApplyDriverSnapshot(reservation, null, request.Driver);

        try
        {
            await _reservationRepository.AddAsync(reservation, cancellationToken);
            await SaveChangesWithConcurrencyHandlingAsync(cancellationToken);

            if (transaction != null)
            {
                await transaction.CommitAsync(cancellationToken);
            }
        }
        catch (DbUpdateException ex) when (IsReservationOverlapViolation(ex))
        {
            if (transaction != null)
            {
                await transaction.RollbackAsync(cancellationToken);
            }

            throw new InvalidOperationException("Vehicle has overlapping reservations", ex);
        }

        _logger.LogInformation(
            "Created admin manual reservation {PublicCode} for vehicle {VehicleId}",
            reservation.PublicCode,
            vehicle.Id);

        return MapToDto(reservation);
    }

    public async Task<ReservationDto?> UpdateReservationAsync(
        Guid id,
        UpdateReservationRequest request,
        CancellationToken cancellationToken = default)
    {
        var reservation = await _reservationRepository.GetByIdAsync(id, cancellationToken);
        if (reservation == null)
        {
            return null;
        }

        if (!CanModifyReservation(reservation.Status))
        {
            throw new InvalidOperationException($"Cannot update reservation in {reservation.Status} status");
        }

        if (reservation.PickupDateTime <= DateTime.UtcNow)
        {
            throw new InvalidOperationException("Cannot update a reservation after pickup has started.");
        }

        var newPickupDateTime = request.PickupDateTimeUtc ?? reservation.PickupDateTime;
        var newReturnDateTime = request.ReturnDateTimeUtc ?? reservation.ReturnDateTime;
        var newPickupOfficeId = request.PickupOfficeId ?? reservation.PickupOfficeId;
        var newReturnOfficeId = request.ReturnOfficeId ?? reservation.ReturnOfficeId;

        if (newReturnDateTime <= newPickupDateTime)
        {
            throw new InvalidOperationException("Return date must be after pickup date.");
        }

        var hasOverlap = await _reservationRepository.HasOverlappingReservationsAsync(
            reservation.VehicleId,
            newPickupDateTime,
            newReturnDateTime,
            reservation.Id,
            cancellationToken);

        if (hasOverlap)
        {
            throw new InvalidOperationException("Vehicle has overlapping reservations");
        }

        var vehicleGroupId = reservation.Vehicle?.GroupId
            ?? await _vehicleRepository
                .GetQueryable()
                .Where(x => x.Id == reservation.VehicleId)
                .Select(x => x.GroupId)
                .FirstOrDefaultAsync(cancellationToken);

        if (vehicleGroupId == Guid.Empty)
        {
            throw new InvalidOperationException("Vehicle group not found.");
        }

        var pricing = await _pricingService.CalculateBreakdownAsync(
            vehicleGroupId,
            newPickupOfficeId,
            newReturnOfficeId,
            newPickupDateTime,
            newReturnDateTime,
            null,
            0,
            0,
            null,
            false,
            cancellationToken);

        if (pricing == null)
        {
            throw new InvalidOperationException("Could not calculate pricing for the reservation");
        }

        if (request.PickupOfficeId.HasValue && request.PickupOfficeId.Value != reservation.PickupOfficeId)
        {
            reservation.PickupOffice = await _officeRepository.GetByIdAsync(request.PickupOfficeId.Value, cancellationToken)
                ?? throw new InvalidOperationException("Pickup office not found.");
        }

        if (request.ReturnOfficeId.HasValue && request.ReturnOfficeId.Value != reservation.ReturnOfficeId)
        {
            reservation.ReturnOffice = await _officeRepository.GetByIdAsync(request.ReturnOfficeId.Value, cancellationToken)
                ?? throw new InvalidOperationException("Return office not found.");
        }

        reservation.PickupDateTime = newPickupDateTime;
        reservation.ReturnDateTime = newReturnDateTime;
        reservation.PickupOfficeId = newPickupOfficeId;
        reservation.ReturnOfficeId = newReturnOfficeId;
        reservation.TotalAmount = pricing.FinalTotal;

        if (request.Customer is not null)
        {
            await ApplyCustomerUpdateAsync(reservation, request.Customer, cancellationToken);
        }

        if (request.Driver is not null || request.Customer is not null)
        {
            ApplyDriverSnapshot(reservation, request.Customer, request.Driver);
        }

        if (request.Notes is not null)
        {
            reservation.Notes = request.Notes;
        }

        reservation.UpdatedAt = DateTime.UtcNow;
        await _applicationDbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Updated reservation {ReservationId}", id);

        return MapToDto(reservation);
    }

    public async Task<bool> CancelReservationAsync(
        Guid id,
        string? reason = null,
        CancellationToken cancellationToken = default)
    {
        var reservation = await _reservationRepository.GetByIdAsync(id, cancellationToken);
        if (reservation == null)
        {
            return false;
        }

        // Only allow cancellation for certain statuses
        if (!CanCancelReservation(reservation.Status))
        {
            _logger.LogWarning(
                "Cannot cancel reservation {ReservationId} in status {Status}",
                id, reservation.Status);
            return false;
        }

        reservation.Status = ReservationStatus.Cancelled;
        reservation.UpdatedAt = DateTime.UtcNow;

        // Release any holds
        await _holdService.ReleaseHoldAsync(id, cancellationToken);
        await _applicationDbContext.SaveChangesAsync(cancellationToken);
        await QueueReservationCancelledNotificationsAsync(reservation, cancellationToken);

        _logger.LogInformation(
            "Cancelled reservation {ReservationId}. HasReason: {HasReason}",
            id, !string.IsNullOrWhiteSpace(reason));

        return true;
    }

    public async Task<ReservationHoldDto?> CreateHoldAsync(
        Guid reservationId,
        string sessionId,
        CancellationToken cancellationToken = default)
    {
        string? holdCreationLockKey = null;

        var reservation = await _reservationRepository.GetByIdAsync(reservationId, cancellationToken);
        if (reservation == null)
        {
            return null;
        }

        // Check if reservation can be held
        if (reservation.Status != ReservationStatus.Draft && reservation.Status != ReservationStatus.Hold)
        {
            _logger.LogWarning(
                "Cannot create hold for reservation {ReservationId} in status {Status}",
                reservationId, reservation.Status);
            return null;
        }

        if (reservation.VehicleId == Guid.Empty)
        {
            _logger.LogWarning(
                "Reservation {ReservationId} has no selected vehicle",
                reservationId);
            return null;
        }

        var selectedVehicle = reservation.Vehicle;
        var vehicleGroupId = selectedVehicle?.GroupId;
        if (vehicleGroupId == null)
        {
            selectedVehicle = await _vehicleRepository
                .GetByIdAsync(reservation.VehicleId, cancellationToken);

            vehicleGroupId = selectedVehicle?.GroupId;
        }

        if (vehicleGroupId == null || selectedVehicle == null)
        {
            _logger.LogWarning(
                "Reservation {ReservationId} could not resolve a vehicle group from vehicle {VehicleId}",
                reservationId,
                reservation.VehicleId);
            return null;
        }

        var pickupOfficeId = selectedVehicle.OfficeId;
        if (pickupOfficeId == Guid.Empty)
        {
            _logger.LogWarning(
                "Reservation {ReservationId} could not resolve a pickup office from vehicle {VehicleId}",
                reservationId,
                reservation.VehicleId);
            return null;
        }

        try
        {
            holdCreationLockKey = BuildHoldCreationLockKey(
                vehicleGroupId.Value,
                reservation.PickupDateTime,
                reservation.ReturnDateTime,
                sessionId);

            var lockAcquired = await _redis.GetDatabase().StringSetAsync(
                holdCreationLockKey,
                reservationId.ToString("N"),
                _holdCreationLockTtl,
                When.NotExists,
                CommandFlags.None);

            if (!lockAcquired)
            {
                _logger.LogWarning(
                    "CreateHoldAsync lock is already held for vehicle group {VehicleGroupId} between {PickupDate} and {ReturnDate}",
                    reservation.VehicleId,
                    reservation.PickupDateTime,
                reservation.ReturnDateTime);
                return null;
            }

            var existingHold = await _holdService.GetHoldAsync(reservationId, cancellationToken);
            if (existingHold != null && existingHold.ExpiresAt > DateTime.UtcNow)
            {
                if (!string.Equals(existingHold.SessionId, sessionId, StringComparison.Ordinal))
                {
                    _logger.LogWarning(
                        "Reservation {ReservationId} is already held by another session",
                        reservationId);
                    return null;
                }

                if (reservation.Status != ReservationStatus.Hold)
                {
                    reservation.Status = ReservationStatus.Hold;
                    reservation.VehicleId = existingHold.VehicleId;
                    reservation.UpdatedAt = DateTime.UtcNow;
                    await SaveChangesWithConcurrencyHandlingAsync(cancellationToken);
                }

                var remaining = Math.Max(0, (int)Math.Ceiling((existingHold.ExpiresAt - DateTime.UtcNow).TotalMinutes));
                return new ReservationHoldDto
                {
                    Id = reservationId,
                    ReservationId = reservationId,
                    ExpiresAt = existingHold.ExpiresAt,
                    SessionId = sessionId,
                    RemainingMinutes = remaining,
                    IsExpired = remaining <= 0
                };
            }

            var candidateVehicles = await GetOrderedCandidateVehiclesAsync(
                vehicleGroupId.Value,
                pickupOfficeId,
                sessionId,
                cancellationToken);

            if (candidateVehicles.Count == 0)
            {
                _logger.LogWarning(
                    "No available vehicle found for reservation {ReservationId}",
                    reservationId);
                return null;
            }

            foreach (var vehicle in candidateVehicles)
            {
                var hasOverlap = await _reservationRepository.HasOverlappingReservationsAsync(
                    vehicle.Id,
                    reservation.PickupDateTime,
                    reservation.ReturnDateTime,
                    reservationId,
                    cancellationToken);

                if (hasOverlap)
                {
                    continue;
                }

                await using var transaction = await TryBeginTransactionAsync(cancellationToken);

                try
                {
                    reservation.Status = ReservationStatus.Hold;
                    reservation.VehicleId = vehicle.Id;
                    reservation.UpdatedAt = DateTime.UtcNow;

                    await SaveChangesWithConcurrencyHandlingAsync(cancellationToken);

                    var success = await _holdService.CreateHoldAsync(
                        reservationId,
                        vehicle.Id,
                        sessionId,
                        _defaultHoldDuration,
                        cancellationToken);

                    if (!success)
                    {
                        if (transaction != null)
                        {
                            await transaction.RollbackAsync(cancellationToken);
                        }
                        return null;
                    }

                    if (transaction != null)
                    {
                        await transaction.CommitAsync(cancellationToken);
                    }

                    var expiresAt = DateTime.UtcNow.Add(_defaultHoldDuration);

                    _logger.LogInformation(
                        "Created hold for reservation {ReservationId}, vehicle {VehicleId}, expires {ExpiresAt}",
                        reservationId, vehicle.Id, expiresAt);

                    return new ReservationHoldDto
                    {
                        Id = Guid.NewGuid(),
                        ReservationId = reservationId,
                        ExpiresAt = expiresAt,
                        SessionId = sessionId,
                        RemainingMinutes = (int)_defaultHoldDuration.TotalMinutes,
                        IsExpired = false
                    };
                }
                catch (DbUpdateException ex) when (IsReservationOverlapViolation(ex))
                {
                    if (transaction != null)
                    {
                        await transaction.RollbackAsync(cancellationToken);
                    }

                    _logger.LogWarning(
                        "Overlap detected while creating hold for reservation {ReservationId} and vehicle {VehicleId}; retrying with next candidate",
                        reservationId,
                        vehicle.Id);
                }
                catch
                {
                    if (transaction != null)
                    {
                        await transaction.RollbackAsync(cancellationToken);
                    }

                    throw;
                }
            }

            _logger.LogWarning(
                "No hold could be created for reservation {ReservationId} after checking all candidate vehicles",
                reservationId);
            return null;
        }
        finally
        {
            if (holdCreationLockKey is not null)
            {
                await _redis.GetDatabase().KeyDeleteAsync(holdCreationLockKey);
            }
        }
    }

    private string BuildHoldCreationLockKey(
        Guid vehicleGroupId,
        DateTime pickupDate,
        DateTime returnDate,
        string sessionId)
    {
        var sessionSuffix = _allowLoadTestSessionPartition && !string.IsNullOrWhiteSpace(sessionId)
            ? $":{sessionId}"
            : string.Empty;

        return $"hold:{vehicleGroupId}:{pickupDate:yyyyMMddHHmm}:{returnDate:yyyyMMddHHmm}{sessionSuffix}";
    }

    public async Task<ReservationHoldDto?> ExtendHoldAsync(
        Guid holdId,
        CancellationToken cancellationToken = default)
    {
        // Note: In Redis implementation, holdId is the reservationId
        var canExtend = await CanHoldBeExtendedAsync(holdId, cancellationToken);
        if (!canExtend)
        {
            return null;
        }

        var success = await _holdService.ExtendHoldAsync(
            holdId,
            TimeSpan.FromMinutes(5), // Extend by 5 minutes
            _maxHoldDuration,
            cancellationToken);

        if (!success)
        {
            return null;
        }

        var reservation = await _reservationRepository.GetByIdAsync(holdId, cancellationToken);
        var newExpiry = DateTime.UtcNow.Add(TimeSpan.FromMinutes(5));

        return new ReservationHoldDto
        {
            Id = holdId,
            ReservationId = holdId,
            ExpiresAt = newExpiry,
            SessionId = string.Empty,
            RemainingMinutes = 5,
            IsExpired = false
        };
    }

    public async Task<bool> ReleaseHoldAsync(
        Guid holdId,
        CancellationToken cancellationToken = default)
    {
        return await _holdService.ReleaseHoldAsync(holdId, cancellationToken);
    }

    public async Task<bool> ReleaseHoldByReservationIdAsync(
        Guid reservationId,
        CancellationToken cancellationToken = default)
    {
        return await _holdService.ReleaseHoldAsync(reservationId, cancellationToken);
    }

    public async Task<ReservationDto?> TransitionStatusAsync(
        Guid reservationId,
        ReservationStatus newStatus,
        CancellationToken cancellationToken = default)
    {
        var reservation = await _reservationRepository.GetByIdAsync(reservationId, cancellationToken);
        if (reservation == null)
        {
            return null;
        }

        var isValidTransition = await IsValidStatusTransitionAsync(
            reservation.Status,
            newStatus,
            cancellationToken);

        if (!isValidTransition)
        {
            _logger.LogWarning(
                "Invalid status transition from {CurrentStatus} to {NewStatus} for reservation {ReservationId}",
                reservation.Status, newStatus, reservationId);
            return null;
        }

        var oldStatus = reservation.Status;
        reservation.Status = newStatus;
        if (newStatus != ReservationStatus.UnpaidRequest)
        {
            reservation.UnpaidRequestExpiresAtUtc = null;
        }
        reservation.UpdatedAt = DateTime.UtcNow;
        await SaveChangesWithConcurrencyHandlingAsync(cancellationToken);

        _logger.LogInformation(
            "Reservation {ReservationId} transitioned from {OldStatus} to {NewStatus}",
            reservationId, oldStatus, newStatus);

        return MapToDto(reservation);
    }

    public async Task<ReservationDto?> ProcessPaymentAsync(
        Guid reservationId,
        PaymentInfoRequest paymentInfo,
        CancellationToken cancellationToken = default)
    {
        var reservation = await _reservationRepository.GetByIdAsync(reservationId, cancellationToken);
        if (reservation == null)
        {
            return null;
        }

        if (reservation.Status is not (ReservationStatus.Hold or ReservationStatus.PendingPayment))
        {
            throw new InvalidOperationException($"Cannot process payment for reservation in {reservation.Status} status");
        }

        var hasPaymentIntent = await _applicationDbContext.PaymentIntents
            .AnyAsync(x => x.ReservationId == reservationId, cancellationToken);

        if (!hasPaymentIntent)
        {
            await _applicationDbContext.PaymentIntents.AddAsync(new PaymentIntent
            {
                ReservationId = reservationId,
                Amount = reservation.TotalAmount,
                Status = PaymentStatus.Pending,
                Provider = string.IsNullOrWhiteSpace(paymentInfo.PaymentMethod)
                    ? "Manual"
                    : paymentInfo.PaymentMethod,
                IdempotencyKey = $"reservation-{reservationId:N}-{Guid.NewGuid():N}"
            }, cancellationToken);
        }

        reservation.Status = ReservationStatus.PendingPayment;
        reservation.UpdatedAt = DateTime.UtcNow;
        await SaveChangesWithConcurrencyHandlingAsync(cancellationToken);

        _logger.LogInformation(
            "Payment processing requested for reservation {ReservationId}",
            reservationId);
        return MapToDto(reservation);
    }

    public async Task<ReservationDto?> ConfirmPaymentAsync(
        Guid reservationId,
        string transactionId,
        CancellationToken cancellationToken = default)
    {
        var reservation = await _reservationRepository.GetByIdAsync(reservationId, cancellationToken);
        if (reservation == null)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(transactionId))
        {
            throw new InvalidOperationException("Geçerli bir ödeme referansı gereklidir.");
        }

        var latestIntent = await _applicationDbContext.PaymentIntents
            .Where(x => x.ReservationId == reservationId)
            .Where(x => x.ProviderTransactionId == transactionId || x.ProviderIntentId == transactionId)
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (latestIntent == null)
        {
            throw new InvalidOperationException("Bu rezervasyon için doğrulanmış ödeme referansı bulunamadı.");
        }

        if (latestIntent.Status is not (PaymentStatus.Succeeded or PaymentStatus.Authorized))
        {
            throw new InvalidOperationException("Ödeme doğrulanmadan rezervasyon paid durumuna alınamaz.");
        }

        reservation.Status = ReservationStatus.Paid;
        reservation.UpdatedAt = DateTime.UtcNow;
        latestIntent.UpdatedAt = DateTime.UtcNow;
        await SaveChangesWithConcurrencyHandlingAsync(cancellationToken);
        await QueueReservationConfirmedNotificationsAsync(reservation, cancellationToken);
        await QueueReservationReminderNotificationsAsync(reservation, cancellationToken);

        _logger.LogInformation(
            "Payment confirmation accepted for reservation {ReservationId}, tx: {TransactionId}",
            reservationId, transactionId);
        return MapToDto(reservation);
    }

    public async Task<ReservationDto?> CheckInAsync(
        Guid reservationId,
        CheckInRequest request,
        CancellationToken cancellationToken = default)
    {
        var reservation = await _reservationRepository.GetByIdAsync(reservationId, cancellationToken);
        if (reservation == null)
        {
            return null;
        }

        if (reservation.Status is not (ReservationStatus.Paid or ReservationStatus.Confirmed))
        {
            throw new InvalidOperationException($"Cannot check in reservation in {reservation.Status} status");
        }

        await _paymentService.CreateDepositPreAuthorizationAsync(reservationId, cancellationToken);
        reservation.Status = ReservationStatus.Active;
        reservation.UpdatedAt = DateTime.UtcNow;
        await SaveChangesWithConcurrencyHandlingAsync(cancellationToken);

        _logger.LogInformation(
            "Checked in reservation {ReservationId}. Mileage: {ActualMileage}, Fuel: {ActualFuelLevel}%",
            reservationId, request.ActualMileage, request.ActualFuelLevel);

        return MapToDto(reservation);
    }

    public async Task<ReservationDto?> CheckOutAsync(
        Guid reservationId,
        CheckOutRequest request,
        CancellationToken cancellationToken = default)
    {
        var reservation = await _reservationRepository.GetByIdAsync(reservationId, cancellationToken);
        if (reservation == null)
        {
            return null;
        }

        if (reservation.Status != ReservationStatus.Active)
        {
            throw new InvalidOperationException($"Cannot check out reservation in {reservation.Status} status");
        }

        if (request.IsDamaged && request.DamageFee.GetValueOrDefault() > 0)
        {
            await _paymentService.CaptureDepositAsync(
                reservationId,
                request.DamageFee!.Value,
                request.Notes,
                cancellationToken);
        }
        else
        {
            await _paymentService.ReleaseDepositAsync(reservationId, request.Notes, cancellationToken);
        }

        reservation.Status = ReservationStatus.Completed;
        reservation.UpdatedAt = DateTime.UtcNow;
        await SaveChangesWithConcurrencyHandlingAsync(cancellationToken);

        _logger.LogInformation(
            "Checked out reservation {ReservationId}. Mileage: {ReturnMileage}, Fuel: {ReturnFuelLevel}%",
            reservationId, request.ReturnMileage, request.ReturnFuelLevel);

        return MapToDto(reservation);
    }

    public async Task<ReservationDto?> ExpireReservationAsync(
        Guid reservationId,
        CancellationToken cancellationToken = default)
    {
        var reservation = await _reservationRepository.GetByIdAsync(reservationId, cancellationToken);
        if (reservation == null)
        {
            return null;
        }

        if (reservation.Status is not (ReservationStatus.Hold or ReservationStatus.Draft or ReservationStatus.UnpaidRequest))
        {
            _logger.LogWarning(
                "Cannot expire reservation {ReservationId} in status {Status}",
                reservationId, reservation.Status);
            return null;
        }

        reservation.Status = ReservationStatus.Expired;
        reservation.UnpaidRequestExpiresAtUtc = null;
        reservation.UpdatedAt = DateTime.UtcNow;

        await _holdService.ReleaseHoldAsync(reservationId, cancellationToken);
        await SaveChangesWithConcurrencyHandlingAsync(cancellationToken);

        _logger.LogInformation("Expired reservation {ReservationId}", reservationId);

        return MapToDto(reservation);
    }

    public async Task ProcessExpiredReservationsAsync(CancellationToken cancellationToken = default)
    {
        var expiredHolds = await _holdService.GetExpiredHoldsAsync(cancellationToken);
        var now = DateTime.UtcNow;

        foreach (var reservationId in expiredHolds)
        {
            try
            {
                await ExpireReservationAsync(reservationId, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Failed to process expired reservation {ReservationId}",
                    reservationId);
            }
        }

        var expiredUnpaidRequests = await _applicationDbContext.Reservations
            .Where(x =>
                x.Status == ReservationStatus.UnpaidRequest
                && x.UnpaidRequestExpiresAtUtc != null
                && x.UnpaidRequestExpiresAtUtc <= now)
            .ToListAsync(cancellationToken);

        foreach (var reservation in expiredUnpaidRequests)
        {
            reservation.Status = ReservationStatus.Expired;
            reservation.UnpaidRequestExpiresAtUtc = null;
            reservation.UpdatedAt = now;
        }

        if (expiredUnpaidRequests.Count > 0)
        {
            await SaveChangesWithConcurrencyHandlingAsync(cancellationToken);
        }

        _logger.LogInformation(
            "Processed {HoldCount} expired holds and {UnpaidRequestCount} expired unpaid requests",
            expiredHolds.Count,
            expiredUnpaidRequests.Count);
    }

    public async Task<IReadOnlyList<ReservationDto>> GetAllReservationsAsync(
        ReservationFilterRequest? filter = null,
        CancellationToken cancellationToken = default)
    {
        // Parse status string to enum if provided
        ReservationStatus? statusFilter = null;
        if (!string.IsNullOrEmpty(filter?.Status) &&
            Enum.TryParse<ReservationStatus>(filter.Status, true, out var parsedStatus))
        {
            statusFilter = parsedStatus;
        }

        var reservations = await _reservationRepository.SearchReservationsAsync(
            filter?.CustomerId,
            filter?.VehicleId,
            statusFilter,
            filter?.FromDate,
            filter?.ToDate,
            filter?.SearchTerm,
            filter?.Page ?? 1,
            filter?.PageSize ?? 20,
            cancellationToken);

        return reservations.Select(MapToDto).ToList();
    }

    public async Task<ReservationDto?> AssignVehicleAsync(
        Guid reservationId,
        Guid vehicleId,
        CancellationToken cancellationToken = default)
    {
        var reservation = await _reservationRepository.GetByIdAsync(reservationId, cancellationToken);
        if (reservation == null)
        {
            return null;
        }

        var vehicle = await _vehicleRepository.GetByIdAsync(vehicleId, cancellationToken);
        if (vehicle == null)
        {
            throw new InvalidOperationException("Vehicle not found");
        }

        // Check for overlapping reservations
        var hasOverlap = await _reservationRepository.HasOverlappingReservationsAsync(
            vehicleId,
            reservation.PickupDateTime,
            reservation.ReturnDateTime,
            reservationId,
            cancellationToken);

        if (hasOverlap)
        {
            throw new InvalidOperationException("Vehicle has overlapping reservations");
        }

        reservation.VehicleId = vehicleId;
        reservation.UpdatedAt = DateTime.UtcNow;
        await SaveChangesWithConcurrencyHandlingAsync(cancellationToken);

        _logger.LogInformation(
            "Assigned vehicle {VehicleId} to reservation {ReservationId}",
            vehicleId, reservationId);

        return MapToDto(reservation);
    }

    public async Task<ReservationDto?> UnassignVehicleAsync(
        Guid reservationId,
        CancellationToken cancellationToken = default)
    {
        var reservation = await _reservationRepository.GetByIdAsync(reservationId, cancellationToken);
        if (reservation == null)
        {
            return null;
        }

        reservation.VehicleId = Guid.Empty;
        reservation.UpdatedAt = DateTime.UtcNow;
        await SaveChangesWithConcurrencyHandlingAsync(cancellationToken);

        _logger.LogInformation("Unassigned vehicle from reservation {ReservationId}", reservationId);

        return MapToDto(reservation);
    }

    public async Task<ReservationDto?> AdminCancelReservationAsync(
        Guid reservationId,
        string? reason = null,
        CancellationToken cancellationToken = default)
    {
        var reservation = await _reservationRepository.GetByIdAsync(reservationId, cancellationToken);
        if (reservation == null)
        {
            return null;
        }

        // Admin can cancel in more statuses than regular users
        if (reservation.Status == ReservationStatus.Completed ||
            reservation.Status == ReservationStatus.Cancelled ||
            reservation.Status == ReservationStatus.Expired)
        {
            throw new InvalidOperationException($"Cannot cancel reservation in {reservation.Status} status");
        }

        reservation.Status = ReservationStatus.Cancelled;
        reservation.UnpaidRequestExpiresAtUtc = null;
        reservation.UpdatedAt = DateTime.UtcNow;

        await _holdService.ReleaseHoldAsync(reservationId, cancellationToken);
        await SaveChangesWithConcurrencyHandlingAsync(cancellationToken);
        await QueueReservationCancelledNotificationsAsync(reservation, cancellationToken);

        _logger.LogInformation(
            "Admin cancelled reservation {ReservationId}. HasReason: {HasReason}",
            reservationId, !string.IsNullOrWhiteSpace(reason));
        return MapToDto(reservation);
    }

    public async Task<ReservationDto?> ConfirmUnpaidRequestAsync(
        Guid reservationId,
        CancellationToken cancellationToken = default)
    {
        var reservation = await _reservationRepository.GetByIdAsync(reservationId, cancellationToken);
        if (reservation == null)
        {
            return null;
        }

        if (reservation.Status != ReservationStatus.UnpaidRequest)
        {
            throw new InvalidOperationException($"Cannot confirm reservation in {reservation.Status} status");
        }

        reservation.Status = ReservationStatus.Confirmed;
        reservation.UnpaidRequestExpiresAtUtc = null;
        reservation.UpdatedAt = DateTime.UtcNow;
        await SaveChangesWithConcurrencyHandlingAsync(cancellationToken);

        _logger.LogInformation("Confirmed unpaid reservation request {ReservationId}", reservationId);

        return MapToDto(reservation);
    }

    public Task<bool> IsValidStatusTransitionAsync(
        ReservationStatus currentStatus,
        ReservationStatus newStatus,
        CancellationToken cancellationToken = default)
    {
        var validTransitions = GetValidTransitions(currentStatus);
        return Task.FromResult(validTransitions.Contains(newStatus));
    }

    public Task<IReadOnlyList<ReservationStatus>> GetValidNextStatusesAsync(
        ReservationStatus currentStatus,
        CancellationToken cancellationToken = default)
    {
        var transitions = GetValidTransitions(currentStatus);
        return Task.FromResult<IReadOnlyList<ReservationStatus>>(transitions);
    }

    public async Task<bool> CanHoldBeExtendedAsync(
        Guid holdId,
        CancellationToken cancellationToken = default)
    {
        var reservation = await _reservationRepository.GetByIdAsync(holdId, cancellationToken);
        if (reservation == null)
        {
            return false;
        }

        return reservation.Status == ReservationStatus.Hold;
    }

    public async Task<IReadOnlyList<ReservationDto>> GetCustomerReservationsAsync(
        Guid customerId,
        CancellationToken cancellationToken = default)
    {
        var reservations = await _reservationRepository.GetByCustomerIdAsync(customerId, cancellationToken);
        return reservations.Select(MapToDto).ToList();
    }

    public async Task<PaginatedResponse<ReservationDto>> GetCustomerReservationsPaginatedAsync(
        Guid customerId,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        const int maxPageSize = 100;
        pageSize = Math.Clamp(pageSize, 1, maxPageSize);
        page = Math.Max(1, page);

        var (items, totalCount) = await _reservationRepository.GetByCustomerIdPaginatedAsync(
            customerId, page, pageSize, cancellationToken);

        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

        return new PaginatedResponse<ReservationDto>
        {
            Items = items.Select(MapToDto).ToList(),
            TotalCount = totalCount,
            TotalPages = totalPages,
            CurrentPage = page,
            PageSize = pageSize
        };
    }

    #region Helper Methods

    private async Task<ReservationPricingContext> ResolveReservationPricingAsync(
        CreateReservationRequest request,
        Guid returnOfficeId,
        CancellationToken cancellationToken)
    {
        if (request.QuoteId.HasValue)
        {
            if (_quoteStore is null || _extraPricingService is null)
            {
                throw new InvalidOperationException("Reservation quote services are unavailable.");
            }
            if (string.IsNullOrWhiteSpace(request.SessionId) || string.IsNullOrWhiteSpace(request.IdempotencyKey))
            {
                throw new InvalidOperationException("X-Session-Id and Idempotency-Key headers are required for quoted reservations.");
            }

            var quote = await _quoteStore.GetAsync(request.QuoteId.Value, cancellationToken)
                ?? throw new ReservationQuoteConflictException("Reservation quote is missing or expired.");
            ValidateQuoteMatchesRequest(quote, request, returnOfficeId);
            if (quote.ExpiresAtUtc <= DateTime.UtcNow)
            {
                throw new ReservationQuoteConflictException("Reservation quote has expired.");
            }
            if (!ReservationQuoteSecurity.SessionHashMatches(quote.SessionHash, request.SessionId))
            {
                _logger.LogWarning("Reservation quote {QuoteId} rejected for session mismatch", quote.QuoteId);
                throw new ReservationQuoteConflictException("Reservation quote does not belong to this session.");
            }

            var claimOwner = ReservationQuoteSecurity.HashSessionId($"idempotency:{request.IdempotencyKey}");
            if (!await _quoteStore.TryClaimAsync(quote.QuoteId, claimOwner, _quoteClaimTtl, cancellationToken))
            {
                _logger.LogWarning("Reservation quote {QuoteId} replay or concurrent claim rejected", quote.QuoteId);
                throw new ReservationQuoteConflictException("Reservation quote is already being used or was consumed.");
            }

            try
            {
                await _extraPricingService.ValidateCurrentAvailabilityAsync(
                    quote.VehicleGroupId,
                    quote.SelectedExtras,
                    cancellationToken);
            }
            catch
            {
                await SafeReleaseQuoteClaimAsync(quote.QuoteId, claimOwner, cancellationToken);
                throw;
            }

            return new ReservationPricingContext(
                BuildSnapshotPriceBreakdown(quote.PricingSnapshot, quote.SelectedExtras),
                quote.PricingSnapshot,
                quote.SelectedExtras,
                quote.QuoteId,
                claimOwner);
        }

        var basePricing = await _pricingService.CalculateBreakdownAsync(
            request.VehicleGroupId,
            request.PickupOfficeId,
            returnOfficeId,
            request.PickupDateTimeUtc,
            request.ReturnDateTimeUtc,
            request.CampaignCode,
            _extraPricingService is null ? request.ExtraDriverCount : 0,
            _extraPricingService is null ? request.ChildSeatCount : 0,
            request.DriverAge,
            request.FullCoverageWaiver,
            cancellationToken)
            ?? throw new InvalidOperationException("Could not calculate pricing for the reservation");

        if (_extraPricingService is null)
        {
            return new ReservationPricingContext(basePricing, null, [], null, null);
        }

        var quotedExtras = await _extraPricingService.CalculateLegacyAsync(
            request.VehicleGroupId,
            request.Locale,
            basePricing.RentalDays,
            request.ExtraDriverCount,
            request.ChildSeatCount,
            cancellationToken);
        var extraTotal = RoundAmount(quotedExtras.Sum(item => item.Total));
        var pricing = basePricing with
        {
            ExtrasTotal = extraTotal,
            FinalTotal = RoundAmount(basePricing.FinalTotal + extraTotal),
            ExtraItems = quotedExtras.Select(ToExtraLineItemDto).ToArray()
        };
        var now = DateTime.UtcNow;
        var snapshot = CreatePricingSnapshot(Guid.Empty, now, now, pricing, quotedExtras);

        if (quotedExtras.Count > 0)
        {
            _logger.LogInformation(
                "Legacy reservation extras adapted for vehicle group {VehicleGroupId} with {ExtraCount} selections",
                request.VehicleGroupId,
                quotedExtras.Count);
        }

        return new ReservationPricingContext(pricing, snapshot, quotedExtras, null, null);
    }

    private async Task<Reservation?> FindReservationByQuoteIdAsync(
        Guid? quoteId,
        CancellationToken cancellationToken)
    {
        if (!quoteId.HasValue)
        {
            return null;
        }

        return await _applicationDbContext.Reservations
            .Include(reservation => reservation.Customer)
            .Include(reservation => reservation.Vehicle)
                .ThenInclude(vehicle => vehicle!.Group)
            .Include(reservation => reservation.PickupOffice)
            .Include(reservation => reservation.ReturnOffice)
            .Include(reservation => reservation.SelectedExtras)
            .FirstOrDefaultAsync(reservation => reservation.QuoteId == quoteId.Value, cancellationToken);
    }

    private async Task<Reservation?> ResolveExistingQuoteReservationAsync(
        CreateReservationRequest request,
        CancellationToken cancellationToken)
    {
        var reservation = await FindReservationByQuoteIdAsync(request.QuoteId, cancellationToken);
        if (reservation is null)
        {
            return null;
        }
        if (_quoteStore is null || !request.QuoteId.HasValue || string.IsNullOrWhiteSpace(request.SessionId))
        {
            throw new ReservationQuoteConflictException("Reservation quote retry cannot be verified.");
        }

        var quote = await _quoteStore.GetAsync(request.QuoteId.Value, cancellationToken)
            ?? throw new ReservationQuoteConflictException("Reservation quote retry is expired or unavailable.");
        var returnOfficeId = request.ReturnOfficeId == Guid.Empty ? request.PickupOfficeId : request.ReturnOfficeId;
        ValidateQuoteMatchesRequest(quote, request, returnOfficeId);
        if (!ReservationQuoteSecurity.SessionHashMatches(quote.SessionHash, request.SessionId))
        {
            _logger.LogWarning("Reservation quote retry {QuoteId} rejected for session mismatch", quote.QuoteId);
            throw new ReservationQuoteConflictException("Reservation quote does not belong to this session.");
        }

        try
        {
            await _quoteStore.ReconcileConsumedAsync(quote.QuoteId, reservation.Id, cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "Reservation quote {QuoteId} replay resolved from database but Redis reconciliation failed",
                quote.QuoteId);
        }

        return reservation;
    }

    private static void ValidateQuoteAndLegacyCombination(CreateReservationRequest request)
    {
        if (request.QuoteId.HasValue && (request.ExtraDriverCount != 0 || request.ChildSeatCount != 0))
        {
            throw new InvalidOperationException("Legacy extra quantities cannot be combined with QuoteId.");
        }
    }

    private static void ValidateQuoteMatchesRequest(
        ReservationQuoteV1 quote,
        CreateReservationRequest request,
        Guid returnOfficeId)
    {
        var campaignCode = string.IsNullOrWhiteSpace(request.CampaignCode)
            ? null
            : request.CampaignCode.Trim().ToUpperInvariant();
        if (quote.VehicleGroupId != request.VehicleGroupId ||
            quote.PickupOfficeId != request.PickupOfficeId ||
            quote.ReturnOfficeId != returnOfficeId ||
            NormalizeUtc(quote.PickupDateTimeUtc) != NormalizeUtc(request.PickupDateTimeUtc) ||
            NormalizeUtc(quote.ReturnDateTimeUtc) != NormalizeUtc(request.ReturnDateTimeUtc) ||
            !string.Equals(quote.CampaignCode, campaignCode, StringComparison.Ordinal) ||
            quote.DriverAge != request.DriverAge ||
            quote.FullCoverageWaiver != request.FullCoverageWaiver ||
            !string.Equals(quote.Locale, NormalizeLocale(request.Locale), StringComparison.Ordinal))
        {
            throw new ReservationQuoteConflictException("Reservation inputs no longer match the issued quote.");
        }
    }

    private static ReservationPricingSnapshotV1 CreatePricingSnapshot(
        Guid quoteId,
        DateTime issuedAtUtc,
        DateTime expiresAtUtc,
        PriceBreakdownDto pricing,
        IReadOnlyList<ReservationQuotedExtraV1> quotedExtras) => new()
        {
            SchemaVersion = 1,
            DailyRate = pricing.DailyRate,
            RentalDays = pricing.RentalDays,
            BaseTotal = pricing.BaseTotal,
            AirportFee = pricing.AirportFee,
            OneWayFee = pricing.OneWayFee,
            YoungDriverFee = pricing.YoungDriverFee,
            CoverageWaiverFee = pricing.FullCoverageWaiverFee,
            OtherFees = pricing.ExtraDriverFee + pricing.ChildSeatFee,
            CampaignId = pricing.AppliedCampaignId,
            CampaignCode = pricing.AppliedCampaignCode,
            DiscountType = pricing.AppliedCampaignDiscountType,
            DiscountValue = pricing.AppliedCampaignDiscountValue,
            DiscountTotal = pricing.CampaignDiscount,
            ExtraItems = quotedExtras.Select(item => new ReservationPricingExtraSnapshot
            {
                ExtraOptionId = item.ExtraOptionId,
                OptionVersion = item.OptionVersion,
                Code = item.Code,
                Name = item.Name,
                UnitPrice = item.UnitPrice,
                PricingMode = item.PricingMode,
                Quantity = item.Quantity,
                RentalDays = item.RentalDays,
                Total = item.Total
            }).ToList(),
            ExtrasTotal = pricing.ExtrasTotal,
            DepositAmount = pricing.DepositAmount,
            PreAuthorizationAmount = pricing.PreAuthorizationAmount,
            Currency = pricing.Currency,
            FinalTotal = pricing.FinalTotal,
            QuoteId = quoteId,
            IssuedAtUtc = issuedAtUtc,
            ExpiresAtUtc = expiresAtUtc
        };

    private static PriceBreakdownDto BuildSnapshotPriceBreakdown(
        ReservationPricingSnapshotV1 snapshot,
        IReadOnlyList<ReservationQuotedExtraV1> quotedExtras) => new(
            DailyRate: snapshot.DailyRate,
            RentalDays: snapshot.RentalDays,
            BaseTotal: snapshot.BaseTotal,
            ExtrasTotal: snapshot.ExtrasTotal,
            CampaignDiscount: snapshot.DiscountTotal,
            AirportFee: snapshot.AirportFee,
            OneWayFee: snapshot.OneWayFee,
            ExtraDriverFee: 0m,
            ChildSeatFee: 0m,
            YoungDriverFee: snapshot.YoungDriverFee,
            FullCoverageWaiverFee: snapshot.CoverageWaiverFee,
            FinalTotal: snapshot.FinalTotal,
            DepositAmount: snapshot.DepositAmount,
            PreAuthorizationAmount: snapshot.PreAuthorizationAmount,
            Currency: snapshot.Currency,
            AppliedCampaignCode: snapshot.CampaignCode)
        {
            AppliedCampaignId = snapshot.CampaignId,
            AppliedCampaignDiscountType = snapshot.DiscountType,
            AppliedCampaignDiscountValue = snapshot.DiscountValue,
            ExtraItems = quotedExtras.Select(ToExtraLineItemDto).ToArray()
        };

    private static void AddSelectedExtraSnapshots(
        Reservation reservation,
        IReadOnlyList<ReservationQuotedExtraV1> quotedExtras)
    {
        foreach (var item in quotedExtras)
        {
            reservation.SelectedExtras.Add(new ReservationSelectedExtra
            {
                ReservationId = reservation.Id,
                ExtraOptionId = item.ExtraOptionId,
                OptionVersionSnapshot = item.OptionVersion,
                Locale = item.Locale,
                OptionCodeSnapshot = item.Code,
                NameSnapshot = item.Name,
                DescriptionSnapshot = item.Description,
                UnitPriceSnapshot = item.UnitPrice,
                PricingModeSnapshot = item.PricingMode == "PER_DAY"
                    ? ReservationExtraPricingMode.PerDay
                    : ReservationExtraPricingMode.PerRental,
                Quantity = item.Quantity,
                RentalDaysSnapshot = item.RentalDays,
                TotalPriceSnapshot = item.Total,
                Currency = "TRY"
            });
        }
    }

    private async Task ReleaseQuoteClaimAsync(
        ReservationPricingContext pricingContext,
        CancellationToken cancellationToken)
    {
        if (pricingContext.QuoteId.HasValue && !string.IsNullOrWhiteSpace(pricingContext.ClaimOwner))
        {
            await SafeReleaseQuoteClaimAsync(pricingContext.QuoteId.Value, pricingContext.ClaimOwner, cancellationToken);
        }
    }

    private async Task SafeReleaseQuoteClaimAsync(
        Guid quoteId,
        string claimOwner,
        CancellationToken cancellationToken)
    {
        try
        {
            if (_quoteStore is not null)
            {
                await _quoteStore.ReleaseClaimAsync(quoteId, claimOwner, cancellationToken);
            }
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Failed to release reservation quote claim {QuoteId}", quoteId);
        }
    }

    private async Task FinalizeQuoteAsync(
        ReservationPricingContext pricingContext,
        Guid reservationId,
        CancellationToken cancellationToken)
    {
        if (!pricingContext.QuoteId.HasValue || string.IsNullOrWhiteSpace(pricingContext.ClaimOwner) || _quoteStore is null)
        {
            return;
        }

        try
        {
            var finalized = await _quoteStore.MarkConsumedAsync(
                pricingContext.QuoteId.Value,
                pricingContext.ClaimOwner,
                reservationId,
                cancellationToken);
            if (!finalized)
            {
                _logger.LogWarning(
                    "Reservation quote {QuoteId} committed as reservation {ReservationId} but Redis finalization did not acquire ownership",
                    pricingContext.QuoteId.Value,
                    reservationId);
            }
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "Reservation quote {QuoteId} committed as reservation {ReservationId} but Redis finalization failed",
                pricingContext.QuoteId.Value,
                reservationId);
        }
    }

    private static ReservationExtraLineItemDto ToExtraLineItemDto(ReservationQuotedExtraV1 item) => new(
        item.ExtraOptionId,
        item.OptionVersion,
        item.Code,
        item.Name,
        item.Description,
        item.UnitPrice,
        item.PricingMode,
        item.Quantity,
        item.RentalDays,
        item.Total);

    private static DateTime NormalizeUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };

    private static string NormalizeLocale(string locale) =>
        string.IsNullOrWhiteSpace(locale) ? "tr" : locale.Trim().ToLowerInvariant();

    private static decimal RoundAmount(decimal amount) =>
        Math.Round(amount, 2, MidpointRounding.AwayFromZero);

    private sealed record ReservationPricingContext(
        PriceBreakdownDto Pricing,
        ReservationPricingSnapshotV1? Snapshot,
        IReadOnlyList<ReservationQuotedExtraV1> QuotedExtras,
        Guid? QuoteId,
        string? ClaimOwner);

    private ReservationDto MapToDto(Reservation reservation)
    {
        var rentalDays = (int)Math.Ceiling((reservation.ReturnDateTime - reservation.PickupDateTime).TotalDays);
        rentalDays = Math.Max(1, rentalDays);

        var isDraftWithoutAssignedVehicle =
            reservation.Status == ReservationStatus.Draft && reservation.Vehicle == null;

        var vehicleId = isDraftWithoutAssignedVehicle ? Guid.Empty : reservation.VehicleId;
        var vehicleGroupId = reservation.Vehicle?.GroupId
            ?? (isDraftWithoutAssignedVehicle ? reservation.VehicleId : Guid.Empty);
        var customerStats = GetCustomerStats(reservation.CustomerId);

        return new ReservationDto
        {
            Id = reservation.Id,
            PublicCode = reservation.PublicCode,
            CustomerId = reservation.CustomerId,
            CustomerName = reservation.Customer?.FullName ?? string.Empty,
            CustomerEmail = IsInternalManualEmail(reservation.Customer?.Email) ? string.Empty : reservation.Customer?.Email ?? string.Empty,
            CustomerPhone = reservation.Customer?.Phone ?? string.Empty,
            VehicleId = vehicleId,
            VehiclePlate = reservation.Vehicle?.Plate,
            VehicleBrand = reservation.Vehicle?.Brand ?? string.Empty,
            VehicleModel = reservation.Vehicle?.Model ?? string.Empty,
            VehicleGroupId = vehicleGroupId,
            VehicleGroupName = reservation.Vehicle?.Group?.NameTr ?? string.Empty,
            PickupOfficeId = reservation.PickupOfficeId,
            PickupOfficeName = reservation.PickupOffice?.Name ?? reservation.Vehicle?.Office?.Name ?? string.Empty,
            ReturnOfficeId = reservation.ReturnOfficeId,
            ReturnOfficeName = reservation.ReturnOffice?.Name ?? reservation.Vehicle?.Office?.Name ?? string.Empty,
            PickupDateTime = reservation.PickupDateTime,
            ReturnDateTime = reservation.ReturnDateTime,
            Status = reservation.Status.ToString(),
            TotalAmount = reservation.TotalAmount,
            DepositAmount = reservation.PricingSnapshot?.DepositAmount ?? reservation.Vehicle?.Group?.DepositAmount ?? 0,
            RentalDays = rentalDays,
            CustomerReservationCount = customerStats.ReservationCount,
            CustomerTotalSpent = customerStats.TotalSpent,
            Driver = BuildDriverDto(reservation),
            PriceBreakdown = BuildReservationPriceBreakdown(reservation, rentalDays),
            SelectedExtras = reservation.SelectedExtras.Select(MapSelectedExtraToDto).ToArray(),
            BreakdownSource = reservation.PricingSnapshot is null ? "LEGACY_TOTAL_ONLY" : "SNAPSHOT",
            CampaignCode = reservation.PricingSnapshot?.CampaignCode,
            DiscountAmount = reservation.PricingSnapshot?.DiscountTotal ?? 0m,
            CreatedAt = reservation.CreatedAt,
            UpdatedAt = reservation.UpdatedAt,
            UnpaidRequestExpiresAtUtc = reservation.UnpaidRequestExpiresAtUtc,
            Notes = reservation.Notes
        };
    }

    private (int ReservationCount, decimal TotalSpent) GetCustomerStats(Guid customerId)
    {
        var query = _applicationDbContext.Reservations
            .AsNoTracking()
            .Where(r => r.CustomerId == customerId);

        var reservationCount = query.Count(r =>
            r.Status != ReservationStatus.Cancelled &&
            r.Status != ReservationStatus.Expired);

        var totalSpent = query
            .Where(r => r.Status == ReservationStatus.Confirmed ||
                        r.Status == ReservationStatus.Active ||
                        r.Status == ReservationStatus.Completed)
            .Sum(r => (decimal?)r.TotalAmount) ?? 0m;

        return (reservationCount, totalSpent);
    }

    private static ReservationDriverDto? BuildDriverDto(Reservation reservation)
    {
        if (string.IsNullOrWhiteSpace(reservation.DriverLicenseNumber))
        {
            return null;
        }

        return new ReservationDriverDto
        {
            FirstName = reservation.DriverFirstName ?? string.Empty,
            LastName = reservation.DriverLastName ?? string.Empty,
            DateOfBirth = reservation.DriverDateOfBirth,
            LicenseNumber = reservation.DriverLicenseNumber,
            LicenseCountry = reservation.DriverLicenseCountry ?? string.Empty,
            LicenseIssueDate = reservation.DriverLicenseIssueDate,
            LicenseExpiryDate = reservation.DriverLicenseExpiryDate
        };
    }

    private static PriceBreakdownDto BuildReservationPriceBreakdown(Reservation reservation, int rentalDays)
    {
        if (reservation.PricingSnapshot is { } snapshot)
        {
            var selectedExtrasById = reservation.SelectedExtras.ToDictionary(item => item.ExtraOptionId);
            return new PriceBreakdownDto(
                DailyRate: snapshot.DailyRate,
                RentalDays: snapshot.RentalDays,
                BaseTotal: snapshot.BaseTotal,
                ExtrasTotal: snapshot.ExtrasTotal,
                CampaignDiscount: snapshot.DiscountTotal,
                AirportFee: snapshot.AirportFee,
                OneWayFee: snapshot.OneWayFee,
                ExtraDriverFee: 0m,
                ChildSeatFee: 0m,
                YoungDriverFee: snapshot.YoungDriverFee,
                FullCoverageWaiverFee: snapshot.CoverageWaiverFee,
                FinalTotal: snapshot.FinalTotal,
                DepositAmount: snapshot.DepositAmount,
                PreAuthorizationAmount: snapshot.PreAuthorizationAmount,
                Currency: snapshot.Currency,
                AppliedCampaignCode: snapshot.CampaignCode)
            {
                AppliedCampaignId = snapshot.CampaignId,
                AppliedCampaignDiscountType = snapshot.DiscountType,
                AppliedCampaignDiscountValue = snapshot.DiscountValue,
                ExtraItems = snapshot.ExtraItems.Select(item =>
                {
                    selectedExtrasById.TryGetValue(item.ExtraOptionId, out var selectedExtra);
                    return new ReservationExtraLineItemDto(
                        item.ExtraOptionId,
                        item.OptionVersion,
                        item.Code,
                        item.Name,
                        selectedExtra?.DescriptionSnapshot ?? string.Empty,
                        item.UnitPrice,
                        item.PricingMode,
                        item.Quantity,
                        item.RentalDays,
                        item.Total);
                }).ToArray()
            };
        }

        var depositAmount = reservation.Vehicle?.Group?.DepositAmount ?? 0m;
        var dailyRate = rentalDays > 0 ? Math.Round(reservation.TotalAmount / rentalDays, 2) : reservation.TotalAmount;

        return new PriceBreakdownDto(
            DailyRate: dailyRate,
            RentalDays: rentalDays,
            BaseTotal: reservation.TotalAmount,
            ExtrasTotal: 0m,
            CampaignDiscount: 0m,
            AirportFee: 0m,
            OneWayFee: 0m,
            ExtraDriverFee: 0m,
            ChildSeatFee: 0m,
            YoungDriverFee: 0m,
            FullCoverageWaiverFee: 0m,
            FinalTotal: reservation.TotalAmount,
            DepositAmount: depositAmount,
            PreAuthorizationAmount: depositAmount,
            Currency: "TRY",
            AppliedCampaignCode: null);
    }

    private static ReservationSelectedExtraDto MapSelectedExtraToDto(ReservationSelectedExtra item) => new()
    {
        OptionId = item.ExtraOptionId,
        OptionVersion = item.OptionVersionSnapshot,
        Code = item.OptionCodeSnapshot,
        Locale = item.Locale,
        Name = item.NameSnapshot,
        Description = item.DescriptionSnapshot,
        UnitPrice = item.UnitPriceSnapshot,
        PricingMode = item.PricingModeSnapshot == ReservationExtraPricingMode.PerDay ? "PER_DAY" : "PER_RENTAL",
        Quantity = item.Quantity,
        RentalDays = item.RentalDaysSnapshot,
        Total = item.TotalPriceSnapshot,
        Currency = item.Currency
    };

    private static void ApplyDriverSnapshot(
        Reservation reservation,
        CustomerInfoRequest? customer,
        DriverInfoRequest? driver)
    {
        reservation.DriverFirstName = ValueOrNull(driver?.FirstName) ?? customer?.FirstName ?? reservation.DriverFirstName;
        reservation.DriverLastName = ValueOrNull(driver?.LastName) ?? customer?.LastName ?? reservation.DriverLastName;
        reservation.DriverDateOfBirth = NormalizeUtc(
            driver?.DateOfBirth ?? customer?.DateOfBirth ?? reservation.DriverDateOfBirth);
        reservation.DriverLicenseNumber = ValueOrNull(driver?.LicenseNumber) ?? customer?.DriverLicenseNumber ?? reservation.DriverLicenseNumber;
        reservation.DriverLicenseCountry = ValueOrNull(driver?.LicenseCountry) ?? reservation.DriverLicenseCountry;
        reservation.DriverLicenseIssueDate = NormalizeUtc(
            driver?.LicenseIssueDate ?? customer?.DriverLicenseIssueDate ?? reservation.DriverLicenseIssueDate);
        reservation.DriverLicenseExpiryDate = NormalizeUtc(
            driver?.LicenseExpiryDate ?? reservation.DriverLicenseExpiryDate);
    }

    private static DateTime? NormalizeUtc(DateTime? value)
    {
        if (!value.HasValue)
        {
            return null;
        }

        return value.Value.Kind switch
        {
            DateTimeKind.Utc => value.Value,
            DateTimeKind.Local => value.Value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value.Value, DateTimeKind.Utc)
        };
    }

    private async Task ApplyCustomerUpdateAsync(
        Reservation reservation,
        CustomerInfoRequest customerRequest,
        CancellationToken cancellationToken)
    {
        var customer = reservation.Customer ?? await _customerRepository.GetByIdAsync(reservation.CustomerId, cancellationToken);
        if (customer == null)
        {
            return;
        }

        var fullName = $"{customerRequest.FirstName} {customerRequest.LastName}".Trim();
        if (!string.IsNullOrWhiteSpace(fullName))
        {
            customer.FullName = fullName;
        }

        if (!string.IsNullOrWhiteSpace(customerRequest.Email))
        {
            customer.Email = customerRequest.Email;
        }

        if (!string.IsNullOrWhiteSpace(customerRequest.Phone))
        {
            customer.Phone = customerRequest.Phone;
        }

        if (customerRequest.DateOfBirth.HasValue)
        {
            customer.BirthDate = DateOnly.FromDateTime(customerRequest.DateOfBirth.Value);
        }

        if (!string.IsNullOrWhiteSpace(customerRequest.IdentityNumber))
        {
            customer.IdentityNumber = customerRequest.IdentityNumber;
        }

        if (customerRequest.DriverLicenseIssueDate.HasValue)
        {
            customer.LicenseYear = customerRequest.DriverLicenseIssueDate.Value.Year;
        }
    }

    private static string? ValueOrNull(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private async Task<Customer> GetOrCreateCustomerAsync(
        CreateReservationRequest request,
        CancellationToken cancellationToken)
    {
        // Try to find an existing customer using normalized email to avoid case-drift duplicates
        var normalizedEmail = Customer.NormalizeEmail(request.Customer.Email);
        var existingCustomer = await _customerRepository
            .GetQueryable()
            .FirstOrDefaultAsync(c => c.NormalizedEmail == normalizedEmail, cancellationToken);

        if (existingCustomer != null)
        {
            return existingCustomer;
        }

        // Create new customer with combined full name
        var fullName = $"{request.Customer.FirstName} {request.Customer.LastName}".Trim();
        var customer = new Customer
        {
            FullName = fullName,
            Email = request.Customer.Email,
            Phone = request.Customer.Phone,
            BirthDate = request.Customer.DateOfBirth.HasValue
                ? DateOnly.FromDateTime(request.Customer.DateOfBirth.Value)
                : null,
            IdentityNumber = request.Customer.IdentityNumber ?? string.Empty,
            LicenseYear = request.Customer.DriverLicenseIssueDate.HasValue
                ? request.Customer.DriverLicenseIssueDate.Value.Year
                : 0,
            Nationality = "TR" // Default nationality, can be updated later
        };

        await _customerRepository.AddAsync(customer, cancellationToken);

        return customer;
    }

    private async Task<Customer> GetOrCreateManualCustomerAsync(
        AdminManualReservationRequest request,
        CancellationToken cancellationToken)
    {
        var email = string.IsNullOrWhiteSpace(request.CustomerEmail)
            ? BuildInternalManualEmail(request.CustomerPhone)
            : request.CustomerEmail.Trim();
        var normalizedEmail = Customer.NormalizeEmail(email);

        var existingCustomer = await _customerRepository
            .GetQueryable()
            .FirstOrDefaultAsync(c => c.NormalizedEmail == normalizedEmail, cancellationToken);

        if (existingCustomer != null)
        {
            return existingCustomer;
        }

        var customer = new Customer
        {
            FullName = $"{request.CustomerFirstName} {request.CustomerLastName}".Trim(),
            Email = email,
            Phone = request.CustomerPhone,
            IdentityNumber = string.Empty,
            LicenseYear = 0,
            Nationality = "TR"
        };

        await _customerRepository.AddAsync(customer, cancellationToken);
        return customer;
    }

    private static string BuildInternalManualEmail(string phone)
    {
        var normalizedPhone = NormalizePhoneForHash(phone);
        if (string.IsNullOrWhiteSpace(normalizedPhone))
        {
            normalizedPhone = Guid.NewGuid().ToString("N");
        }

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(normalizedPhone));
        var token = Convert.ToHexString(hash)[..16].ToLowerInvariant();
        return $"manual-{token}@internal.rentacar.local";
    }

    private static string NormalizePhoneForHash(string phone)
    {
        if (string.IsNullOrWhiteSpace(phone))
        {
            return string.Empty;
        }

        var digits = phone.Where(char.IsDigit).ToArray();
        return new string(digits);
    }

    private static bool IsInternalManualEmail(string? email)
    {
        return !string.IsNullOrWhiteSpace(email)
            && email.StartsWith("manual-", StringComparison.OrdinalIgnoreCase)
            && email.EndsWith("@internal.rentacar.local", StringComparison.OrdinalIgnoreCase);
    }

    private async Task<Vehicle?> FindAvailableVehicleAsync(
        Guid vehicleGroupId,
        Guid pickupOfficeId,
        DateTime pickupDateTime,
        DateTime returnDateTime,
        string? sessionId,
        CancellationToken cancellationToken)
    {
        var vehicles = await GetOrderedCandidateVehiclesAsync(
            vehicleGroupId,
            pickupOfficeId,
            sessionId,
            cancellationToken);

        if (vehicles.Count == 0)
        {
            return null;
        }

        foreach (var vehicle in vehicles)
        {
            var hasOverlap = await _reservationRepository.HasOverlappingReservationsAsync(
                vehicle.Id,
                pickupDateTime,
                returnDateTime,
                null,
                cancellationToken);

            if (!hasOverlap)
            {
                return vehicle;
            }
        }

        return null;
    }

    private async Task<List<Vehicle>> GetOrderedCandidateVehiclesAsync(
        Guid vehicleGroupId,
        Guid pickupOfficeId,
        string? sessionId,
        CancellationToken cancellationToken)
    {
        var vehicleQuery = _vehicleRepository
            .GetQueryable()
            .Where(v =>
                v.GroupId == vehicleGroupId &&
                v.OfficeId == pickupOfficeId &&
                v.Status == VehicleStatus.Available);

        var vehicles = vehicleQuery.Provider is IAsyncQueryProvider
            ? await vehicleQuery.ToListAsync(cancellationToken)
            : vehicleQuery.ToList();

        if (vehicles.Count == 0)
        {
            return vehicles;
        }

        if (_allowLoadTestSessionPartition && !string.IsNullOrWhiteSpace(sessionId))
        {
            var startIndex = TryGetLoadTestVehicleStartIndex(sessionId, vehicles.Count, out var parsedStartIndex)
                ? parsedStartIndex
                : (sessionId.GetHashCode(StringComparison.Ordinal) & int.MaxValue) % vehicles.Count;
            return vehicles.Skip(startIndex).Concat(vehicles.Take(startIndex)).ToList();
        }

        return vehicles;
    }

    private static string GeneratePublicCode()
    {
        // Generate a readable public code like "ABC-1234-DEF"
        var chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        var random = new Random();
        var code = new char[12];

        for (int i = 0; i < 12; i++)
        {
            if (i == 3 || i == 8)
            {
                code[i] = '-';
            }
            else
            {
                code[i] = chars[random.Next(chars.Length)];
            }
        }

        return new string(code);
    }

    private static bool TryGetLoadTestVehicleStartIndex(string sessionId, int vehicleCount, out int startIndex)
    {
        startIndex = 0;

        var parts = sessionId.Split('-', 4, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 4 || !string.Equals(parts[0], "load", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!int.TryParse(parts[1], out var vuNumber) || vuNumber <= 0)
        {
            return false;
        }

        startIndex = (vuNumber - 1) % vehicleCount;
        return true;
    }

    private static bool CanModifyReservation(ReservationStatus status)
    {
        return status is ReservationStatus.UnpaidRequest or ReservationStatus.Confirmed;
    }

    private static bool CanCancelReservation(ReservationStatus status)
    {
        return status is ReservationStatus.Draft
            or ReservationStatus.Hold
            or ReservationStatus.UnpaidRequest
            or ReservationStatus.PendingPayment
            or ReservationStatus.Paid
            or ReservationStatus.Confirmed;
    }

    private async Task QueueReservationCancelledNotificationsAsync(
        Reservation reservation,
        CancellationToken cancellationToken)
    {
        var customer = reservation.Customer ?? await _customerRepository
            .GetQueryable()
            .FirstOrDefaultAsync(x => x.Id == reservation.CustomerId, cancellationToken);

        if (customer == null)
        {
            return;
        }

        var locale = ResolveNotificationLocale(customer.Nationality);
        var variables = new Dictionary<string, string>
        {
            ["PublicCode"] = reservation.PublicCode
        };

        if (!string.IsNullOrWhiteSpace(customer.Email))
        {
            await _notificationQueueService.EnqueueEmailAsync(
                new QueuedEmailNotificationRequest
                {
                    ToEmail = customer.Email,
                    TemplateKey = NotificationTemplateKeys.ReservationCancelled,
                    Locale = locale,
                    Variables = variables
                },
                cancellationToken: cancellationToken);
        }

        if (!string.IsNullOrWhiteSpace(customer.Phone))
        {
            await _notificationQueueService.EnqueueSmsAsync(
                new QueuedSmsNotificationRequest
                {
                    ToPhoneNumber = customer.Phone,
                    TemplateKey = NotificationTemplateKeys.ReservationCancelled,
                    Locale = locale,
                    Variables = variables
                },
                cancellationToken: cancellationToken);
        }
    }

    private async Task QueueReservationConfirmedNotificationsAsync(
        Reservation reservation,
        CancellationToken cancellationToken)
    {
        var customer = await ResolveCustomerAsync(reservation, cancellationToken);
        if (customer == null)
        {
            return;
        }

        var locale = ResolveNotificationLocale(customer.Nationality);
        var variables = new Dictionary<string, string>
        {
            ["PublicCode"] = reservation.PublicCode
        };

        if (!string.IsNullOrWhiteSpace(customer.Email))
        {
            await _notificationQueueService.EnqueueEmailAsync(
                new QueuedEmailNotificationRequest
                {
                    ToEmail = customer.Email,
                    TemplateKey = NotificationTemplateKeys.ReservationConfirmed,
                    Locale = locale,
                    Variables = variables
                },
                cancellationToken: cancellationToken);
        }

        if (!string.IsNullOrWhiteSpace(customer.Phone))
        {
            await _notificationQueueService.EnqueueSmsAsync(
                new QueuedSmsNotificationRequest
                {
                    ToPhoneNumber = customer.Phone,
                    TemplateKey = NotificationTemplateKeys.ReservationConfirmed,
                    Locale = locale,
                    Variables = variables
                },
                cancellationToken: cancellationToken);
        }
    }

    private async Task QueueReservationReminderNotificationsAsync(
        Reservation reservation,
        CancellationToken cancellationToken)
    {
        var customer = await ResolveCustomerAsync(reservation, cancellationToken);
        if (customer == null)
        {
            return;
        }

        var locale = ResolveNotificationLocale(customer.Nationality);
        var variables = new Dictionary<string, string>
        {
            ["PublicCode"] = reservation.PublicCode
        };

        await QueueReminderIfFutureAsync(
            customer,
            NotificationTemplateKeys.PickupReminder,
            reservation.PickupDateTime.AddHours(-24),
            locale,
            variables,
            cancellationToken);

        await QueueReminderIfFutureAsync(
            customer,
            NotificationTemplateKeys.ReturnReminder,
            reservation.ReturnDateTime.AddHours(-24),
            locale,
            variables,
            cancellationToken);
    }

    private async Task QueueReminderIfFutureAsync(
        Customer customer,
        string templateKey,
        DateTime scheduledAtUtc,
        string locale,
        IReadOnlyDictionary<string, string> variables,
        CancellationToken cancellationToken)
    {
        if (scheduledAtUtc <= DateTime.UtcNow)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(customer.Email))
        {
            await _notificationQueueService.EnqueueEmailAsync(
                new QueuedEmailNotificationRequest
                {
                    ToEmail = customer.Email,
                    TemplateKey = templateKey,
                    Locale = locale,
                    Variables = variables
                },
                scheduledAtUtc,
                cancellationToken);
        }

        if (!string.IsNullOrWhiteSpace(customer.Phone))
        {
            await _notificationQueueService.EnqueueSmsAsync(
                new QueuedSmsNotificationRequest
                {
                    ToPhoneNumber = customer.Phone,
                    TemplateKey = templateKey,
                    Locale = locale,
                    Variables = variables
                },
                scheduledAtUtc,
                cancellationToken);
        }
    }

    private static string ResolveNotificationLocale(string? nationality)
    {
        if (string.IsNullOrWhiteSpace(nationality))
        {
            return "tr-TR";
        }

        return nationality.Trim().ToUpperInvariant() switch
        {
            "TR" => "tr-TR",
            "EN" => "en-US",
            "GB" => "en-US",
            "US" => "en-US",
            "RU" => "ru-RU",
            "AR" => "ar-SA",
            "DE" => "de-DE",
            _ => "tr-TR"
        };
    }

    private async Task<Customer?> ResolveCustomerAsync(Reservation reservation, CancellationToken cancellationToken)
    {
        return reservation.Customer ?? await _customerRepository
            .GetQueryable()
            .FirstOrDefaultAsync(x => x.Id == reservation.CustomerId, cancellationToken);
    }

    private static ReservationStatus[] GetValidTransitions(ReservationStatus currentStatus)
    {
        return currentStatus switch
        {
            ReservationStatus.Draft => new[]
            {
                ReservationStatus.Hold,
                ReservationStatus.UnpaidRequest,
                ReservationStatus.Cancelled,
                ReservationStatus.Expired
            },
            ReservationStatus.Hold => new[]
            {
                ReservationStatus.PendingPayment,
                ReservationStatus.Cancelled,
                ReservationStatus.Expired
            },
            ReservationStatus.PendingPayment => new[]
            {
                ReservationStatus.Paid,
                ReservationStatus.Cancelled
            },
            ReservationStatus.UnpaidRequest => new[]
            {
                ReservationStatus.Confirmed,
                ReservationStatus.Cancelled,
                ReservationStatus.Expired
            },
            ReservationStatus.Paid => new[]
            {
                ReservationStatus.Active,
                ReservationStatus.Cancelled
            },
            ReservationStatus.Confirmed => new[]
            {
                ReservationStatus.Active,
                ReservationStatus.Cancelled
            },
            ReservationStatus.Active => new[]
            {
                ReservationStatus.Completed,
                ReservationStatus.Cancelled
            },
            ReservationStatus.Completed => Array.Empty<ReservationStatus>(),
            ReservationStatus.Cancelled => Array.Empty<ReservationStatus>(),
            ReservationStatus.Expired => Array.Empty<ReservationStatus>(),
            _ => Array.Empty<ReservationStatus>()
        };
    }

    private static string BuildAvailabilityCacheKey(
        Guid pickupOfficeId,
        Guid returnOfficeId,
        DateTime pickupDateTimeUtc,
        DateTime returnDateTimeUtc,
        Guid? vehicleGroupId,
        int page,
        int pageSize)
    {
        var groupPart = vehicleGroupId?.ToString("N") ?? "all";
        return $"availability:{pickupOfficeId:N}:{returnOfficeId:N}:{pickupDateTimeUtc:O}:{returnDateTimeUtc:O}:{groupPart}:{page}:{pageSize}";
    }

    private void InvalidateAvailabilityCache()
    {
        _availabilityCacheInvalidationSignal.Invalidate();
    }

    private async Task SaveChangesWithConcurrencyHandlingAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _applicationDbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            _logger.LogWarning(ex, "Concurrency conflict while updating reservation state");
            throw new InvalidOperationException("Reservation was updated by another request. Please retry.", ex);
        }
    }

    private static bool IsReservationOverlapViolation(DbUpdateException exception)
    {
        for (var current = exception.InnerException; current != null; current = current.InnerException)
        {
            if (current is PostgresException postgresException && postgresException.SqlState == "23P01")
            {
                return true;
            }
        }

        return false;
    }

    private async Task<IDbContextTransaction?> TryBeginTransactionAsync(CancellationToken cancellationToken)
    {
        if (_applicationDbContext is not DbContext dbContext || !dbContext.Database.IsRelational())
        {
            return null;
        }

        return await dbContext.Database.BeginTransactionAsync(cancellationToken);
    }

    #endregion
}
