namespace SustainablePS.Core.Services;

/// <summary>Resolves the JSON state file path for the legacy file-based service.</summary>
public static class MarketplaceDataPath
{
    /// <summary>Environment variable that overrides the default path.</summary>
    public const string EnvironmentVariableName = "SUSTAINABLEPS_DATA_PATH";

    /// <summary>Returns the OS-appropriate default path, or the env-var override if set.</summary>
    public static string ResolveDefault()
    {
        var configuredPath = Environment.GetEnvironmentVariable(EnvironmentVariableName);
        if (!string.IsNullOrWhiteSpace(configuredPath))
        {
            return configuredPath;
        }

        if (OperatingSystem.IsMacOS() || OperatingSystem.IsMacCatalyst())
        {
            return Path.Combine(
                "/Users",
                Environment.UserName,
                "Library",
                "Application Support",
                "SustainablePS",
                "marketplace-state.json");
        }

        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(appData, "SustainablePS", "marketplace-state.json");
    }
}
