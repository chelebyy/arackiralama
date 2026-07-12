using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using RentACar.API.Contracts.ReservationExtraOptions;
using RentACar.Core.Entities;
using RentACar.Core.Enums;
using RentACar.Core.Interfaces;

namespace RentACar.API.Services;

public sealed class ReservationExtraOptionCatalogService(IApplicationDbContext dbContext)
    : IReservationExtraOptionCatalogService
{
    private const string EntityType = "ReservationExtraOption";
    private static readonly string[] SupportedLocales = ["tr", "en", "de", "ru", "ar"];
    private static readonly HashSet<string> SupportedLocaleSet = new(SupportedLocales, StringComparer.Ordinal);
    private static readonly HashSet<string> AllowedIconKeys = new(["baby", "users", "navigation", "wifi"], StringComparer.Ordinal);

    public async Task<AdminReservationExtraOptionListResponse> GetAdminListAsync(
        string? search,
        string? status,
        Guid? vehicleGroupId,
        bool includeArchived,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        ValidatePagination(page, pageSize);

        var query = dbContext.ReservationExtraOptions
            .AsNoTracking()
            .Include(option => option.Translations)
            .Include(option => option.VehicleGroups)
            .AsQueryable();

        if (!includeArchived)
        {
            query = query.Where(option => !option.IsArchived);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var normalizedSearch = search.Trim().ToLower();
            query = query.Where(option =>
                option.Code.ToLower().Contains(normalizedSearch) ||
                option.Translations.Any(translation => translation.Name.ToLower().Contains(normalizedSearch)));
        }

        query = ApplyStatusFilter(query, status);

        if (vehicleGroupId.HasValue)
        {
            query = query.Where(option => option.VehicleGroups.Any(group => group.VehicleGroupId == vehicleGroupId.Value));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var entities = await query
            .OrderBy(option => option.SortOrder)
            .ThenBy(option => option.Code)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new AdminReservationExtraOptionListResponse(
            totalCount,
            page,
            pageSize,
            entities.Select(MapAdminDto).ToList());
    }

    public async Task<PublicReservationExtraOptionCatalogResponse> GetPublicCatalogAsync(
        Guid vehicleGroupId,
        string locale,
        CancellationToken cancellationToken = default)
    {
        var normalizedLocale = NormalizeLocale(locale);
        if (vehicleGroupId == Guid.Empty ||
            !await dbContext.VehicleGroups.AsNoTracking().AnyAsync(group => group.Id == vehicleGroupId, cancellationToken))
        {
            throw new ArgumentException("A valid vehicle group is required.");
        }

        var items = await dbContext.ReservationExtraOptions
            .AsNoTracking()
            .Where(option => option.IsActive && !option.IsArchived)
            .Where(option => option.VehicleGroups.Any(group => group.VehicleGroupId == vehicleGroupId))
            .Select(option => new
            {
                Option = option,
                Translation = option.Translations.Single(translation => translation.Locale == normalizedLocale)
            })
            .OrderBy(item => item.Option.SortOrder)
            .ThenBy(item => item.Translation.Name)
            .Select(item => new PublicReservationExtraOptionDto(
                item.Option.Id,
                item.Option.Code,
                item.Translation.Name,
                item.Translation.Description,
                item.Option.UnitPrice,
                item.Option.PricingMode,
                item.Option.MaxQuantity,
                item.Option.IconKey,
                item.Option.SortOrder,
                item.Option.Version))
            .ToListAsync(cancellationToken);

        return new PublicReservationExtraOptionCatalogResponse(items);
    }

    public async Task<AdminReservationExtraOptionDto> CreateAsync(
        CreateReservationExtraOptionRequest request,
        ReservationExtraOptionAuditContext auditContext,
        CancellationToken cancellationToken = default)
    {
        var normalized = await ValidateAndNormalizeAsync(
            request.UnitPrice,
            request.PricingMode,
            request.MaxQuantity,
            request.IconKey,
            request.SortOrder,
            request.VehicleGroupIds,
            request.Translations,
            requireActivationCompleteness: false,
            cancellationToken);

        var option = new ReservationExtraOption
        {
            Code = $"extra-{Guid.NewGuid():N}",
            UnitPrice = request.UnitPrice,
            PricingMode = request.PricingMode,
            MaxQuantity = request.MaxQuantity,
            IconKey = normalized.IconKey,
            SortOrder = request.SortOrder,
            IsActive = false,
            IsArchived = false,
            Translations = normalized.Translations
                .Select(translation => new ReservationExtraOptionTranslation
                {
                    Locale = translation.Locale,
                    Name = translation.Name,
                    Description = translation.Description
                })
                .ToList(),
            VehicleGroups = normalized.VehicleGroupIds
                .Select(vehicleGroupId => new ReservationExtraOptionVehicleGroup { VehicleGroupId = vehicleGroupId })
                .ToList()
        };

        dbContext.ReservationExtraOptions.Add(option);
        AddAudit("ReservationExtraOptionCreated", option, ["Code", "CatalogFields", "Translations", "VehicleGroupIds"], auditContext);
        await SaveChangesAsync(cancellationToken);
        return MapAdminDto(option);
    }

    public async Task<AdminReservationExtraOptionDto> UpdateAsync(
        Guid id,
        UpdateReservationExtraOptionRequest request,
        ReservationExtraOptionAuditContext auditContext,
        CancellationToken cancellationToken = default)
    {
        var option = await LoadForMutationAsync(id, cancellationToken);
        EnsureVersion(option, request.Version);
        if (option.IsArchived)
        {
            throw new ArgumentException("Archived options must be restored before editing.");
        }

        var normalized = await ValidateAndNormalizeAsync(
            request.UnitPrice,
            request.PricingMode,
            request.MaxQuantity,
            request.IconKey,
            request.SortOrder,
            request.VehicleGroupIds,
            request.Translations,
            option.IsActive,
            cancellationToken);

        var changedFields = GetChangedFields(option, request, normalized);
        var assignmentsChanged = !option.VehicleGroups.Select(group => group.VehicleGroupId)
            .OrderBy(idValue => idValue)
            .SequenceEqual(normalized.VehicleGroupIds.OrderBy(idValue => idValue));

        option.UnitPrice = request.UnitPrice;
        option.PricingMode = request.PricingMode;
        option.MaxQuantity = request.MaxQuantity;
        option.IconKey = normalized.IconKey;
        option.SortOrder = request.SortOrder;
        option.UpdatedAt = DateTime.UtcNow;

        var translationsByLocale = normalized.Translations.ToDictionary(translation => translation.Locale);
        foreach (var translation in option.Translations.ToList())
        {
            if (!translationsByLocale.Remove(translation.Locale, out var updatedTranslation))
            {
                option.Translations.Remove(translation);
                dbContext.ReservationExtraOptionTranslations.Remove(translation);
                continue;
            }

            translation.Name = updatedTranslation.Name;
            translation.Description = updatedTranslation.Description;
        }
        foreach (var translation in translationsByLocale.Values)
        {
            option.Translations.Add(new ReservationExtraOptionTranslation
            {
                OptionId = option.Id,
                Locale = translation.Locale,
                Name = translation.Name,
                Description = translation.Description
            });
        }

        var requestedVehicleGroupIds = normalized.VehicleGroupIds.ToHashSet();
        foreach (var assignment in option.VehicleGroups.ToList())
        {
            if (requestedVehicleGroupIds.Remove(assignment.VehicleGroupId))
            {
                continue;
            }

            option.VehicleGroups.Remove(assignment);
            dbContext.ReservationExtraOptionVehicleGroups.Remove(assignment);
        }
        foreach (var vehicleGroupId in requestedVehicleGroupIds)
        {
            option.VehicleGroups.Add(new ReservationExtraOptionVehicleGroup
            {
                OptionId = option.Id,
                VehicleGroupId = vehicleGroupId
            });
        }

        AddAudit("ReservationExtraOptionUpdated", option, changedFields, auditContext);
        if (assignmentsChanged)
        {
            AddAudit("ReservationExtraOptionAssignmentsChanged", option, ["VehicleGroupIds"], auditContext);
        }

        await SaveChangesAsync(cancellationToken);
        return MapAdminDto(option);
    }

    public async Task<AdminReservationExtraOptionDto> UpdateStatusAsync(
        Guid id,
        UpdateReservationExtraOptionStatusRequest request,
        ReservationExtraOptionAuditContext auditContext,
        CancellationToken cancellationToken = default)
    {
        var option = await LoadForMutationAsync(id, cancellationToken);
        EnsureVersion(option, request.Version);
        if (option.IsArchived)
        {
            throw new ArgumentException("Archived options must be restored before changing status.");
        }

        if (request.IsActive)
        {
            ValidateActivationCompleteness(option.Translations.Select(MapTranslationDto).ToList(), option.VehicleGroups.Count);
        }

        option.IsActive = request.IsActive;
        option.UpdatedAt = DateTime.UtcNow;
        AddAudit(
            request.IsActive ? "ReservationExtraOptionActivated" : "ReservationExtraOptionDeactivated",
            option,
            ["IsActive"],
            auditContext);
        await SaveChangesAsync(cancellationToken);
        return MapAdminDto(option);
    }

    public async Task<RestoreReservationExtraOptionResult> RestoreAsync(
        Guid id,
        RestoreReservationExtraOptionRequest request,
        ReservationExtraOptionAuditContext auditContext,
        CancellationToken cancellationToken = default)
    {
        var option = await LoadForMutationAsync(id, cancellationToken);
        EnsureVersion(option, request.Version);
        if (!option.IsArchived)
        {
            throw new ArgumentException("Only archived options can be restored.");
        }

        option.IsArchived = false;
        option.IsActive = false;
        option.UpdatedAt = DateTime.UtcNow;
        AddAudit("ReservationExtraOptionRestored", option, ["IsArchived", "IsActive"], auditContext);
        await SaveChangesAsync(cancellationToken);
        return new RestoreReservationExtraOptionResult(MapAdminDto(option));
    }

    public async Task<DeleteReservationExtraOptionResult> DeleteAsync(
        Guid id,
        uint version,
        ReservationExtraOptionAuditContext auditContext,
        CancellationToken cancellationToken = default)
    {
        var option = await LoadForMutationAsync(id, cancellationToken);
        EnsureVersion(option, version);

        if (await dbContext.ReservationSelectedExtras.AsNoTracking()
            .AnyAsync(selected => selected.ExtraOptionId == option.Id, cancellationToken))
        {
            await ArchiveAsync(option, auditContext, cancellationToken);
            return new DeleteReservationExtraOptionResult("archived");
        }

        dbContext.ReservationExtraOptions.Remove(option);
        AddAudit("ReservationExtraOptionDeleted", option, ["Deleted"], auditContext);

        try
        {
            await SaveChangesAsync(cancellationToken);
            return new DeleteReservationExtraOptionResult("deleted");
        }
        catch (DbUpdateException exception) when (
            exception.InnerException is PostgresException postgresException &&
            postgresException.SqlState is PostgresErrorCodes.ForeignKeyViolation or PostgresErrorCodes.RestrictViolation)
        {
            dbContext.ChangeTracker.Clear();
            var concurrentlyUsedOption = await LoadForMutationAsync(id, cancellationToken);
            await ArchiveAsync(concurrentlyUsedOption, auditContext, cancellationToken);
            return new DeleteReservationExtraOptionResult("archived");
        }
    }

    private async Task ArchiveAsync(
        ReservationExtraOption option,
        ReservationExtraOptionAuditContext auditContext,
        CancellationToken cancellationToken)
    {
        option.IsArchived = true;
        option.IsActive = false;
        option.UpdatedAt = DateTime.UtcNow;
        AddAudit("ReservationExtraOptionArchived", option, ["IsArchived", "IsActive"], auditContext);
        await SaveChangesAsync(cancellationToken);
    }

    private async Task<ReservationExtraOption> LoadForMutationAsync(Guid id, CancellationToken cancellationToken)
    {
        var option = await dbContext.ReservationExtraOptions
            .Include(item => item.Translations)
            .Include(item => item.VehicleGroups)
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        return option ?? throw new ReservationExtraOptionNotFoundException();
    }

    private async Task<NormalizedCatalogInput> ValidateAndNormalizeAsync(
        decimal unitPrice,
        ReservationExtraPricingMode pricingMode,
        int maxQuantity,
        string iconKey,
        int sortOrder,
        IReadOnlyList<Guid>? vehicleGroupIds,
        IReadOnlyList<ReservationExtraOptionTranslationDto>? translations,
        bool requireActivationCompleteness,
        CancellationToken cancellationToken)
    {
        if (unitPrice is < 0 or > 1_000_000)
        {
            throw new ArgumentException("Unit price must be between 0 and 1,000,000.");
        }

        if (!Enum.IsDefined(pricingMode))
        {
            throw new ArgumentException("Pricing mode is invalid.");
        }

        if (maxQuantity is < 1 or > 20)
        {
            throw new ArgumentException("Maximum quantity must be between 1 and 20.");
        }

        if (sortOrder is < 0 or > 9_999)
        {
            throw new ArgumentException("Sort order must be between 0 and 9,999.");
        }

        var normalizedIconKey = iconKey?.Trim().ToLowerInvariant() ?? string.Empty;
        if (!AllowedIconKeys.Contains(normalizedIconKey))
        {
            throw new ArgumentException("Icon key is invalid.");
        }

        var normalizedGroupIds = vehicleGroupIds?.ToList() ?? [];
        if (normalizedGroupIds.Any(id => id == Guid.Empty) || normalizedGroupIds.Distinct().Count() != normalizedGroupIds.Count)
        {
            throw new ArgumentException("Vehicle group identifiers must be non-empty and distinct.");
        }

        var existingGroupCount = await dbContext.VehicleGroups.AsNoTracking()
            .CountAsync(group => normalizedGroupIds.Contains(group.Id), cancellationToken);
        if (existingGroupCount != normalizedGroupIds.Count)
        {
            throw new ArgumentException("Every vehicle group must exist.");
        }

        var normalizedTranslations = NormalizeTranslations(translations);
        if (requireActivationCompleteness)
        {
            ValidateActivationCompleteness(normalizedTranslations, normalizedGroupIds.Count);
        }

        return new NormalizedCatalogInput(normalizedIconKey, normalizedGroupIds, normalizedTranslations);
    }

    private static List<ReservationExtraOptionTranslationDto> NormalizeTranslations(
        IReadOnlyList<ReservationExtraOptionTranslationDto>? translations)
    {
        var normalized = (translations ?? [])
            .Select(translation => new ReservationExtraOptionTranslationDto(
                NormalizeLocale(translation.Locale),
                translation.Name?.Trim() ?? string.Empty,
                translation.Description?.Trim() ?? string.Empty))
            .ToList();

        if (normalized.Select(translation => translation.Locale).Distinct(StringComparer.Ordinal).Count() != normalized.Count)
        {
            throw new ArgumentException("Only one translation per locale is allowed.");
        }

        if (normalized.Any(translation => translation.Name.Length > 100 || translation.Description.Length > 300))
        {
            throw new ArgumentException("Translation names and descriptions exceed their supported limits.");
        }

        return normalized;
    }

    private static void ValidateActivationCompleteness(
        IReadOnlyCollection<ReservationExtraOptionTranslationDto> translations,
        int vehicleGroupCount)
    {
        var translationsByLocale = translations.ToDictionary(item => item.Locale, StringComparer.Ordinal);
        if (SupportedLocales.Any(locale =>
                !translationsByLocale.TryGetValue(locale, out var translation) ||
                string.IsNullOrWhiteSpace(translation.Name) ||
                string.IsNullOrWhiteSpace(translation.Description)))
        {
            throw new ArgumentException("All five complete translations are required before activation.");
        }

        if (vehicleGroupCount == 0)
        {
            throw new ArgumentException("At least one vehicle group is required before activation.");
        }
    }

    private static string NormalizeLocale(string locale)
    {
        var normalized = locale?.Trim().ToLowerInvariant() ?? string.Empty;
        if (!SupportedLocaleSet.Contains(normalized))
        {
            throw new ArgumentException("Locale is not supported.");
        }

        return normalized;
    }

    private static IQueryable<ReservationExtraOption> ApplyStatusFilter(
        IQueryable<ReservationExtraOption> query,
        string? status)
    {
        if (string.IsNullOrWhiteSpace(status) || status.Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            return query;
        }

        return status.Trim().ToLowerInvariant() switch
        {
            "active" => query.Where(option => option.IsActive && !option.IsArchived),
            "inactive" => query.Where(option => !option.IsActive && !option.IsArchived),
            "archived" => query.Where(option => option.IsArchived),
            _ => throw new ArgumentException("Status filter is invalid.")
        };
    }

    private static void ValidatePagination(int page, int pageSize)
    {
        if (page < 1 || pageSize is < 1 or > 100)
        {
            throw new ArgumentException("Page must be positive and page size must be between 1 and 100.");
        }
    }

    private static void EnsureVersion(ReservationExtraOption option, uint version)
    {
        if (version == 0 || option.Version != version)
        {
            throw new ReservationExtraOptionConcurrencyException();
        }
    }

    private async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ReservationExtraOptionConcurrencyException();
        }
    }

    private void AddAudit(
        string action,
        ReservationExtraOption option,
        IReadOnlyCollection<string> changedFields,
        ReservationExtraOptionAuditContext auditContext)
    {
        dbContext.AuditLogs.Add(new AuditLog
        {
            Action = action,
            EntityType = EntityType,
            EntityId = option.Id.ToString(),
            UserId = auditContext.UserId,
            IpAddress = auditContext.IpAddress,
            Timestamp = DateTime.UtcNow,
            OldValue = null,
            NewValue = JsonSerializer.Serialize(new { option.Code, ChangedFields = changedFields }),
            Details = $"{action} for reservation extra option {option.Id}."
        });
    }

    private static IReadOnlyCollection<string> GetChangedFields(
        ReservationExtraOption option,
        UpdateReservationExtraOptionRequest request,
        NormalizedCatalogInput normalized)
    {
        var fields = new List<string>();
        if (option.UnitPrice != request.UnitPrice) fields.Add("UnitPrice");
        if (option.PricingMode != request.PricingMode) fields.Add("PricingMode");
        if (option.MaxQuantity != request.MaxQuantity) fields.Add("MaxQuantity");
        if (option.IconKey != normalized.IconKey) fields.Add("IconKey");
        if (option.SortOrder != request.SortOrder) fields.Add("SortOrder");
        fields.Add("Translations");
        fields.Add("VehicleGroupIds");
        return fields;
    }

    private static AdminReservationExtraOptionDto MapAdminDto(ReservationExtraOption option) => new(
        option.Id,
        option.Code,
        option.UnitPrice,
        option.PricingMode,
        option.MaxQuantity,
        option.IconKey,
        option.SortOrder,
        option.IsActive,
        option.IsArchived,
        option.Version,
        option.UpdatedAt,
        option.VehicleGroups.Select(group => group.VehicleGroupId).OrderBy(id => id).ToList(),
        option.Translations.Select(MapTranslationDto).OrderBy(translation => translation.Locale).ToList());

    private static ReservationExtraOptionTranslationDto MapTranslationDto(ReservationExtraOptionTranslation translation) =>
        new(translation.Locale, translation.Name, translation.Description);

    private sealed record NormalizedCatalogInput(
        string IconKey,
        IReadOnlyList<Guid> VehicleGroupIds,
        IReadOnlyList<ReservationExtraOptionTranslationDto> Translations);
}
