using System.Runtime.Versioning;
using TH09.Record;

namespace TH09.Shell.Data;

[SupportedOSPlatform("windows")]
internal static class AppSettingsSource
{
    private static AppSettings? loaded;
    private static string? readFrom;

    public static void ReadFrom(string configPath)
    {
        readFrom = configPath;
        loaded = null;
    }

    public static string ConfigPath
        => readFrom ?? (OperatingSystem.IsWindows() ? Paths.Default.ConfigPath : NoConfigName);

    public static AppSettings Current => loaded ??= ConfigStore.Load(ConfigPath);

    public static void Adopt(AppSettings settings) => loaded = settings;

    private const string NoConfigName = "th09_config_unavailable.json";
}
