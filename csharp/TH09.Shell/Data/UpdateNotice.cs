using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace TH09.Shell.Data;

internal static class UpdateNotice
{
    public const string UpdateFileName = "UPDATE.md";

    public const string Message = "新しいバージョンがあります。更新方法は " + UpdateFileName + " を見てください。";

    public static string CurrentVersionText
        => typeof(UpdateNotice).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";

    public static bool ShouldNotify(string? releaseJson, string? currentVersionText)
    {
        if (string.IsNullOrWhiteSpace(releaseJson)) return false;
        if (!TryParseVersion(currentVersionText, out var current)) return false;

        string? tag;
        try
        {
            using var doc = JsonDocument.Parse(releaseJson);
            if (!doc.RootElement.TryGetProperty("tag_name", out var tagProp)) return false;
            tag = tagProp.GetString();
        }
        catch (JsonException)
        {
            return false;
        }

        if (!TryParseVersion(tag, out var latest)) return false;
        return latest > current;
    }

    private static bool TryParseVersion(string? text, [NotNullWhen(true)] out Version? version)
    {
        version = null;
        if (string.IsNullOrWhiteSpace(text)) return false;
        var trimmed = text.Trim();
        if (trimmed.Length > 0 && (trimmed[0] is 'v' or 'V')) trimmed = trimmed[1..];
        return Version.TryParse(trimmed, out version);
    }
}
