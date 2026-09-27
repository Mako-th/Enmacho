using System.Text.RegularExpressions;

namespace TH09.Replay;

public static partial class ReplayOwner
{
    public static readonly string[] DefaultOwnNames = [];

    public const string SourceAdonis = "Adonis";

    public const string SourceGame = "Game";

    [GeneratedRegex(@"^.+\s+vs\s+.+$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex AdonisFolder();

    [GeneratedRegex(@"^\d{1,2}:\d{2}:\d{2}$", RegexOptions.CultureInvariant)]
    private static partial Regex AdonisTimeName();

    public static IReadOnlyList<string> OwnNames(string? playerName, IReadOnlyList<string>? ownPlayerNames)
    {
        var names = new List<string>();
        if (playerName is not null)
        {
            var legacy = ReplayDecode.PyStrip(playerName);
            if (legacy.Length > 0) names.Add(legacy);
        }
        IReadOnlyList<string> raw =
            ownPlayerNames ?? (names.Count == 0 ? DefaultOwnNames : Array.Empty<string>());
        foreach (var item in raw)
        {
            if (item is null) continue;
            var n = ReplayDecode.PyStrip(item);
            if (n.Length == 0) continue;
            if (!names.Contains(n, StringComparer.Ordinal)) names.Add(n);
        }
        return names;
    }

    public static (bool IgnoreCase, bool Partial) OwnMatchOptions(bool? ignoreCase, bool? partial) =>
        (ignoreCase ?? true, partial ?? true);

    public static int? OwnerSide(ReplayResult decoded, string? fullPath, IReadOnlyList<string>? names,
                                 bool ignoreCase = true, bool partial = true) =>
        OwnerSide(decoded.P1Name, decoded.P2Name, decoded.Name, fullPath, names, ignoreCase, partial);

    public static int? OwnerSide(string? p1Name, string? p2Name, string? name, string? fullPath,
                                 IReadOnlyList<string>? names, bool ignoreCase = true, bool partial = true)
    {
        if (names is null) return null;
        var usable = false;
        foreach (var n in names)
        {
            if (n is not null && ReplayDecode.PyStrip(n).Length > 0) { usable = true; break; }
        }
        if (!usable) return null;

        var p1 = p1Name;
        var p2 = p2Name;
        if (p1 is null && p2 is null)
        {
            (p1, p2) = ReplayDecode.MatchPlayerNames(fullPath);
        }
        if (NameHit(p1, names, ignoreCase, partial)) return 1;
        if (NameHit(p2, names, ignoreCase, partial)) return 2;
        if (NameHit(name, names, ignoreCase, partial)) return 1;
        return 0;
    }

    public static string ReplaySource(string? fullPath, ReplayResult? decoded = null) =>
        ReplaySourceOf(fullPath, decoded?.Name);

    public static string ReplaySourceOf(string? fullPath, string? decodedName)
    {
        var parent = ParentName(fullPath);
        var name = decodedName is null ? "" : ReplayDecode.PyStrip(decodedName);
        return parent.Length > 0 && AdonisFolder().IsMatch(parent) && AdonisTimeName().IsMatch(name)
            ? SourceAdonis : SourceGame;
    }

    private static bool NameHit(string? candidate, IReadOnlyList<string> names, bool ignoreCase, bool partial)
    {
        if (candidate is null) return false;
        var v = ReplayDecode.PyStrip(candidate);
        if (v.Length == 0) return false;
        if (ignoreCase) v = v.ToLowerInvariant();
        foreach (var item in names)
        {
            if (item is null) continue;
            var n = ReplayDecode.PyStrip(item);
            if (ignoreCase) n = n.ToLowerInvariant();
            if (n.Length == 0) continue;
            if (partial ? v.Contains(n, StringComparison.Ordinal)
                        : string.Equals(v, n, StringComparison.Ordinal)) return true;
        }
        return false;
    }

    private static string ParentName(string? fullPath)
    {
        if (string.IsNullOrEmpty(fullPath)) return "";
        var dir = Path.GetDirectoryName(fullPath);
        return string.IsNullOrEmpty(dir) ? "" : new DirectoryInfo(dir).Name;
    }
}
