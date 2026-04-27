using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;

namespace PointyPal.Infrastructure;

public static class AppInfo
{
    public const string AppName = "PointyPal";
    private static readonly string[] AllowedBuildChannels = { "dev", "private-rc", "production-preview" };

    public static string Version
    {
        get
        {
            var assembly = Assembly.GetExecutingAssembly();
            return assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
                ?? FileVersionInfo.GetVersionInfo(assembly.Location).ProductVersion
                ?? assembly.GetName().Version?.ToString()
                ?? "dev";
        }
    }

    public static string BuildChannel => GetMetadata("BuildChannel", "dev");

    public static string ReleaseLabel => GetMetadata("ReleaseLabel", "");

    public static string BaselineDate => GetMetadata("BaselineDate", "");

    public static string BuildDate => GetMetadata("BuildDate", "");

    public static string GitCommit => GetMetadata("GitCommit", "");

    public static string WorkerContractVersion => GetMetadata("WorkerContractVersion", "1.0.0");

    public static string FullReleaseVersion => string.IsNullOrWhiteSpace(ReleaseLabel)
        ? Version
        : $"{Version}-{ReleaseLabel}";

    public static string LogMetadata
    {
        get
        {
            string metadata =
                $"AppName={AppName}; Version={Version}; ReleaseLabel={ReleaseLabel}; BuildChannel={BuildChannel}; " +
                $"BuildDate={BuildDate}; BaselineDate={BaselineDate}; WorkerContractVersion={WorkerContractVersion}";

            if (!string.IsNullOrWhiteSpace(GitCommit))
            {
                metadata += $"; GitCommit={GitCommit}";
            }

            return metadata;
        }
    }

    public static string VersionCliText
    {
        get
        {
            var lines = new[]
            {
                $"{AppName} {Version} {BuildChannel}",
                $"ReleaseLabel: {ReleaseLabel}",
                $"WorkerContractVersion: {WorkerContractVersion}",
                $"BuildDate: {BuildDate}",
                $"BaselineDate: {BaselineDate}",
                $"GitCommit: {(string.IsNullOrWhiteSpace(GitCommit) ? "not available" : GitCommit)}"
            };

            return string.Join(Environment.NewLine, lines);
        }
    }

    public static string DisplayText
    {
        get
        {
            string text = string.IsNullOrWhiteSpace(ReleaseLabel)
                ? $"{AppName} {Version} ({BuildChannel})"
                : $"{AppName} {Version} {ReleaseLabel} ({BuildChannel})";

            if (!string.IsNullOrWhiteSpace(BuildDate))
            {
                text += $" ({BuildDate})";
            }

            if (!string.IsNullOrWhiteSpace(GitCommit))
            {
                text += $" [{GitCommit}]";
            }

            return text;
        }
    }

    public static bool IsValidBuildChannel(string? value)
    {
        return !string.IsNullOrWhiteSpace(value)
            && AllowedBuildChannels.Contains(value, StringComparer.OrdinalIgnoreCase);
    }

    public static bool IsValidVersionString(string? value)
    {
        return !string.IsNullOrWhiteSpace(value)
            && Regex.IsMatch(value, @"^\d+\.\d+\.\d+(?:[-+][A-Za-z0-9][A-Za-z0-9.-]*)?$");
    }

    public static bool IsValidReleaseLabel(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            || Regex.IsMatch(value, @"^[A-Za-z0-9][A-Za-z0-9.-]*$");
    }

    private static string GetMetadata(string key, string fallback)
    {
        var assembly = Assembly.GetExecutingAssembly();
        var value = assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(a => string.Equals(a.Key, key, StringComparison.OrdinalIgnoreCase))
            ?.Value;

        return string.IsNullOrWhiteSpace(value) ? fallback : value;
    }
}
