using System.Diagnostics;
using System.Text;

namespace RentACar.Worker;

internal static class DailyBackupCommandPolicy
{
    private static readonly char[] InvalidArgumentCharacters = ['\r', '\n', '\0', '&', '|', ';', '<', '>', '`'];

    public static ProcessStartInfo CreateStartInfo(DailyBackupOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var command = options.Command?.Trim();
        if (string.IsNullOrWhiteSpace(command))
        {
            throw new InvalidOperationException("Daily backup command is not configured.");
        }

        if (options.AllowedCommands is null || options.AllowedCommands.Count == 0)
        {
            throw new InvalidOperationException("Daily backup allowed commands must be configured.");
        }

        var commandName = Path.GetFileName(command);
        var isAllowed = options.AllowedCommands.Any(allowedCommand =>
            allowedCommand.Equals(command, StringComparison.OrdinalIgnoreCase) ||
            allowedCommand.Equals(commandName, StringComparison.OrdinalIgnoreCase));

        if (!isAllowed)
        {
            throw new InvalidOperationException($"Daily backup command '{commandName}' is not allowlisted.");
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = command,
            WorkingDirectory = string.IsNullOrWhiteSpace(options.WorkingDirectory)
                ? AppContext.BaseDirectory
                : options.WorkingDirectory.Trim(),
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        foreach (var argument in ParseArguments(options.Arguments))
        {
            startInfo.ArgumentList.Add(argument);
        }

        return startInfo;
    }

    public static string SanitizeForLog(string? value, int maxLength = 500)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "<empty>";
        }

        var builder = new StringBuilder(value.Length);
        var previousWasSpace = false;

        foreach (var ch in value.Trim())
        {
            if (char.IsControl(ch))
            {
                if (!previousWasSpace)
                {
                    builder.Append(' ');
                    previousWasSpace = true;
                }

                continue;
            }

            if (char.IsWhiteSpace(ch))
            {
                if (!previousWasSpace)
                {
                    builder.Append(' ');
                    previousWasSpace = true;
                }

                continue;
            }

            builder.Append(ch);
            previousWasSpace = false;
        }

        var normalized = builder.ToString().Trim();
        if (normalized.Length <= maxLength)
        {
            return normalized;
        }

        return normalized[..maxLength] + "...";
    }

    private static IReadOnlyList<string> ParseArguments(string? rawArguments)
    {
        if (string.IsNullOrWhiteSpace(rawArguments))
        {
            return [];
        }

        if (rawArguments.IndexOfAny(InvalidArgumentCharacters) >= 0 || rawArguments.Contains('"'))
        {
            throw new InvalidOperationException("Daily backup command arguments contain unsupported characters.");
        }

        return rawArguments
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToArray();
    }
}
