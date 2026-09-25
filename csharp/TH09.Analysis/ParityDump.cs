using System.Globalization;
using System.Text;

namespace TH09.Analysis;

public static class ParityDump
{
    public const int FormatVersion = 1;

    public static readonly string[] Columns =
        ["session_id", "window_no", "event_no", "key", "type", "value"];

    public const int WindowEventNo = -1;

    public static readonly string[] ColumnKeys = ["@analysis_version", "@seq", "@side", "@trigger"];
    public static readonly string[] ColumnTypes = ["i", "i", "i", "s"];

    public const string KeyWindowPresent = "@window_present";
    public const string KeyEventCount = "@event_count";

    private const string TableWindows = "session_hit_windows";
    private const string TableFeatures = "hit_window_features";

    public sealed class Scope
    {
        public required Dictionary<long, List<long>> Windows { get; init; }
        public required Dictionary<long, int> Events { get; init; }
        public required SortedSet<long> Running { get; init; }
        public required int SessionsMainTotal { get; init; }
        public required List<long> Layer0Unknown { get; init; }

        public long WindowsTotal => Windows.Values.Sum(v => (long)v.Count);
        public long EventsTotal => Events.Values.Sum(v => (long)v);
        public long WindowsExcluded => Windows.Where(kv => Running.Contains(kv.Key)).Sum(kv => (long)kv.Value.Count);
        public long EventsExcluded => Events.Where(kv => Running.Contains(kv.Key)).Sum(kv => (long)kv.Value);
    }

    public readonly record struct Result(long Windows, long Events, long ValueLines);

    public static Scope Summarize(AnalysisDb l0, AnalysisDb main)
    {
        var windows = new Dictionary<long, List<long>>();
        var events = new Dictionary<long, int>();

        if (l0.HasTable(TableWindows))
        {
            using (var cmd = l0.Command(
                       $"SELECT session_id,window_no FROM {TableWindows} ORDER BY session_id,window_no"))
            using (var r = cmd.ExecuteReader())
            {
                while (r.Read())
                {
                    var sid = r.GetInt64(0);
                    if (!windows.TryGetValue(sid, out var list)) windows[sid] = list = [];
                    list.Add(r.GetInt64(1));
                }
            }
            using (var cmd = l0.Command(
                       $"SELECT session_id,COUNT(*) FROM {TableFeatures} GROUP BY session_id ORDER BY session_id"))
            using (var r = cmd.ExecuteReader())
            {
                while (r.Read()) events[r.GetInt64(0)] = r.GetInt32(1);
            }
        }

        var known = new HashSet<long>();
        using (var cmd = main.Command("SELECT session_id FROM sessions"))
        using (var r = cmd.ExecuteReader())
        {
            while (r.Read()) known.Add(r.GetInt64(0));
        }

        var running = new SortedSet<long>();
        using (var cmd = main.Command("SELECT session_id FROM sessions WHERE status='running'"))
        using (var r = cmd.ExecuteReader())
        {
            while (r.Read()) running.Add(r.GetInt64(0));
        }

        var seen = new SortedSet<long>(windows.Keys);
        seen.UnionWith(events.Keys);
        var unknown = seen.Where(s => !known.Contains(s)).ToList();

        return new Scope
        {
            Windows = windows, Events = events, Running = running,
            SessionsMainTotal = known.Count, Layer0Unknown = unknown,
        };
    }

    public static List<string> HeaderLines(Scope s, long? onlySession, int? limit)
    {
        var ids = s.Running.Count == 0 ? "-" : string.Join(",", s.Running);
        return
        [
            $"# th09-hit-window-parity format={FormatVersion}",
            "# float=ieee754-be-hex",
            "# text_escape=backslash-tab-cr-lf",
            "# key_order=ordinal",
            "# columns=" + string.Join(",", Columns),
            $"# sessions_main_total={s.SessionsMainTotal}",
            $"# sessions_running_excluded={s.Running.Count}",
            $"# sessions_running_ids={ids}",
            $"# sessions_layer0_unknown={s.Layer0Unknown.Count}",
            $"# windows_total={s.WindowsTotal}",
            $"# windows_excluded_running={s.WindowsExcluded}",
            $"# windows_in_scope={s.WindowsTotal - s.WindowsExcluded}",
            $"# events_total={s.EventsTotal}",
            $"# events_excluded_running={s.EventsExcluded}",
            "# scope_session=" + (onlySession is null ? "all" : onlySession.Value.ToString(CultureInfo.InvariantCulture)),
            "# scope_limit=" + (limit is null or 0 ? "none" : limit.Value.ToString(CultureInfo.InvariantCulture)),
        ];
    }

    private readonly record struct Event(int EventNo, long Version, long Seq, long Side, string Trigger, string Json);

    public static Result Dump(AnalysisDb l0, AnalysisDb main, string outPath,
                              long? onlySession, int? limit, TextWriter log)
    {
        var scope = Summarize(l0, main);
        LogScope(scope, l0, main, log);

        var dir = Path.GetDirectoryName(Path.GetFullPath(outPath));
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

        var seenWindows = new HashSet<(long, long)>();
        var seenEvents = new HashSet<(long, long, int)>();
        long lines = 0;

        using (var fs = new FileStream(outPath, FileMode.Create, FileAccess.Write, FileShare.Read))
        using (var w = new StreamWriter(fs, new UTF8Encoding(false)) { NewLine = "\n" })
        {
            foreach (var line in HeaderLines(scope, onlySession, limit)) w.Write(line + "\n");

            foreach (var (sid, wno, eno, key, type, value) in Body(l0, scope, onlySession, limit))
            {
                w.Write(sid.ToString(CultureInfo.InvariantCulture)); w.Write('\t');
                w.Write(wno.ToString(CultureInfo.InvariantCulture)); w.Write('\t');
                w.Write(eno.ToString(CultureInfo.InvariantCulture)); w.Write('\t');
                w.Write(key); w.Write('\t');
                w.Write(type); w.Write('\t');
                w.Write(value); w.Write('\n');
                lines++;
                seenWindows.Add((sid, wno));
                if (eno != WindowEventNo) seenEvents.Add((sid, wno, eno));
            }
            w.Write($"# end windows={seenWindows.Count} events={seenEvents.Count} value_lines={lines}\n");
        }

        log.WriteLine("書き出し: " + outPath);
        if (lines == 0)
        {
            log.WriteLine("★1 行も吐いていない。被弾窓の索引がまだ無いのでは"
                          + "（tools/gen_hit_window_features.py）");
        }
        log.WriteLine($"窓 {seenWindows.Count} 本 / 出来事 {seenEvents.Count} 件 / 値 {lines} 行を吐いた");
        return new Result(seenWindows.Count, seenEvents.Count, lines);
    }

    private static void LogScope(Scope s, AnalysisDb l0, AnalysisDb main, TextWriter log)
    {
        log.WriteLine("Layer 0 : " + l0.Path);
        log.WriteLine("本体 DB : " + main.Path);
        log.WriteLine($"本体 DB の sessions {s.SessionsMainTotal} 件のうち status='running' は "
                      + $"{s.Running.Count} 件（{(s.Running.Count == 0 ? "なし" : string.Join(",", s.Running))}）→ ★除外する");
        log.WriteLine("  ★被弾窓は生 tick より先に書き出されるので、途中で落ちると"
                      + "「窓はあるが生 tick が無い」状態が残る（データの壊れではない）");
        log.WriteLine($"除外した窓 {s.WindowsExcluded} 本 / 全 {s.WindowsTotal} 本、"
                      + $"除外した出来事 {s.EventsExcluded} 件 / 全 {s.EventsTotal} 件");
        if (s.Layer0Unknown.Count > 0)
        {
            log.WriteLine($"※ Layer 0 に居て本体 DB に居ないセッション {s.Layer0Unknown.Count} 件"
                          + "（★除外しない ——除外規則は running の 1 つだけ、と先に決めてある）: "
                          + string.Join(",", s.Layer0Unknown.Take(20)));
        }
    }

    public static IEnumerable<(long Sid, long Wno, int Eno, string Key, string Type, string Value)>
        Body(AnalysisDb l0, Scope scope, long? onlySession, int? limit)
    {
        var sessions = new SortedSet<long>(scope.Windows.Keys);
        sessions.UnionWith(scope.Events.Keys);
        sessions.ExceptWith(scope.Running);
        var ordered = onlySession is null ? sessions.ToList()
                                          : sessions.Where(s => s == onlySession.Value).ToList();
        int done = 0;
        foreach (var sid in ordered)
        {
            var rows = new Dictionary<long, List<Event>>();
            {
                using var cmd = l0.Command(
                    "SELECT window_no,event_no,analysis_version,seq,side,trigger,json"
                    + $" FROM {TableFeatures} WHERE session_id=$0 ORDER BY window_no,event_no", sid);
                using var r = cmd.ExecuteReader();
                while (r.Read())
                {
                    var wno = r.GetInt64(0);
                    if (!rows.TryGetValue(wno, out var list)) rows[wno] = list = [];
                    if (r.IsDBNull(5) || r.IsDBNull(6))
                        throw new InvalidDataException(
                            $"{TableFeatures} に NULL があります（NOT NULL のはず）: session={sid} window={wno}");
                    list.Add(new Event(r.GetInt32(1), r.GetInt64(2), r.GetInt64(3), r.GetInt64(4),
                                       r.GetString(5), r.GetString(6)));
                }
            }

            var present = new HashSet<long>(scope.Windows.TryGetValue(sid, out var ws) ? ws : new List<long>());
            var all = new SortedSet<long>(present);
            all.UnionWith(rows.Keys);
            foreach (var wno in all)
            {
                if (limit is > 0 && done >= limit.Value) yield break;
                done++;
                var evs = rows.TryGetValue(wno, out var list) ? list : new List<Event>();
                yield return (sid, wno, WindowEventNo, KeyWindowPresent, "i", present.Contains(wno) ? "1" : "0");
                yield return (sid, wno, WindowEventNo, KeyEventCount, "i",
                              evs.Count.ToString(CultureInfo.InvariantCulture));
                foreach (var ev in evs)
                {
                    string[] vals =
                    [
                        ev.Version.ToString(CultureInfo.InvariantCulture),
                        ev.Seq.ToString(CultureInfo.InvariantCulture),
                        ev.Side.ToString(CultureInfo.InvariantCulture),
                        FeatureJson.Escape(ev.Trigger),
                    ];
                    for (int k = 0; k < ColumnKeys.Length; k++)
                        yield return (sid, wno, ev.EventNo, ColumnKeys[k], ColumnTypes[k], vals[k]);

                    var where = $"session={sid} window={wno} event={ev.EventNo}";
                    var feat = FeatureJson.Parse(ev.Json, where);
                    foreach (var key in FeatureJson.SortedKeys(feat))
                    {
                        var (type, text) = feat[key].Cells();
                        yield return (sid, wno, ev.EventNo, key, type, text);
                    }
                }
            }
        }
    }
}
