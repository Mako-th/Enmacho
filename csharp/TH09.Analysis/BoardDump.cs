using System.Globalization;
using System.Text;
using TH09.Layer0;

namespace TH09.Analysis;

public static class BoardDump
{
    public const int FormatVersion = 1;

    public const int TicksPerWindow = 16;

    private static readonly string[] LaserFields =
        ["angle", "tail", "head", "width", "timer", "phase", "lethal"];

    public static (int Windows, int Ticks, long Lines) Dump(string l0Path, Corpus corpus,
                                                           string outPath, TextWriter log)
    {
        var dir = Path.GetDirectoryName(Path.GetFullPath(outPath));
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

        using var db = new AnalysisDb(l0Path);
        int windows = 0, ticks = 0;
        long lines = 0;

        using (var fs = new FileStream(outPath, FileMode.Create, FileAccess.Write, FileShare.Read))
        using (var w = new StreamWriter(fs, new UTF8Encoding(false)) { NewLine = "\n" })
        {
            var items = corpus.Items.Where(x => x.Kind == Corpus.Kind.Window).ToList();
            w.Write($"# th09-hit-window-board format={FormatVersion}\n");
            w.Write("# float=ieee754-be-hex\n");
            w.Write("# columns=session_id,window_no,tick,side,kind,slot,field,value\n");
            w.Write($"# corpus_windows={items.Count}\n");
            w.Write($"# ticks_per_window={TicksPerWindow}（★等間隔＋主役の前後。全 tick ではない）\n");

            foreach (var item in items)
            {
                var win = HitWindowReader.Open(db, item.SessionId, item.No);
                if (win is null)
                {
                    w.Write($"# missing session={item.SessionId} window={item.No}\n");
                    continue;
                }
                windows++;
                w.Write($"# window session={item.SessionId} window_no={item.No}"
                        + $" tick_count={win.Meta.TickCount} slot_count={win.Meta.SlotCount}\n");
                foreach (var i in TicksOf(win))
                {
                    ticks++;
                    for (int side = 1; side <= 2; side++)
                    {
                        var board = win.BoardAt(i, side);
                        Emit(w, item, i, side, "bullet", board.Bullets, ref lines);
                        Emit(w, item, i, side, "enemy", board.Enemies, ref lines);
                        Emit(w, item, i, side, "laser", board.Lasers, ref lines);
                    }
                }
            }
            w.Write($"# end windows={windows} ticks={ticks} value_lines={lines}\n");
        }

        log.WriteLine("書き出し: " + outPath);
        log.WriteLine($"窓 {windows} 本 / tick {ticks} 点 / 値 {lines} 行を吐いた");
        if (lines == 0) log.WriteLine("★1 行も吐いていない。窓が開けていないのでは");
        return (windows, ticks, lines);
    }

    public static List<int> TicksOf(Window win)
    {
        int n = win.TickCount;
        if (n <= 0) return [];
        int step = Math.Max(1, n / TicksPerWindow);
        var want = new SortedSet<int>();
        for (int i = 0; i < n; i += step) want.Add(i);
        var k = win.PrimaryEventIndex();
        if (k is not null)
        {
            int c = win.Events()[k.Value].Index;
            foreach (var x in new[] { c - 1, c, c + 1 }) if (x >= 0 && x < n) want.Add(x);
        }
        return want.ToList();
    }

    private static void Emit(TextWriter w, Corpus.Item item, int tick, int side, string kind,
                             List<BoardItem> list, ref long lines)
    {
        foreach (var it in list.OrderBy(x => x.Slot))
        {
            foreach (var (name, val) in Fields(kind, it))
            {
                w.Write($"{item.SessionId}\t{item.No}\t{tick}\t{side}\t{kind}\t{it.Slot}\t{name}\t{val}\n");
                lines++;
            }
        }
    }

    private static IEnumerable<(string Name, string Value)> Fields(string kind, BoardItem it)
    {
        yield return ("state", Raw(it.State));
        yield return ("kind", Raw(it.Kind));
        yield return ("x", Flt(it.X));
        yield return ("y", Flt(it.Y));
        if (kind == "enemy")
        {
            yield return ("class", it.EnemyClass ?? "-");
            yield break;
        }
        if (kind != "laser") yield break;
        var L = it.Laser!.Value;
        for (int k = 0; k < LaserFields.Length; k++)
        {
            var name = LaserFields[k];
            yield return name switch
            {
                "angle" => (name, Flt(L.Angle)),
                "tail" => (name, Flt(L.Tail)),
                "head" => (name, Flt(L.Head)),
                "width" => (name, Flt(L.Width)),
                "timer" => (name, Raw(L.Timer)),
                "phase" => (name, Raw(L.Phase)),
                _ => (name, L.Lethal ? "1" : "0"),
            };
        }
    }

    private static string Raw(uint? v) => v is null ? "-" : v.Value.ToString(CultureInfo.InvariantCulture);

    private static string Flt(double? v) =>
        v is null ? "-" : BitConverter.DoubleToInt64Bits(v.Value).ToString("x16", CultureInfo.InvariantCulture);
}
