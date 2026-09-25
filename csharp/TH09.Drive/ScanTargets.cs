using System.Collections.Frozen;
using System.Globalization;
using System.Runtime.Versioning;
using Microsoft.Data.Sqlite;
using TH09.Record;
using TH09.Record.Generated;

using Cols = TH09.Generated.DbColumns;
using IOPath = System.IO.Path;

namespace TH09.Drive;

public sealed record ReplayRow(long ReplayId, long? Mode, long? Difficulty,
                               long? P1Char, long? IsOwn, string? DecodedJson);

[SupportedOSPlatform("windows")]
public sealed record ScanTarget(long ReplayId, ReplayRow Row, string Path,
                                CanonJson.Node Decoded,
                                IReadOnlyList<ReplayStages.P1Stage> Stages, bool Done);

public sealed record ScanFilter(
    IReadOnlyList<string>? Dirs = null,
    bool Recurse = true,
    IReadOnlyList<long>? Modes = null,
    bool Own = false,
    int MinStages = 0,
    bool SkipDone = true,
    int? Limit = null,
    string Order = ScanTargets.OrderId,
    IReadOnlyList<long>? Chars = null,
    IReadOnlyList<long>? Difficulties = null,
    IReadOnlyList<MatchSides>? MatchKinds = null,
    int? MinRecordVersion = null,
    string? Layer0Path = null);

[SupportedOSPlatform("windows")]
public static class ScanTargets
{
    public const string OrderId = "id";

    public const string OrderSize = "size";

    public const string OrderMtime = "mtime";

    public const string DecodedStatus = "decoded";

    public const string CapturedStatus = "captured";

    public const double SecPerStage = 7.5;

    public const double SecOverhead = 10.0;

    public const int RuleWidth = 72;

    public const int DefaultShow = 30;


    public static bool AlreadyScanned(SqliteConnection conn, long replayId)
    {
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = $"SELECT 1 FROM {Cols.SessionReplays.Table}"
                            + $" WHERE {Cols.SessionReplays.ReplayId}=$0"
                            + $" AND {Cols.SessionReplays.LinkMethod}=$1";
            cmd.Parameters.AddWithValue("$0", replayId);
            cmd.Parameters.AddWithValue("$1", RecordLabels.ScanLinkMethod);
            if (cmd.ExecuteScalar() is not null) return true;
        }
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = $"SELECT 1 FROM {Cols.ReplayScanItems.Table}"
                            + $" WHERE {Cols.ReplayScanItems.ReplayId}=$0"
                            + $" AND {Cols.ReplayScanItems.Status}=$1"
                            + $" AND {Cols.ReplayScanItems.VerifyStatus}=$2";
            cmd.Parameters.AddWithValue("$0", replayId);
            cmd.Parameters.AddWithValue("$1", CapturedStatus);
            cmd.Parameters.AddWithValue("$2", VerifySession.StatusOk);
            return cmd.ExecuteScalar() is not null;
        }
    }

    public static string? CurrentPath(SqliteConnection conn, long replayId)
    {
        var all = ExistingPaths(conn, replayId);
        return all.Count > 0 ? all[0] : null;
    }

    internal static List<string> ExistingPaths(SqliteConnection conn, long replayId)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"SELECT {Cols.ReplayPaths.FullPath} FROM {Cols.ReplayPaths.Table}"
                        + $" WHERE {Cols.ReplayPaths.ReplayId}=$0"
                        + $" ORDER BY {Cols.ReplayPaths.IsCurrent} DESC,"
                        + $" {Cols.ReplayPaths.ReplayPathId} DESC";
        cmd.Parameters.AddWithValue("$0", replayId);
        var result = new List<string>();
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            if (r.IsDBNull(0)) continue;
            var path = r.GetString(0);
            if (File.Exists(path)) result.Add(path);
        }
        return result;
    }

    public static IReadOnlyList<long>? CharsArg(string? raw)
    {
        if (string.IsNullOrEmpty(raw)) return null;
        var seen = new HashSet<long>();
        var outList = new List<long>();
        foreach (var part in raw.Split(','))
        {
            if (part.Trim().Length == 0) continue;
            var v = long.Parse(part.Trim(), NumberStyles.AllowLeadingSign,
                               CultureInfo.InvariantCulture);
            if (seen.Add(v)) outList.Add(v);
        }
        return outList.Count == 0 ? null : outList;
    }

    public static IReadOnlyList<long>? DifficultyArg(string? raw)
    {
        if (string.IsNullOrEmpty(raw)) return null;
        var seen = new HashSet<long>();
        var outList = new List<long>();
        foreach (var part in raw.Split(','))
        {
            var text = part.Trim();
            if (text.Length == 0) continue;
            long value;
            if (long.TryParse(text, NumberStyles.AllowLeadingSign,
                              CultureInfo.InvariantCulture, out var n))
            {
                value = n;
            }
            else
            {
                var hit = RecordLabels.Difficulties
                    .Where(kv => string.Equals(kv.Value, text, StringComparison.OrdinalIgnoreCase))
                    .Select(kv => (long?)kv.Key).FirstOrDefault();
                value = hit ?? throw new ScanUsageError(
                    "取れない値です（--difficulty は " + DifficultyChoices() + " か数）: " + text);
            }
            if (seen.Add(value)) outList.Add(value);
        }
        return outList.Count == 0 ? null : outList;
    }

    public static IReadOnlyList<long>? ModesArg(string? raw)
    {
        if (string.IsNullOrEmpty(raw)) return null;
        var seen = new HashSet<long>();
        var outList = new List<long>();
        foreach (var part in raw.Split(','))
        {
            var text = part.Trim();
            if (text.Length == 0) continue;
            long value;
            if (long.TryParse(text, NumberStyles.AllowLeadingSign,
                              CultureInfo.InvariantCulture, out var n))
            {
                value = n;
            }
            else
            {
                var hit = RecordLabels.Modes
                    .Where(kv => string.Equals(kv.Value, text, StringComparison.OrdinalIgnoreCase))
                    .Select(kv => (long?)kv.Key).FirstOrDefault();
                value = hit ?? throw new ScanUsageError(
                    "取れない値です（--mode は " + ModeChoices() + " か数）: " + text);
            }
            if (seen.Add(value)) outList.Add(value);
        }
        return outList.Count == 0 ? null : outList;
    }

    public static readonly (string Word, MatchSides Kind)[] MatchKindWords =
    [
        ("human-vs-human", MatchSides.HumanVsHuman),
        ("human-vs-cpu", MatchSides.HumanVsCpu),
        ("cpu-vs-human", MatchSides.CpuVsHuman),
        ("cpu-vs-cpu", MatchSides.CpuVsCpu),
    ];

    public static IReadOnlyList<MatchSides>? MatchKindsArg(string? raw)
    {
        if (string.IsNullOrEmpty(raw)) return null;
        var seen = new HashSet<MatchSides>();
        var outList = new List<MatchSides>();
        foreach (var part in raw.Split(','))
        {
            var text = part.Trim();
            if (text.Length == 0) continue;
            MatchSides value;
            if (long.TryParse(text, NumberStyles.AllowLeadingSign,
                              CultureInfo.InvariantCulture, out var n))
            {
                value = Enum.IsDefined((MatchSides)(int)n)
                    ? (MatchSides)(int)n
                    : throw new ScanUsageError(
                        "取れない値です（--match-kind は " + MatchKindChoices() + "）: " + text);
            }
            else
            {
                var hit = MatchKindWords
                    .Where(p => string.Equals(p.Word, text, StringComparison.OrdinalIgnoreCase))
                    .Select(p => (MatchSides?)p.Kind).FirstOrDefault();
                value = hit ?? throw new ScanUsageError(
                    "取れない値です（--match-kind は " + MatchKindChoices() + "）: " + text);
            }
            if (seen.Add(value)) outList.Add(value);
        }
        return outList.Count == 0 ? null : outList;
    }

    public static string MatchKindChoices() =>
        string.Join(" / ", MatchKindWords.Select(p => Num((int)p.Kind) + "=" + p.Word));

    public static string MatchKindWord(MatchSides kind)
    {
        foreach (var (word, k) in MatchKindWords)
        {
            if (k == kind) return word;
        }
        return Num((int)kind);
    }

    public static string ModeChoices() =>
        string.Join(" / ", RecordLabels.Modes.OrderBy(kv => kv.Key)
                                       .Select(kv => Num(kv.Key) + "=" + kv.Value));

    public static string DifficultyChoices() =>
        string.Join(" / ", RecordLabels.Difficulties.OrderBy(kv => kv.Key)
                                       .Select(kv => Num(kv.Key) + "=" + kv.Value));

    public static IReadOnlyList<ScanTarget> SelectTargets(
        SqliteConnection conn, ScanFilter filter, Action<string>? warn = null) =>
        SelectTargets(conn, filter, warn, out _);

    public static IReadOnlyList<ScanTarget> SelectTargets(
        SqliteConnection conn, ScanFilter filter, Action<string>? warn, out SelectionCounts counts)
    {
        var rows = ReadRows(conn, filter);
        var dirs = (filter.Dirs ?? []).Select(IOPath.GetFullPath).ToList();

        var doneVersion = Truthy(filter.MinRecordVersion)
            ? Layer0MinRecordVersions(filter.Layer0Path ?? Paths.Default.Layer0Db, warn)
            : null;

        var targets = new List<ScanTarget>();
        var dropMinStages = 0;
        var dropMatchKind = 0;
        var dropNoFile = 0;
        var dropDir = 0;
        var dropDone = 0;
        var dropVersion = 0;
        foreach (var row in rows)
        {
            var decoded = ReplayStages.Parse(row.DecodedJson);
            var stages = ReplayStages.P1Stages(decoded);
            if (filter.MinStages != 0 && stages.Count < filter.MinStages) { dropMinStages++; continue; }
            if (filter.MatchKinds is { Count: > 0 } kinds
                && ReplayStages.MatchSidesOf(decoded) is { } sides
                && !kinds.Contains(sides)) { dropMatchKind++; continue; }
            var existing = ExistingPaths(conn, row.ReplayId);
            if (existing.Count == 0) { dropNoFile++; continue; }
            string path;
            if (dirs.Count > 0)
            {
                var underDir = existing.FirstOrDefault(p => UnderAny(p, dirs, filter.Recurse));
                if (underDir is null) { dropDir++; continue; }
                path = underDir;
            }
            else
            {
                path = existing[0];
            }
            var done = AlreadyScanned(conn, row.ReplayId);
            if (filter.SkipDone && done) { dropDone++; continue; }
            if (doneVersion is not null)
            {
                var sid = LinkedSessionId(conn, row.ReplayId);
                if (sid is long s
                    && (doneVersion.TryGetValue(s, out var v) ? v : 0)
                       >= filter.MinRecordVersion!.Value)
                    { dropVersion++; continue; }
            }
            targets.Add(new ScanTarget(row.ReplayId, row, path, decoded, stages, done));
        }

        counts = new SelectionCounts(rows.Count, dropMinStages, dropMatchKind, dropNoFile,
                                     dropDir, dropDone, dropVersion, targets.Count);

        IReadOnlyList<ScanTarget> ordered = filter.Order switch
        {
            OrderSize => targets.OrderBy(SizeOf).ToList(),
            OrderMtime => targets.OrderBy(MTimeOf).ToList(),
            _ => targets,
        };
        return Truthy(filter.Limit) ? Slice(ordered, filter.Limit!.Value) : ordered;
    }

    public sealed record SelectionCounts(
        int SqlRows, int DroppedMinStages, int DroppedMatchKind, int DroppedNoFile,
        int DroppedDir, int DroppedDone, int DroppedVersion, int Selected)
    {
        public int Residual => SqlRows - (DroppedMinStages + DroppedMatchKind + DroppedNoFile
                                          + DroppedDir + DroppedDone + DroppedVersion + Selected);
    }

    public static string DescribeZeroCounts(SelectionCounts c) =>
        "対象 0 件の内訳（SQL 絞り後 " + Num(c.SqlRows) + " 件のうち）: "
        + "面数不足 " + Num(c.DroppedMinStages) + " 件 / "
        + "対戦区分 " + Num(c.DroppedMatchKind) + " 件 / "
        + "実ファイル無し " + Num(c.DroppedNoFile) + " 件 / "
        + "--dir 対象外 " + Num(c.DroppedDir) + " 件 / "
        + "走査済み " + Num(c.DroppedDone) + " 件 / "
        + "版が最新 " + Num(c.DroppedVersion) + " 件 / "
        + "選定 " + Num(c.Selected) + " 件"
        + "（残差 " + Num(c.Residual) + "）";

    public static double EstimateSeconds(IEnumerable<ScanTarget> targets) =>
        targets.Sum(t => SecOverhead + SecPerStage * Math.Max(1, t.Stages.Count));


    public static string DescribeReplay(ReplayRow row, int stages)
    {
        var chr = row.P1Char is long c ? MonitorRules.Char(c) : "?";
        return "replay_id=" + Num(row.ReplayId)
             + "  " + LabelOf(RecordLabels.Modes, row.Mode)
             + "  " + LabelOf(RecordLabels.Difficulties, row.Difficulty)
             + "  " + chr
             + "  stages=" + Num(stages) + "  " + (Truthy(row.IsOwn) ? "自分" : "他人");
    }

    internal static string LabelOf(FrozenDictionary<int, string> map, long? key)
    {
        if (key is not long k) return "?";
        var i = unchecked((int)k);
        if (i != k) return "?";
        return map.TryGetValue(i, out var v) ? v : "?";
    }

    public static ScanProgressLines.ScanPlanSummary WriteDryRun(
        TextWriter w, IReadOnlyList<ScanTarget> targets, ScanFilter filter, bool rescan, int show,
        int slot, string? replayDir, ScanRunPlan? plan = null)
    {
        var doneNote = plan is not null ? plan.DoneNote
            : rescan ? "--rescan あり＝記録済みもやり直す" : "--rescan なし＝記録済みは除外";
        w.Write(new string('=', RuleWidth) + "\n");
        w.Write("対象 " + Num(targets.Count) + " 件（" + doneNote + "）\n");
        if (Truthy(filter.MinRecordVersion))
            w.Write("  --min-record-version " + Num(filter.MinRecordVersion!.Value)
                    + ": Layer 0 が既にこの版以上のものは飛ばす（再開）\n");
        if (Truthy(filter.Limit))
            w.Write("  --max " + Num(filter.Limit!.Value) + ": 上限を掛けている\n");
        if (filter.Dirs is { Count: > 0 } dirs)
            w.Write("ディレクトリ指定: " + string.Join(", ", dirs)
                    + "（" + (filter.Recurse ? "配下も含む" : "直下のみ") + "）\n");
        if (filter.MatchKinds is { Count: > 0 })
        {
            var counted = 0L;
            var parts = new List<string>();
            foreach (var (word, kind) in MatchKindWords)
            {
                var n = targets.Count(t => ReplayStages.MatchSidesOf(t.Decoded) == kind);
                counted += n;
                parts.Add(word + " " + Num(n));
            }
            var none = targets.Count(t => ReplayStages.MatchSidesOf(t.Decoded) is null);
            counted += none;
            w.Write("対戦区分の内訳: " + string.Join(" / ", parts)
                    + " / 区分なし " + Num(none) + "（Story・Extra と、読めなかった Match）"
                    + "  ——残差 " + Num(counted - targets.Count) + "\n");
        }
        w.Write(new string('=', RuleWidth) + "\n");

        var slots = targets.Select(t => (Target: t, Slot: SlotIfIn(t.Path, replayDir))).ToList();
        var inSlot = slots.Count(x => x.Slot is not null);
        var shown = show != 0 ? show : DefaultShow;
        foreach (var (t, n) in Slice(slots, shown))
        {
            w.Write("  replay_id=" + Pad(Num(t.ReplayId), 5)
                    + " stages=" + Pad(Num(t.Stages.Count), 2)
                    + " " + Pad(IOPath.GetFileName(t.Path), 40)
                    + " " + (n is int s
                             ? "スロット" + Pad2(s) + " 在中＝ファイル操作なし"
                             : "スロット" + Pad2(slot) + " へコピー") + "\n");
        }
        if (targets.Count > shown)
            w.Write("  … 他 " + Num(targets.Count - shown) + " 件（表示は --limit "
                    + Num(shown) + " 件まで。**対象は上の " + Num(targets.Count) + " 件全部**）\n");
        var sec = EstimateSeconds(targets);
        w.Write(new string('-', RuleWidth) + "\n");
        w.Write("所要見積: " + F(sec, 0) + " 秒 = " + F(sec / 3600.0, 1)
                + " 時間（1本あたり " + F(SecOverhead, 0) + " 秒 + " + F(SecPerStage, 0) + " 秒/面）\n");
        w.Write("ファイル操作: コピー " + Num(targets.Count - inSlot)
                + " 件 / 在中でそのまま " + Num(inSlot) + " 件\n");
        w.Write("**再生はしていません。**\n");
        return new ScanProgressLines.ScanPlanSummary(targets.Count, doneNote, sec / 3600.0,
                                                      targets.Count - inSlot, inSlot);
    }

    public static void WriteList(TextWriter w, IReadOnlyList<ScanTarget> targets, string? replayDir)
    {
        foreach (var t in targets)
        {
            var n = SlotIfIn(t.Path, replayDir);
            var slot = n is int v ? "  ← スロット " + Pad2(v) + " に在中" : "";
            w.Write(DescribeReplay(t.Row, t.Stages.Count) + slot + (t.Done ? "  [済]" : "") + "\n");
        }
        w.Write("\n");
        w.Write(Num(targets.Count) + " 件（--mode 0 で Story、--own で自分のもの、"
                + "--min-stages 9 で通し、--dir でフォルダ指定）\n");
    }

    public static ScanFilter ForList(ScanFilter shared, bool rescan, bool hideDone, int limit) =>
        shared with { SkipDone = !rescan && hideDone, Limit = limit };

    public static ScanFilter ForBatch(ScanFilter shared, bool rescan, int? max) =>
        shared with { SkipDone = !rescan, Limit = max };


    public static IReadOnlyDictionary<long, long> Layer0MinRecordVersions(
        string layer0Path, Action<string>? warn = null)
    {
        var found = new Dictionary<long, long>();
        if (!File.Exists(layer0Path))
        {
            warn?.Invoke("警告: Layer 0 が見つかりません（" + layer0Path
                         + "）。--min-record-version による再開が効きません。");
            return found;
        }
        var cs = new SqliteConnectionStringBuilder
        {
            DataSource = layer0Path,
            Mode = SqliteOpenMode.ReadOnly,
            Pooling = false,
            DefaultTimeout = RecordDb.BusyTimeoutMs / 1000,
        }.ToString();
        using var conn = new SqliteConnection(cs);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT session_id,MIN(record_version) FROM session_ticks"
                        + " GROUP BY session_id";
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            if (r.IsDBNull(0) || r.IsDBNull(1)) continue;
            found[r.GetInt64(0)] = r.GetInt64(1);
        }
        return found;
    }


    private static List<ReplayRow> ReadRows(SqliteConnection conn, ScanFilter filter)
    {
        var where = new List<string> { Cols.Replays.DecodeStatus + "=$s" };
        var values = new List<(string Name, object Value)> { ("$s", DecodedStatus) };
        if (filter.Modes is { Count: > 0 } modes)
        {
            var names = modes.Select((_, i) => "$m" + Num(i)).ToList();
            for (var i = 0; i < modes.Count; i++) values.Add((names[i], modes[i]));
            where.Add(Cols.Replays.Mode + " IN (" + string.Join(",", names) + ")");
        }
        if (filter.Own) where.Add(Cols.Replays.IsOwn + "=1");
        if (filter.Difficulties is { Count: > 0 } diffs)
        {
            var names = diffs.Select((_, i) => "$d" + Num(i)).ToList();
            for (var i = 0; i < diffs.Count; i++) values.Add((names[i], diffs[i]));
            where.Add(Cols.Replays.Difficulty + " IN (" + string.Join(",", names) + ")");
        }
        if (filter.Chars is { Count: > 0 } chars)
        {
            var names = chars.Select((_, i) => "$c" + Num(i)).ToList();
            for (var i = 0; i < chars.Count; i++) values.Add((names[i], chars[i]));
            var ph = string.Join(",", names);
            where.Add("(" + Cols.Replays.P1Char + " IN (" + ph + ") OR "
                      + Cols.Replays.P2Char + " IN (" + ph + "))");
        }
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT " + Cols.Replays.ReplayId + "," + Cols.Replays.Mode + ","
                        + Cols.Replays.Difficulty + "," + Cols.Replays.P1Char + ","
                        + Cols.Replays.IsOwn + "," + Cols.Replays.DecodedJson
                        + " FROM " + Cols.Replays.Table
                        + " WHERE " + string.Join(" AND ", where)
                        + " ORDER BY " + Cols.Replays.ReplayId;
        foreach (var (name, value) in values) cmd.Parameters.AddWithValue(name, value);
        var rows = new List<ReplayRow>();
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            rows.Add(new ReplayRow(
                r.GetInt64(0),
                r.IsDBNull(1) ? null : r.GetInt64(1),
                r.IsDBNull(2) ? null : r.GetInt64(2),
                r.IsDBNull(3) ? null : r.GetInt64(3),
                r.IsDBNull(4) ? null : r.GetInt64(4),
                r.IsDBNull(5) ? null : r.GetString(5)));
        }
        return rows;
    }

    private static long? LinkedSessionId(SqliteConnection conn, long replayId)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"SELECT {Cols.SessionReplays.SessionId} FROM {Cols.SessionReplays.Table}"
                        + $" WHERE {Cols.SessionReplays.ReplayId}=$0";
        cmd.Parameters.AddWithValue("$0", replayId);
        using var r = cmd.ExecuteReader();
        return r.Read() && !r.IsDBNull(0) ? r.GetInt64(0) : null;
    }

    public static bool UnderAnyWatchRoot(string dir, IReadOnlyList<string> watchRoots) =>
        watchRoots.Any(root => IsUnder(dir, root));

    private static bool UnderAny(string path, IReadOnlyList<string> dirs, bool recurse)
    {
        foreach (var d in dirs)
        {
            if (recurse)
            {
                if (IsUnder(path, d)) return true;
            }
            else if (ReplaySlots.SameDir(IOPath.GetDirectoryName(path) ?? path, d))
            {
                return true;
            }
        }
        return false;
    }

    private static bool IsUnder(string path, string dir)
    {
        var p = Parts(IOPath.GetFullPath(path));
        var d = Parts(dir);
        if (p.Count < d.Count) return false;
        for (var i = 0; i < d.Count; i++)
            if (!string.Equals(p[i], d[i], StringComparison.OrdinalIgnoreCase)) return false;
        return true;
    }

    private static List<string> Parts(string full) =>
        full.Split([IOPath.DirectorySeparatorChar, IOPath.AltDirectorySeparatorChar],
                   StringSplitOptions.RemoveEmptyEntries).ToList();

    private static int? SlotIfIn(string path, string? replayDir)
    {
        if (replayDir is null) return null;
        if (!ReplaySlots.SameDir(IOPath.GetDirectoryName(path) ?? path, replayDir)) return null;
        return ReplaySlots.SlotOf(path);
    }

    private static long SizeOf(ScanTarget t) =>
        File.Exists(t.Path) ? new FileInfo(t.Path).Length : 0L;

    private static double MTimeOf(ScanTarget t)
    {
        if (!File.Exists(t.Path)) return 0.0;
        var ticks = File.GetLastWriteTimeUtc(t.Path).Ticks - DateTime.UnixEpoch.Ticks;
        var sec = ticks / TimeSpan.TicksPerSecond;
        var nsec = (ticks % TimeSpan.TicksPerSecond) * 100;
        return sec + 1e-9 * nsec;
    }

    private static IReadOnlyList<T> Slice<T>(IReadOnlyList<T> seq, int n)
    {
        var take = n >= 0 ? Math.Min(n, seq.Count) : Math.Max(0, seq.Count + n);
        return take >= seq.Count ? seq : seq.Take(take).ToList();
    }

    private static bool Truthy(int? v) => v is int n && n != 0;

    private static bool Truthy(long? v) => v is long n && n != 0;

    private static string Num(long v) => v.ToString(CultureInfo.InvariantCulture);

    private static string Pad2(int v) => v.ToString("00", CultureInfo.InvariantCulture);

    private static string Pad(string s, int width) => s.Length >= width ? s : s.PadRight(width);

    internal static string F(double value, int digits) =>
        value.ToString("F" + digits.ToString(CultureInfo.InvariantCulture),
                       CultureInfo.InvariantCulture);
}
