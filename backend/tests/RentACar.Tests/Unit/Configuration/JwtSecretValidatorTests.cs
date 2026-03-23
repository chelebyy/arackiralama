using FluentAssertions;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using RentACar.API.Configuration;
using RentACar.API.Options;
using Xunit;

namespace RentACar.Tests.Unit.Configuration;

public sealed class JwtSecretValidatorTests
{
    [Fact]
    public void Validate_WhenStagingAndPlaceholderSecret_Throws()
    {
        var options = new JwtOptions { Secret = "CHANGE_THIS_TO_A_32_CHAR_MINIMUM_SECRET" };
        var environment = new TestHostEnvironment(Environments.Staging);

        var action = () => JwtSecretValidator.Validate(options, environment);

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*placeholder*");
    }

    [Fact]
    public void Validate_WhenProductionAndWeakDevSecret_Throws()
    {
        var options = new JwtOptions { Secret = "DevelopmentOnlySecretKeyAtLeast32Chars" };
        var environment = new TestHostEnvironment(Environments.Production);

        var action = () => JwtSecretValidator.Validate(options, environment);

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*placeholder*");
    }

    [Fact]
    public void Validate_WhenDevelopmentAndPlaceholderSecret_Allows()
    {
        var options = new JwtOptions { Secret = "CHANGE_THIS_TO_A_32_CHAR_MINIMUM_SECRET" };
        var environment = new TestHostEnvironment(Environments.Development);

        var action = () => JwtSecretValidator.Validate(options, environment);

        action.Should().NotThrow();
    }

    private sealed class TestHostEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;
        public string ApplicationName { get; set; } = "RentACar.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = null!;
    }
}
