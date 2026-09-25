using System.Globalization;

namespace TH09.Record;

public static class StreamPanelDump
{
    public const string Flag = "--dump-stream-panel";

    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public static int Run(TextWriter w, string dbPath, string? configPath, bool ownOnly, IReadOnlyList<long> sids)
    {
        ArgumentNullException.ThrowIfNull(w);
        ArgumentNullException.ThrowIfNull(sids);
        using var db = RecordDb.OpenReadOnly(dbPath);
        if (db is null)
        {
            Console.Error.WriteLine("本体 DB を読み取り専用で開けません: " + dbPath);
            return 1;
        }
        var c = db.Connection;

        Row(w, "opt", "own_only", ownOnly ? "1" : "0", "sids", sids.Count == 0 ? "~" : Join(sids),
            "config", configPath ?? "~");

        var targets = new List<StreamTargetEntry>();
        if (configPath is not null)
        {
            var loaded = StreamTargets.Load(configPath);
            targets.AddRange(loaded.Entries);
            foreach (var e in loaded.Entries)
                Row(w, "target", N(e.Mode), N(e.Difficulty), N(e.Character), N(e.Stage), N(e.Round),
                    N(e.Target), N(e.Wr), N(e.ClearBonus));
            foreach (var note in loaded.Notes)
                Row(w, "note", note.Key, note.Kind, note.Text);
        }

        var (_, cbWith, cbWithout) = SelfBests.BestTableRaw(c, ownOnly: ownOnly);
        Row(w, "cbstat", "with_bonus", N(cbWith), "without_bonus", N(cbWithout));

        long?[] live = sids.Count == 0 ? [null] : sids.Select(x => (long?)x).ToArray();
        var shown = 0;
        var stageRows = 0;
        var matchRows = 0;
        foreach (var sid in live)
        {
            var v = StreamBests.BuildSessionPanel(c, sid, targets, ownOnly);
            if (v is null)
            {
                Row(w, "panel", sid is null ? "~" : N(sid.Value), "none");
                continue;
            }
            shown++;
            Row(w, "panel", N(v.Session), "head", N(v.Mode), N(v.Difficulty), N(v.Character));
            foreach (var st in v.Stages)
            {
                stageRows++;
                var spot = st.Round is null ? "stage" : "round";
                Row(w, spot, N(v.Session), N(st.Stage), N(st.Round), N(st.Opponent), B(st.Running),
                    B(st.Won), N(st.CurrentSegment), N(st.CurrentTotal));
                foreach (var item in st.Items)
                    Row(w, "item", N(v.Session), spot, N(st.Stage), N(st.Round), item.Kind, N(item.Value),
                        N(item.Delta), Src(item.Source), N(item.UsedRound), B(item.Fallback));
            }
            if (v.Match is { } m)
            {
                matchRows++;
                Row(w, "match", N(v.Session), N(m.Opponent), B(m.Running), N(m.CurrentTotal));
                foreach (var item in m.Items)
                    Row(w, "item", N(v.Session), "match", "~", "~", item.Kind, N(item.Value),
                        N(item.Delta), Src(item.Source), N(item.UsedRound), B(item.Fallback));
            }
        }

        Row(w, "count", "targets", N(targets.Count), "sessions", N(shown), "stages", N(stageRows),
            "match", N(matchRows));
        return 0;
    }

    private static string Src(BestSource? s) => s is null ? "~" : s.Text();

    private static string N(long? v) => v is null ? "~" : v.Value.ToString(Inv);

    private static string N(long v) => v.ToString(Inv);

    private static string N(int v) => v.ToString(Inv);

    private static string B(bool v) => v ? "1" : "0";

    private static string Join(IEnumerable<long> ids) => string.Join(",", ids.Select(x => x.ToString(Inv)));

    private static void Row(TextWriter w, params string[] cells) => w.Write(string.Join("\t", cells) + "\n");
}
