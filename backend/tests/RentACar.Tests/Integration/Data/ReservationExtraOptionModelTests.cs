using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using RentACar.Core.Entities;
using RentACar.Tests.TestFixtures;
using Xunit;

namespace RentACar.Tests.Integration.Data;

public sealed class ReservationExtraOptionModelTests : IClassFixture<TestDbContextFactory>
{
    private readonly TestDbContextFactory _factory;

    public ReservationExtraOptionModelTests(TestDbContextFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public void CatalogModel_ConfiguresKeysIndexesPrecisionAndConcurrency()
    {
        using var context = _factory.CreateContext();

        var option = context.Model.FindEntityType(typeof(ReservationExtraOption));
        var translation = context.Model.FindEntityType(typeof(ReservationExtraOptionTranslation));
        var assignment = context.Model.FindEntityType(typeof(ReservationExtraOptionVehicleGroup));

        option.Should().NotBeNull();
        option!.GetTableName().Should().Be("reservation_extra_options");
        option.FindProperty(nameof(ReservationExtraOption.UnitPrice))!.GetPrecision().Should().Be(18);
        option.FindProperty(nameof(ReservationExtraOption.UnitPrice))!.GetScale().Should().Be(2);
        option.FindProperty(nameof(ReservationExtraOption.Version))!.IsConcurrencyToken.Should().BeTrue();
        option.FindProperty(nameof(ReservationExtraOption.Version))!.ValueGenerated.Should().Be(ValueGenerated.OnAddOrUpdate);
        option.GetIndexes().Should().Contain(index =>
            index.Properties.Select(property => property.Name).SequenceEqual(new[] { nameof(ReservationExtraOption.Code) }) &&
            index.IsUnique);

        translation!.FindPrimaryKey()!.Properties.Select(property => property.Name)
            .Should().Equal(nameof(ReservationExtraOptionTranslation.OptionId), nameof(ReservationExtraOptionTranslation.Locale));
        assignment!.FindPrimaryKey()!.Properties.Select(property => property.Name)
            .Should().Equal(nameof(ReservationExtraOptionVehicleGroup.OptionId), nameof(ReservationExtraOptionVehicleGroup.VehicleGroupId));
    }

    [Fact]
    public void SnapshotModel_ConfiguresDeleteBehaviorUniquenessAndJsonStorage()
    {
        using var context = _factory.CreateContext();

        var selectedExtra = context.Model.FindEntityType(typeof(ReservationSelectedExtra));
        var reservation = context.Model.FindEntityType(typeof(Reservation));

        selectedExtra.Should().NotBeNull();
        selectedExtra!.FindProperty(nameof(ReservationSelectedExtra.UnitPriceSnapshot))!.GetPrecision().Should().Be(18);
        selectedExtra.FindProperty(nameof(ReservationSelectedExtra.TotalPriceSnapshot))!.GetScale().Should().Be(2);
        selectedExtra.GetIndexes().Should().Contain(index =>
            index.Properties.Select(property => property.Name).SequenceEqual(new[] {
                nameof(ReservationSelectedExtra.ReservationId),
                nameof(ReservationSelectedExtra.ExtraOptionId) }) &&
            index.IsUnique);
        selectedExtra.GetForeignKeys().Single(key => key.PrincipalEntityType.ClrType == typeof(Reservation))
            .DeleteBehavior.Should().Be(DeleteBehavior.Cascade);
        selectedExtra.GetForeignKeys().Single(key => key.PrincipalEntityType.ClrType == typeof(ReservationExtraOption))
            .DeleteBehavior.Should().Be(DeleteBehavior.Restrict);

        reservation!.FindProperty(nameof(Reservation.PricingSnapshot))!
            .FindAnnotation(RelationalAnnotationNames.ColumnType)!.Value.Should().Be("jsonb");
        reservation.GetIndexes().Should().Contain(index =>
            index.Properties.Select(property => property.Name).SequenceEqual(new[] { nameof(Reservation.QuoteId) }) &&
            index.IsUnique);
    }
}
