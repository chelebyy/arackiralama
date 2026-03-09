using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Caching.Memory;
using RentACar.API.Contracts.Reservations;
using RentACar.Core.Entities;
using RentACar.Core.Enums;
using RentACar.Core.Interfaces;

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
    private readonly IMemoryCache _memoryCache;
    private readonly ILogger<ReservationService> _logger;
    private readonly TimeSpan _defaultHoldDuration = TimeSpan.FromMinutes(15);
    private readonly TimeSpan _maxHoldDuration = TimeSpan.FromMinutes(15);
    private readonly TimeSpan _availabilityCacheTtl = TimeSpan.FromMinutes(5);

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
        IMemoryCache memoryCache,
        ILogger<ReservationService> logger)
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
        _memoryCache = memoryCache;
        _logger = logger;
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

        _memoryCache.Set(cacheKey, result, new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = _availabilityCacheTtl
        });

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

        // Calculate pricing
        var returnOfficeId = request.ReturnOfficeId == Guid.Empty
            ? request.PickupOfficeId
            : request.ReturnOfficeId;
        var pricing = await _pricingService.CalculateBreakdownAsync(
            request.VehicleGroupId,
            request.PickupOfficeId,
            returnOfficeId,
            request.PickupDateTimeUtc,
            request.ReturnDateTimeUtc,
            request.CampaignCode,
            request.ExtraDriverCount,
            request.ChildSeatCount,
            request.DriverAge,
            request.FullCoverageWaiver,
            cancellationToken);

        if (pricing == null)
        {
            throw new InvalidOperationException("Could not calculate pricing for the reservation");
        }

        // Create reservation
        // VehicleId carries the selected vehicle group until a concrete vehicle is held/assigned.
        var reservation = new Reservation
        {
            PublicCode = GeneratePublicCode(),
            CustomerId = customer.Id,
            Customer = customer, // Set navigation property for mapping
            VehicleId = request.VehicleGroupId,
            PickupDateTime = request.PickupDateTimeUtc,
            ReturnDateTime = request.ReturnDateTimeUtc,
            Status = ReservationStatus.Draft,
            TotalAmount = pricing.FinalTotal
        };

        await _reservationRepository.AddAsync(reservation, cancellationToken);
        await _applicationDbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Created draft reservation {PublicCode} for customer {CustomerId}",
            reservation.PublicCode, customer.Id);

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

        // Only allow updates for certain statuses
        if (!CanModifyReservation(reservation.Status))
        {
            throw new InvalidOperationException($"Cannot update reservation in {reservation.Status} status");
        }

        // Update dates if provided
        if (request.PickupDateTimeUtc.HasValue)
        {
            reservation.PickupDateTime = request.PickupDateTimeUtc.Value;
        }

        if (request.ReturnDateTimeUtc.HasValue)
        {
            reservation.ReturnDateTime = request.ReturnDateTimeUtc.Value;
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
                "Reservation {ReservationId} has no selected vehicle group",
                reservationId);
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

        await using var transaction = await TryBeginTransactionAsync(cancellationToken);

        // Find an available vehicle in the selected group
        var vehicle = await FindAvailableVehicleAsync(
            reservation.VehicleId,
            reservation.PickupDateTime,
            reservation.ReturnDateTime,
            cancellationToken);

        if (vehicle == null)
        {
            if (transaction != null)
            {
                await transaction.RollbackAsync(cancellationToken);
            }
            _logger.LogWarning(
                "No available vehicle found for reservation {ReservationId}",
                reservationId);
            return null;
        }

        var hasOverlap = await _reservationRepository.HasOverlappingReservationsAsync(
            vehicle.Id,
            reservation.PickupDateTime,
            reservation.ReturnDateTime,
            reservationId,
            cancellationToken);

        if (hasOverlap)
        {
            if (transaction != null)
            {
                await transaction.RollbackAsync(cancellationToken);
            }
            _logger.LogWarning(
                "Overlap detected while creating hold for reservation {ReservationId} and vehicle {VehicleId}",
                reservationId,
                vehicle.Id);
            return null;
        }

        reservation.Status = ReservationStatus.Hold;
        reservation.VehicleId = vehicle.Id;
        reservation.UpdatedAt = DateTime.UtcNow;

        await SaveChangesWithConcurrencyHandlingAsync(cancellationToken);

        // Create the hold
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

        reservation.Status = newStatus;
        reservation.UpdatedAt = DateTime.UtcNow;
        await SaveChangesWithConcurrencyHandlingAsync(cancellationToken);

        _logger.LogInformation(
            "Reservation {ReservationId} transitioned from {OldStatus} to {NewStatus}",
            reservationId, reservation.Status, newStatus);

        return MapToDto(reservation);
    }

    public Task<ReservationDto?> ProcessPaymentAsync(
        Guid reservationId,
        PaymentInfoRequest paymentInfo,
        CancellationToken cancellationToken = default)
    {
        // Payment processing will be implemented in Faz 5
        _logger.LogInformation(
            "Payment processing requested for reservation {ReservationId}",
            reservationId);
        return Task.FromResult<ReservationDto?>(null);
    }

    public Task<ReservationDto?> ConfirmPaymentAsync(
        Guid reservationId,
        string transactionId,
        CancellationToken cancellationToken = default)
    {
        // Payment confirmation will be implemented in Faz 5
        _logger.LogInformation(
            "Payment confirmation requested for reservation {ReservationId}, tx: {TransactionId}",
            reservationId, transactionId);
        return Task.FromResult<ReservationDto?>(null);
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

        if (reservation.Status != ReservationStatus.Paid)
        {
            throw new InvalidOperationException($"Cannot check in reservation in {reservation.Status} status");
        }

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

        if (reservation.Status != ReservationStatus.Hold && reservation.Status != ReservationStatus.Draft)
        {
            _logger.LogWarning(
                "Cannot expire reservation {ReservationId} in status {Status}",
                reservationId, reservation.Status);
            return null;
        }

        reservation.Status = ReservationStatus.Expired;
        reservation.UpdatedAt = DateTime.UtcNow;

        await _holdService.ReleaseHoldAsync(reservationId, cancellationToken);
        await SaveChangesWithConcurrencyHandlingAsync(cancellationToken);

        _logger.LogInformation("Expired reservation {ReservationId}", reservationId);

        return MapToDto(reservation);
    }

    public async Task ProcessExpiredReservationsAsync(CancellationToken cancellationToken = default)
    {
        var expiredHolds = await _holdService.GetExpiredHoldsAsync(cancellationToken);

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

        _logger.LogInformation("Processed {Count} expired reservations", expiredHolds.Count);
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
        reservation.UpdatedAt = DateTime.UtcNow;

        await _holdService.ReleaseHoldAsync(reservationId, cancellationToken);
        await SaveChangesWithConcurrencyHandlingAsync(cancellationToken);

        _logger.LogInformation(
            "Admin cancelled reservation {ReservationId}. HasReason: {HasReason}",
            reservationId, !string.IsNullOrWhiteSpace(reason));
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

    #region Helper Methods

    private ReservationDto MapToDto(Reservation reservation)
    {
        var rentalDays = (int)Math.Ceiling((reservation.ReturnDateTime - reservation.PickupDateTime).TotalDays);
        rentalDays = Math.Max(1, rentalDays);

        var isDraftWithoutAssignedVehicle =
            reservation.Status == ReservationStatus.Draft && reservation.Vehicle == null;

        var vehicleId = isDraftWithoutAssignedVehicle ? Guid.Empty : reservation.VehicleId;
        var vehicleGroupId = reservation.Vehicle?.GroupId
            ?? (isDraftWithoutAssignedVehicle ? reservation.VehicleId : Guid.Empty);

        return new ReservationDto
        {
            Id = reservation.Id,
            PublicCode = reservation.PublicCode,
            CustomerId = reservation.CustomerId,
            CustomerName = reservation.Customer?.FullName ?? string.Empty,
            CustomerEmail = reservation.Customer?.Email ?? string.Empty,
            CustomerPhone = reservation.Customer?.Phone ?? string.Empty,
            VehicleId = vehicleId,
            VehiclePlate = reservation.Vehicle?.Plate,
            VehicleBrand = reservation.Vehicle?.Brand ?? string.Empty,
            VehicleModel = reservation.Vehicle?.Model ?? string.Empty,
            VehicleGroupId = vehicleGroupId,
            VehicleGroupName = reservation.Vehicle?.Group?.NameTr ?? string.Empty,
            PickupOfficeId = reservation.Vehicle?.OfficeId ?? Guid.Empty,
            PickupOfficeName = reservation.Vehicle?.Office?.Name ?? string.Empty,
            ReturnOfficeId = reservation.Vehicle?.OfficeId ?? Guid.Empty,
            ReturnOfficeName = reservation.Vehicle?.Office?.Name ?? string.Empty,
            PickupDateTime = reservation.PickupDateTime,
            ReturnDateTime = reservation.ReturnDateTime,
            Status = reservation.Status.ToString(),
            TotalAmount = reservation.TotalAmount,
            DepositAmount = 0, // Will be calculated from pricing
            RentalDays = rentalDays,
            CreatedAt = reservation.CreatedAt,
            UpdatedAt = reservation.UpdatedAt
        };
    }

    private async Task<Customer> GetOrCreateCustomerAsync(
        CreateReservationRequest request,
        CancellationToken cancellationToken)
    {
        // Try to find existing customer by email
        var existingCustomer = await _customerRepository
            .GetQueryable()
            .FirstOrDefaultAsync(c => c.Email == request.Customer.Email, cancellationToken);

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

    private async Task<Vehicle?> FindAvailableVehicleAsync(
        Guid vehicleGroupId,
        DateTime pickupDateTime,
        DateTime returnDateTime,
        CancellationToken cancellationToken)
    {
        // Get vehicles in the same group
        var vehicles = await _vehicleRepository
            .GetQueryable()
            .Where(v => v.GroupId == vehicleGroupId && v.Status == VehicleStatus.Available)
            .ToListAsync(cancellationToken);

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

    private static bool CanModifyReservation(ReservationStatus status)
    {
        return status is ReservationStatus.Draft or ReservationStatus.Hold;
    }

    private static bool CanCancelReservation(ReservationStatus status)
    {
        return status is ReservationStatus.Draft
            or ReservationStatus.Hold
            or ReservationStatus.PendingPayment
            or ReservationStatus.Paid;
    }

    private static ReservationStatus[] GetValidTransitions(ReservationStatus currentStatus)
    {
        return currentStatus switch
        {
            ReservationStatus.Draft => new[]
            {
                ReservationStatus.Hold,
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
            ReservationStatus.Paid => new[]
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

    private async Task<IDbContextTransaction?> TryBeginTransactionAsync(CancellationToken cancellationToken)
    {
        if (_applicationDbContext is not DbContext dbContext)
        {
            return null;
        }

        return await dbContext.Database.BeginTransactionAsync(cancellationToken);
    }

    #endregion
}
