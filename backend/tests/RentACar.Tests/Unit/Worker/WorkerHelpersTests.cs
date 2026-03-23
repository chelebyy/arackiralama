using FluentAssertions;
using RentACar.Worker;
using Xunit;

namespace RentACar.Tests.Unit.Worker;

public sealed class WorkerHelpersTests
{
    [Fact]
    public void HasReservationId_WhenPayloadMatches_ReturnsTrue()
    {
        var payload = $$"""{"ReservationId":"{{Guid.Parse("4f9b1b0b-f8c5-4f40-9d32-18ed9bd3ed18")}}"}""";

        var result = WorkerPayloadMatcher.HasReservationId(payload, Guid.Parse("4f9b1b0b-f8c5-4f40-9d32-18ed9bd3ed18"));

        result.Should().BeTrue();
    }

    [Fact]
    public void CreateStartInfo_WhenCommandIsAllowlisted_BuildsArgumentList()
    {
        var options = new DailyBackupOptions
        {
            Command = "backup.exe",
            Arguments = "--database rentacar --full",
            WorkingDirectory = "C:/backup",
            AllowedCommands = ["backup.exe"]
        };

        var startInfo = DailyBackupCommandPolicy.CreateStartInfo(options);

        startInfo.FileName.Should().Be("backup.exe");
        startInfo.WorkingDirectory.Should().Be("C:/backup");
        startInfo.ArgumentList.Should().ContainInOrder("--database", "rentacar", "--full");
    }

    [Fact]
    public void CreateStartInfo_WhenCommandIsNotAllowlisted_Throws()
    {
        var options = new DailyBackupOptions
        {
            Command = "powershell.exe",
            Arguments = "-NoProfile",
            AllowedCommands = ["backup.exe"]
        };

        var action = () => DailyBackupCommandPolicy.CreateStartInfo(options);

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*not allowlisted*");
    }

    [Fact]
    public void SanitizeForLog_TruncatesAndNormalizesWhitespace()
    {
        var value = "line1\r\nline2\tline3 " + new string('x', 600);

        var sanitized = DailyBackupCommandPolicy.SanitizeForLog(value);

        sanitized.Should().StartWith("line1 line2 line3");
        sanitized.Should().EndWith("...");
        sanitized.Length.Should().BeLessThanOrEqualTo(503);
    }
}
