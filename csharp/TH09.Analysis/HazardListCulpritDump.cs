using System.Globalization;
using System.Text.Json;
using TH09.Generated;

namespace TH09.Analysis;

public static class HazardListCulpritDump
{
    private readonly record struct Row(long Wno, int Side, bool IsQuick, long Seq, string? HitType);

    private readonly record struct HitTypeStat(int Total, int Resolved, int Match);

    private readonly record struct Example(long Sid, long Wno, int Side, long Seq, string Note);

    public static int DiagUnmatchedBullets(string l0Path, int? sessions, int want)
    {
        using var l0 = new AnalysisDb(l0Path);
        var bySid = new SortedDictionary<long, List<Row>>();
        using (var cmd = l0.Command(
                   "SELECT session_id, window_no, side, trigger, seq, json FROM hit_window_features"))
        using (var r = cmd.ExecuteReader())
        {
            while (r.Read())
            {
                var trig = r.GetString(3);
                if (trig != "life_raw") continue;
                var json = r.IsDBNull(5) ? "{}" : r.GetString(5);
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                if (!root.TryGetProperty("my_control", out var mc) || mc.ValueKind != JsonValueKind.Number
                    || mc.GetInt32() != 1
                    || !root.TryGetProperty("my_cpu_level", out var lvl) || lvl.ValueKind != JsonValueKind.Number
                    || lvl.GetInt32() != 60) continue;
                if (!root.TryGetProperty("hit_type", out var htEl) || htEl.GetString() != "弾") continue;
                var sid = r.GetInt64(0);
                if (!bySid.TryGetValue(sid, out var list)) bySid[sid] = list = [];
                list.Add(new Row(r.GetInt64(1), r.GetInt32(2), false, r.GetInt64(4), "弾"));
            }
        }
        var sids = bySid.Keys.ToList();
        if (sessions is int n && n > 0 && sids.Count > n) sids = sids.Take(n).ToList();

        int shown = 0;
        Console.WriteLine("# 診断: 弾に対応づかなかった被弾で、生の弾枠を前後 3 tick 走査する");
        foreach (var sid in sids)
        {
            if (shown >= want) break;
            foreach (var g in bySid[sid].GroupBy(x => (x.Wno, x.Side)))
            {
                if (shown >= want) break;
                Window? w;
                try { w = HitWindowReader.Open(l0, sid, g.Key.Wno); } catch { continue; }
                if (w is null || !w.Cols.Has("p" + g.Key.Side + "_h0_x")) continue;
                foreach (var row in g)
                {
                    if (shown >= want) break;
                    int raw = checked((int)(row.Seq - w.Meta.FirstSeq));
                    var got = HitCandidates.HazardListCulprit(w, g.Key.Side, raw, false);
                    if (got is null || got.Match is not null) continue;

                    int bas = got.TickIndex;
                    double ex = got.Element.X, ey = got.Element.Y;
                    int baseSlot = g.Key.Side == 1 ? AnalysisTables.CoordBaseP1Bullet : AnalysisTables.CoordBaseP2Bullet;
                    string best = "（見つからない）";
                    double bestD = double.MaxValue;
                    for (int d = -3; d <= 3; d++)
                    {
                        int t = bas + d;
                        if (t < 0 || t >= w.TickCount) continue;
                        for (int s = 0; s < AnalysisTables.CoordBulletSlots; s++)
                        {
                            int slot = baseSlot + s;
                            var st = w.Raw(CoordRing.ColState(slot), t);
                            if (st is null) continue;
                            var bx = w.Float(CoordRing.ColX(slot), t);
                            var by = w.Float(CoordRing.ColY(slot), t);
                            if (bx is null || by is null) continue;
                            double dd = Math.Sqrt((bx.Value - ex) * (bx.Value - ex) + (by.Value - ey) * (by.Value - ey));
                            if (dd < bestD)
                            {
                                bestD = dd;
                                bool alive = CoordRing.SlotIsAlive(slot, st.Value);
                                bool vanish = CoordRing.SlotIsVanishing(slot, st.Value);
                                best = $"tick差={d} 枠={slot} 距離={dd:F3} state=0x{st.Value:X} alive={alive} vanish={vanish}";
                            }
                        }
                    }
                    Console.WriteLine($"  session={sid} window={g.Key.Wno} side={g.Key.Side} seq={row.Seq} "
                                      + $"要素=({ex:F3},{ey:F3}) 最短: {best}");
                    shown++;
                }
            }
        }
        Console.WriteLine($"# {shown} 件を出した");
        return 0;
    }

    public static int DiagOneEx(string l0Path, long sid, long wno, int side)
    {
        using var l0 = new AnalysisDb(l0Path);
        var w = HitWindowReader.Open(l0, sid, wno);
        if (w is null) { Console.Error.WriteLine("窓が開けない"); return 1; }
        var got0 = HitCandidates.HazardListCulprit(w, side);
        if (got0 is null) { Console.WriteLine("HazardListCulprit が null"); return 0; }
        Console.WriteLine($"TickIndex={got0.TickIndex} ListIndex={got0.ListIndex} "
                          + $"要素形={got0.Element.Kind} x={got0.Element.X:F3} y={got0.Element.Y:F3} "
                          + $"重なり={got0.OverlapCount} Match={got0.Match}");
        int bas = got0.TickIndex;
        for (int d = -1; d <= 2; d++)
        {
            int t = bas + d;
            Console.WriteLine($"-- tick差={d}（{t}）--");
            foreach (var ex in ExItems.At(w, t, side))
            {
                double dd = Math.Sqrt((ex.X - got0.Element.X) * (ex.X - got0.Element.X)
                                      + (ex.Y - got0.Element.Y) * (ex.Y - got0.Element.Y));
                Console.WriteLine($"  slot={ex.Slot} x={ex.X:F3} y={ex.Y:F3} 距離={dd:F3} "
                                  + $"type={ex.ExType} name={ex.Name} hasHitbox={ex.HasHitbox} "
                                  + $"trail={(ex.Hitbox?.Trail is { } tr ? string.Join(",", tr) : "-")} "
                                  + $"center={ex.Hitbox?.Center ?? "-"}");
            }
        }
        return 0;
    }

    public static int DiagExFullTrail(string l0Path, long sid, long wno, int side, int slot)
    {
        using var l0 = new AnalysisDb(l0Path);
        var w = HitWindowReader.Open(l0, sid, wno);
        if (w is null) { Console.Error.WriteLine("窓が開けない"); return 1; }
        Console.WriteLine($"session={sid} window={wno} side={side} slot={slot} 窓の長さ={w.TickCount} tick");
        double? prevX = null, prevY = null;
        int prevTick = -1;
        int shown = 0, jumps = 0;
        for (int t = 0; t < w.TickCount; t++)
        {
            ExItem? found = null;
            foreach (var ex in ExItems.At(w, t, side))
                if (ex.Slot == slot) { found = ex; break; }
            if (found is null)
            {
                prevX = null; prevY = null;
                continue;
            }
            shown++;
            string jumpNote = "";
            if (prevX is double px && prevY is double py)
            {
                double dd = Math.Sqrt((found.X - px) * (found.X - px)
                                      + (found.Y - py) * (found.Y - py));
                if (dd > 100.0) { jumpNote = $"  ★跳んだ（前 tick={prevTick} からの距離={dd:F1}）"; jumps++; }
            }
            Console.WriteLine($"  tick={t} x={found.X:F3} y={found.Y:F3} "
                              + $"type={found.ExType} name={found.Name} "
                              + $"hasHitbox={found.HasHitbox}{jumpNote}");
            prevX = found.X; prevY = found.Y; prevTick = t;
        }
        Console.WriteLine($"# 出た tick 数={shown} / 跳んだ回数={jumps}");
        return 0;
    }

    public static int Run(string l0Path, int? sessions, int examplesWanted = 0)
    {
        Console.WriteLine("# 危険物リストの『最初の重なり』―― 精度測定（読むだけ）");
        Console.WriteLine("# 読む Layer 0: " + l0Path);
        using var l0 = new AnalysisDb(l0Path);
        if (!l0.HasTable("hit_window_features"))
        {
            Console.Error.WriteLine("★hit_window_features が Layer 0 にありません。測れていないので中止します。");
            return 1;
        }

        var bySid = new SortedDictionary<long, List<Row>>();
        int nRowsSeen = 0, nSkipTrigger = 0, nSkipScope = 0, nSkipNoHitType = 0;
        using (var cmd = l0.Command(
                   "SELECT session_id, window_no, side, trigger, seq, json FROM hit_window_features"))
        using (var r = cmd.ExecuteReader())
        {
            while (r.Read())
            {
                nRowsSeen++;
                var trig = r.GetString(3);
                if (trig != "life_raw" && trig != "quick") { nSkipTrigger++; continue; }
                var json = r.IsDBNull(5) ? "{}" : r.GetString(5);
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                if (!root.TryGetProperty("my_control", out var mc) || mc.ValueKind != JsonValueKind.Number
                    || mc.GetInt32() != 1
                    || !root.TryGetProperty("my_cpu_level", out var lvl) || lvl.ValueKind != JsonValueKind.Number
                    || lvl.GetInt32() != 60)
                {
                    nSkipScope++;
                    continue;
                }
                string? ht = null;
                bool isQuick = trig == "quick";
                if (!isQuick)
                {
                    if (!root.TryGetProperty("hit_type", out var htEl))
                    {
                        nSkipNoHitType++;
                        continue;
                    }
                    ht = htEl.GetString();
                }
                var sid = r.GetInt64(0);
                if (!bySid.TryGetValue(sid, out var list)) bySid[sid] = list = [];
                list.Add(new Row(r.GetInt64(1), r.GetInt32(2), isQuick, r.GetInt64(4), ht));
            }
        }

        var allSids = bySid.Keys.ToList();
        var sids = allSids;
        if (sessions is int want && want > 0 && allSids.Count > want)
        {
            int step = Math.Max(1, allSids.Count / want);
            sids = allSids.Where((_, i) => i % step == 0).Take(want).ToList();
        }
        Console.WriteLine($"# 索引: 行 {nRowsSeen}（trigger 対象外 {nSkipTrigger} / スコープ外 {nSkipScope} "
                          + $"/ 種別なしで外した {nSkipNoHitType}）／ セッション {sids.Count} 本"
                          + (sids.Count == allSids.Count ? "" : $"（全 {allSids.Count} 本から間引き）"));

        int nHitTotal = 0, nQuickTotal = 0, nHitResolved = 0, nHitMatch = 0, nQuickOverlap = 0;
        var byType = new SortedDictionary<string, HitTypeStat>(StringComparer.Ordinal);
        var skipped = new SortedDictionary<string, int>(StringComparer.Ordinal);
        void Skip(string why, int n = 1) => skipped[why] = skipped.GetValueOrDefault(why) + n;

        var exQuickLaser = new List<Example>();
        var exQuickBullet = new List<Example>();
        var exQuickEx = new List<Example>();
        var exQuickEnemy = new List<Example>();
        var exHitUnresolved = new List<Example>();
        var exHitEnemy = new List<Example>();
        var exUnmatchedRemaining = new List<Example>();
        bool WantMore(List<Example> l) => examplesWanted > 0 && l.Count < examplesWanted;

        static string MatchKindJa(HitCandidates.HazardBoardCandidate? m) => m switch
        {
            { Kind: HitCandidates.HazardMatchKind.Bullet } => "弾",
            { Kind: HitCandidates.HazardMatchKind.Enemy } => "敵",
            { Kind: HitCandidates.HazardMatchKind.Laser } => "レーザー",
            { Kind: HitCandidates.HazardMatchKind.Ex } => "Ex",
            _ => "対応づけられない",
        };
        var matchByKindHit = new SortedDictionary<string, int>(StringComparer.Ordinal);
        var matchByKindQuick = new SortedDictionary<string, int>(StringComparer.Ordinal);

        foreach (var sid in sids)
        {
            var byWin = bySid[sid].GroupBy(x => (x.Wno, x.Side));
            foreach (var g in byWin)
            {
                Window? w;
                try { w = HitWindowReader.Open(l0, sid, g.Key.Wno); }
                catch (Exception openEx) { w = null; Skip($"窓が開けない: {openEx.GetType().Name}", g.Count()); continue; }
                bool hasList = w is not null && w.Cols.Has("p" + g.Key.Side + "_h0_x");
                if (w is null || !hasList) { Skip("窓が開けない／本物のリストが無い", g.Count()); continue; }

                foreach (var row in g)
                {
                    int raw = checked((int)(row.Seq - w.Meta.FirstSeq));
                    HitCandidates.HazardCulprit? got;
                    try { got = HitCandidates.HazardListCulprit(w, g.Key.Side, raw, row.IsQuick); }
                    catch (Exception rowEx) { Skip($"例外: {rowEx.GetType().Name}"); continue; }

                    if (row.IsQuick)
                    {
                        nQuickTotal++;
                        if (got is not null)
                        {
                            nQuickOverlap++;
                            string mk = MatchKindJa(got.Match);
                            matchByKindQuick[mk] = matchByKindQuick.GetValueOrDefault(mk) + 1;
                        }
                        if (got?.Match is { Kind: HitCandidates.HazardMatchKind.Laser } lm && WantMore(exQuickLaser))
                            exQuickLaser.Add(new Example(sid, g.Key.Wno, g.Key.Side, row.Seq,
                                "レーザー スロット " + lm.Slot));
                        if (got?.Match is { Kind: HitCandidates.HazardMatchKind.Bullet } bm && WantMore(exQuickBullet))
                            exQuickBullet.Add(new Example(sid, g.Key.Wno, g.Key.Side, row.Seq,
                                "弾 スロット " + bm.Slot));
                        if (got?.Match is { Kind: HitCandidates.HazardMatchKind.Ex } xm && WantMore(exQuickEx))
                            exQuickEx.Add(new Example(sid, g.Key.Wno, g.Key.Side, row.Seq,
                                "Ex スロット " + xm.Slot));
                        if (got?.Match is { Kind: HitCandidates.HazardMatchKind.Enemy } nm && WantMore(exQuickEnemy))
                            exQuickEnemy.Add(new Example(sid, g.Key.Wno, g.Key.Side, row.Seq,
                                "敵 スロット " + nm.Slot));
                        if (got is { Match: null } && WantMore(exUnmatchedRemaining))
                            exUnmatchedRemaining.Add(new Example(sid, g.Key.Wno, g.Key.Side, row.Seq,
                                "quick / 形=" + got.Element.Kind));
                        continue;
                    }

                    nHitTotal++;
                    string key = row.HitType ?? "(不明)";
                    var cur = byType.TryGetValue(key, out var found) ? found : new HitTypeStat(0, 0, 0);
                    if (got is null) { byType[key] = cur with { Total = cur.Total + 1 }; continue; }
                    nHitResolved++;
                    string mkHit = MatchKindJa(got.Match);
                    matchByKindHit[mkHit] = matchByKindHit.GetValueOrDefault(mkHit) + 1;
                    if (got.Match is { Kind: HitCandidates.HazardMatchKind.Enemy } hnm && WantMore(exHitEnemy))
                        exHitEnemy.Add(new Example(sid, g.Key.Wno, g.Key.Side, row.Seq,
                            "体当たり 敵スロット " + hnm.Slot));
                    if (got.Match is null && WantMore(exUnmatchedRemaining))
                        exUnmatchedRemaining.Add(new Example(sid, g.Key.Wno, g.Key.Side, row.Seq,
                            "hit / " + (row.HitType ?? "?") + " / 形=" + got.Element.Kind));
                    if (WantMore(exHitUnresolved))
                    {
                        var set = HitCandidates.Resolve(w, g.Key.Side);
                        if (set is not null && set.Confidence != HitConfidence.Resolved)
                            exHitUnresolved.Add(new Example(sid, g.Key.Wno, g.Key.Side, row.Seq,
                                (row.HitType ?? "?") + " / " + set.Confidence));
                    }

                    bool isLaser = row.HitType == "レーザー";
                    string gxName = isLaser
                        ? (g.Key.Side == 1 ? TickWords.Record.P1HitLaserX : TickWords.Record.P2HitLaserX)
                        : (g.Key.Side == 1 ? TickWords.Record.P1HitX : TickWords.Record.P2HitX);
                    string gyName = isLaser
                        ? (g.Key.Side == 1 ? TickWords.Record.P1HitLaserY : TickWords.Record.P2HitLaserY)
                        : (g.Key.Side == 1 ? TickWords.Record.P1HitY : TickWords.Record.P2HitY);
                    var gx = w.MainAt(gxName, got.TickIndex);
                    var gy = w.MainAt(gyName, got.TickIndex);
                    double elemX = isLaser ? got.Element.PivotX : got.Element.X;
                    double elemY = isLaser ? got.Element.PivotY : got.Element.Y;
                    bool match = gx is double gxv && gy is double gyv
                                 && Math.Abs(gxv - elemX) < 0.01 && Math.Abs(gyv - elemY) < 0.01;
                    if (match) nHitMatch++;
                    byType[key] = cur with { Total = cur.Total + 1, Resolved = cur.Resolved + 1,
                                             Match = cur.Match + (match ? 1 : 0) };
                }
            }
        }

        Console.WriteLine();
        Console.WriteLine("## 1. 被弾: ゲームの記録した『当てた物』と、リストの『最初の重なり』が一致するか");
        Console.WriteLine($"  対象 {nHitTotal} 件 のうち、危険物リストの重なりから答えが出た: " + Pct(nHitResolved, nHitTotal));
        Console.WriteLine($"  そのうち、ゲームの記録と 0.01 以内で一致: " + Pct(nHitMatch, nHitResolved));
        Console.WriteLine($"  対象 {nHitTotal} 件のうち一致（全体比）: " + Pct(nHitMatch, nHitTotal));
        foreach (var (k, v) in byType)
            Console.WriteLine($"    種別 {k}（{v.Total} 件）: 答えが出た {Pct(v.Resolved, v.Total)} ／ "
                              + $"一致 {Pct(v.Match, v.Resolved)}");

        Console.WriteLine();
        Console.WriteLine("## 2. クイック: その瞬間に重なりがあったか（＝クイックが無ければ被弾していたはず）");
        Console.WriteLine($"  対象 {nQuickTotal} 件 のうち、重なりがあった: " + Pct(nQuickOverlap, nQuickTotal));

        Console.WriteLine();
        Console.WriteLine("## 2-B. 種別ごとの対応づけ成功率（答えが出た回のうち、盤面の物に対応づいた先）");
        Console.WriteLine("  被弾（答えが出た " + nHitResolved + " 件が母数）:");
        foreach (var (k, v) in matchByKindHit) Console.WriteLine("    " + k + ": " + Pct(v, nHitResolved));
        Console.WriteLine("  クイック（重なりがあった " + nQuickOverlap + " 件が母数）:");
        foreach (var (k, v) in matchByKindQuick) Console.WriteLine("    " + k + ": " + Pct(v, nQuickOverlap));

        if (examplesWanted > 0)
        {
            Console.WriteLine();
            Console.WriteLine("## 3. 例（静止画を撮る窓。--pick にそのまま渡せる）");
            void PrintEx(string label, List<Example> l)
            {
                Console.WriteLine($"  {label}（{l.Count} 件）:");
                foreach (var e in l)
                    Console.WriteLine($"    --pick \"{e.Sid}:{e.Wno}\"  # side={e.Side} seq={e.Seq} {e.Note}");
            }
            PrintEx("クイック / レーザー", exQuickLaser);
            PrintEx("クイック / 弾", exQuickBullet);
            PrintEx("クイック / Ex", exQuickEx);
            PrintEx("クイック / 敵（体当たり）", exQuickEnemy);
            PrintEx("被弾でゲームの値に決まらなかった窓", exHitUnresolved);
            PrintEx("被弾 / 敵（体当たり）", exHitEnemy);
            PrintEx("残り: 答えは出たが盤面の物に対応づけられない", exUnmatchedRemaining);
        }

        if (skipped.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine("## 外した理由");
            foreach (var (k, v) in skipped) Console.WriteLine($"  {k}: {v}");
        }

        if (nHitTotal == 0 && nQuickTotal == 0)
        {
            Console.Error.WriteLine("★被弾もクイックも 1 件も測れていません。合格にしません。");
            return 1;
        }
        return 0;
    }

    private static string Pct(int a, int b) =>
        b == 0 ? $"{a} / 0（母数 0）"
               : $"{a} / {b}（{(100.0 * a / b).ToString("F1", CultureInfo.InvariantCulture)}%）";
}
