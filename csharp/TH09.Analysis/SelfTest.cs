using System.Text;

namespace TH09.Analysis;

public static class SelfTest
{
    private static int _ng;
    private static int _ran;

    public static int Run()
    {
        _ng = 0; _ran = 0;
        Console.WriteLine("=== 段階 6（分析・集計）の自己検査 ===");

        TableSanity();
        FloatBits();
        IntNormalize();
        Escapes();
        KeyOrder();
        JsonKinds();
        DecodeValues();
        CoordPredicates();
        BoardLayers();
        Display();
        HitWindowSides();
        Negative();

        Console.WriteLine();
        Console.WriteLine($"{_ran} 件検査して NG {_ng} 件");
        if (_ran == 0)
        {
            Console.WriteLine("★1 件も検査していません。これは『全部通った』ではありません。");
            return 2;
        }
        Console.WriteLine(_ng == 0 ? "すべて ok" : "★失敗あり");
        Console.WriteLine("★これは全数の突き合わせではない（parity/run_parity.py を回すこと）");
        return _ng == 0 ? 0 : 1;
    }

    private static void Check(string label, Func<string?> body)
    {
        _ran++;
        string? why;
        try { why = body(); }
        catch (Exception e) { why = $"{e.GetType().Name}: {e.Message}"; }
        if (why is null) { Console.WriteLine("  ok   " + label); return; }
        _ng++;
        Console.WriteLine($"  NG   {label} —— {why}");
    }

    private static string? Eq<T>(string what, T got, T want) =>
        EqualityComparer<T>.Default.Equals(got, want) ? null : $"{what}: {got} ≠ {want}";


    private static void TableSanity()
    {
        Console.WriteLine("[表の母数]（★別の道から数え直す。0 件は「測れていない」の兆候）");
        Check("主リングの語と解き方の表が同じ数", () =>
            Eq("語数", TimelineDecode.SpecCount, TimelineDecode.RecordFields.Length));
        Check("語が 1 つ以上ある", () =>
            TimelineDecode.SpecCount > 0 ? null : "0 語。生成物を引けていない");
        Check("float の語の数が、生成器の数え上げと一致", () =>
        {
            int n = TimelineDecode.RecordFields.Count(x => TimelineDecode.SpecOf(x).Kind == 'f');
            return Eq("float 語", n, AnalysisTables.FloatFieldCount);
        });
        Check("符号付きの語の数が、生成器の数え上げと一致", () =>
        {
            int n = TimelineDecode.RecordFields.Count(x => TimelineDecode.SpecOf(x).Kind == 's');
            return Eq("符号付き語", n, AnalysisTables.SignedFieldCount);
        });
        Check("flags の区画の数が、生成器の数え上げと一致", () =>
        {
            int n = TimelineDecode.RecordFields.Select(x => TimelineDecode.SpecOf(x).FlagMask)
                                               .Where(m => m != 0).Distinct().Count();
            return Eq("区画", n, AnalysisTables.FlagGroupCount);
        });
        Check("座標リングの枠の数が、生成器の定数と一致", () =>
            Eq("枠", CoordRing.SlotCount, AnalysisTables.CoordSlots));
        Check("枠の種別が全部で 1938 枠を覆う（隙間も重なりも無い）", () =>
        {
            for (int s = 0; s < CoordRing.SlotCount; s++)
            {
                int kinds = (CoordRing.IsBulletSlot(s) ? 1 : 0) + (CoordRing.IsEnemySlot(s) ? 1 : 0)
                          + (CoordRing.IsLaserSlot(s) ? 1 : 0) + (CoordRing.IsExSlot(s) ? 1 : 0)
                          + (CoordRing.IsShotSlot(s) ? 1 : 0);
                if (kinds != 1) return $"枠 {s}（{CoordRing.BaseName(s)}）が {kinds} 種に当たる";
            }
            return null;
        });
        Check("出力形式の版が 1（Python 側の FORMAT_VERSION と揃える）", () =>
            Eq("format", ParityDump.FormatVersion, 1));
    }


    private static readonly (string Literal, string Hex)[] FloatFixtures =
    [
        ("0.0", "0000000000000000"),
        ("-0.0", "8000000000000000"),
        ("0.1", "3fb999999999999a"),
        ("0.3333333333333333", "3fd5555555555555"),
        ("1e-5", "3ee4f8b588e368f1"),
        ("1E308", "7fe1ccf385ebc8a0"),
        ("1e999", "7ff0000000000000"),
        ("-86.410400390625", "c0559a4400000000"),
    ];

    private static void FloatBits()
    {
        Console.WriteLine("[小数のビット]（★実データに無い極値・指数表記を合成で通す）");
        foreach (var (lit, hex) in FloatFixtures)
        {
            Check($"{lit} → {hex}", () =>
            {
                var (type, text) = FeatureJson.Value.Float(lit).Cells();
                return type != "f64" ? $"型が {type}" : Eq("16 進", text, hex);
            });
        }
    }

    private static void IntNormalize()
    {
        Console.WriteLine("[整数の正規化]（★long を超える桁も落とさない）");
        (string, string)[] cases =
        [
            ("0", "0"), ("-0", "0"), ("-1", "-1"),
            ("9223372036854775807", "9223372036854775807"),
            ("-9223372036854775808", "-9223372036854775808"),
            ("18446744073709551616", "18446744073709551616"),
        ];
        foreach (var (lit, want) in cases)
            Check($"{lit} → {want}", () => Eq("整数", FeatureJson.Value.Int(lit).Cells().Text, want));
    }

    private static void Escapes()
    {
        Console.WriteLine("[逃がし]（★実データに 0 件。合成でしか通らない）");
        Check("バックスラッシュ", () => Eq("", FeatureJson.Escape("a\\b"), "a\\\\b"));
        Check("タブ", () => Eq("", FeatureJson.Escape("a\tb"), "a\\tb"));
        Check("CR", () => Eq("", FeatureJson.Escape("a\rb"), "a\\rb"));
        Check("LF", () => Eq("", FeatureJson.Escape("a\nb"), "a\\nb"));
        Check("4 つ全部（順序が要点）", () => Eq("", FeatureJson.Escape("\\\t\r\n"), "\\\\\\t\\r\\n"));
        Check("二重引用符は逃がさない（JSON のエスケープは使わない）", () =>
            Eq("", FeatureJson.Escape("a\"b"), "a\"b"));
        Check("空文字", () => Eq("", FeatureJson.Escape(""), ""));
        Check("非 ASCII はそのまま（UTF-8 で書く）", () => Eq("", FeatureJson.Escape("弾"), "弾"));
    }

    private static readonly string[] KeyProbe = ["ab", "aB", "a_b", "A_b", "Ab", "a", "_a", "zz", "Zz"];

    private static void KeyOrder()
    {
        Console.WriteLine("[鍵の並び]（★Ordinal 固定。Python の sorted() と揃える）");
        Check("Ordinal 昇順になる", () =>
        {
            var map = KeyProbe.ToDictionary(k => k, _ => FeatureJson.Value.Int("0"), StringComparer.Ordinal);
            var got = string.Join(",", FeatureJson.SortedKeys(map));
            return Eq("並び", got, "A_b,Ab,Zz,_a,a,aB,a_b,ab,zz");
        });
    }

    private static void JsonKinds()
    {
        Console.WriteLine("[JSON のリテラルの姿]（★0 と 0.0 を区別できているか）");
        Check("0 は整数、0.0 は小数", () =>
        {
            var m = FeatureJson.Parse("{\"i\":0,\"f\":0.0}", "selftest");
            var a = m["i"].Cells(); var b = m["f"].Cells();
            if (a.Type != "i") return "0 が " + a.Type;
            if (b.Type != "f64") return "0.0 が " + b.Type;
            return null;
        });
        Check("真偽値（★実データに 0 件）", () =>
        {
            var m = FeatureJson.Parse("{\"t\":true,\"f\":false}", "selftest");
            return Eq("true", m["t"].Cells().Text, "true") ?? Eq("false", m["f"].Cells().Text, "false");
        });
        Check("null は空セル", () =>
        {
            var (type, text) = FeatureJson.Parse("{\"n\":null}", "selftest")["n"].Cells();
            return Eq("型", type, "null") ?? Eq("値", text, "");
        });
        Check("鍵の重複は後勝ち（Python の dict と同じ）", () =>
            Eq("", FeatureJson.Parse("{\"d\":1,\"d\":2.0,\"d\":\"last\"}", "selftest")["d"].Cells().Text, "last"));
        Check("代理対（サロゲートペア）が壊れない", () =>
            Eq("", FeatureJson.Parse("{\"s\":\"\\ud83c\\udf38\"}", "selftest")["s"].Cells().Text, "🌸"));
        Check("空の JSON / null 列は {} として扱う", () =>
            Eq("鍵の数", FeatureJson.Parse(null, "selftest").Count, 0));
    }

    private static void DecodeValues()
    {
        Console.WriteLine("[主リングの値]（★層で -999999 の扱いが違う。ここは欠測にする側）");
        Check("float の語は float になる", () =>
        {
            var v = TimelineDecode.Decode("p1_gauge", BitConverter.SingleToUInt32Bits(1.5f), 0xFFFF_FFFF);
            return v.Kind != TickKind.Float ? "float にならない" : Eq("値", v.AsDouble, 1.5);
        });
        Check("符号付きの語の番兵は欠測（★段階 1 の層は -999999 のまま通す。混ぜない）", () =>
        {
            var v = TimelineDecode.Decode("p1_cpu_quick_timer", unchecked((uint)-999999), 0xFFFF_FFFF);
            return v.IsMissing ? null : "欠測にならない: " + v;
        });
        Check("flags の区画が落ちていれば欠測（★0 は「0 だった」ではない）", () =>
        {
            var spec = TimelineDecode.SpecOf("p1_gauge");
            if (spec.FlagMask == 0) return "p1_gauge に flags のガードが無い（表が壊れている）";
            return TimelineDecode.Decode(spec, 0, 0).IsMissing ? null : "欠測にならない";
        });
        Check("ガードの無い語は flags に関係なく読める", () =>
        {
            var spec = TimelineDecode.SpecOf("seq_begin");
            return spec.FlagMask != 0 ? "seq_begin にガードが付いている"
                 : Eq("値", TimelineDecode.Decode(spec, 42, 0).AsLong, 42L);
        });
    }

    private static void CoordPredicates()
    {
        Console.WriteLine("[枠の述語]（★生存・消滅演出・レーザーの致死）");
        int bullet = AnalysisTables.CoordBaseP1Bullet;
        int enemy = AnalysisTables.CoordBaseP1Enemy;
        int laser = AnalysisTables.CoordBaseP1Laser;
        int shot = AnalysisTables.CoordBaseP1Shot;

        Check("弾: 由来が相乗りした 6 は死んでいる（★マスクしてから見る）", () =>
            CoordRing.SlotIsAlive(bullet, 6u | (5u << 3)) ? "生きていることになった" : null);
        Check("弾: 由来が相乗りした 1 は生きている", () =>
            CoordRing.SlotIsAlive(bullet, 1u | (5u << 3)) ? null : "死んでいることになった");
        Check("弾: state 5 は生きているが消滅演出中", () =>
            CoordRing.SlotIsAlive(bullet, 5u | (3u << 3)) && CoordRing.SlotIsVanishing(bullet, 5u | (3u << 3))
                ? null : "消滅演出として拾えていない");
        Check("ショット: state 2 は生きているが消滅演出中", () =>
            CoordRing.SlotIsAlive(shot, 2u) && CoordRing.SlotIsVanishing(shot, 2u) ? null : "拾えていない");
        Check("弾以外・ショット以外は消滅演出の段を持たない（確かめていないものを混ぜない）", () =>
            !CoordRing.SlotIsVanishing(enemy, 5u) && !CoordRing.SlotIsVanishing(laser, 5u)
                ? null : "敵かレーザーが消滅演出になった");
        Check("敵: bit0 が生死", () =>
            CoordRing.SlotIsAlive(enemy, 1u) && !CoordRing.SlotIsAlive(enemy, 2u) ? null : "判定が逆");

        Check("レーザー phase1 は無条件に当たる", () =>
            CoordRing.LaserIsLethal(1u, 0, 999, 0) ? null : "当たらないことになった");
        Check("レーザー phase0 は timer >= gate0", () =>
            CoordRing.LaserIsLethal(0u, 10, 10, 0) && !CoordRing.LaserIsLethal(0u, 9, 10, 0)
                ? null : "setge になっていない");
        Check("レーザー phase2 は timer < gate2", () =>
            CoordRing.LaserIsLethal(2u, 9, 0, 10) && !CoordRing.LaserIsLethal(2u, 10, 0, 10)
                ? null : "setl になっていない");
        Check("知らない phase は当たらない", () =>
            CoordRing.LaserIsLethal(3u, 0, 0, 0) ? "当たることになった" : null);

        Check("敵の分類: リリーは妖精より先に見る", () =>
            Eq("", CoordRing.EnemyClass(AnalysisTables.CoordEnemyLilyBit), AnalysisTables.EnemyClassLily));
        Check("敵の分類: 本物のボスは列挙 3", () =>
            Eq("", CoordRing.EnemyClass(AnalysisTables.CoordEnemyBossMask), AnalysisTables.EnemyClassBoss));
        Check("敵の分類: kind==0 は妖精（★これは仕様。生死は呼ぶ側が見る）", () =>
            Eq("", CoordRing.EnemyClass(0), AnalysisTables.EnemyClassFairy));

        Check("幽霊の活性化: bit12（0x1000）が立っていれば真、無ければ偽", () =>
        {
            uint ghost = AnalysisTables.CoordEnemyGhostMask;
            if (CoordRing.EnemyGhostActivated(ghost)) return "bit12 を立てていないのに真になった";
            return CoordRing.EnemyGhostActivated(ghost | 0x1000u) ? null : "bit12 を立てても偽のまま";
        });
        Check("幽霊の活性化: 由来ビットが相乗りする高位（bit16 以上）があっても答えは変わらない", () =>
        {
            uint kind = AnalysisTables.CoordEnemyGhostMask | 0x1000u | (0xABu << 16);
            return CoordRing.EnemyGhostActivated(kind) ? null : "高位ビットに引かれて偽になった";
        });
        Check("★否定: 旧（＝常に偽を返す）なら、bit12 を立てても活性化を見落とす", () =>
        {
            static bool LegacyAlwaysFalse(uint _) => false;
            uint activated = AnalysisTables.CoordEnemyGhostMask | 0x1000u;
            if (LegacyAlwaysFalse(activated)) return "旧の写しが真を返した（否定テストが効いていない）";
            return CoordRing.EnemyGhostActivated(activated) ? null : "新でも活性化を見落とした";
        });
    }

    private static void Display()
    {
        Console.WriteLine("[表示種]（★人手で作った表。C# は写さずに引くだけ）");
        Check("項目が 1 つ以上ある", () =>
            FieldDisplay.Count > 0 ? null : "0 項目。生成物を引けていない");
        Check("表示種の表と主リングの語が同じ数（★別の道から数え直す）", () =>
            Eq("項目", FieldDisplay.Count, TimelineDecode.RecordFields.Length));
        Check("横軸は round_frames の 1 本だけ（★あちらの docstring が明記）", () =>
        {
            var axis = FieldDisplay.NamesOf("AXIS");
            return axis.Count != 1 ? $"{axis.Count} 本ある"
                 : Eq("語", axis[0], "round_frames");
        });
        Check("表に無い語は NONE", () => Eq("", FieldDisplay.Kind("そんな語は無い"), "NONE"));
        Check("表示倍率（ライフは /2、スコアは *10）", () =>
            Eq("life", FieldDisplay.Scale("p1_life_raw"), 0.5)
            ?? Eq("score", FieldDisplay.Scale("p2_score_raw"), 10.0)
            ?? (FieldDisplay.Scale("p1_gauge") is null ? null : "倍率の無い語に倍率が付いた"));
        Check("1P/2P の対を 1 つの系列名にまとめる", () =>
            Eq("p1_gauge", FieldDisplay.PairBase("p1_gauge"), "gauge")
            ?? Eq("internal_rank", FieldDisplay.PairBase("internal_rank"), "internal_rank"));
        Check("側の判定", () =>
            Eq("p1", FieldDisplay.SideOf("p1_gauge"), 1) ?? Eq("p2", FieldDisplay.SideOf("p2_gauge"), 2)
            ?? Eq("共通", FieldDisplay.SideOf("internal_rank"), 0));
        Check("1P/2P で 1 本しかない語に internal_rank が入っている", () =>
            FieldDisplay.SharedFields.Contains("internal_rank") ? null : "入っていない");
    }


    private static string LegacyEscapeWrongOrder(string text) =>
        text.Replace("\t", "\\t").Replace("\r", "\\r").Replace("\n", "\\n").Replace("\\", "\\\\");

    private static string LegacyFloatBitsLittleEndian(double v) =>
        Convert.ToHexStringLower(BitConverter.GetBytes(v));

    private static string LegacyNumberAsDouble(string literal) =>
        double.Parse(literal, System.Globalization.CultureInfo.InvariantCulture)
              .ToString(System.Globalization.CultureInfo.InvariantCulture);

    private static bool LegacyBulletAliveNoMask(uint state) => state != 0 && state != 6;


    private static string Hits(params (int Side, string Trigger, long Seq)[] ev) =>
        "[" + string.Join(",", ev.Select(e =>
            $"{{\"seq\":{e.Seq},\"side\":{e.Side},\"trigger\":\"{e.Trigger}\"}}")) + "]";

    private static void HitWindowSides()
    {
        Console.WriteLine("[被弾窓の一覧]（★1 本の窓に両側が入ったら、側ごとに 1 件）");
        Check("① 片側だけの窓は 1 件（★対象が生きていることのガード）", () =>
        {
            var got = HitWindowIndex.PerSide(Hits((1, "hit_kind", 10), (1, "life_raw", 11)));
            if (got.Count != 1) return $"件数 {got.Count} ≠ 1";
            return Eq("側", got[0].Side, 1) ?? Eq("種別", got[0].Trigger, "hit_kind");
        });
        Check("② 1P→2P の連続被弾は 2 件（両側が同じ窓に入る形）", () =>
        {
            var got = HitWindowIndex.PerSide(Hits(
                (1, "life_raw", 10), (1, "hit_kind", 9),
                (2, "life_raw", 40), (2, "hit_kind", 39),
                (1, "life_raw", 70), (1, "hit_kind", 69)));
            if (got.Count != 2) return $"件数 {got.Count} ≠ 2（側ごとに 1 件のはず）";
            return Eq("1 件目の側", got[0].Side, 1) ?? Eq("2 件目の側", got[1].Side, 2);
        });
        Check("③ 2P が先なら 2P が先に出る（★側の番号ではなく通し番号の順）", () =>
        {
            var got = HitWindowIndex.PerSide(Hits(
                (2, "hit_kind", 9), (1, "hit_kind", 39)));
            if (got.Count != 2) return $"件数 {got.Count} ≠ 2";
            return Eq("1 件目の側", got[0].Side, 2) ?? Eq("2 件目の側", got[1].Side, 1);
        });
        Check("④ その側の先頭が詰みクイックでも、本当の被弾を種別に採る", () =>
        {
            var got = HitWindowIndex.PerSide(Hits(
                (1, "quick", 10), (1, "hit_kind", 30), (2, "quick", 50)));
            if (got.Count != 2) return $"件数 {got.Count} ≠ 2";
            var p1 = got.First(x => x.Side == 1);
            var p2 = got.First(x => x.Side == 2);
            return Eq("1P の種別", p1.Trigger, "hit_kind") ?? Eq("1P の seq", p1.Seq, 30L)
                   ?? Eq("2P の種別", p2.Trigger, "quick");
        });
        Check("⑤ 読めない hits は空（★側を 1 に倒さない）", () =>
        {
            if (HitWindowIndex.PerSide("これは JSON ではない").Count != 0) return "空でない";
            return HitWindowIndex.PerSide("").Count == 0 ? null : "空文字で空でない";
        });
    }

    private static void Negative()
    {
        Console.WriteLine("[否定]（★旧実装の写しを置いて『旧なら落ちる』ことを見る）");

        Check("旧: 逃がす順を間違えると二重に逃がされる（★新は正しい）", () =>
        {
            var legacy = LegacyEscapeWrongOrder("a\tb");
            var now = FeatureJson.Escape("a\tb");
            if (legacy == now) return "旧実装でも同じ答えになった（否定テストが効いていない）";
            return Eq("新", now, "a\\tb");
        });
        Check("旧: リトルエンディアンで吐くと Python と揃わない（★新は揃う）", () =>
        {
            var legacy = LegacyFloatBitsLittleEndian(1.0);
            var now = FeatureJson.Value.Float("1.0").Cells().Text;
            if (legacy == now) return "旧実装でも同じ答えになった";
            return Eq("旧", legacy, "000000000000f03f") ?? Eq("新", now, "3ff0000000000000");
        });
        Check("旧: 全部 double にすると 0.0 と 0 が同じになる（★新は分かれる）", () =>
        {
            if (LegacyNumberAsDouble("0") != LegacyNumberAsDouble("0.0"))
                return "旧実装でも分かれてしまった（否定テストが効いていない）";
            var a = FeatureJson.Value.Int("0").Cells();
            var b = FeatureJson.Value.Float("0.0").Cells();
            return a.Type == b.Type ? "新でも同じ型になった" : null;
        });
        Check("旧: マスク無しだと由来つきの 6 が生きているに化ける（★新は死んでいる）", () =>
        {
            uint state = 6u | (5u << 3);
            if (!LegacyBulletAliveNoMask(state)) return "旧実装でも死んでいた（否定テストが効いていない）";
            return CoordRing.SlotIsAlive(AnalysisTables.CoordBaseP1Bullet, state)
                ? "新でも生きていることになった" : null;
        });
        Check("旧: 大小を無視して並べると Python の sorted() と変わる（★新は Ordinal）", () =>
        {
            var legacy = KeyProbe.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList();
            var map = KeyProbe.ToDictionary(k => k, _ => FeatureJson.Value.Int("0"), StringComparer.Ordinal);
            var now = FeatureJson.SortedKeys(map);
            return string.Join(",", legacy) == string.Join(",", now)
                ? "旧実装でも同じ並びになった（否定テストが効いていない）" : null;
        });

        Check("入れ子は落とす（黙って平たくしない）", () =>
            Throws(() => FeatureJson.Parse("{\"a\":[1,2]}", "selftest")) ? null : "落ちなかった");
        Check("入れ子のオブジェクトも落とす", () =>
            Throws(() => FeatureJson.Parse("{\"a\":{\"b\":1}}", "selftest")) ? null : "落ちなかった");
        Check("オブジェクトでない JSON は落とす", () =>
            Throws(() => FeatureJson.Parse("[1,2]", "selftest")) ? null : "落ちなかった");
        Check("知らない語は落とす（既定値で黙って埋めない）", () =>
            Throws(() => TimelineDecode.SpecOf("p1_score")) ? null : "落ちなかった");
        Check("主リングに無い列を引いたら落ちる", () =>
            Throws(() => { var _ = MainColumns.Empty["p1_gauge"]; }) ? null : "落ちなかった");
        Check("欠測を数として読んだら落ちる（0 で埋めない）", () =>
            Throws(() => { var _ = TickValue.Missing.AsDouble; }) ? null : "落ちなかった");
        Check("レーザー以外に laser_column_names を引いたら落ちる", () =>
            Throws(() => CoordRing.LaserCol(AnalysisTables.CoordBaseP1Bullet, 0)) ? null : "落ちなかった");
    }


    private static void BoardLayers()
    {
        Console.WriteLine("[盤面の層]（★層ごとのファイルが持つ検査をここで回す）");
        RunLayer("画面の語", BoardLabels.SelfTestCases());
        RunLayer("敵の実寸", EnemySize.SelfTestCases());
        RunLayer("無敵", PlayerInvincible.SelfTestCases());
        RunLayer("枠の占有区間", SlotSegments.SelfTestCases());
        RunLayer("弾の由来", BulletOrigin.SelfTestCases());
        RunLayer("レーザーの由来", LaserOrigin.SelfTestCases());
        RunLayer("自機ショット", PlayerShots.SelfTestCases());
        RunLayer("盤の幾何", BoardGeometry.SelfTestCases());
        RunLayer("弾消しリング", ClearRings.SelfTestCases());
        RunLayer("被弾の弾消しの点／ゲージ回復の予定", GaugeRecovery.SelfTestCases());
        RunLayer("撃破の爆風", EnemyBlasts.SelfTestCases());
        RunLayer("吸霊範囲", SpiritField.SelfTestCases());
        RunLayer("吸霊の形の語", SpiritShapeNames.SelfTestCases());
        RunLayer("ボスの攻撃名", BossAttacks.SelfTestCases());
        RunLayer("Ex アタック", ExItems.SelfTestCases());
        RunLayer("凍結の沈め・帯", FreezeSpans.SelfTestCases());
        RunLayer("カードアタックの事象", CardEvents.SelfTestCases());
        RunLayer("窓の打ち切り", WindowCut.SelfTestCases());
        RunLayer("軌跡・候補・被弾の確定", HitCandidates.SelfTestCases());
        RunLayer("危険物リストの重なり（本物のリスト）", HazardList.SelfTestCases());
    }

    private static void RunLayer(string name, IEnumerable<(string Label, Func<string?> Body)> cases)
    {
        int n = 0;
        foreach (var (label, body) in cases) { Check(name + ": " + label, body); n++; }
        Check(name + " の検査が 1 件以上ある", () =>
            n > 0 ? null : "0 件（★繋がっていないか、中身が空）");
    }

    private static bool Throws(Action body)
    {
        try { body(); return false; }
        catch { return true; }
    }

    private static bool Throws(Func<object?> body)
    {
        try { body(); return false; }
        catch { return true; }
    }
}
