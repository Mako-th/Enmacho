using System.Runtime.CompilerServices;

namespace TH09.Analysis;


public readonly record struct RecoveryEvent(int Index, int Side, int Due, int? At, bool DueKnown,
                                            bool Fired, bool? Got, bool Outside, bool Certain,
                                            string? Note);

public static class GaugeRecovery
{
    public const int ValidTicks = AnalysisTables.RecoveryValidTicks;
    public const int MatchTol = AnalysisTables.RecoveryMatchTol;
    public const double MinRise = AnalysisTables.RecoveryMinRise;

    private const int PlayerStateDown = AnalysisTables.PlayerStateDown;

    private const long HitListValid = AnalysisTables.HitlistValid;

    public static string CardNoteRecoveryLostJa => AnalysisTables.CardNoteRecoveryLostJa;

    private static IEnumerable<string> WordsRead(int side)
    {
        yield return $"p{side}_player_state";
        yield return $"p{side}_gauge";
        yield return $"p{side}_hit_list_count";
    }

    private static IEnumerable<int> Sides(int? side) => side is int sv ? [sv] : [1, 2];

    private static int ByIndexThenSide(ClearRingOrigin a, ClearRingOrigin b) =>
        a.Index != b.Index ? a.Index.CompareTo(b.Index) : a.Side.CompareTo(b.Side);

    private static int ByIndexThenSide(RecoveryEvent a, RecoveryEvent b) =>
        a.Index != b.Index ? a.Index.CompareTo(b.Index) : a.Side.CompareTo(b.Side);


    public static IReadOnlyList<ClearRingOrigin> HitPoints(Window w, int? side = null)
    {
        var outv = new List<ClearRingOrigin>();
        foreach (int s in Sides(side))
            outv.AddRange(Scan.For(w).HitOrigins(s).Where(e => e.Index >= 0));
        outv.Sort(ByIndexThenSide);
        return outv;
    }

    public static IReadOnlyList<RecoveryEvent> Events(Window w, int? side = null)
    {
        var outv = new List<RecoveryEvent>();
        foreach (int s in Sides(side))
            outv.AddRange(Scan.For(w).FilteredEvents(s));
        outv.Sort(ByIndexThenSide);
        return outv;
    }

    public static string RingPointLabel(Window w, ClearRingOrigin e) => Scan.For(w).RingLabel(e);

    public static string? RecoveryPointLabel(Window w, RecoveryEvent e) => Scan.For(w).RecoveryLabel(e);

    public static bool CancelsRecovery(Window w, int side, int index) => Scan.For(w).Cancels(side, index);

    public static bool CancelsRecovery(Window w, CardAttackEvent e) => CancelsRecovery(w, e.Side, e.Index);


    private sealed class Scan
    {
        private static readonly ConditionalWeakTable<Window, Scan> Cached = new();

        public static Scan For(Window w) =>
            Cached.GetValue(w, static x => new Scan(new WindowExt(x)));

        private readonly IExtSource _src;
        private readonly int _n, _preN;
        private readonly Dictionary<int, bool[]> _adv = [];
        private readonly Dictionary<int, List<RecoveryEvent>> _raw = [];
        private readonly Dictionary<int, List<ClearRingOrigin>> _hits = [];

        public Scan(IExtSource src) { _src = src; _n = src.TickCount; _preN = src.PreCount; }

        private int X(int i) => _src.ExtIndex(i);
        private double?[]? Ext(string name) => _src.ExtSeries(name);

        private double? ValueAt(double?[]? col, int i)
        {
            if (col is null) return null;
            int k = X(i);
            return (uint)k < (uint)col.Length ? col[k] : null;
        }

        private bool[] Adv(int side)
        {
            if (_adv.TryGetValue(side, out var got)) return got;
            var col = Ext($"p{side}_hit_list_count");
            var outv = new bool[_preN + _n];
            for (int k = 0; k < outv.Length; k++)
                outv[k] = col is null || col[k] is null || ((long)col[k]!.Value & HitListValid) != 0;
            _adv[side] = outv;
            return outv;
        }

        public List<ClearRingOrigin> HitOrigins(int side)
        {
            if (_hits.TryGetValue(side, out var got)) return got;
            var outv = new List<ClearRingOrigin>();
            var st = Ext($"p{side}_player_state");
            if (st is not null)
            {
                for (int i = -_preN + 1; i < _n; i++)
                {
                    var a = ValueAt(st, i - 1);
                    var b = ValueAt(st, i);
                    if (a is null || b is null) continue;
                    if ((int)a.Value == PlayerStateDown && (int)b.Value != PlayerStateDown)
                        outv.Add(new ClearRingOrigin(i, side, ClearRings.KindHit,
                                                     ClearRingSource.Hit, true, null));
                }
            }
            outv.Sort((x, y) => x.Index.CompareTo(y.Index));
            _hits[side] = outv;
            return outv;
        }

        private List<RecoveryEvent> Raw(int side)
        {
            if (_raw.TryGetValue(side, out var got)) return got;
            var outv = new List<RecoveryEvent>();
            var st = Ext($"p{side}_player_state");
            var g = Ext($"p{side}_gauge");
            if (st is not null)
            {
                var adv = Adv(side);
                for (int i = -_preN + 1; i < _n; i++)
                {
                    var a = ValueAt(st, i - 1);
                    var b = ValueAt(st, i);
                    if (a is null || b is null || (int)a.Value == PlayerStateDown
                        || (int)b.Value != PlayerStateDown)
                        continue;

                    int? at = null;
                    for (int j = i + 1; j < _n; j++)
                    {
                        var v = ValueAt(st, j);
                        if (v is null) break;
                        if ((int)v.Value != PlayerStateDown) { at = j; break; }
                    }

                    int due = i, seen = 0;
                    while (due + 1 < _n && seen < ValidTicks)
                    {
                        due++;
                        if (adv[X(due)]) seen++;
                    }
                    bool outside = seen < ValidTicks;
                    if (at is int atv && Math.Abs(atv - due) <= MatchTol)
                    {
                        due = atv;
                        outside = false;
                    }

                    bool fired = FiredBefore(g, i, at ?? due);
                    bool? gotRose = at is int av ? GaugeRose(g, av) : null;
                    string? note = fired ? AnalysisTables.RecoveryCancelNote
                                  : outside ? AnalysisTables.RecoveryOutsideNote : null;
                    outv.Add(new RecoveryEvent(i, side, due, at, at is not null, fired, gotRose,
                                                outside, at is not null, note));
                }
            }
            outv.Sort((x, y) => x.Index.CompareTo(y.Index));
            _raw[side] = outv;
            return outv;
        }

        public IEnumerable<RecoveryEvent> FilteredEvents(int side) =>
            Raw(side).Where(e => e.Due >= 0 && e.Index <= _n - 1);

        private RecoveryEvent? RecoveryAt(int side, int index)
        {
            foreach (var e in FilteredEvents(side))
                if (e.At is int at && at == index) return e;
            return null;
        }

        public string RingLabel(ClearRingOrigin e)
        {
            var rec = RecoveryAt(e.Side, e.Index);
            string name = rec is null ? AnalysisTables.HitRingShortJa
                         : rec.Value.Fired ? AnalysisTables.RingOnlyJa : AnalysisTables.RecoveryWithRingJa;
            return CardEvents.SideLabel(e.Side, name)!;
        }

        public string? RecoveryLabel(RecoveryEvent e)
        {
            if (e.At is int at && HitOrigins(e.Side).Any(g => g.Index >= 0 && g.Index == at))
                return null;
            return CardEvents.SideLabel(e.Side,
                e.Fired ? AnalysisTables.RecoveryLostShortJa : AnalysisTables.RecoveryShortJa);
        }

        public bool Cancels(int side, int index)
        {
            foreach (var r in FilteredEvents(side))
            {
                if (!r.Fired) continue;
                int end = r.At ?? r.Due;
                if (r.Index <= index && index <= end) return true;
            }
            return false;
        }

        private bool FiredBefore(double?[]? g, int start, int end)
        {
            if (g is null) return false;
            for (int k = start; k < end; k++)
            {
                var a = ValueAt(g, k);
                var b = ValueAt(g, k + 1);
                if (a is not null && b is not null && b.Value < a.Value - QuickCards.GaugeTol)
                    return true;
            }
            return false;
        }

        private bool? GaugeRose(double?[]? g, int at)
        {
            if (g is null) return null;
            var basev = ValueAt(g, at - 1);
            if (basev is null) return null;
            for (int d = 0; d < 4; d++)
            {
                var v = ValueAt(g, at + d);
                if (v is not null && v.Value - basev.Value >= MinRise) return true;
            }
            return false;
        }
    }


    private sealed class FakeExt : IExtSource
    {
        private readonly Dictionary<string, double?[]> _cols = new(StringComparer.Ordinal);
        public int TickCount { get; }
        public int PreCount { get; }
        public FakeExt(int tickCount, int preCount) { TickCount = tickCount; PreCount = preCount; }
        public int ExtIndex(int i) => PreCount + i;
        public double?[]? ExtSeries(string name) => _cols.TryGetValue(name, out var v) ? v : null;

        public FakeExt With(string name, Func<int, double?> f)
        {
            var col = new double?[PreCount + TickCount];
            for (int k = 0; k < col.Length; k++) col[k] = f(k - PreCount);
            _cols[name] = col;
            return this;
        }
    }

    private static FakeExt Synthetic(int n, int preN, int side, int enter, int leave,
                                     Func<int, double?> gauge, bool everyTickValid = true)
    {
        var src = new FakeExt(n, preN)
            .With($"p{side}_player_state", i => i >= enter && i < leave
                                                 ? (double)PlayerStateDown : 0.0)
            .With($"p{side}_gauge", i => gauge(i));
        if (everyTickValid)
            src = src.With($"p{side}_hit_list_count", _ => (double)HitListValid);
        return src;
    }

    public static IEnumerable<(string Label, Func<string?> Body)> SelfTestCases()
    {
        yield return ($"★母数（RECOVERY 定数: ValidTicks={ValidTicks} MatchTol={MatchTol} MinRise={MinRise}）",
            () => ValidTicks > 0 && MatchTol >= 0 && MinRise > 0
                  ? null : "定数が 0 以下。生成物を引けていない");

        yield return ("★読む語が RECORD_FIELDS に在る（綴りを人の目で守らない）", () =>
        {
            var known = new HashSet<string>(TimelineDecode.RecordFields, StringComparer.Ordinal);
            var miss = new[] { 1, 2 }.SelectMany(WordsRead).Where(x => !known.Contains(x)).ToList();
            return miss.Count == 0 ? null : "主リングに無い語を読もうとしている: " + string.Join(",", miss);
        });

        yield return ("★合成: RecoveryEvent.Index は『やられに入った』tick・HitOrigins は『抜けた』tick", () =>
        {
            const int enter = 3, leave = 9;
            var sc = new Scan(Synthetic(n: 30, preN: 0, side: 1, enter, leave, _ => 300.0));
            var raw = sc.FilteredEvents(1).ToList();
            var ho = sc.HitOrigins(1);
            if (raw.Count != 1 || ho.Count != 1) return $"raw={raw.Count} ho={ho.Count}（各 1 件のはず）";
            if (raw[0].Index != enter) return $"index が {raw[0].Index}（{enter} のはず）";
            if (raw[0].At != leave) return $"at が {raw[0].At}（{leave} のはず）";
            if (raw[0].Due != leave) return $"due が {raw[0].Due}（at に寄ったはず）";
            return ho[0].Index == leave ? null : $"HitOrigins の index が {ho[0].Index}（{leave} のはず）";
        });

        yield return ("★否定: 旧（index を『抜けた』tick に取り違える）なら enter と leave が入れ替わる", () =>
        {
            const int enter = 3, leave = 9;
            var sc = new Scan(Synthetic(n: 30, preN: 0, side: 1, enter, leave, _ => 300.0));
            var raw = sc.FilteredEvents(1).ToList();
            if (raw.Count != 1) return $"件数が {raw.Count}";
            int legacyIndex = raw[0].At ?? -1;
            if (legacyIndex != leave) return "旧の写しが間違っている（否定テストの意味が無い）";
            return raw[0].Index != leave ? null : "新でも enter と leave が区別できていない";
        });

        yield return ("★合成: 間に撃つと fired=true（ゲージが GaugeTol を超えて落ちる）", () =>
        {
            const int enter = 3, leave = 9;
            var sc = new Scan(Synthetic(n: 30, preN: 0, side: 1, enter, leave,
                                        i => i < enter + 2 ? 300.0 : 50.0));
            var raw = sc.FilteredEvents(1).ToList();
            if (raw.Count != 1) return $"件数が {raw.Count}";
            return raw[0].Fired ? null : "fired になっていない";
        });

        yield return ("★合成: 間に撃たなければ fired=false", () =>
        {
            const int enter = 3, leave = 9;
            var sc = new Scan(Synthetic(n: 30, preN: 0, side: 1, enter, leave, _ => 300.0));
            var raw = sc.FilteredEvents(1).ToList();
            if (raw.Count != 1) return $"件数が {raw.Count}";
            return !raw[0].Fired ? null : "fired になっている（撃っていないのに）";
        });

        yield return ("★合成: 復帰でゲージが MinRise 以上跳ねたら got=true", () =>
        {
            const int enter = 3, leave = 9;
            var sc = new Scan(Synthetic(n: 30, preN: 0, side: 1, enter, leave,
                                        i => i < leave ? 100.0 : 100.0 + MinRise + 5.0));
            var raw = sc.FilteredEvents(1).ToList();
            if (raw.Count != 1) return $"件数が {raw.Count}";
            return raw[0].Got == true ? null : "got が true でない: " + raw[0].Got;
        });

        yield return ("★合成: 復帰でゲージが動かなければ got=false（『消えた回』と同じ形になるが、決め手は fired）", () =>
        {
            const int enter = 3, leave = 9;
            var sc = new Scan(Synthetic(n: 30, preN: 0, side: 1, enter, leave, _ => 100.0));
            var raw = sc.FilteredEvents(1).ToList();
            if (raw.Count != 1) return $"件数が {raw.Count}";
            return raw[0].Got == false ? null : "got が false でない: " + raw[0].Got;
        });

        yield return ("★合成: at が読めれば Certain=true・読めなければ false（Got を見ない）", () =>
        {
            const int enter = 3, leave = 40;
            var sc = new Scan(Synthetic(n: 10, preN: 0, side: 1, enter, leave, _ => 300.0));
            var raw = sc.FilteredEvents(1).ToList();
            if (raw.Count != 1) return $"件数が {raw.Count}";
            if (raw[0].At is not null) return "at が読めている（窓の外のはず）";
            if (raw[0].Certain) return "Certain が true（at が無いのに）";
            return raw[0].Got is null ? null : "Got が null でない（at が無いのに読んでいる）";
        });

        yield return ("★合成: Fired なら Note は RecoveryCancelNote（Outside より優先）", () =>
        {
            const int enter = 3, leave = 400;
            var sc = new Scan(Synthetic(n: 10, preN: 0, side: 1, enter, leave,
                                        i => i < enter + 2 ? 300.0 : 50.0));
            var raw = sc.FilteredEvents(1).ToList();
            if (raw.Count != 1) return $"件数が {raw.Count}";
            if (!raw[0].Fired) return "fired になっていない（合成が効いていない）";
            return raw[0].Note == AnalysisTables.RecoveryCancelNote
                ? null : "Note が RecoveryCancelNote でない: " + raw[0].Note;
        });

        yield return ("★合成: Outside なら Note は RecoveryOutsideNote（Fired でないとき）", () =>
        {
            const int enter = 3, leave = 400;
            var sc = new Scan(Synthetic(n: 10, preN: 0, side: 1, enter, leave, _ => 300.0));
            var raw = sc.FilteredEvents(1).ToList();
            if (raw.Count != 1) return $"件数が {raw.Count}";
            if (!raw[0].Outside) return "outside になっていない（合成が効いていない）";
            return raw[0].Note == AnalysisTables.RecoveryOutsideNote
                ? null : "Note が RecoveryOutsideNote でない: " + raw[0].Note;
        });

        yield return ("★合成: どちらでもなければ Note は null（Fired でも Outside でもない）", () =>
        {
            const int enter = 3, leave = 9;
            var sc = new Scan(Synthetic(n: 30, preN: 0, side: 1, enter, leave, _ => 300.0));
            var raw = sc.FilteredEvents(1).ToList();
            if (raw.Count != 1) return $"件数が {raw.Count}";
            return raw[0].Note is null ? null : "Note が空でない: " + raw[0].Note;
        });

        yield return ("★合成: at が VALID の予定から MatchTol 以内なら due が at に寄る", () =>
        {
            const int enter = 3, leave = enter + ValidTicks + 5;
            var sc = new Scan(Synthetic(n: leave + 5, preN: 0, side: 1, enter, leave, _ => 300.0));
            var raw = sc.FilteredEvents(1).ToList();
            if (raw.Count != 1) return $"件数が {raw.Count}";
            if (raw[0].Outside) return "outside のまま（寄っていない）";
            return raw[0].Due == leave ? null : $"due が {raw[0].Due}（{leave} のはず）";
        });

        yield return ("★否定: 旧（at をいつでも due に採用する）なら MatchTol の外でも寄ってしまう", () =>
        {
            const int enter = 3, leave = enter + ValidTicks + MatchTol + 50;
            var sc = new Scan(Synthetic(n: leave + 5, preN: 0, side: 1, enter, leave, _ => 300.0));
            var raw = sc.FilteredEvents(1).ToList();
            if (raw.Count != 1) return $"件数が {raw.Count}";
            int legacyDue = raw[0].At ?? -1;
            if (legacyDue != leave) return "旧の写しが間違っている（否定テストの意味が無い）";
            return raw[0].Due != leave ? null : "新も at に寄ってしまっている（MatchTol を見ていない）";
        });

        yield return ("★合成: 窓の頭より前の被弾でも、回復が窓の中に掛かれば FilteredEvents に出る", () =>
        {
            const int preN = 6, enter = -5, leave = 2;
            var sc = new Scan(Synthetic(n: 20, preN: preN, side: 1, enter, leave, _ => 300.0));
            var got = sc.FilteredEvents(1).ToList();
            return got.Count == 1 && got[0].Index == enter && got[0].At == leave
                ? null : $"件数 {got.Count}（1 件・index={enter}・at={leave} のはず）";
        });

        yield return ("★否定: 旧（起点が窓の中限定）なら窓の頭より前の被弾を落としてしまう", () =>
        {
            const int preN = 6, enter = -5, leave = 2;
            var sc = new Scan(Synthetic(n: 20, preN: preN, side: 1, enter, leave, _ => 300.0));
            var got = sc.FilteredEvents(1).ToList();
            var legacy = got.Where(e => e.Index >= 0).ToList();
            if (legacy.Count != 0) return "旧の写しが間違っている（否定テストの意味が無い）";
            return got.Count != 0 ? null : "新も落としている（区間の扱いになっていない）";
        });

        yield return ("★合成: 対のゲージ回復が無ければ HitRingShortJa（記録の外から『やられ』が続いていた回）", () =>
        {
            const int leave = 5;
            var sc = new Scan(Synthetic(n: 30, preN: 0, side: 1, enter: 0, leave: leave, gauge: _ => 300.0));
            var ho = sc.HitOrigins(1).Where(e => e.Index >= 0).ToList();
            if (ho.Count != 1) return $"件数が {ho.Count}";
            if (sc.FilteredEvents(1).Any())
                return "対になる Raw イベントが出てしまっている（テストの前提が崩れている）";
            var label = sc.RingLabel(ho[0]);
            return label.Contains(AnalysisTables.HitRingShortJa, StringComparison.Ordinal)
                ? null : "HitRingShortJa を含まない: " + label;
        });

        yield return ("★合成: 対のゲージ回復があり Fired なら RingOnlyJa（撃って消えた形）", () =>
        {
            const int enter = 3, leave = 9;
            var sc = new Scan(Synthetic(n: 30, preN: 0, side: 1, enter, leave,
                                        i => i < enter + 2 ? 300.0 : 50.0));
            var ho = sc.HitOrigins(1).Where(e => e.Index >= 0).ToList();
            if (ho.Count != 1) return $"件数が {ho.Count}";
            var label = sc.RingLabel(ho[0]);
            return label.Contains(AnalysisTables.RingOnlyJa, StringComparison.Ordinal)
                ? null : "RingOnlyJa を含まない: " + label;
        });

        yield return ("★合成: 対のゲージ回復があり撃っていなければ RecoveryWithRingJa", () =>
        {
            const int enter = 3, leave = 9;
            var sc = new Scan(Synthetic(n: 30, preN: 0, side: 1, enter, leave, _ => 300.0));
            var ho = sc.HitOrigins(1).Where(e => e.Index >= 0).ToList();
            if (ho.Count != 1) return $"件数が {ho.Count}";
            var label = sc.RingLabel(ho[0]);
            return label.Contains(AnalysisTables.RecoveryWithRingJa, StringComparison.Ordinal)
                ? null : "RecoveryWithRingJa を含まない: " + label;
        });

        yield return ("★合成: 弾消しの点にまとまった回は RecoveryLabel が null（同じ出来事が 2 行にならない）", () =>
        {
            const int enter = 3, leave = 9;
            var sc = new Scan(Synthetic(n: 30, preN: 0, side: 1, enter, leave, _ => 300.0));
            var rec = sc.FilteredEvents(1).ToList();
            if (rec.Count != 1) return $"件数が {rec.Count}";
            return sc.RecoveryLabel(rec[0]) is null
                ? null : "null になっていない（弾消しの点と重複して出る）";
        });

        yield return ("★合成: 対が無いゲージ回復は RecoveryShortJa／消えていれば RecoveryLostShortJa", () =>
        {
            const int enter = 3, leave = 400;
            var sc1 = new Scan(Synthetic(n: 10, preN: 0, side: 1, enter, leave, _ => 300.0));
            var rec1 = sc1.FilteredEvents(1).ToList();
            if (rec1.Count != 1) return $"件数が {rec1.Count}（通常）";
            var l1 = sc1.RecoveryLabel(rec1[0]);
            if (l1 is null || !l1.Contains(AnalysisTables.RecoveryShortJa, StringComparison.Ordinal))
                return "RecoveryShortJa を含まない: " + l1;

            var sc2 = new Scan(Synthetic(n: 10, preN: 0, side: 1, enter, leave,
                                         i => i < enter + 2 ? 300.0 : 50.0));
            var rec2 = sc2.FilteredEvents(1).ToList();
            if (rec2.Count != 1) return $"件数が {rec2.Count}（消えた）";
            var l2 = sc2.RecoveryLabel(rec2[0]);
            return l2 is not null && l2.Contains(AnalysisTables.RecoveryLostShortJa, StringComparison.Ordinal)
                ? null : "RecoveryLostShortJa を含まない: " + l2;
        });

        yield return ("★合成: Fired の区間に入るカードアタックは Cancels=true", () =>
        {
            const int enter = 3, leave = 9;
            var sc = new Scan(Synthetic(n: 30, preN: 0, side: 1, enter, leave,
                                        i => i < enter + 2 ? 300.0 : 50.0));
            return sc.Cancels(1, 5) ? null : "true にならない（区間 [3,9] の中のはず）";
        });

        yield return ("★合成: 区間の外のカードアタックは Cancels=false", () =>
        {
            const int enter = 3, leave = 9;
            var sc = new Scan(Synthetic(n: 30, preN: 0, side: 1, enter, leave,
                                        i => i < enter + 2 ? 300.0 : 50.0));
            return !sc.Cancels(1, 20) ? null : "true になっている（区間の外のはず）";
        });

        yield return ("★合成: Fired でない回は Cancels が常に false", () =>
        {
            const int enter = 3, leave = 9;
            var sc = new Scan(Synthetic(n: 30, preN: 0, side: 1, enter, leave, _ => 300.0));
            return !sc.Cancels(1, 5) ? null : "true になっている（fired でないのに）";
        });

        yield return ("★合成: 予定もリングも撃った側にしか出ない", () =>
        {
            const int enter = 3, leave = 9;
            var sc = new Scan(Synthetic(n: 30, preN: 0, side: 1, enter, leave, _ => 300.0));
            if (sc.HitOrigins(2).Count != 0) return "2P に弾消しの点が漏れている";
            return sc.FilteredEvents(2).ToList().Count == 0 ? null : "2P にゲージ回復の予定が漏れている";
        });

        yield return ("★語が 1 つも無い窓でも落ちない（0 件を返す）", () =>
        {
            var sc = new Scan(new FakeExt(10, 0));
            return sc.FilteredEvents(1).ToList().Count == 0 && sc.HitOrigins(1).Count == 0
                ? null : "件数が 0 でない";
        });

        yield return ("★否定: 旧（VALID の欠測を『進んでいない』に倒す）なら回復の判定は変わらないが、進みが遅くなる", () =>
        {
            const int enter = 3, leave = 20;
            var src = new FakeExt(200, 0)
                .With("p1_player_state", i => i >= enter && i < leave ? (double)PlayerStateDown : 0.0)
                .With("p1_gauge", _ => 300.0)
                .With("p1_hit_list_count", i => i % 2 == 0 ? (double)HitListValid : null);
            var sc = new Scan(src);
            var raw = sc.FilteredEvents(1).ToList();
            if (raw.Count != 1) return $"件数が {raw.Count}";
            bool Legacy(double? v) => v is not null && ((long)v!.Value & HitListValid) != 0;
            if (Legacy(null)) return "旧の写しが間違っている（否定テストの意味が無い）";
            return raw[0].Due - raw[0].Index <= 199 ? null : "新でも足りていない（合成の作りが違う）";
        });

        yield return ("★合成: 複数ケースで Note が『Fired なら Cancel／それ以外で Outside なら Outside／どちらでもなければ null』の優先順を守る", () =>
        {
            var cases = new[]
            {
                new Scan(Synthetic(n: 30, preN: 0, side: 1, 3, 9, _ => 300.0)),
                new Scan(Synthetic(n: 30, preN: 0, side: 1, 3, 9, i => i < 5 ? 300.0 : 50.0)),
                new Scan(Synthetic(n: 10, preN: 0, side: 1, 3, 400, _ => 300.0)),
                new Scan(Synthetic(n: 10, preN: 0, side: 1, 3, 400, i => i < 5 ? 300.0 : 50.0)),
            };
            int n = 0;
            bool sawBoth = false;
            foreach (var sc in cases)
                foreach (var e in sc.FilteredEvents(1))
                {
                    n++;
                    if (e.Fired && e.Outside) sawBoth = true;
                    string? want = e.Fired ? AnalysisTables.RecoveryCancelNote
                                  : e.Outside ? AnalysisTables.RecoveryOutsideNote : null;
                    if (e.Note != want)
                        return $"優先順どおりでない: Fired={e.Fired} Outside={e.Outside} Note={e.Note}";
                }
            if (n == 0) return "1 件も出なかった（母数 0）";
            return sawBoth ? null : "Fired と Outside が両方 true のケースを 1 件も踏んでいない（検算が働いていない）";
        });
    }
}
