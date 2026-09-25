using System.Globalization;
using System.Runtime.Versioning;
using TH09.Record;

using IOPath = System.IO.Path;

namespace TH09.Drive;

public sealed record BackupSource(string Label, string Base, string Destination,
                                  IReadOnlyList<string> Files, long Bytes);

public sealed record BackupRemoval(string Path, long Bytes, int Files);

public sealed record BackupPlan(string Root, IReadOnlyList<BackupSource> Sources, long Bytes,
                                long FreeBytes, IReadOnlyList<BackupRemoval> Removing,
                                IReadOnlyList<string> Kept,
                                string Destination, bool DestinationExists,
                                bool RemovalConfirmed,
                                IReadOnlyList<BackupRemoval> Stale, bool RemovalForced)
{
    public long RemovingBytes => Removing.Sum(r => r.Bytes);

    public long StaleBytes => Stale.Sum(r => r.Bytes);
}

[SupportedOSPlatform("windows")]
public static class ScanBackup
{
    public const string Layer0Label = "Layer 0";

    public const string MainDbLabel = "本体 DB";

    public const string Prefix = BackupLayout.Prefix;

    public const string StampFormat = BackupLayout.StampFormat;

    public const string ManifestName = BackupLayout.ManifestName;

    public const string Layer0Dir = "layer0";

    public const string MainDir = "main";

    public const string ReplayDir = "replay";

    public const string DropOldBackupsFlag = "--drop-old-backups";

    public const string DropOldBackupsHelp =
        "控えを取ったあと、前の回の控えを消す（★既定は残す。★消す前に大きさを出します）";

    public const string RemovalNotConfirmed =
        "★旧控えは消していません（消してよいなら " + DropOldBackupsFlag + " を付けてください）";

    public const string StaleNotConfirmed =
        "★控えと見做さないもの（未完成）も消していません";

    public const string ShrinkKeep =
        "★いま取った控えの方が極端に小さいので、旧控えは消していません"
        + "（本当に消してよいなら " + DropOldBackupsFlag + " を付けてください）";

    public const double ShrinkWarnRatio = 0.5;

    public const string KeepOldBackupsFlag = "--keep-old-backups";

    public const string KeepOldBackupsHelp =
        "前の回の控えを消さない（★設定 " + ConfigStore.BackupKeepOneKey + " をこの回だけ切る）";

    public const int StaleGraceMinutes = BackupLayout.StaleGraceMinutes;

    public static bool IsStaleBackup(string root, string path, DateTime now)
        => BackupLayout.IsStaleBackup(root, path, now);


    public sealed record Options(Paths? Paths = null, IReadOnlyList<string>? ReplayFiles = null,
                                 DateTime? Now = null, bool DropOldBackups = false,
                                 bool RemovalForced = false);

    public static BackupPlan Plan(Options o)
    {
        ArgumentNullException.ThrowIfNull(o);
        var paths = o.Paths ?? TH09.Record.Paths.Default;
        var root = paths.BackupRoot;
        var stamp = (o.Now ?? DateTime.Now).ToString(StampFormat, CultureInfo.InvariantCulture);
        var here = IOPath.Combine(root, Prefix + stamp);

        var sources = new List<BackupSource>
        {
            One(Layer0Label, paths.Layer0Db, IOPath.Combine(here, Layer0Dir)),
            One(MainDbLabel, paths.MainDb, IOPath.Combine(here, MainDir)),
            Many("リプレイ", o.ReplayFiles ?? [], IOPath.Combine(here, ReplayDir)),
        };
        var (removing, stale, kept) = Sweep(root, here, paths.Layer0Db, o.Now ?? DateTime.Now);
        return new BackupPlan(here, sources, sources.Sum(s => s.Bytes), FreeBytes(root),
                              removing, kept,
                              paths.Layer0Db, File.Exists(paths.Layer0Db),
                              o.DropOldBackups, stale, o.RemovalForced);
    }

    public static void Describe(TextWriter w, BackupPlan plan)
    {
        ArgumentNullException.ThrowIfNull(w);
        ArgumentNullException.ThrowIfNull(plan);
        var rule = new string('=', ScanTargets.RuleWidth);
        w.Write(rule + "\n");
        w.Write("控えを取ります: " + plan.Root + "\n");
        foreach (var s in plan.Sources)
        {
            w.Write("  " + s.Label + ": " + (s.Base.Length == 0 ? "（無し）" : s.Base)
                    + " → " + s.Destination
                    + "（" + Num(s.Files.Count) + " 本 / " + Size(s.Bytes) + "）\n");
        }
        w.Write("合計 " + Size(plan.Bytes) + " ／ 空き "
                + (plan.FreeBytes < 0 ? "不明" : Size(plan.FreeBytes)) + "\n");
        w.Write("旧控え: " + Num(plan.Removing.Count) + " 件"
                + (plan.Removing.Count == 0 ? "（無し）" : "（合計 " + Size(plan.RemovingBytes) + "）")
                + " ／ " + (plan.RemovalConfirmed
                            ? "★消します（" + DropOldBackupsFlag + "）"
                            : "残します（既定）") + "\n");
        foreach (var r in plan.Removing)
        {
            w.Write("  " + (plan.RemovalConfirmed ? "消す" : "残す") + ": " + r.Path
                    + "（" + Num(r.Files) + " 本 / " + Size(r.Bytes) + "）\n");
        }
        w.Write("控えと見做さないもの（未完成 ／ " + StaleGraceMinutes + " 分以上そのまま）: "
                + Num(plan.Stale.Count) + " 件"
                + (plan.Stale.Count == 0 ? "（無し）" : "（合計 " + Size(plan.StaleBytes) + "）")
                + (plan.Stale.Count == 0 ? "" : " ／ " + (plan.RemovalConfirmed
                                                         ? "★消します" : "残します"))
                + "\n");
        foreach (var r in plan.Stale)
        {
            w.Write("  " + (plan.RemovalConfirmed ? "消す" : "残す") + ": " + r.Path
                    + "（" + Num(r.Files) + " 本 / " + Size(r.Bytes) + " ／ 目印なし）\n");
        }
        if (Shrinking(plan.Bytes, plan.RemovingBytes))
            w.Write(ShrinkWarning(plan.Bytes, plan.RemovingBytes) + "\n");
        w.Write("消さないもの: " + Num(plan.Kept.Count) + " 件"
                + (plan.Kept.Count == 0 ? "（無し）" : "") + "\n");
        foreach (var line in plan.Kept) w.Write("  残す: " + line + "\n");
        w.Write("いま生きている Layer 0: " + plan.Destination
                + (plan.DestinationExists
                   ? "（★あります ——空にする走り方は隣に建てて最後に入れ替え ／ "
                     + "追記の走り方（新規のみ・上書き）はここへ直接書き足します）"
                   : "（★ありません ——最初の走査です）") + "\n");
        w.Write(rule + "\n");
    }

    public static BackupPlan Run(TextWriter w, BackupPlan plan)
    {
        ArgumentNullException.ThrowIfNull(w);
        ArgumentNullException.ThrowIfNull(plan);
        Describe(w, plan);
        if (plan.FreeBytes >= 0 && plan.FreeBytes < plan.Bytes)
        {
            throw new ScanSetupFailed(
                "エラー: 控えの置き先に空きが足りません（要 " + Size(plan.Bytes)
                + " / 空き " + Size(plan.FreeBytes) + "）。");
        }

        foreach (var s in plan.Sources)
        {
            if (s.Label != Layer0Label && s.Label != MainDbLabel) continue;
            foreach (var file in s.Files)
                w.Write(RecordDb.Checkpoint(file).Describe(s.Label) + Lf);
        }

        Directory.CreateDirectory(plan.Root);
        var copied = 0;
        foreach (var s in plan.Sources)
        {
            foreach (var file in s.Files)
            {
                var rel = IOPath.GetRelativePath(s.Base, file);
                var how = CopyOne(file, IOPath.Combine(s.Destination, rel));
                copied++;
                if (s.Label == Layer0Label || s.Label == MainDbLabel)
                    w.Write("[copy] " + s.Label + ": " + how + Lf);
            }
        }
        var manifest = new List<string>
        {
            "tool=th09_drive --scan --backup",
            "at=" + DateTime.Now.ToString("o", CultureInfo.InvariantCulture),
        };
        manifest.AddRange(plan.Sources.Select(
            s => "source=" + s.Label + "\t" + s.Base + "\t" + Num(s.Files.Count)
                 + "\t" + Num(s.Bytes)));
        File.WriteAllLines(IOPath.Combine(plan.Root, ManifestName), manifest);
        var taken = TakenBytes(plan.Root);
        w.Write("控えを取りました: " + plan.Root
                + "（" + Num(copied) + " 本 / " + Size(taken) + "）\n");

        if (Shrinking(taken, plan.RemovingBytes))
            w.Write(ShrinkWarning(taken, plan.RemovingBytes) + "\n");
        if (!plan.RemovalConfirmed)
        {
            if (plan.Removing.Count > 0)
            {
                w.Write(RemovalNotConfirmed + ": " + Num(plan.Removing.Count) + " 件 / "
                        + Size(plan.RemovingBytes) + "\n");
            }
            if (plan.Stale.Count > 0)
            {
                w.Write(StaleNotConfirmed + ": " + Num(plan.Stale.Count) + " 件 / "
                        + Size(plan.StaleBytes) + "\n");
            }
            return plan;
        }
        var root = IOPath.GetDirectoryName(plan.Root)!;
        BackupLayout.TidyStale(
            root, DateTime.Now,
            f => w.Write("未完成の控えを消しました: " + f.Path
                         + "（" + Num(f.Files) + " 本 / " + Size(f.Bytes) + " ／ 目印なし）" + Lf),
            why => w.Write("★" + why + Lf));
        if (Shrinking(taken, plan.RemovingBytes) && !plan.RemovalForced && plan.Removing.Count > 0)
        {
            w.Write(ShrinkKeep + ": " + Num(plan.Removing.Count) + " 件 / "
                    + Size(plan.RemovingBytes) + "\n");
            return plan;
        }
        foreach (var r in plan.Removing)
        {
            if (!IsOwnBackup(root, r.Path) || SamePath(r.Path, plan.Root))
            {
                throw new ScanSetupFailed(
                    "エラー: 消してよいと名指しできないものが予定に入っています: " + r.Path);
            }
            Directory.Delete(r.Path, recursive: true);
            w.Write("旧控えを消しました: " + r.Path + "（" + Size(r.Bytes) + "）\n");
        }
        return plan;
    }

    public static string? BlockedByExistingLayer0(Paths? paths)
    {
        var p = paths ?? TH09.Record.Paths.Default;
        if (!File.Exists(p.Layer0Db)) return null;
        return "エラー: 走査の行き先（Layer 0）が既にあります: " + p.Layer0Db + Lf
             + "       この道具は空から建てます（上書きしません）。" + Lf
             + "       控えを取ってから、このファイルを退けて（消すか改名して）ください。" + Lf
             + "       控えがまだなら --backup を先に走らせてください。";
    }

    public static bool IsOwnBackup(string root, string path)
        => BackupLayout.IsOwnBackup(root, path);

    public static string? LatestBackupOf(string root, string layer0Db)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(layer0Db);
        if (!Directory.Exists(root) || !File.Exists(layer0Db)) return null;
        var want = RecordDb.DatabaseBytes(layer0Db);
        var name = IOPath.GetFileName(layer0Db);
        string? best = null;
        foreach (var entry in Ordered(Directory.EnumerateFileSystemEntries(root)))
        {
            if (!IsOwnBackup(root, entry)) continue;
            var copy = IOPath.Combine(entry, Layer0Dir, name);
            if (!File.Exists(copy) || new FileInfo(copy).Length != want) continue;
            best = entry;
        }
        return best;
    }


    private static (IReadOnlyList<BackupRemoval> Removing, IReadOnlyList<BackupRemoval> Stale,
                    IReadOnlyList<string> Kept) Sweep(
        string root, string here, string layer0Db, DateTime now)
    {
        var removing = new List<BackupRemoval>();
        var stale = new List<BackupRemoval>();
        var kept = new List<string>();
        if (Directory.Exists(root))
        {
            foreach (var entry in Ordered(Directory.EnumerateFileSystemEntries(root)))
            {
                if (SamePath(entry, here)) continue;
                if (IsOwnBackup(root, entry)) removing.Add(Weigh(entry));
                else if (IsStaleBackup(root, entry, now)) stale.Add(Weigh(entry));
                else kept.Add(entry + "（この道具が作った控えではありません）");
            }
        }
        var dir = IOPath.GetDirectoryName(IOPath.GetFullPath(layer0Db));
        var stem = IOPath.GetFileNameWithoutExtension(layer0Db);
        if (dir is not null && Directory.Exists(dir))
        {
            foreach (var file in Ordered(Directory.EnumerateFiles(dir)))
            {
                if (SamePath(file, layer0Db)) continue;
                var name = IOPath.GetFileName(file);
                if (!name.StartsWith(stem, StringComparison.OrdinalIgnoreCase)) continue;
                if (name.EndsWith("-wal", StringComparison.OrdinalIgnoreCase)
                    || name.EndsWith("-shm", StringComparison.OrdinalIgnoreCase)) continue;
                kept.Add(file + Label(name));
            }
        }
        return (removing, stale, kept);
    }

    private static string Label(string name)
    {
        if (name.Contains(Layer0Swap.RetiredMark, StringComparison.OrdinalIgnoreCase))
            return "（この道具が退けた古い Layer 0。★消していません）";
        if (name.Contains(Layer0Swap.ScanningMark, StringComparison.OrdinalIgnoreCase))
            return "（入れ替えに至らなかった走査の Layer 0。★中身は人が見て決められます）";
        return "（Layer 0 の隣の旧控え。★検査が読んでいるものが混ざっています）";
    }

    private static BackupRemoval Weigh(string path)
    {
        try
        {
            var files = Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories).ToList();
            return new BackupRemoval(path, files.Sum(f => new FileInfo(f).Length), files.Count);
        }
        catch (Exception)
        {
            return new BackupRemoval(path, 0L, 0);
        }
    }

    private static long TakenBytes(string root) => Weigh(root).Bytes;

    public static bool Shrinking(long taken, long removing) =>
        removing > 0 && taken < removing * ShrinkWarnRatio;

    public static string ShrinkWarning(long taken, long removing) =>
        "★注意: いま取った控え（" + Size(taken) + "）は、消そうとしている旧控え（"
        + Size(removing) + "）より小さいです。"
        + "走査が空振りしていると、中身のある控えを中身の無い控えで置き換えることになります。";

    private static BackupSource One(string label, string source, string destination)
    {
        var exists = File.Exists(source);
        return new BackupSource(
            label, exists ? IOPath.GetDirectoryName(IOPath.GetFullPath(source)) ?? "" : "",
            destination, exists ? [source] : [],
            exists ? new FileInfo(source).Length : 0L);
    }

    private static BackupSource Many(string label, IReadOnlyList<string> files, string destination)
    {
        var live = Ordered(files.Where(File.Exists).Select(IOPath.GetFullPath));
        return new BackupSource(label, CommonBase(live), destination, live,
                                live.Sum(f => new FileInfo(f).Length));
    }

    private static string CommonBase(IReadOnlyList<string> files)
    {
        if (files.Count == 0) return "";
        var parts = IOPath.GetDirectoryName(files[0])!.Split(IOPath.DirectorySeparatorChar);
        foreach (var file in files)
        {
            var other = IOPath.GetDirectoryName(file)!.Split(IOPath.DirectorySeparatorChar);
            var n = 0;
            while (n < parts.Length && n < other.Length
                   && string.Equals(parts[n], other[n], StringComparison.OrdinalIgnoreCase)) n++;
            parts = parts[..n];
        }
        return string.Join(IOPath.DirectorySeparatorChar, parts);
    }

    private static string CopyOne(string source, string destination)
    {
        Directory.CreateDirectory(IOPath.GetDirectoryName(destination)!);
        if (RecordDb.IsSqliteFile(source))
        {
            RecordDb.BackupTo(source, destination);
            return CopiedByBackupApi;
        }
        File.Copy(source, destination, overwrite: false);
        return CopiedByFileCopy;
    }

    public const string CopiedByBackupApi = "backup API";

    public const string CopiedByFileCopy = "File.Copy";

    private static long FreeBytes(string root)
    {
        try
        {
            var drive = IOPath.GetPathRoot(IOPath.GetFullPath(root));
            return string.IsNullOrEmpty(drive) ? -1L : new DriveInfo(drive).AvailableFreeSpace;
        }
        catch (Exception)
        {
            return -1L;
        }
    }

    private static List<string> Ordered(IEnumerable<string> seq) =>
        [.. seq.OrderBy(x => x, StringComparer.Ordinal)];

    private static bool SamePath(string a, string b) =>
        string.Equals(IOPath.GetFullPath(a).TrimEnd(IOPath.DirectorySeparatorChar),
                      IOPath.GetFullPath(b).TrimEnd(IOPath.DirectorySeparatorChar),
                      StringComparison.OrdinalIgnoreCase);

    public static string Size(long bytes)
    {
        if (bytes < 1024) return Num(bytes) + " B";
        double v = bytes;
        foreach (var unit in new[] { "KiB", "MiB", "GiB" })
        {
            v /= 1024.0;
            if (v < 1024.0) return ScanTargets.F(v, 1) + " " + unit;
        }
        return ScanTargets.F(v / 1024.0, 1) + " TiB";
    }

    private static readonly string Lf = ((char)10).ToString();

    private static string Num(long value) => value.ToString(CultureInfo.InvariantCulture);
}
