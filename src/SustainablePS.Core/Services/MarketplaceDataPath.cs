namespace SustainablePS.Core.Services;

public static class MarketplaceDataPath
{
    public const string EnvironmentVariableName = "SUSTAINABLEPS_DATA_PATH";

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
