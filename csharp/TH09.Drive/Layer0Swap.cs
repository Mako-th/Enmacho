using System.Globalization;
using System.Runtime.Versioning;

using IOPath = System.IO.Path;

namespace TH09.Drive;

public sealed record ScanOutcome(int Ok, int Mismatch, int Empty, int Failed,
                                 int Ran, int Total, string? StoppedBy)
{
    public static ScanOutcome NotRun { get; } =
        new(0, 0, 0, 0, 0, 0, "走り切れませんでした（走り出す前に止まったか、例外で抜けたか）");

    public int Recorded => Ok + Mismatch;

    public string Summary =>
        "対象 " + N(Total) + " 本 ／ 走った " + N(Ran) + " 本 ／ 記録 " + N(Recorded)
        + " 本（成功 " + N(Ok) + " ＋ 突合ずれ " + N(Mismatch) + "）／ 中身が空 " + N(Empty)
        + " 本 ／ 失敗 " + N(Failed) + " 本";

    public static ScanOutcome FromOne(int exitCode, string? problem)
    {
        if (!string.IsNullOrEmpty(problem)) return new(0, 0, 1, 0, 1, 1, null);
        if (exitCode == 0) return new(1, 0, 0, 0, 1, 1, null);
        if (exitCode == 2) return new(0, 1, 0, 0, 1, 1, null);
        return new(0, 0, 0, 1, 1, 1, null);
    }

    public static ScanOutcome FromBatch(ScanBatchResult r)
    {
        ArgumentNullException.ThrowIfNull(r);
        return new(r.Ok, r.Mismatch, r.Empty, r.Failed, r.Ran, r.Total, r.StoppedBy);
    }

    private static string N(long value) => value.ToString(CultureInfo.InvariantCulture);
}

public sealed record Layer0Ready(string Live, string Destination, string Stamp,
                                 bool BackupRequested, BackupPlan? Backup, bool DropRetired,
                                 bool Emptying, bool Appending = false)
{
    public string? BackupRoot => Backup?.Root;
}

public sealed record Layer0SwapResult(bool Swapped, string? Blocked, string? Retired,
                                      int Sidecars, string? Dropped);

[SupportedOSPlatform("windows")]
public static class Layer0Swap
{
    public const string RetiredMark = ".retired_";

    public const string ScanningMark = ".scanning_";

    public static readonly string[] SidecarSuffixes = ["-wal", "-shm"];

    public const string DropOldFlag = "--drop-old-layer0";

    public const string DropOldHelp =
        "走査が最後まで行って入れ替えたとき、退けた古い Layer 0 を消す（既定は残す）";

    public const string NotEmptying =
        "この走り方は Layer 0 を入れ替えません（入れ替えるのは「空にして走査」を選んだときだけです）";

    public const string NotSwappingAppend =
        "この回は追記なので、入れ替えるものがありません（走査は最初から本来の名前へ書いています）";

    public const string NoBackupAndDropWarning =
        "★注意: 控えを取らない指定と、退けた古い Layer 0 を消す指定が同時に指定されています。"
        + "走査が最後まで行って入れ替えた時点で、これまでの走査記録は戻せなくなります。";


    public static string Scanning(string layer0Db, string stamp) =>
        Beside(layer0Db, ScanningMark, stamp);

    public static string Retired(string layer0Db, string stamp) =>
        Beside(layer0Db, RetiredMark, stamp);


    public static string? Blocked(Layer0Ready ready, ScanOutcome outcome)
    {
        ArgumentNullException.ThrowIfNull(ready);
        ArgumentNullException.ThrowIfNull(outcome);
        if (ready.Appending) return NotSwappingAppend;
        if (!ready.Emptying) return NotEmptying;
        if (outcome.StoppedBy is { } stopped)
            return "走査が最後まで行きませんでした（" + stopped + "）";
        if (outcome.Recorded <= 0)
            return "新しい Layer 0 に記録が 1 本も入っていません（" + outcome.Summary + "）";
        if (!File.Exists(ready.Destination))
            return "今回の走査の Layer 0 が建っていません: " + ready.Destination;
        if (BackupUnusable(ready) is { } why)
            return why;
        if (Occupied(ready) is { } taken)
            return "退ける先の名前が既にふさがっています: " + taken;
        if (StillOpen(ready) is { } held)
            return "まだ開かれているので動かせません: " + held;
        return null;
    }

    public static string? BackupIncomplete(BackupPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        var manifest = IOPath.Combine(plan.Root, ScanBackup.ManifestName);
        if (!File.Exists(manifest)) return "目印がありません: " + manifest;
        foreach (var s in plan.Sources)
        {
            foreach (var file in s.Files)
            {
                var copy = IOPath.Combine(s.Destination, IOPath.GetRelativePath(s.Base, file));
                if (!File.Exists(copy)) return "控えに入っていません: " + file;
                if (new FileInfo(copy).Length != TH09.Record.RecordDb.DatabaseBytes(file))
                    return "大きさが違います: " + file;
            }
        }
        return null;
    }

    public static string? BackupUnusable(Layer0Ready ready)
    {
        ArgumentNullException.ThrowIfNull(ready);
        if (!ready.BackupRequested) return null;
        if (ready.Backup is not { } plan)
            return "控えを取る指定でしたが、控えが 1 つも作られていません";
        var manifest = IOPath.Combine(plan.Root, ScanBackup.ManifestName);
        if (!File.Exists(manifest))
            return "控えの目印がありません（走っている間に消えた可能性）: " + manifest;
        if (!File.Exists(ready.Live)) return null;
        var copy = IOPath.Combine(plan.Root, ScanBackup.Layer0Dir,
                                  IOPath.GetFileName(ready.Live));
        if (!File.Exists(copy))
            return "控えの中に Layer 0 がありません: " + copy;
        var was = new FileInfo(copy).Length;
        var now = TH09.Record.RecordDb.DatabaseBytes(ready.Live);
        if (was != now)
        {
            return "控えの中の Layer 0 の大きさが、いまの Layer 0 と違います（控え "
                   + ScanBackup.Size(was) + " ／ いま " + ScanBackup.Size(now) + "）";
        }
        return null;
    }


    public static Layer0SwapResult Finish(TextWriter w, Layer0Ready ready, ScanOutcome outcome)
    {
        ArgumentNullException.ThrowIfNull(w);
        ArgumentNullException.ThrowIfNull(ready);
        ArgumentNullException.ThrowIfNull(outcome);
        var blocked = Blocked(ready, outcome);
        Describe(w, ready, outcome, blocked);
        if (blocked is not null)
        {
            return new Layer0SwapResult(false, blocked, null, 0, null);
        }

        string? retired = null;
        var sidecars = 0;
        if (File.Exists(ready.Live))
        {
            retired = Retired(ready.Live, ready.Stamp);
            File.Move(ready.Live, retired);
            sidecars += MoveSidecars(ready.Live, retired);
        }
        File.Move(ready.Destination, ready.Live);
        sidecars += MoveSidecars(ready.Destination, ready.Live);
        w.Write("Layer 0 を入れ替えました: " + ready.Destination + " → " + ready.Live
                + (retired is null ? "（退ける相手はありませんでした）"
                                   : "（古い方は " + retired + " へ退けました）")
                + "（連れ " + N(sidecars) + " 本）" + Lf);

        string? dropped = null;
        if (ready.DropRetired && retired is not null)
        {
            var bytes = File.Exists(retired) ? new FileInfo(retired).Length : 0L;
            File.Delete(retired);
            foreach (var suffix in SidecarSuffixes)
            {
                var side = retired + suffix;
                if (File.Exists(side)) File.Delete(side);
            }
            dropped = retired;
            w.Write("退けた古い Layer 0 を消しました: " + retired
                    + "（" + ScanBackup.Size(bytes) + "）" + Lf);
        }
        return new Layer0SwapResult(true, null, retired, sidecars, dropped);
    }

    public static void Describe(TextWriter w, Layer0Ready ready, ScanOutcome outcome,
                                string? blocked)
    {
        ArgumentNullException.ThrowIfNull(w);
        ArgumentNullException.ThrowIfNull(ready);
        ArgumentNullException.ThrowIfNull(outcome);
        var rule = new string('=', ScanTargets.RuleWidth);
        w.Write(rule + Lf);
        w.Write("走査の結果: " + outcome.Summary + Lf);
        w.Write("止まった理由: " + (outcome.StoppedBy ?? "（最後まで走りました）") + Lf);
        w.Write("  いま生きている Layer 0: " + ready.Live + Where(ready.Live) + Lf);
        w.Write("  今回の走査の Layer 0  : " + ready.Destination + Where(ready.Destination)
                + (ready.Appending ? "（★生きている Layer 0 そのもの ——書き足しました）" : "") + Lf);
        w.Write("  走り方: " + (ready.Emptying
                                ? "空にして走査（最後まで行けば入れ替えます）"
                                : ready.Appending
                                  ? "追記（生きている Layer 0 へ書き足しました。入れ替えません）"
                                  : "追記（入れ替えません）") + Lf);
        w.Write("  控え: " + (ready.BackupRequested
                              ? (ready.BackupRoot ?? "（取れていません）")
                              : "（取らない指定です）") + Lf);
        if (blocked is not null)
        {
            w.Write("★入れ替えません: " + blocked + Lf);
            w.Write("  生きている Layer 0 はそのままです（1 バイトも動かしていません）。" + Lf);
            w.Write("  今回の走査の Layer 0 は残します（消しません ——中身は人が見て決められます）。"
                    + Lf);
            w.Write(rule + Lf);
            return;
        }
        w.Write("★入れ替えます:" + Lf);
        w.Write("    " + ready.Live + " → " + Retired(ready.Live, ready.Stamp)
                + "（改名。消しません）" + Lf);
        w.Write("    " + ready.Destination + " → " + ready.Live + Lf);
        w.Write("  退けた古い方: "
                + (ready.DropRetired ? "★消します（" + DropOldFlag + "）" : "残します（既定）") + Lf);
        if (ready.DropRetired && !ready.BackupRequested) w.Write(NoBackupAndDropWarning + Lf);
        w.Write(rule + Lf);
    }


    private static int MoveSidecars(string from, string to)
    {
        var moved = 0;
        foreach (var suffix in SidecarSuffixes)
        {
            var side = from + suffix;
            if (!File.Exists(side)) continue;
            File.Move(side, to + suffix);
            moved++;
        }
        return moved;
    }

    private static string? Occupied(Layer0Ready ready)
    {
        if (!File.Exists(ready.Live)) return null;
        var retired = Retired(ready.Live, ready.Stamp);
        if (File.Exists(retired) || Directory.Exists(retired)) return retired;
        foreach (var suffix in SidecarSuffixes)
        {
            if (File.Exists(retired + suffix)) return retired + suffix;
        }
        return null;
    }

    private static string? StillOpen(Layer0Ready ready)
    {
        foreach (var path in Movers(ready))
        {
            if (!File.Exists(path)) continue;
            try
            {
                using var probe = new FileStream(path, FileMode.Open, FileAccess.Read,
                                                 FileShare.None);
            }
            catch (IOException)
            {
                return path;
            }
            catch (UnauthorizedAccessException)
            {
                return path;
            }
        }
        return null;
    }

    private static IEnumerable<string> Movers(Layer0Ready ready)
    {
        foreach (var head in new[] { ready.Live, ready.Destination })
        {
            yield return head;
            foreach (var suffix in SidecarSuffixes) yield return head + suffix;
        }
    }

    private static string Beside(string layer0Db, string mark, string stamp)
    {
        ArgumentNullException.ThrowIfNull(layer0Db);
        var dir = IOPath.GetDirectoryName(IOPath.GetFullPath(layer0Db)) ?? "";
        var stem = IOPath.GetFileNameWithoutExtension(layer0Db);
        var ext = IOPath.GetExtension(layer0Db);
        return IOPath.Combine(dir, stem + mark + stamp + ext);
    }

    private static string Where(string path) =>
        File.Exists(path) ? "（" + ScanBackup.Size(new FileInfo(path).Length) + "）" : "（ありません）";

    private static readonly string Lf = ((char)10).ToString();

    private static string N(long value) => value.ToString(CultureInfo.InvariantCulture);
}
