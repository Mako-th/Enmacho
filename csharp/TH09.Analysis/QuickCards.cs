using TH09.Generated;
using TA = TH09.Layer0.TickArchive;

namespace TH09.Analysis;

public enum CardSource
{
    Gauge,

    Quick,

    Spell,
}

public enum CardLevel
{
    C2,
    C3,
    C4,
}

public readonly record struct CardDrop(CardSource Source, CardLevel Level, bool Certain);

public static class QuickCards
{
    public const double GaugeTol = 6.0;

    public const double CardZeroTol = 1e-6;

    public const double QuickGaugeTol = 1.5;

    public static readonly (double Drop, CardLevel Level)[] DropLevel =
    [
        (100.0, CardLevel.C2),
        (200.0, CardLevel.C3),
        (300.0, CardLevel.C4),
    ];

    public static readonly (double Gauge, CardLevel Level)[] QuickLevelByGauge =
    [
        (400.0, CardLevel.C4),
        (300.0, CardLevel.C3),
        (200.0, CardLevel.C2),
    ];

    public sealed class SessionMarks
    {
        public required IReadOnlyList<bool?> P1 { get; init; }

        public required IReadOnlyList<bool?> P2 { get; init; }

        public required IReadOnlyList<bool?> P1Boss { get; init; }

        public required IReadOnlyList<bool?> P2Boss { get; init; }

        public IReadOnlyList<bool?> Side(int side) => side switch
        {
            1 => P1,
            2 => P2,
            _ => throw new ArgumentOutOfRangeException(nameof(side), side, "側は 1 か 2"),
        };

        public IReadOnlyList<bool?> BossSide(int side) => side switch
        {
            1 => P1Boss,
            2 => P2Boss,
            _ => throw new ArgumentOutOfRangeException(nameof(side), side, "側は 1 か 2"),
        };

        public int Count => P1.Count + P2.Count + P1Boss.Count + P2Boss.Count;
    }

    public static SessionMarks? Load(AnalysisDb layer0, long sessionId)
    {
        var want = new HashSet<string>(StringComparer.Ordinal)
        {
            TickWords.Record.Flags,
            TickWords.Record.P1Gauge, TickWords.Record.P2Gauge,
            TickWords.Record.P1SpellAttacks, TickWords.Record.P2SpellAttacks,
            TickWords.Record.P1BossAttacks, TickWords.Record.P2BossAttacks,
        };

        var chunks = new List<TA.TickColumns>();
        using (var cmd = layer0.Command(
                   "SELECT field_order,blob,encoding FROM session_ticks WHERE session_id=$0 ORDER BY segment_no",
                   sessionId))
        using (var r = cmd.ExecuteReader())
        {
            while (r.Read())
            {
                var order = TA.ParseFieldOrder(r.GetString(0));
                chunks.Add(TA.Decode((byte[])r.GetValue(1), r.GetString(2), order, want));
            }
        }
        if (chunks.Count == 0) return null;

        var raw = new Dictionary<string, uint[]>(StringComparer.Ordinal);
        int total = 0;
        foreach (var c in chunks) total += c[TickWords.Record.Flags].Length;
        foreach (var name in want)
        {
            var buf = new uint[total];
            int at = 0;
            foreach (var c in chunks)
            {
                var col = c[name];
                col.CopyTo(buf, at);
                at += col.Length;
            }
            raw[name] = buf;
        }

        return new SessionMarks
        {
            P1 = MarksOf(raw, 1, bossWord: false),
            P2 = MarksOf(raw, 2, bossWord: false),
            P1Boss = MarksOf(raw, 1, bossWord: true),
            P2Boss = MarksOf(raw, 2, bossWord: true),
        };
    }

    private static List<bool?> MarksOf(Dictionary<string, uint[]> raw, int side, bool bossWord)
    {
        var flags = raw[TickWords.Record.Flags];
        var gauge = raw[side == 1 ? TickWords.Record.P1Gauge : TickWords.Record.P2Gauge];
        var spell = raw[side == 1 ? TickWords.Record.P1SpellAttacks : TickWords.Record.P2SpellAttacks];
        var boss = raw[side == 1 ? TickWords.Record.P1BossAttacks : TickWords.Record.P2BossAttacks];

        var gaugeSpec = TimelineDecode.SpecOf(side == 1 ? TickWords.Record.P1Gauge : TickWords.Record.P2Gauge);
        var spellSpec = TimelineDecode.SpecOf(side == 1 ? TickWords.Record.P1SpellAttacks : TickWords.Record.P2SpellAttacks);
        var bossSpec = TimelineDecode.SpecOf(side == 1 ? TickWords.Record.P1BossAttacks : TickWords.Record.P2BossAttacks);

        var watch = bossWord ? boss : spell;

        var outList = new List<bool?>();
        for (int i = 1; i < flags.Length; i++)
        {
            if (watch[i] <= watch[i - 1]) continue;

            var g0 = TimelineDecode.Decode(gaugeSpec, gauge[i - 1], flags[i - 1]);
            var g1 = TimelineDecode.Decode(gaugeSpec, gauge[i], flags[i]);
            var b0 = TimelineDecode.Decode(bossSpec, boss[i - 1], flags[i - 1]);
            var b1 = TimelineDecode.Decode(bossSpec, boss[i], flags[i]);
            var s0 = TimelineDecode.Decode(spellSpec, spell[i - 1], flags[i - 1]);
            var s1 = TimelineDecode.Decode(spellSpec, spell[i], flags[i]);
            if (g0.IsMissing || g1.IsMissing || b0.IsMissing || b1.IsMissing
                || s0.IsMissing || s1.IsMissing)
            {
                outList.Add(null);
                continue;
            }

            double before = g0.AsDouble, after = g1.AsDouble;
            outList.Add(IsQuickDrop(before, after,
                                    b1.AsLong > b0.AsLong, s1.AsLong > s0.AsLong));
        }
        return outList;
    }

    public static bool? IsQuickDrop(double before, double after, bool? bossRose, bool spellRose)
    {
        if (after >= before - CardZeroTol) return false;
        var got = OfDrop(before, after, bossRose, spellRose);
        if (got is CardDrop d) return d.Source == CardSource.Quick;
        return Math.Abs(after) <= CardZeroTol ? true : null;
    }

    public static CardDrop? OfDrop(double before, double after, bool? bossRose, bool spellRose)
    {
        bool zero = Math.Abs(after) <= CardZeroTol;
        if (bossRose == true)
            return new CardDrop(zero ? CardSource.Quick : CardSource.Gauge, CardLevel.C4, true);

        if (zero)
            return QuickOfGauge(before);

        double drop = before - after;
        CardLevel? level = null;
        foreach (var (v, lv) in DropLevel)
        {
            if (Math.Abs(drop - v) <= GaugeTol) { level = lv; break; }
        }
        if (level is null) return null;
        if (level == CardLevel.C4)
        {
            if (bossRose is null) return new CardDrop(CardSource.Gauge, CardLevel.C4, false);
            if (!spellRose) return null;
            return new CardDrop(CardSource.Quick, CardLevel.C3, true);
        }
        return new CardDrop(CardSource.Gauge, level.Value, true);
    }

    public static CardDrop? QuickOfGauge(double before)
    {
        CardLevel? level = null;
        foreach (var (th, lv) in QuickLevelByGauge)
        {
            if (before >= th - QuickGaugeTol) { level = lv; break; }
        }
        if (level is null) return null;
        bool near = false;
        foreach (var (th, _) in QuickLevelByGauge)
        {
            if (Math.Abs(before - th) <= QuickGaugeTol) { near = true; break; }
        }
        return new CardDrop(CardSource.Quick, level.Value, !near);
    }
}
