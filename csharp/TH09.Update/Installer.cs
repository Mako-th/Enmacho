using System.IO.Compression;
using System.Security.Cryptography;

namespace TH09.Update;

internal static class Installer
{
    public const string DownloadUrlPrefix = "https://github.com/Mako-th/Enmacho/releases/download/";

    public const string ZipTop = "Enmacho";

    public const string BackupDirName = "update_backup";

    public const string LogFileName = "update.log";

    public static readonly string[] ProtectedNames =
    [
        "data",
        "replay",
        "perf",
        "replay_paths.txt",
        "game_dir.txt",
        BackupDirName,
        LogFileName,
    ];

    public static readonly string[] ProtectedPrefixes =
    [
        "th09_tracker.sqlite3",
        "config.json",
        "ui_settings.json",
        "crash.log",
    ];

    public static bool IsProtected(string relativePath)
    {
        string top = relativePath.Replace('\\', '/').TrimStart('/').Split('/')[0];
        foreach (var n in ProtectedNames)
            if (string.Equals(top, n, StringComparison.OrdinalIgnoreCase)) return true;
        foreach (var p in ProtectedPrefixes)
            if (top.StartsWith(p, StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    public static bool IsAcceptedUrl(string? url)
    {
        if (string.IsNullOrEmpty(url) || !url.StartsWith(DownloadUrlPrefix, StringComparison.Ordinal)) return false;
        if (!Uri.TryCreate(url, UriKind.Absolute, out var u)) return false;
        return u.Scheme == Uri.UriSchemeHttps
               && string.Equals(u.Host, "github.com", StringComparison.OrdinalIgnoreCase)
               && string.IsNullOrEmpty(u.UserInfo)
               && u.AbsolutePath.StartsWith("/Mako-th/Enmacho/releases/download/", StringComparison.Ordinal);
    }

    public static string? RefuseDestination(string dest)
    {
        if (!Directory.Exists(dest)) return "入れ替え先のフォルダがありません: " + dest;
        for (var d = new DirectoryInfo(Path.GetFullPath(dest)); d is not null; d = d.Parent)
        {
            if (Directory.Exists(Path.Combine(d.FullName, "csharp", "TH09.Shell"))
                || (Directory.Exists(Path.Combine(d.FullName, "python"))
                    && Directory.Exists(Path.Combine(d.FullName, "project_material_documents"))))
                return "開発用のフォルダの中には入れ替えません: " + d.FullName;
        }
        return null;
    }

    public static string Sha256Of(string path)
    {
        using var s = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(s)).ToLowerInvariant();
    }

    public sealed record Staged(string Root, List<string> Files, string? Error);

    public static Staged Extract(string zipPath, string stageDir, string shellExe)
    {
        var files = new List<string>();
        try
        {
            using var zip = ZipFile.OpenRead(zipPath);
            var names = new List<(ZipArchiveEntry E, string Rel)>();
            foreach (var e in zip.Entries)
            {
                string full = e.FullName.Replace('\\', '/');
                if (!full.StartsWith(ZipTop + "/", StringComparison.Ordinal))
                    return Bad(stageDir, "zip の中身が想定の形ではありません（" + ZipTop + "/ の下ではない名前: " + full + "）");
                string rel = full[(ZipTop.Length + 1)..];
                if (rel.Length == 0) continue;
                var segs = rel.TrimEnd('/').Split('/');
                if (segs.Any(s => s is "" or "." or ".." || s.Contains(':')))
                    return Bad(stageDir, "zip の名前が安全ではありません: " + full);
                if (IsProtected(rel))
                    return Bad(stageDir, "zip に記録・設定の名前が入っているので中止します: " + full);
                if (!full.EndsWith('/')) names.Add((e, rel));
            }

            if (!names.Any(n => string.Equals(n.Rel, shellExe, StringComparison.OrdinalIgnoreCase)))
                return Bad(stageDir, "zip に本体（" + shellExe + "）が見つかりません");

            Directory.CreateDirectory(stageDir);
            foreach (var (e, rel) in names)
            {
                string to = Path.Combine(stageDir, rel.Replace('/', Path.DirectorySeparatorChar));
                Directory.CreateDirectory(Path.GetDirectoryName(to)!);
                e.ExtractToFile(to, overwrite: true);
                files.Add(rel);
            }
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException or UnauthorizedAccessException)
        {
            return Bad(stageDir, "zip を展開できません: " + ex.Message);
        }

        files.Sort(StringComparer.Ordinal);
        return new Staged(stageDir, files, null);

        static Staged Bad(string root, string why) => new(root, [], why);
    }

    public static List<string> Locked(string dest, IEnumerable<string> relFiles)
    {
        var locked = new List<string>();
        foreach (var rel in relFiles)
        {
            string p = Path.Combine(dest, rel.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(p)) continue;
            try
            {
                using var fs = new FileStream(p, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                locked.Add(rel);
            }
        }
        return locked;
    }

    internal static int RetryCount = 20;
    internal static TimeSpan RetryDelay = TimeSpan.FromMilliseconds(250);

    private static void Retry(Action act)
    {
        for (int i = 1; ; i++)
        {
            try { act(); return; }
            catch (Exception ex) when ((ex is IOException or UnauthorizedAccessException) && i < RetryCount)
            {
                Thread.Sleep(RetryDelay);
            }
        }
    }

    public sealed record ApplyResult(bool Ok, string? Error, bool RollbackFailed, int Written, int Restored);

    public static ApplyResult Apply(string dest, Staged staged, Action<string> log)
    {
        string backup = Path.Combine(dest, BackupDirName);
        var created = new List<string>();
        var touched = new List<(string Rel, bool Existed)>();

        try
        {
            if (Directory.Exists(backup)) Directory.Delete(backup, recursive: true);
            Directory.CreateDirectory(backup);
            foreach (var rel in staged.Files)
            {
                string cur = Path.Combine(dest, rel.Replace('/', Path.DirectorySeparatorChar));
                if (!File.Exists(cur)) continue;
                string to = Path.Combine(backup, rel.Replace('/', Path.DirectorySeparatorChar));
                Directory.CreateDirectory(Path.GetDirectoryName(to)!);
                Retry(() => File.Copy(cur, to, overwrite: true));
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return new ApplyResult(false, "控えを取れませんでした（何も書き換えていません）: " + ex.Message, false, 0, 0);
        }
        log("控えを取りました: " + backup);

        try
        {
            foreach (var rel in staged.Files)
            {
                string from = Path.Combine(staged.Root, rel.Replace('/', Path.DirectorySeparatorChar));
                string to = Path.Combine(dest, rel.Replace('/', Path.DirectorySeparatorChar));
                MakeDirs(Path.GetDirectoryName(to)!, dest, created);
                touched.Add((rel, File.Exists(to)));
                Retry(() => File.Copy(from, to, overwrite: true));
            }
            return new ApplyResult(true, null, false, touched.Count, 0);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            log("入れ替えに失敗しました: " + ex.Message + " ——控えから戻します");
            int restored = 0;
            bool failed = false;
            for (int i = touched.Count - 1; i >= 0; i--)
            {
                var (rel, existed) = touched[i];
                string to = Path.Combine(dest, rel.Replace('/', Path.DirectorySeparatorChar));
                try
                {
                    if (existed)
                    {
                        string from = Path.Combine(backup, rel.Replace('/', Path.DirectorySeparatorChar));
                        Retry(() => File.Copy(from, to, overwrite: true));
                        restored++;
                    }
                    else if (File.Exists(to))
                    {
                        Retry(() => File.Delete(to));
                    }
                }
                catch (Exception ex2) when (ex2 is IOException or UnauthorizedAccessException)
                {
                    failed = true;
                    log("戻せませんでした: " + rel + "（" + ex2.Message + "）");
                }
            }
            for (int i = created.Count - 1; i >= 0; i--)
            {
                try
                {
                    if (Directory.Exists(created[i]) && !Directory.EnumerateFileSystemEntries(created[i]).Any())
                        Directory.Delete(created[i]);
                }
                catch (Exception ex3) when (ex3 is IOException or UnauthorizedAccessException) { }
            }
            return new ApplyResult(false, ex.Message, failed, touched.Count, restored);
        }
    }

    private static void MakeDirs(string dir, string dest, List<string> created)
    {
        if (Directory.Exists(dir)) return;
        var parent = Path.GetDirectoryName(dir);
        if (parent is not null && !string.Equals(parent, dest, StringComparison.OrdinalIgnoreCase))
            MakeDirs(parent, dest, created);
        Directory.CreateDirectory(dir);
        created.Add(dir);
    }
}
