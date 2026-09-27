using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace TH09.Analysis;

public static class Program
{
    private const string Usage = """
        TH09 分析・集計（段階 6）。Python 版 th09_hit_window.py ほかと同じ値を出す。

          th09_analysis --selftest
              自己検査。★合成（実データに無い経路）＋ 否定テスト。DB もゲームも触らない。

          th09_analysis --dump --l0 <layer0.db> --main <tracker.sqlite3> --out <csharp.tsv>
                        [--session N] [--limit N]
              保存済みの hit_window_features を、Python 側と同じ形式で全部吐く。
              ★特徴量は 1 つも計算し直さない（保存済みの値をそのまま吐くだけ）。
              ★sessions.status='running' のセッションは除外し、除外したことをログに出す。

          th09_analysis --board --corpus <corpus.txt> --out <csharp.tsv>
              corpus に並んだ窓を open_window して、盤面（弾・敵・レーザー）を全部吐く。
              ★DB の場所も対象も corpus.txt から取る（C# 側で数え直さない）。
              ★全窓照合では open_window も board も 1 行も通らないので、こちらで見る。

          th09_analysis --bench --l0 <layer0.db> [--session N --window M] [--n 10]
              ★窓を開いて盤面が出るまでの時間（出口条件: 最大の実窓で p95 < 500 ms、n = 10）。
              --session / --window を省くと、★tick 数がいちばん多い実窓を自分で選ぶ。

          th09_analysis --dump-list-culprit --l0 <layer0.db> [--sessions N] [--examples N]
              ★本物の危険物リストの『最初の重なり』の精度を測る（読むだけ）。
              被弾: ゲームが記録した当てた物との一致件数。クイック: 重なりがあった割合。
              --sessions を省くと全セッションを対象にする（★重いので、試すなら小さい数を渡す）。
              --examples N を渡すと、静止画を撮るのに使える窓（クイック/レーザー・クイック/弾・
              被弾でゲームの値に決まらなかった窓）を種類ごとに N 件まで挙げる。
        """;

    public static int Main(string[] args)
    {
        try { Console.OutputEncoding = new UTF8Encoding(false); } catch (IOException) { }

        var opt = ParseArgs(args);
        if (args.Length == 0 || opt.ContainsKey("help") || opt.ContainsKey("h"))
        {
            Console.WriteLine(Usage);
            return args.Length == 0 ? 1 : 0;
        }

        try
        {
            if (opt.ContainsKey("selftest")) return SelfTest.Run();
            if (opt.ContainsKey("dump")) return RunDump(opt);
            if (opt.ContainsKey("board")) return RunBoard(opt);
            if (opt.ContainsKey("bench")) return RunBench(opt);
            if (opt.ContainsKey("dump-list-culprit")) return RunDumpListCulprit(opt);
            if (opt.ContainsKey("diag-unmatched-bullets")) return RunDiagUnmatchedBullets(opt);
            if (opt.ContainsKey("diag-one-ex")) return RunDiagOneEx(opt);
            if (opt.ContainsKey("diag-ex-full-trail")) return RunDiagExFullTrail(opt);
        }
        catch (Exception e)
        {
            Console.Error.WriteLine($"落ちた: {e.GetType().Name}: {e.Message}");
            return 2;
        }

        Console.Error.WriteLine("引数が分かりません。--help を見てください。");
        return 1;
    }


    private static int RunDump(Dictionary<string, string?> opt)
    {
        if (!Require(opt, "l0", out var l0Path) || !Require(opt, "out", out var outPath)) return 1;
        if (!Require(opt, "main", out var mainPath)) return 1;
        if (!File.Exists(mainPath))
        {
            Console.Error.WriteLine($"本体 DB が見つかりません: {mainPath}");
            Console.Error.WriteLine("  ★sessions.status を見ないと running の除外ができないので走らせません。");
            return 1;
        }
        long? session = opt.TryGetValue("session", out var s) && s is not null
            ? long.Parse(s, CultureInfo.InvariantCulture) : null;
        int? limit = opt.TryGetValue("limit", out var l) && l is not null
            ? int.Parse(l, CultureInfo.InvariantCulture) : null;

        using var l0 = new AnalysisDb(l0Path);
        using var main = new AnalysisDb(mainPath);
        var res = ParityDump.Dump(l0, main, outPath, session, limit, Console.Error);
        return res.ValueLines > 0 ? 0 : 1;
    }


    private static int RunBoard(Dictionary<string, string?> opt)
    {
        if (!Require(opt, "corpus", out var corpusPath) || !Require(opt, "out", out var outPath)) return 1;
        var corpus = TH09.Layer0.Corpus.Load(corpusPath);
        if (corpus.WindowCount == 0)
        {
            Console.Error.WriteLine("corpus に窓が 1 本もありません。測れていないので中止します。");
            return 1;
        }
        var res = BoardDump.Dump(corpus.DbPath, corpus, outPath, Console.Error);
        return res.Lines > 0 ? 0 : 1;
    }


    private static int RunDumpListCulprit(Dictionary<string, string?> opt)
    {
        if (!Require(opt, "l0", out var l0Path)) return 1;
        int? sessions = opt.TryGetValue("sessions", out var s) && s is not null
            ? int.Parse(s, CultureInfo.InvariantCulture) : null;
        int examples = opt.TryGetValue("examples", out var ex) && ex is not null
            ? int.Parse(ex, CultureInfo.InvariantCulture) : 0;
        return HazardListCulpritDump.Run(l0Path, sessions, examples);
    }

    private static int RunDiagUnmatchedBullets(Dictionary<string, string?> opt)
    {
        if (!Require(opt, "l0", out var l0Path)) return 1;
        int? sessions = opt.TryGetValue("sessions", out var s) && s is not null
            ? int.Parse(s, CultureInfo.InvariantCulture) : null;
        int want = opt.TryGetValue("n", out var n) && n is not null
            ? int.Parse(n, CultureInfo.InvariantCulture) : 10;
        return HazardListCulpritDump.DiagUnmatchedBullets(l0Path, sessions, want);
    }

    private static int RunDiagOneEx(Dictionary<string, string?> opt)
    {
        if (!Require(opt, "l0", out var l0Path)) return 1;
        if (!Require(opt, "session", out var s) || !Require(opt, "window", out var wn)
            || !Require(opt, "side", out var sd)) return 1;
        return HazardListCulpritDump.DiagOneEx(l0Path, long.Parse(s, CultureInfo.InvariantCulture),
            long.Parse(wn, CultureInfo.InvariantCulture), int.Parse(sd, CultureInfo.InvariantCulture));
    }

    private static int RunDiagExFullTrail(Dictionary<string, string?> opt)
    {
        if (!Require(opt, "l0", out var l0Path)) return 1;
        if (!Require(opt, "session", out var s) || !Require(opt, "window", out var wn)
            || !Require(opt, "side", out var sd) || !Require(opt, "slot", out var sl)) return 1;
        return HazardListCulpritDump.DiagExFullTrail(l0Path, long.Parse(s, CultureInfo.InvariantCulture),
            long.Parse(wn, CultureInfo.InvariantCulture), int.Parse(sd, CultureInfo.InvariantCulture),
            int.Parse(sl, CultureInfo.InvariantCulture));
    }


    private static int RunBench(Dictionary<string, string?> opt)
    {
        if (!Require(opt, "l0", out var l0Path)) return 1;
        int n = opt.TryGetValue("n", out var ns) && ns is not null
            ? int.Parse(ns, CultureInfo.InvariantCulture) : 10;

        using var pick = new AnalysisDb(l0Path);
        long sid, wno;
        if (opt.TryGetValue("session", out var ss) && ss is not null
            && opt.TryGetValue("window", out var ws) && ws is not null)
        {
            sid = long.Parse(ss, CultureInfo.InvariantCulture);
            wno = long.Parse(ws, CultureInfo.InvariantCulture);
        }
        else
        {
            using var cmd = pick.Command(
                "SELECT session_id,window_no,tick_count FROM session_hit_windows"
                + " ORDER BY tick_count DESC, session_id, window_no LIMIT 1");
            using var r = cmd.ExecuteReader();
            if (!r.Read())
            {
                Console.Error.WriteLine("★被弾窓が 1 本もありません。測れていないので中止します。");
                return 1;
            }
            sid = r.GetInt64(0); wno = r.GetInt64(1);
        }

        var times = new List<double>();
        int tickCount = 0, slotCount = 0, boardItems = 0, mainCols = 0, coordCols = 0;
        for (int k = -1; k < n; k++)
        {
            var sw = Stopwatch.StartNew();
            using var db = new AnalysisDb(l0Path);
            var win = HitWindowReader.Open(db, sid, wno)
                      ?? throw new InvalidDataException($"窓が見つかりません: session={sid} window={wno}");
            var ev = win.PrimaryEventIndex();
            int tick = ev is null ? 0 : win.Events()[ev.Value].Index;
            var b1 = win.BoardAt(tick, 1);
            var b2 = win.BoardAt(tick, 2);
            sw.Stop();
            if (k < 0)
            {
                tickCount = win.TickCount; slotCount = win.Meta.SlotCount;
                boardItems = b1.Count + b2.Count;
                mainCols = win.Main.Count; coordCols = win.Cols.Count;
                continue;
            }
            times.Add(sw.Elapsed.TotalMilliseconds);
        }

        times.Sort();
        Console.WriteLine($"窓 session={sid} window={wno}: {tickCount} tick / {slotCount} 枠 /"
                          + $" 座標リング {coordCols} 列 / 主リング {mainCols} 語");
        Console.WriteLine($"盤面（両陣あわせて）{boardItems} 個を作るところまでを測った");
        Console.WriteLine($"n = {times.Count}（★ウォームアップ 1 回は捨てた）");
        Console.WriteLine($"  最小 {times[0]:F1} ms / 中央値 {Median(times):F1} ms /"
                          + $" p95 {Percentile(times, 0.95):F1} ms / 最大 {times[^1]:F1} ms");
        Console.WriteLine("  ★p95 は nearest-rank（n=10 なら 10 番目＝最大値）。★合格の線は 500 ms");
        var p95 = Percentile(times, 0.95);
        Console.WriteLine(p95 < 500.0 ? "★出口条件を満たしている（p95 < 500 ms）"
                                      : "★出口条件を満たしていない（p95 >= 500 ms）");
        return p95 < 500.0 ? 0 : 1;
    }

    private static double Median(List<double> sorted) =>
        sorted.Count % 2 == 1 ? sorted[sorted.Count / 2]
                              : (sorted[sorted.Count / 2 - 1] + sorted[sorted.Count / 2]) / 2.0;

    private static double Percentile(List<double> sorted, double q)
    {
        if (sorted.Count == 0) throw new InvalidOperationException("母数 0 では百分位を出せない");
        int rank = (int)Math.Ceiling(q * sorted.Count);
        return sorted[Math.Clamp(rank - 1, 0, sorted.Count - 1)];
    }


    private static Dictionary<string, string?> ParseArgs(string[] args)
    {
        var opt = new Dictionary<string, string?>(StringComparer.Ordinal);
        for (int i = 0; i < args.Length; i++)
        {
            var a = args[i];
            if (!a.StartsWith("--", StringComparison.Ordinal)) continue;
            var key = a[2..];
            string? val = i + 1 < args.Length && !args[i + 1].StartsWith("--", StringComparison.Ordinal)
                ? args[++i] : null;
            opt[key] = val;
        }
        return opt;
    }

    private static bool Require(Dictionary<string, string?> opt, string key, out string value)
    {
        if (opt.TryGetValue(key, out var v) && !string.IsNullOrEmpty(v)) { value = v; return true; }
        Console.Error.WriteLine($"--{key} が要ります。--help を見てください。");
        value = "";
        return false;
    }
}
