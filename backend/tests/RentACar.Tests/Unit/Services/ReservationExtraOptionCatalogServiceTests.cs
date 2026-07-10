using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using RentACar.API.Contracts.ReservationExtraOptions;
using RentACar.API.Services;
using RentACar.Core.Entities;
using RentACar.Core.Enums;
using RentACar.Tests.TestFixtures;
using Xunit;

namespace RentACar.Tests.Unit.Services;

public sealed class ReservationExtraOptionCatalogServiceTests : IClassFixture<TestDbContextFactory>
{
    private readonly TestDbContextFactory _factory;

    public ReservationExtraOptionCatalogServiceTests(TestDbContextFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task CreateAsync_AllowsIncompleteDraftAndGeneratesImmutableCode()
    {
        using var dbContext = _factory.CreateContext();
        var group = CreateVehicleGroup();
        dbContext.VehicleGroups.Add(group);
        await dbContext.SaveChangesAsync();
        var service = new ReservationExtraOptionCatalogService(dbContext);

        var result = await service.CreateAsync(
            new CreateReservationExtraOptionRequest(
                10,
                ReservationExtraPricingMode.PerDay,
                2,
                " WIFI ",
                5,
                [group.Id],
                [new ReservationExtraOptionTranslationDto("tr", " İnternet ", " Açıklama ")]),
            AuditContext(),
            CancellationToken.None);

        result.Code.Should().StartWith("extra-").And.HaveLength(38);
        result.IsActive.Should().BeFalse();
        result.IconKey.Should().Be("wifi");
        result.Translations.Single().Name.Should().Be("İnternet");
        dbContext.AuditLogs.Should().ContainSingle(log => log.Action == "ReservationExtraOptionCreated");
        dbContext.AuditLogs.Single().NewValue.Should().NotContain("İnternet");
    }

    [Fact]
    public async Task CreateAsync_RejectsDuplicateVehicleGroups()
    {
        using var dbContext = _factory.CreateContext();
        var group = CreateVehicleGroup();
        dbContext.VehicleGroups.Add(group);
        await dbContext.SaveChangesAsync();
        var service = new ReservationExtraOptionCatalogService(dbContext);

        var action = () => service.CreateAsync(
            DraftRequest([group.Id, group.Id]),
            AuditContext(),
            CancellationToken.None);

        await action.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*distinct*");
    }

    [Theory]
    [InlineData(-1, 1, 0, "wifi")]
    [InlineData(1000001, 1, 0, "wifi")]
    [InlineData(1, 0, 0, "wifi")]
    [InlineData(1, 21, 0, "wifi")]
    [InlineData(1, 1, -1, "wifi")]
    [InlineData(1, 1, 10000, "wifi")]
    [InlineData(1, 1, 0, "script")]
    public async Task CreateAsync_RejectsInvalidCatalogBounds(
        decimal price,
        int maxQuantity,
        int sortOrder,
        string iconKey)
    {
        using var dbContext = _factory.CreateContext();
        var service = new ReservationExtraOptionCatalogService(dbContext);
        var request = new CreateReservationExtraOptionRequest(
            price,
            ReservationExtraPricingMode.PerDay,
            maxQuantity,
            iconKey,
            sortOrder,
            [],
            []);

        var action = () => service.CreateAsync(request, AuditContext(), CancellationToken.None);

        await action.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task CreateAsync_RejectsUnknownVehicleGroupAndUnsupportedLocale()
    {
        using var dbContext = _factory.CreateContext();
        var service = new ReservationExtraOptionCatalogService(dbContext);

        var unknownGroupAction = () => service.CreateAsync(
            DraftRequest([Guid.NewGuid()]),
            AuditContext(),
            CancellationToken.None);
        var unsupportedLocaleAction = () => service.CreateAsync(
            DraftRequest([]) with
            {
                Translations = [new ReservationExtraOptionTranslationDto("fr", "Nom", "Description")]
            },
            AuditContext(),
            CancellationToken.None);

        await unknownGroupAction.Should().ThrowAsync<ArgumentException>().WithMessage("*must exist*");
        await unsupportedLocaleAction.Should().ThrowAsync<ArgumentException>().WithMessage("*not supported*");
    }

    [Fact]
    public async Task CreateAsync_RejectsDuplicateAndOversizedTranslations()
    {
        using var dbContext = _factory.CreateContext();
        var service = new ReservationExtraOptionCatalogService(dbContext);
        var duplicateLocaleAction = () => service.CreateAsync(
            DraftRequest([]) with
            {
                Translations =
                [
                    new ReservationExtraOptionTranslationDto("tr", "Ad", "Açıklama"),
                    new ReservationExtraOptionTranslationDto("TR", "Başka", "Başka açıklama")
                ]
            },
            AuditContext(),
            CancellationToken.None);
        var oversizedAction = () => service.CreateAsync(
            DraftRequest([]) with
            {
                Translations = [new ReservationExtraOptionTranslationDto("tr", new string('n', 101), "Açıklama")]
            },
            AuditContext(),
            CancellationToken.None);

        await duplicateLocaleAction.Should().ThrowAsync<ArgumentException>().WithMessage("*one translation*");
        await oversizedAction.Should().ThrowAsync<ArgumentException>().WithMessage("*limits*");
    }

    [Fact]
    public async Task UpdateStatusAsync_RejectsActivationWithoutCompleteTranslations()
    {
        using var dbContext = _factory.CreateContext();
        var option = CreateOption(version: 7);
        dbContext.ReservationExtraOptions.Add(option);
        await dbContext.SaveChangesAsync();
        var service = new ReservationExtraOptionCatalogService(dbContext);

        var action = () => service.UpdateStatusAsync(
            option.Id,
            new UpdateReservationExtraOptionStatusRequest(option.Version, true),
            AuditContext(),
            CancellationToken.None);

        await action.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*five complete translations*");
    }

    [Fact]
    public async Task UpdateStatusAsync_ActivatesCompleteOptionAndAuditsAction()
    {
        using var dbContext = _factory.CreateContext();
        var group = CreateVehicleGroup();
        var option = CreateOption(version: 7, translations: CompleteTranslations());
        option.VehicleGroups.Add(new ReservationExtraOptionVehicleGroup { OptionId = option.Id, VehicleGroupId = group.Id });
        dbContext.AddRange(group, option);
        await dbContext.SaveChangesAsync();
        var service = new ReservationExtraOptionCatalogService(dbContext);

        var result = await service.UpdateStatusAsync(
            option.Id,
            new UpdateReservationExtraOptionStatusRequest(option.Version, true),
            AuditContext(),
            CancellationToken.None);

        result.IsActive.Should().BeTrue();
        dbContext.AuditLogs.Should().ContainSingle(log => log.Action == "ReservationExtraOptionActivated");
    }

    [Fact]
    public async Task UpdateStatusAsync_DeactivatesOptionAndAuditsAction()
    {
        using var dbContext = _factory.CreateContext();
        var option = CreateOption(version: 7, translations: CompleteTranslations());
        option.IsActive = true;
        dbContext.ReservationExtraOptions.Add(option);
        await dbContext.SaveChangesAsync();
        var service = new ReservationExtraOptionCatalogService(dbContext);

        var result = await service.UpdateStatusAsync(
            option.Id,
            new UpdateReservationExtraOptionStatusRequest(option.Version, false),
            AuditContext(),
            CancellationToken.None);

        result.IsActive.Should().BeFalse();
        dbContext.AuditLogs.Should().ContainSingle(log => log.Action == "ReservationExtraOptionDeactivated");
    }

    [Fact]
    public async Task UpdateAsync_RejectsStaleVersion()
    {
        using var dbContext = _factory.CreateContext();
        var option = CreateOption(version: 9);
        dbContext.ReservationExtraOptions.Add(option);
        await dbContext.SaveChangesAsync();
        var service = new ReservationExtraOptionCatalogService(dbContext);

        var action = () => service.UpdateAsync(
            option.Id,
            new UpdateReservationExtraOptionRequest(
                8,
                option.UnitPrice,
                option.PricingMode,
                option.MaxQuantity,
                option.IconKey,
                option.SortOrder,
                [],
                []),
            AuditContext(),
            CancellationToken.None);

        await action.Should().ThrowAsync<ReservationExtraOptionConcurrencyException>();
    }

    [Fact]
    public async Task UpdateAsync_ReplacesAssignmentsAndTranslationsAndAuditsBothActions()
    {
        using var dbContext = _factory.CreateContext();
        var firstGroup = CreateVehicleGroup();
        var secondGroup = CreateVehicleGroup();
        var option = CreateOption(version: 9, translations: CompleteTranslations());
        option.VehicleGroups.Add(new ReservationExtraOptionVehicleGroup
        {
            OptionId = option.Id,
            VehicleGroupId = firstGroup.Id
        });
        dbContext.AddRange(firstGroup, secondGroup, option);
        await dbContext.SaveChangesAsync();
        var service = new ReservationExtraOptionCatalogService(dbContext);

        var result = await service.UpdateAsync(
            option.Id,
            new UpdateReservationExtraOptionRequest(
                option.Version,
                25,
                ReservationExtraPricingMode.PerRental,
                3,
                "users",
                15,
                [secondGroup.Id],
                CompleteTranslations("Updated")),
            AuditContext(),
            CancellationToken.None);

        result.Code.Should().Be(option.Code);
        result.VehicleGroupIds.Should().Equal(secondGroup.Id);
        result.Translations.Should().OnlyContain(translation => translation.Name == "Updated");
        dbContext.AuditLogs.Select(log => log.Action).Should().Contain([
            "ReservationExtraOptionUpdated",
            "ReservationExtraOptionAssignmentsChanged"
        ]);
    }

    [Fact]
    public async Task RestoreAsync_AlwaysRestoresToInactiveDraft()
    {
        using var dbContext = _factory.CreateContext();
        var option = CreateOption(version: 4);
        option.IsArchived = true;
        option.IsActive = true;
        dbContext.ReservationExtraOptions.Add(option);
        await dbContext.SaveChangesAsync();
        var service = new ReservationExtraOptionCatalogService(dbContext);

        var result = await service.RestoreAsync(
            option.Id,
            new RestoreReservationExtraOptionRequest(option.Version),
            AuditContext(),
            CancellationToken.None);

        result.Item.IsArchived.Should().BeFalse();
        result.Item.IsActive.Should().BeFalse();
        dbContext.AuditLogs.Should().ContainSingle(log => log.Action == "ReservationExtraOptionRestored");
    }

    [Fact]
    public async Task DeleteAsync_DeletesUnusedOption()
    {
        using var dbContext = _factory.CreateContext();
        var option = CreateOption(version: 3);
        dbContext.ReservationExtraOptions.Add(option);
        await dbContext.SaveChangesAsync();
        var service = new ReservationExtraOptionCatalogService(dbContext);

        var result = await service.DeleteAsync(option.Id, option.Version, AuditContext(), CancellationToken.None);

        result.Disposition.Should().Be("deleted");
        dbContext.ReservationExtraOptions.Should().BeEmpty();
        dbContext.AuditLogs.Should().ContainSingle(log => log.Action == "ReservationExtraOptionDeleted");
    }

    [Fact]
    public async Task DeleteAsync_ArchivesUsedOption()
    {
        using var dbContext = _factory.CreateContext();
        var option = CreateOption(version: 3);
        option.IsActive = true;
        dbContext.ReservationExtraOptions.Add(option);
        dbContext.ReservationSelectedExtras.Add(new ReservationSelectedExtra
        {
            ReservationId = Guid.NewGuid(),
            ExtraOptionId = option.Id,
            OptionVersionSnapshot = option.Version,
            Locale = "tr",
            OptionCodeSnapshot = option.Code,
            NameSnapshot = "Test",
            DescriptionSnapshot = "Test",
            UnitPriceSnapshot = 10,
            PricingModeSnapshot = ReservationExtraPricingMode.PerDay,
            Quantity = 1,
            RentalDaysSnapshot = 1,
            TotalPriceSnapshot = 10
        });
        await dbContext.SaveChangesAsync();
        var service = new ReservationExtraOptionCatalogService(dbContext);

        var result = await service.DeleteAsync(option.Id, option.Version, AuditContext(), CancellationToken.None);

        result.Disposition.Should().Be("archived");
        option.IsArchived.Should().BeTrue();
        option.IsActive.Should().BeFalse();
        dbContext.AuditLogs.Should().ContainSingle(log => log.Action == "ReservationExtraOptionArchived");
    }

    [Fact]
    public async Task GetPublicCatalogAsync_FiltersByGroupStatusAndLocaleAndOrdersItems()
    {
        using var dbContext = _factory.CreateContext();
        var requestedGroup = CreateVehicleGroup();
        var otherGroup = CreateVehicleGroup();
        dbContext.VehicleGroups.AddRange(requestedGroup, otherGroup);
        AddPublicOption(dbContext, requestedGroup.Id, "second", 20, "Zulu");
        AddPublicOption(dbContext, requestedGroup.Id, "first", 10, "Alpha");
        AddPublicOption(dbContext, otherGroup.Id, "other", 1, "Other");
        var inactive = AddPublicOption(dbContext, requestedGroup.Id, "inactive", 1, "Inactive");
        inactive.IsActive = false;
        await dbContext.SaveChangesAsync();
        var service = new ReservationExtraOptionCatalogService(dbContext);

        var result = await service.GetPublicCatalogAsync(requestedGroup.Id, "TR", CancellationToken.None);

        result.Items.Select(item => item.Code).Should().Equal("first", "second");
        result.Items.Should().OnlyContain(item => item.Name == "Alpha" || item.Name == "Zulu");
    }

    private static ReservationExtraOption AddPublicOption(
        DbContext dbContext,
        Guid groupId,
        string code,
        int sortOrder,
        string name)
    {
        var option = CreateOption(version: 2, translations: CompleteTranslations(name));
        option.Code = code;
        option.SortOrder = sortOrder;
        option.IsActive = true;
        option.VehicleGroups.Add(new ReservationExtraOptionVehicleGroup { OptionId = option.Id, VehicleGroupId = groupId });
        dbContext.Add(option);
        return option;
    }

    private static CreateReservationExtraOptionRequest DraftRequest(IReadOnlyList<Guid> groupIds) => new(
        10,
        ReservationExtraPricingMode.PerRental,
        1,
        "users",
        1,
        groupIds,
        []);

    private static ReservationExtraOption CreateOption(
        uint version,
        IReadOnlyList<ReservationExtraOptionTranslationDto>? translations = null) => new()
        {
            Code = $"extra-{Guid.NewGuid():N}",
            UnitPrice = 10,
            PricingMode = ReservationExtraPricingMode.PerDay,
            MaxQuantity = 2,
            IconKey = "wifi",
            SortOrder = 10,
            Version = version,
            Translations = (translations ?? [])
                .Select(item => new ReservationExtraOptionTranslation
                {
                    Locale = item.Locale,
                    Name = item.Name,
                    Description = item.Description
                })
                .ToList()
        };

    private static IReadOnlyList<ReservationExtraOptionTranslationDto> CompleteTranslations(string name = "Extra") =>
    [
        new("tr", name, "Açıklama"),
        new("en", name, "Description"),
        new("de", name, "Beschreibung"),
        new("ru", name, "Описание"),
        new("ar", name, "وصف")
    ];

    private static VehicleGroup CreateVehicleGroup() => new()
    {
        NameTr = "Ekonomi",
        NameEn = "Economy",
        NameDe = "Economy",
        NameRu = "Economy",
        NameAr = "Economy",
        DepositAmount = 1000,
        MinAge = 21,
        MinLicenseYears = 2
    };

    private static ReservationExtraOptionAuditContext AuditContext() => new("admin-id", "127.0.0.1");
}
