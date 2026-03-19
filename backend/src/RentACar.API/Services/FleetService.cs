using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using RentACar.API.Contracts.Fleet;
using RentACar.Core.Entities;
using RentACar.Core.Enums;
using RentACar.Core.Interfaces;

namespace RentACar.API.Services;

public sealed class FleetService(
    IVehicleGroupRepository vehicleGroupRepository,
    IVehicleRepository vehicleRepository,
    IOfficeRepository officeRepository,
    IUnitOfWork unitOfWork,
    IVehiclePhotoStorage vehiclePhotoStorage,
    IApplicationDbContext dbContext,
    IHttpContextAccessor? httpContextAccessor = null) : IFleetService
{
    private static readonly ReservationStatus[] BlockingReservationStatuses =
    [
        ReservationStatus.Hold,
        ReservationStatus.PendingPayment,
        ReservationStatus.Paid,
        ReservationStatus.Active
    ];

    public async Task<IReadOnlyList<VehicleGroupDto>> GetVehicleGroupsAsync(CancellationToken cancellationToken = default)
    {
        var vehicleGroups = await vehicleGroupRepository.ListAsync(cancellationToken);
        return vehicleGroups.Select(MapToDto).ToList();
    }

    public async Task<VehicleGroupDto> CreateVehicleGroupAsync(CreateVehicleGroupRequest request, CancellationToken cancellationToken = default)
    {
        var vehicleGroup = new VehicleGroup
        {
            NameTr = request.NameTr.Trim(),
            NameEn = request.NameEn.Trim(),
            NameRu = request.NameRu.Trim(),
            NameAr = request.NameAr.Trim(),
            NameDe = request.NameDe.Trim(),
            DepositAmount = request.DepositAmount,
            MinAge = request.MinAge,
            MinLicenseYears = request.MinLicenseYears,
            Features = NormalizeFeatures(request.Features)
        };

        await vehicleGroupRepository.AddAsync(vehicleGroup, cancellationToken);
        WriteAuditLog(
            action: "VehicleGroupCreated",
            entityType: nameof(VehicleGroup),
            entityId: vehicleGroup.Id,
            details: new
            {
                vehicleGroup.NameTr,
                vehicleGroup.NameEn,
                vehicleGroup.DepositAmount,
                vehicleGroup.MinAge,
                vehicleGroup.MinLicenseYears,
                vehicleGroup.Features
            });
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return MapToDto(vehicleGroup);
    }

    public async Task<VehicleGroupDto?> UpdateVehicleGroupAsync(Guid id, UpdateVehicleGroupRequest request, CancellationToken cancellationToken = default)
    {
        var existingVehicleGroup = await vehicleGroupRepository.GetByIdAsync(id, cancellationToken);
        if (existingVehicleGroup is null)
        {
            return null;
        }

        var previousState = new
        {
            existingVehicleGroup.NameTr,
            existingVehicleGroup.NameEn,
            existingVehicleGroup.DepositAmount,
            existingVehicleGroup.MinAge,
            existingVehicleGroup.MinLicenseYears,
            Features = existingVehicleGroup.Features.ToList()
        };

        existingVehicleGroup.NameTr = request.NameTr.Trim();
        existingVehicleGroup.NameEn = request.NameEn.Trim();
        existingVehicleGroup.NameRu = request.NameRu.Trim();
        existingVehicleGroup.NameAr = request.NameAr.Trim();
        existingVehicleGroup.NameDe = request.NameDe.Trim();
        existingVehicleGroup.DepositAmount = request.DepositAmount;
        existingVehicleGroup.MinAge = request.MinAge;
        existingVehicleGroup.MinLicenseYears = request.MinLicenseYears;
        existingVehicleGroup.Features = NormalizeFeatures(request.Features);

        WriteAuditLog(
            action: "VehicleGroupUpdated",
            entityType: nameof(VehicleGroup),
            entityId: existingVehicleGroup.Id,
            details: new
            {
                Previous = previousState,
                Current = new
                {
                    existingVehicleGroup.NameTr,
                    existingVehicleGroup.NameEn,
                    existingVehicleGroup.DepositAmount,
                    existingVehicleGroup.MinAge,
                    existingVehicleGroup.MinLicenseYears,
                    existingVehicleGroup.Features
                }
            });
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return MapToDto(existingVehicleGroup);
    }

    public async Task<IReadOnlyList<VehicleDto>> GetVehiclesAsync(CancellationToken cancellationToken = default)
    {
        var vehicles = await vehicleRepository.ListAsync(cancellationToken);
        return vehicles.Select(MapToDto).ToList();
    }

    public async Task<IReadOnlyList<AvailableVehicleGroupDto>> SearchAvailableVehicleGroupsAsync(
        Guid officeId,
        DateTime pickupDateTimeUtc,
        DateTime returnDateTimeUtc,
        Guid? vehicleGroupId = null,
        CancellationToken cancellationToken = default)
    {
        if (pickupDateTimeUtc >= returnDateTimeUtc)
        {
            throw new ArgumentException("Alis tarihi, donus tarihinden once olmalidir.");
        }

        var blockedVehicleIds = await dbContext.Reservations
            .AsNoTracking()
            .Where(reservation =>
                BlockingReservationStatuses.Contains(reservation.Status) &&
                pickupDateTimeUtc < reservation.ReturnDateTime &&
                returnDateTimeUtc > reservation.PickupDateTime)
            .Select(reservation => reservation.VehicleId)
            .ToListAsync(cancellationToken);

        var availableVehicles = await (
                from vehicle in dbContext.Vehicles.AsNoTracking()
                join vehicleGroup in dbContext.VehicleGroups.AsNoTracking() on vehicle.GroupId equals vehicleGroup.Id
                where vehicle.OfficeId == officeId
                    && vehicle.Status == VehicleStatus.Available
                    && (!vehicleGroupId.HasValue || vehicle.GroupId == vehicleGroupId.Value)
                    && !blockedVehicleIds.Contains(vehicle.Id)
                select new { Vehicle = vehicle, VehicleGroup = vehicleGroup })
            .ToListAsync(cancellationToken);

        return availableVehicles
            .GroupBy(item => item.VehicleGroup.Id)
            .Select(grouping =>
            {
                var group = grouping.First().VehicleGroup;
                return new AvailableVehicleGroupDto(
                    GroupId: group.Id,
                    GroupName: group.NameTr,
                    GroupNameEn: group.NameEn,
                    AvailableCount: grouping.Count(),
                    DailyPrice: 0m,
                    Currency: "TRY",
                    DepositAmount: group.DepositAmount,
                    MinAge: group.MinAge,
                    MinLicenseYears: group.MinLicenseYears,
                    Features: group.Features,
                    ImageUrl: null);
            })
            .OrderBy(item => item.GroupName)
            .ToList();
    }

    public async Task<VehicleDto?> CreateVehicleAsync(CreateVehicleRequest request, CancellationToken cancellationToken = default)
    {
        var vehicle = new Vehicle
        {
            Plate = request.Plate.Trim().ToUpperInvariant(),
            Brand = request.Brand.Trim(),
            Model = request.Model.Trim(),
            Year = request.Year,
            Color = request.Color.Trim(),
            GroupId = request.GroupId,
            OfficeId = request.OfficeId,
            Status = request.Status
        };

        await vehicleRepository.AddAsync(vehicle, cancellationToken);
        WriteAuditLog(
            action: "VehicleCreated",
            entityType: nameof(Vehicle),
            entityId: vehicle.Id,
            details: new
            {
                vehicle.Plate,
                vehicle.Brand,
                vehicle.Model,
                vehicle.OfficeId,
                vehicle.GroupId,
                Status = vehicle.Status.ToString()
            });
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return MapToDto(vehicle);
    }

    public async Task<VehicleDto?> UpdateVehicleAsync(Guid id, UpdateVehicleRequest request, CancellationToken cancellationToken = default)
    {
        var existingVehicle = await vehicleRepository.GetByIdAsync(id, cancellationToken);
        if (existingVehicle is null)
        {
            return null;
        }

        var previousState = new
        {
            existingVehicle.Plate,
            existingVehicle.Brand,
            existingVehicle.Model,
            existingVehicle.Year,
            existingVehicle.Color,
            existingVehicle.GroupId,
            existingVehicle.OfficeId,
            Status = existingVehicle.Status.ToString()
        };

        existingVehicle.Plate = request.Plate.Trim().ToUpperInvariant();
        existingVehicle.Brand = request.Brand.Trim();
        existingVehicle.Model = request.Model.Trim();
        existingVehicle.Year = request.Year;
        existingVehicle.Color = request.Color.Trim();
        existingVehicle.GroupId = request.GroupId;
        existingVehicle.OfficeId = request.OfficeId;
        existingVehicle.Status = request.Status;

        WriteAuditLog(
            action: "VehicleUpdated",
            entityType: nameof(Vehicle),
            entityId: existingVehicle.Id,
            details: new
            {
                Previous = previousState,
                Current = new
                {
                    existingVehicle.Plate,
                    existingVehicle.Brand,
                    existingVehicle.Model,
                    existingVehicle.Year,
                    existingVehicle.Color,
                    existingVehicle.GroupId,
                    existingVehicle.OfficeId,
                    Status = existingVehicle.Status.ToString()
                }
            });
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return MapToDto(existingVehicle);
    }

    public async Task<bool> DeleteVehicleAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var existingVehicle = await vehicleRepository.GetByIdAsync(id, cancellationToken);
        if (existingVehicle is null)
        {
            return false;
        }

        await vehiclePhotoStorage.DeleteAsync(existingVehicle.PhotoUrl, cancellationToken);
        WriteAuditLog(
            action: "VehicleDeleted",
            entityType: nameof(Vehicle),
            entityId: existingVehicle.Id,
            details: new
            {
                existingVehicle.Plate,
                existingVehicle.Brand,
                existingVehicle.Model,
                existingVehicle.OfficeId,
                Status = existingVehicle.Status.ToString()
            });
        vehicleRepository.Remove(existingVehicle);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<VehicleDto?> UpdateVehicleStatusAsync(Guid id, VehicleStatus status, CancellationToken cancellationToken = default)
    {
        var existingVehicle = await vehicleRepository.GetByIdAsync(id, cancellationToken);
        if (existingVehicle is null)
        {
            return null;
        }

        var previousStatus = existingVehicle.Status;
        existingVehicle.Status = status;
        WriteAuditLog(
            action: "VehicleStatusUpdated",
            entityType: nameof(Vehicle),
            entityId: existingVehicle.Id,
            details: new
            {
                existingVehicle.Plate,
                PreviousStatus = previousStatus.ToString(),
                NewStatus = status.ToString()
            });
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return MapToDto(existingVehicle);
    }

    public async Task<VehicleDto?> TransferVehicleAsync(Guid id, Guid targetOfficeId, VehicleStatus? status, CancellationToken cancellationToken = default)
    {
        var existingVehicle = await vehicleRepository.GetByIdAsync(id, cancellationToken);
        if (existingVehicle is null)
        {
            return null;
        }

        var previousOfficeId = existingVehicle.OfficeId;
        var previousStatus = existingVehicle.Status;

        existingVehicle.OfficeId = targetOfficeId;
        if (status.HasValue)
        {
            existingVehicle.Status = status.Value;
        }

        WriteAuditLog(
            action: "VehicleTransferred",
            entityType: nameof(Vehicle),
            entityId: existingVehicle.Id,
            details: new
            {
                existingVehicle.Plate,
                PreviousOfficeId = previousOfficeId,
                NewOfficeId = targetOfficeId,
                PreviousStatus = previousStatus.ToString(),
                NewStatus = existingVehicle.Status.ToString()
            });
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return MapToDto(existingVehicle);
    }

    public async Task<VehicleDto?> ScheduleVehicleMaintenanceAsync(
        Guid id,
        DateTime startDateUtc,
        DateTime endDateUtc,
        string? notes,
        CancellationToken cancellationToken = default)
    {
        var existingVehicle = await vehicleRepository.GetByIdAsync(id, cancellationToken);
        if (existingVehicle is null)
        {
            return null;
        }

        if (startDateUtc >= endDateUtc || endDateUtc <= DateTime.UtcNow)
        {
            throw new ArgumentException("Gecersiz bakim tarih araligi.");
        }

        var previousStatus = existingVehicle.Status;
        existingVehicle.Status = VehicleStatus.Maintenance;
        WriteAuditLog(
            action: "VehicleMaintenanceScheduled",
            entityType: nameof(Vehicle),
            entityId: existingVehicle.Id,
            details: new
            {
                existingVehicle.Plate,
                StartDateUtc = startDateUtc,
                EndDateUtc = endDateUtc,
                Notes = notes,
                PreviousStatus = previousStatus.ToString(),
                NewStatus = existingVehicle.Status.ToString()
            });
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return MapToDto(existingVehicle);
    }

    public async Task<VehicleDto?> UploadVehiclePhotoAsync(Guid id, IFormFile file, CancellationToken cancellationToken = default)
    {
        var existingVehicle = await vehicleRepository.GetByIdAsync(id, cancellationToken);
        if (existingVehicle is null)
        {
            return null;
        }

        var previousPhotoUrl = existingVehicle.PhotoUrl;
        var photoUrl = await vehiclePhotoStorage.SaveAsync(id, file, cancellationToken);
        existingVehicle.PhotoUrl = photoUrl;
        WriteAuditLog(
            action: "VehiclePhotoUploaded",
            entityType: nameof(Vehicle),
            entityId: existingVehicle.Id,
            details: new
            {
                existingVehicle.Plate,
                PreviousPhotoUrl = previousPhotoUrl,
                NewPhotoUrl = photoUrl,
                FileName = file.FileName
            });
        await unitOfWork.SaveChangesAsync(cancellationToken);

        if (!string.Equals(previousPhotoUrl, photoUrl, StringComparison.OrdinalIgnoreCase))
        {
            await vehiclePhotoStorage.DeleteAsync(previousPhotoUrl, cancellationToken);
        }

        return MapToDto(existingVehicle);
    }

    public async Task<VehicleDto?> GetVehicleByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var vehicle = await vehicleRepository.GetByIdAsync(id, cancellationToken);
        return vehicle is null ? null : MapToDto(vehicle);
    }

    public Task<bool> IsVehiclePlateAvailableAsync(string plate, Guid? excludeVehicleId = null, CancellationToken cancellationToken = default)
    {
        return vehicleRepository.IsPlateAvailableAsync(plate, excludeVehicleId, cancellationToken);
    }

    public Task<bool> VehicleGroupExistsAsync(Guid groupId, CancellationToken cancellationToken = default)
    {
        return vehicleRepository.VehicleGroupExistsAsync(groupId, cancellationToken);
    }

    public Task<bool> OfficeExistsAsync(Guid officeId, CancellationToken cancellationToken = default)
    {
        return vehicleRepository.OfficeExistsAsync(officeId, cancellationToken);
    }

    public async Task<IReadOnlyList<OfficeDto>> GetOfficesAsync(CancellationToken cancellationToken = default)
    {
        var offices = await officeRepository.ListAsync(cancellationToken);
        return offices.Select(MapToDto).ToList();
    }

    public async Task<OfficeDto> CreateOfficeAsync(CreateOfficeRequest request, CancellationToken cancellationToken = default)
    {
        var office = new Office
        {
            Name = request.Name.Trim(),
            Address = request.Address.Trim(),
            Phone = request.Phone.Trim(),
            IsAirport = request.IsAirport,
            OpeningHours = request.OpeningHours.Trim()
        };

        await officeRepository.AddAsync(office, cancellationToken);
        WriteAuditLog(
            action: "OfficeCreated",
            entityType: nameof(Office),
            entityId: office.Id,
            details: new
            {
                office.Name,
                office.IsAirport,
                office.OpeningHours
            });
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return MapToDto(office);
    }

    public async Task<OfficeDto?> UpdateOfficeAsync(Guid id, UpdateOfficeRequest request, CancellationToken cancellationToken = default)
    {
        var existingOffice = await officeRepository.GetByIdAsync(id, cancellationToken);
        if (existingOffice is null)
        {
            return null;
        }

        var previousState = new
        {
            existingOffice.Name,
            existingOffice.Address,
            existingOffice.Phone,
            existingOffice.IsAirport,
            existingOffice.OpeningHours
        };

        existingOffice.Name = request.Name.Trim();
        existingOffice.Address = request.Address.Trim();
        existingOffice.Phone = request.Phone.Trim();
        existingOffice.IsAirport = request.IsAirport;
        existingOffice.OpeningHours = request.OpeningHours.Trim();

        WriteAuditLog(
            action: "OfficeUpdated",
            entityType: nameof(Office),
            entityId: existingOffice.Id,
            details: new
            {
                Previous = previousState,
                Current = new
                {
                    existingOffice.Name,
                    existingOffice.Address,
                    existingOffice.Phone,
                    existingOffice.IsAirport,
                    existingOffice.OpeningHours
                }
            });
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return MapToDto(existingOffice);
    }

    private void WriteAuditLog(string action, string entityType, Guid entityId, object details)
    {
        dbContext.AuditLogs.Add(new AuditLog
        {
            Action = action,
            EntityType = entityType,
            EntityId = entityId.ToString(),
            UserId = GetCurrentActorId(),
            Timestamp = DateTime.UtcNow,
            Details = JsonSerializer.Serialize(details)
        });
    }

    private string? GetCurrentActorId()
    {
        var user = httpContextAccessor?.HttpContext?.User;
        return user?.FindFirstValue(ClaimTypes.NameIdentifier) ?? user?.Identity?.Name;
    }

    private static VehicleGroupDto MapToDto(VehicleGroup vehicleGroup)
    {
        return new VehicleGroupDto(
            vehicleGroup.Id,
            vehicleGroup.NameTr,
            vehicleGroup.NameEn,
            vehicleGroup.NameRu,
            vehicleGroup.NameAr,
            vehicleGroup.NameDe,
            vehicleGroup.DepositAmount,
            vehicleGroup.MinAge,
            vehicleGroup.MinLicenseYears,
            vehicleGroup.Features);
    }

    private static VehicleDto MapToDto(Vehicle vehicle)
    {
        return new VehicleDto(
            vehicle.Id,
            vehicle.Plate,
            vehicle.Brand,
            vehicle.Model,
            vehicle.Year,
            vehicle.Color,
            vehicle.GroupId,
            vehicle.OfficeId,
            vehicle.Status,
            vehicle.PhotoUrl);
    }

    private static OfficeDto MapToDto(Office office)
    {
        return new OfficeDto(
            office.Id,
            office.Name,
            office.Address,
            office.Phone,
            office.IsAirport,
            office.OpeningHours);
    }

    private static List<string> NormalizeFeatures(IReadOnlyList<string>? features)
    {
        return (features ?? [])
            .Where(feature => !string.IsNullOrWhiteSpace(feature))
            .Select(feature => feature.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}