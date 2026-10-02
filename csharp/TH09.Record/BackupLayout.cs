using System.Globalization;
using System.Runtime.Versioning;

using IOPath = System.IO.Path;

namespace TH09.Record;

public sealed record BackupFolder(string Path, long Bytes, int Files);

public sealed record BackupEntry(BackupFolder Folder, bool Complete);

public sealed record BackupInventory(IReadOnlyList<BackupEntry> Entries, int InProgress);

public sealed record BackupRemoveResult(BackupFolder? Removed, string? Reason);

[SupportedOSPlatform("windows")]
public static class BackupLayout
{
    public const string Prefix = "scan_";

    public const string StampFormat = "yyyyMMdd_HHmmss";

    public const string ManifestName = "th09_scan_backup.txt";

    public const int StaleGraceMinutes = 10;

    public static bool IsOwnBackup(string root, string path)
        => Shaped(root, path) && File.Exists(IOPath.Combine(path, ManifestName));

    public static bool IsStaleBackup(string root, string path, DateTime now)
        => Shaped(root, path)
           && !File.Exists(IOPath.Combine(path, ManifestName))
           && LastWrite(path) <= now.AddMinutes(-StaleGraceMinutes);

    private static bool Shaped(string root, string path)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(path);
        if (!Directory.Exists(path)) return false;
        var parent = IOPath.GetDirectoryName(IOPath.GetFullPath(path));
        if (parent is null || !SamePath(parent, root)) return false;
        var name = IOPath.GetFileName(IOPath.GetFullPath(path));
        if (!name.StartsWith(Prefix, StringComparison.Ordinal)) return false;
        var stamp = name[Prefix.Length..];
        return DateTime.TryParseExact(stamp, StampFormat, CultureInfo.InvariantCulture,
                                      DateTimeStyles.None, out _);
    }

    public static IReadOnlyList<BackupFolder> TidyStale(
        string root, DateTime now,
        Action<BackupFolder>? onRemoved = null, Action<string>? onFailed = null)
    {
        ArgumentNullException.ThrowIfNull(root);
        var removed = new List<BackupFolder>();
        if (!Directory.Exists(root)) return removed;
        foreach (var entry in Directory.EnumerateDirectories(root).Order(StringComparer.Ordinal))
        {
            if (!IsStaleBackup(root, entry, now)) continue;
            var weighed = Weigh(entry);
            try
            {
                Directory.Delete(entry, recursive: true);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                onFailed?.Invoke("未完成のバックアップを消せませんでした: " + entry
                                 + "（" + ex.GetType().Name + ": " + ex.Message + "）");
                continue;
            }
            removed.Add(weighed);
            onRemoved?.Invoke(weighed);
        }
        return removed;
    }

    public static BackupInventory List(string root, DateTime now)
    {
        ArgumentNullException.ThrowIfNull(root);
        var entries = new List<BackupEntry>();
        var inProgress = 0;
        if (!Directory.Exists(root)) return new BackupInventory(entries, inProgress);
        foreach (var entry in Directory.EnumerateDirectories(root).Order(StringComparer.Ordinal))
        {
            if (IsOwnBackup(root, entry)) entries.Add(new BackupEntry(Weigh(entry), true));
            else if (IsStaleBackup(root, entry, now)) entries.Add(new BackupEntry(Weigh(entry), false));
            else if (Shaped(root, entry)) inProgress++;
        }
        return new BackupInventory(entries, inProgress);
    }

    public static bool ScanIsActiveElsewhere() => ScanLedger.ScanIsActive();

    public static BackupRemoveResult Remove(string root, string path, DateTime now)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(path);
        if (!IsOwnBackup(root, path) && !IsStaleBackup(root, path, now))
            return new BackupRemoveResult(null, "消してよいバックアップではありません（完成したバックアップでも、"
                                                + "10 分以上動いていない未完成のバックアップでもありません）: " + path);
        var weighed = Weigh(path);
        try
        {
            Directory.Delete(IOPath.GetFullPath(path), recursive: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return new BackupRemoveResult(null, "消せませんでした: " + path
                                                + "（" + ex.GetType().Name + ": " + ex.Message + "）");
        }
        return new BackupRemoveResult(weighed, null);
    }

    public static string Size(long bytes)
    {
        if (bytes < 1024) return bytes.ToString(CultureInfo.InvariantCulture) + " B";
        double v = bytes;
        foreach (var unit in new[] { "KiB", "MiB", "GiB" })
        {
            v /= 1024.0;
            if (v < 1024.0) return v.ToString("F1", CultureInfo.InvariantCulture) + " " + unit;
        }
        return (v / 1024.0).ToString("F1", CultureInfo.InvariantCulture) + " TiB";
    }

    public static DateTime LastWrite(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        var newest = Directory.GetLastWriteTime(path);
        foreach (var f in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
        {
            var t = File.GetLastWriteTime(f);
            if (t > newest) newest = t;
        }
        return newest;
    }

    public static BackupFolder Weigh(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        long bytes = 0;
        var files = 0;
        if (Directory.Exists(path))
        {
            foreach (var f in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
            {
                files++;
                try { bytes += new FileInfo(f).Length; }
                catch (IOException) { }
            }
        }
        return new BackupFolder(path, bytes, files);
    }

    private static bool SamePath(string a, string b) =>
        string.Equals(IOPath.GetFullPath(a).TrimEnd(IOPath.DirectorySeparatorChar),
                      IOPath.GetFullPath(b).TrimEnd(IOPath.DirectorySeparatorChar),
                      StringComparison.OrdinalIgnoreCase);
}
