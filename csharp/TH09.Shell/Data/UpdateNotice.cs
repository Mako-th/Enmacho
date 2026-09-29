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
        => Parse(releaseJson, currentVersionText) is not null;

    public const string DownloadUrlPrefix = "https://github.com/Mako-th/Enmacho/releases/download/";

    public const string ZipAssetName = "Enmacho-win-x64.zip";

    public const string PromptMessage =
        "新しいバージョンがあります。いま更新しますか？\n（このツールを閉じて新しい版を入れ、開き直します）";

    public sealed record Offer(string? ZipUrl, string? Sha256);

    public static Offer? Parse(string? releaseJson, string? currentVersionText)
    {
        if (string.IsNullOrWhiteSpace(releaseJson)) return null;
        if (!TryParseVersion(currentVersionText, out var current)) return null;

        try
        {
            using var doc = JsonDocument.Parse(releaseJson);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return null;
            if (!root.TryGetProperty("tag_name", out var tagProp) || tagProp.ValueKind != JsonValueKind.String) return null;
            if (!TryParseVersion(tagProp.GetString(), out var latest)) return null;
            if (latest <= current) return null;

            string? url = null, sha = null;
            if (root.TryGetProperty("assets", out var assets) && assets.ValueKind == JsonValueKind.Array)
            {
                foreach (var a in assets.EnumerateArray())
                {
                    if (a.ValueKind != JsonValueKind.Object) continue;
                    if (!a.TryGetProperty("name", out var n) || n.ValueKind != JsonValueKind.String
                        || n.GetString() != ZipAssetName) continue;
                    if (a.TryGetProperty("browser_download_url", out var u) && u.ValueKind == JsonValueKind.String
                        && u.GetString() is { } us && us.StartsWith(DownloadUrlPrefix, StringComparison.Ordinal))
                        url = us;
                    if (a.TryGetProperty("digest", out var d) && d.ValueKind == JsonValueKind.String
                        && d.GetString() is { } ds && ds.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase))
                    {
                        var hex = ds["sha256:".Length..].Trim().ToLowerInvariant();
                        if (hex.Length == 64 && hex.All(Uri.IsHexDigit)) sha = hex;
                    }
                    break;
                }
            }
            return new Offer(url, sha);
        }
        catch (JsonException)
        {
            return null;
        }
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
