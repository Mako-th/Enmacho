using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace TH09.Replay;

public static class Program
{
    private const string Usage = """
        TH09 リプレイ復号（段階 4）。Python 版 th09_replay_decode.py と同じ dict を出す。

          th09_replay --selftest [--file <実物の.rpy>]
              自己検査。★否定テスト込み（旧実装の写し / 1 バイト改変 / 母数 0）。
              --file を渡すと実物でも旧実装との差を見る。

          th09_replay --dump --corpus <corpus.txt> --out <csharp.jsonl>
              corpus.txt（1 行 1 パス）を全数復号して JSONL を書く。

          th09_replay --compare --python <python.jsonl> --csharp <csharp.jsonl> [--report <out.txt>]
              2 つの JSONL を突き合わせる。★不一致は 1 本ずつ理由を出す（除外しない）。
              ★0 件は「一致」ではなく NG。

          th09_replay --cp932-dump <out.tsv>
              cp932 の表そのものを吐く（単バイト 256 通り ＋ 先導×後続の全通り）。
              ★「名前が読めた」で済ませないための道具。Python の表と総当たりで比べる。

          th09_replay --decode <file.rpy>
              1 本だけ復号して JSON を出す（目視用）。
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

        if (opt.ContainsKey("selftest"))
            return SelfTest.Run(opt.GetValueOrDefault("file"));

        if (opt.ContainsKey("dump"))
            return RunDump(opt);

        if (opt.ContainsKey("compare"))
            return RunCompare(opt);

        if (opt.TryGetValue("dump-replay-owner", out var ownerScript) && ownerScript is not null)
            return ReplayOwnerDump.Run(ownerScript, Console.Out);

        if (opt.TryGetValue("cp932-dump", out var tablePath) && tablePath is not null)
        {
            File.WriteAllLines(tablePath, ReplayDecode.Cp932Table(), new UTF8Encoding(false));
            Console.WriteLine($"cp932 の表を書いた → {tablePath}");
            return 0;
        }

        if (opt.TryGetValue("decode", out var one) && one is not null)
        {
            Console.WriteLine(ParityJson.ResultToJson(ReplayDecode.DecodeReplay(one)));
            return 0;
        }

        Console.Error.WriteLine("引数が分かりません。--help を見てください。");
        return 1;
    }

    private static int RunDump(Dictionary<string, string?> opt)
    {
        if (!Require(opt, "corpus", out var corpusPath) || !Require(opt, "out", out var outPath)) return 1;
        if (!File.Exists(corpusPath))
        {
            Console.Error.WriteLine($"corpus が見つかりません: {corpusPath}");
            return 1;
        }
        var corpus = ParityJson.ReadCorpus(corpusPath);
        if (corpus.Count == 0)
        {
            Console.Error.WriteLine("corpus が 0 件です。測れていないので中止します。");
            return 1;
        }
        var sw = Stopwatch.StartNew();
        int n = ParityJson.Dump(corpus, outPath);
        sw.Stop();
        Console.WriteLine($"C# 側を書き出した: {n} 本 / {sw.Elapsed.TotalSeconds:F1} 秒 → {outPath}");
        return 0;
    }

    private static int RunCompare(Dictionary<string, string?> opt)
    {
        if (!Require(opt, "python", out var pyPath) || !Require(opt, "csharp", out var csPath)) return 1;
        foreach (var p in new[] { pyPath, csPath })
        {
            if (File.Exists(p)) continue;
            Console.Error.WriteLine($"JSONL が見つかりません: {p}");
            return 1;
        }
        var rep = ParityCompare.CompareFiles(pyPath!, csPath!);
        Console.WriteLine($"突き合わせ: {rep.Matched} / {rep.Total} 本が一致（不一致 {rep.Total - rep.Matched} 本 / 構造の問題 {rep.Problems.Count} 件）");
        if (rep.Total == 0)
            Console.WriteLine("★リプレイが 0 本です。★これは「全部一致」ではなく「測れていない」。");

        if (opt.TryGetValue("report", out var reportPath) && reportPath is not null)
        {
            File.WriteAllLines(reportPath,
                [$"# 突き合わせ {rep.Matched}/{rep.Total} 本が一致", .. rep.Problems], new UTF8Encoding(false));
            Console.WriteLine($"詳細を書いた: {reportPath}");
        }
        foreach (var line in rep.Problems.Take(20)) Console.WriteLine("  " + line);
        if (rep.Problems.Count > 20) Console.WriteLine($"  …ほか {rep.Problems.Count - 20} 件（--report で全部出る）");

        Console.WriteLine(rep.Ok ? "OK: 全数一致" : "NG");
        return rep.Ok ? 0 : 1;
    }

    private static bool Require(Dictionary<string, string?> opt, string key, [NotNullWhen(true)] out string? value)
    {
        opt.TryGetValue(key, out value);
        if (!string.IsNullOrEmpty(value)) return true;
        Console.Error.WriteLine($"--{key} が要ります。");
        return false;
    }

    private static Dictionary<string, string?> ParseArgs(string[] args)
    {
        var map = new Dictionary<string, string?>(StringComparer.Ordinal);
        for (int i = 0; i < args.Length; i++)
        {
            if (!args[i].StartsWith("--", StringComparison.Ordinal)) continue;
            var key = args[i][2..];
            string? value = (i + 1 < args.Length && !args[i + 1].StartsWith("--", StringComparison.Ordinal))
                ? args[++i] : null;
            map[key] = value;
        }
        return map;
    }
}
