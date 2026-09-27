using System.Globalization;
namespace TH09.Analysis;

public static class BoardLabels
{
    private static readonly Dictionary<string, string> Enemy = Load(AnalysisTables.EnemyJaPacked);
    private static readonly Dictionary<string, string> CardLevel = Load(AnalysisTables.CardLevelJaPacked);

    private static Dictionary<string, string> Load(string packed)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var line in Packed.Lines(packed))
        {
            var f = line.Split('\t');
            if (f.Length != 2)
                throw new InvalidDataException($"語の表の行が 2 列でない: {line}");
            map[f[0]] = f[1];
        }
        return map;
    }

    public static int EnemyCount => Enemy.Count;
    public static int CardLevelCount => CardLevel.Count;

    public static string EnemyJa(string? cls) =>
        cls is not null && Enemy.TryGetValue(cls, out var ja) ? ja : (cls ?? "");

    public static string? CardLevelJa(int? level) =>
        level is not null
        && CardLevel.TryGetValue(level.Value.ToString(System.Globalization.CultureInfo.InvariantCulture),
                                 out var ja)
            ? ja : null;

    public static IEnumerable<(string Label, Func<string?> Body)> SelfTestCases()
    {
        yield return ($"母数（敵の分類 {EnemyCount} / C2・C3 {CardLevelCount}）", () =>
            EnemyCount > 0 && CardLevelCount > 0 ? null : "0 件。生成物を引けていない");
        yield return ("★分類の表が、分類の語の全部を覆っている", () =>
        {
            string[] all = [AnalysisTables.EnemyClassFairy, AnalysisTables.EnemyClassGhost,
                            AnalysisTables.EnemyClassLily, AnalysisTables.EnemyClassBoss,
                            AnalysisTables.EnemyClassC2C3, AnalysisTables.EnemyClassOther];
            var miss = all.Where(x => EnemyJa(x) == x).ToList();
            return miss.Count == 0 ? null : "日本語が無い分類: " + string.Join(",", miss);
        });
        yield return ("★表に無い鍵は鍵をそのまま返す（黙って空にしない）", () =>
            EnemyJa("no_such_class") == "no_such_class" ? null : "空になった");
        yield return ("★null は空文字（落ちない）", () =>
            EnemyJa(null) == "" ? null : "null で落ちるか別の答えになる");
        yield return ("C2 / C3 の語が引ける", () =>
            CardLevelJa(2) is not null && CardLevelJa(3) is not null ? null : "引けない");
        yield return ("★決まらないレベルは null（『不明』という語を足さない）", () =>
            CardLevelJa(null) is null && CardLevelJa(4) is null ? null : "null にならない");
    }
}

public static class BoardGeometry
{
    public static double FieldX0 => AnalysisTables.FieldX0;
    public static double FieldX1 => AnalysisTables.FieldX1;
    public static double FieldY0 => AnalysisTables.FieldY0;
    public static double FieldY1 => AnalysisTables.FieldY1;

    public static int BossTypeBoss => AnalysisTables.BossTypeBoss;

    public static double ShotC1LDrop => AnalysisTables.ShotC1LDrop;

    public static double LaserHalfFactor => AnalysisTables.LaserHalfFactor;

    public static IEnumerable<(string Label, Func<string?> Body)> SelfTestCases()
    {
        yield return ("盤の広さが 0 でない（母数）", () =>
            FieldX1 > FieldX0 && FieldY1 > FieldY0 ? null
            : $"広さが取れていない: x[{FieldX0},{FieldX1}] y[{FieldY0},{FieldY1}]");
        yield return ("★盤は横より縦が長い（軸の取り違えの見張り）", () =>
            FieldY1 - FieldY0 > FieldX1 - FieldX0 ? null : "縦横が入れ替わっている");
        yield return ("レーザーの係数が 0 でも 1 でもない（母数）", () =>
            LaserHalfFactor > 0 && LaserHalfFactor < 1 ? null
            : $"係数が {LaserHalfFactor}。0 なら幅が消え、1 なら全幅を半幅と読んでいる");
        yield return ("★釘付け: 原本が動いたら落ちる（★否定テストではない）", () =>
        {
            const double legacyX0 = -136.0, legacyX1 = 136.0, legacyY0 = 16.0, legacyY1 = 432.0;
            const double legacyLaser = 0.25;
            if (legacyX0 != FieldX0 || legacyX1 != FieldX1
                || legacyY0 != FieldY0 || legacyY1 != FieldY1)
                return "盤の広さが写しと違う。★原本が動いたなら、この検査の値も一緒に見直すこと";
            return legacyLaser == LaserHalfFactor ? null
                : "レーザーの係数が写しと違う。★原本が動いたなら、この検査の値も一緒に見直すこと";
        });
    }
}

public static class SpiritShapeNames
{
    public static string Circle => AnalysisTables.SpiritShapeCircle;
    public static string CircleUp => AnalysisTables.SpiritShapeCircleUp;
    public static string BandV => AnalysisTables.SpiritShapeBandV;
    public static string BandH => AnalysisTables.SpiritShapeBandH;
    public static string Cross => AnalysisTables.SpiritShapeCross;
    public static string Fan => AnalysisTables.SpiritShapeFan;
    public static string ConeUp => AnalysisTables.SpiritShapeConeUp;
    public static string Lens => AnalysisTables.SpiritShapeLens;
    public static string Flower => AnalysisTables.SpiritShapeFlower;
    public static string Star => AnalysisTables.SpiritShapeStar;

    public static IReadOnlyList<string> All =>
        [Circle, CircleUp, BandV, BandH, Cross, Fan, ConeUp, Lens, Flower, Star];

    private static readonly Dictionary<string, string> ShapeJa = new(StringComparer.Ordinal)
    {
        [AnalysisTables.SpiritShapeCircle] = "円（自機中心）",
        [AnalysisTables.SpiritShapeCircleUp] = "円（自機上部）",
        [AnalysisTables.SpiritShapeBandV] = "上下無限",
        [AnalysisTables.SpiritShapeBandH] = "左右無限",
        [AnalysisTables.SpiritShapeCross] = "十字",
        [AnalysisTables.SpiritShapeFan] = "扇",
        [AnalysisTables.SpiritShapeConeUp] = "上方コーン",
        [AnalysisTables.SpiritShapeLens] = "レンズ",
        [AnalysisTables.SpiritShapeFlower] = "花",
        [AnalysisTables.SpiritShapeStar] = "星",
    };

    public static string Ja(string? name) =>
        name is not null && ShapeJa.TryGetValue(name, out var ja) ? ja : (name ?? "");

    public static IEnumerable<(string Label, Func<string?> Body)> SelfTestCases()
    {
        yield return ($"形の語が {All.Count} 種そろっている（母数）", () =>
            All.Count > 0 && All.All(x => !string.IsNullOrEmpty(x)) ? null : "空の語がある");
        yield return ("★語が重複していない（別の形が同じ字にならない）", () =>
            All.Distinct(StringComparer.Ordinal).Count() == All.Count ? null
            : "重複: " + string.Join(",", All.GroupBy(x => x).Where(g => g.Count() > 1).Select(g => g.Key)));
        yield return ("★表に居る全キャラの形が、この一覧に入っている", () =>
        {
            var miss = SpiritField.Chars.Select(c => SpiritField.Of(c)!.Name)
                                        .Distinct(StringComparer.Ordinal)
                                        .Where(n => !All.Contains(n, StringComparer.Ordinal)).ToList();
            return miss.Count == 0 ? null : "一覧に無い形: " + string.Join(",", miss);
        });
        yield return ($"★日本語の語が 10 種そろっている（母数 {ShapeJa.Count}）", () =>
            ShapeJa.Count == All.Count ? null
            : $"{ShapeJa.Count} 語しかない（形は {All.Count} 種）");
        yield return ("★指定どおりの語が引ける（言い換えていないか）", () =>
        {
            (string Key, string Want)[] want =
            [
                (Circle, "円（自機中心）"), (CircleUp, "円（自機上部）"),
                (BandV, "上下無限"), (BandH, "左右無限"), (Cross, "十字"),
                (Fan, "扇"), (ConeUp, "上方コーン"), (Lens, "レンズ"),
                (Flower, "花"), (Star, "星"),
            ];
            var bad = want.Where(w => Ja(w.Key) != w.Want).ToList();
            return bad.Count == 0 ? null
                : "食い違い: " + string.Join(",", bad.Select(b => $"{b.Key}→{Ja(b.Key)}"));
        });
        yield return ("★★全 10 種が日本語で引ける（英語キーが画面へ漏れない）", () =>
        {
            var raw = All.Where(k => Ja(k) == k).ToList();
            return raw.Count == 0 ? null
                : "日本語が無く鍵のまま出る形: " + string.Join(",", raw);
        });
        yield return ("★表に無い鍵は鍵をそのまま返す（落ちない。新しい形が増えたときの受け）", () =>
            Ja("shape_that_does_not_exist") == "shape_that_does_not_exist" ? null
            : "知らない鍵で落ちるか別の答えになる");
        yield return ("★null は空文字（落ちない）", () =>
            Ja(null) == "" ? null : "null で落ちるか別の答えになる");
    }
}

public static class BossAttacks
{
    private static readonly Dictionary<(int Char, int Sub), string> Names = LoadNames();
    private static readonly Dictionary<int, string> Display = LoadDisplay();
    private static readonly HashSet<(int Char, int Sub)> Idle = LoadIdle();

    private static Dictionary<(int, int), string> LoadNames()
    {
        var map = new Dictionary<(int, int), string>();
        foreach (var line in Packed.Lines(AnalysisTables.BossAttackNamesPacked))
        {
            var f = line.Split('\t');
            if (f.Length != 3) throw new InvalidDataException($"攻撃名の行が 3 列でない: {line}");
            map[(int.Parse(f[0], CultureInfo.InvariantCulture),
                 int.Parse(f[1], CultureInfo.InvariantCulture))] = f[2];
        }
        return map;
    }

    private static Dictionary<int, string> LoadDisplay()
    {
        var map = new Dictionary<int, string>();
        foreach (var line in Packed.Lines(AnalysisTables.BossDisplayNamesPacked))
        {
            var f = line.Split('\t');
            if (f.Length != 2) throw new InvalidDataException($"弾幕名の行が 2 列でない: {line}");
            map[int.Parse(f[0], CultureInfo.InvariantCulture)] = f[1];
        }
        return map;
    }

    private static HashSet<(int, int)> LoadIdle()
    {
        var set = new HashSet<(int, int)>();
        foreach (var line in Packed.Lines(AnalysisTables.BossIdleSubsPacked))
        {
            var f = line.Split('\t');
            if (f.Length != 2) throw new InvalidDataException($"待機の sub の行が 2 列でない: {line}");
            set.Add((int.Parse(f[0], CultureInfo.InvariantCulture),
                     int.Parse(f[1], CultureInfo.InvariantCulture)));
        }
        return set;
    }

    public static int NameCount => Names.Count;
    public static int DisplayCount => Display.Count;
    public static int IdleCount => Idle.Count;

    public static string? NameOf(int? charId, int? sub) =>
        charId is not null && sub is not null
        && Names.TryGetValue((charId.Value, sub.Value), out var v) ? v : null;

    public static string? DisplayOf(int? charId) =>
        charId is not null && Display.TryGetValue(charId.Value, out var v) ? v : null;

    public static bool IsIdle(int? charId, int? sub) =>
        charId is not null && sub is not null && Idle.Contains((charId.Value, sub.Value));

    public static IEnumerable<(string Label, Func<string?> Body)> SelfTestCases()
    {
        yield return ($"母数（攻撃名 {NameCount} / 弾幕名 {DisplayCount} / 待機 {IdleCount}）", () =>
            NameCount > 0 && DisplayCount > 0 && IdleCount > 0 ? null : "0 件。生成物を引けていない");
        yield return ("★16 キャラぶんの弾幕名がある（別の道から数え直す）", () =>
            DisplayCount == 16 ? null : $"{DisplayCount} キャラぶんしかない");
        yield return ("★攻撃名が空の行が 1 つも無い（載せない決まり）", () =>
            Names.Values.All(v => !string.IsNullOrWhiteSpace(v)) ? null : "空の攻撃名がある");
        yield return ("★知らない組み合わせは null（推測で作らない）", () =>
            NameOf(0, 999) is null && NameOf(99, 3) is null ? null : "名前を作っている");
        yield return ("★null を渡しても落ちない", () =>
            NameOf(null, 3) is null && NameOf(0, null) is null && DisplayOf(null) is null
            ? null : "null で落ちるか別の答えになる");
        yield return ("★★待機の sub は「名前が無いのが正しい」と言える", () =>
        {
            if (!IsIdle(0, 2)) return "霊夢の待機（sub 2）を待機と言えない";
            if (NameOf(0, 2) is not null) return "待機に攻撃名が付いている";
            return null;
        });
        yield return ("★否定: 旧（名前が無ければ待機と答える）なら、知らない sub が待機に化ける", () =>
        {
            bool LegacyIdle(int c, int s) => NameOf(c, s) is null;
            if (!LegacyIdle(0, 999)) return "旧が落ちない（写しが間違っている）";
            return IsIdle(0, 999) ? "新でも知らない sub が待機に化けている" : null;
        });
        yield return ("★★攻撃の sub と待機の sub が重なっていない", () =>
        {
            var bad = Names.Keys.Where(k => Idle.Contains(k)).ToList();
            return bad.Count == 0 ? null
                : "攻撃名と待機の両方にある: " + string.Join(",", bad.Select(k => $"{k.Char}:{k.Sub}"));
        });
    }
}
