using System.Globalization;
using System.Text;
using TH09.Layer0;
using TH09.Record.Generated;
using TH09.TickBus;
using R = TH09.Generated.TickWords.Record;

namespace TH09.Record;

public sealed class HitWindowCollector
{
    public sealed record Options(
        int Before = HitWindowConst.WindowBeforeTicks,
        int After = HitWindowConst.WindowAfterTicks,
        bool CountValid = true,
        int? BeforeRawMax = null,
        int? AfterRawMax = null,
        int? MaxWindow = null,
        IReadOnlyList<string>? Triggers = null,
        int Margin = CoordRingReader.DefaultMargin,
        int SlotCount = CoordLayout.Slots,
        string Quant = HitWindowConst.QuantRawF32);

    private readonly CoordRingReader _reader;
    private readonly Layer0Writer _sink;
    private readonly Action<string> _log;
    private readonly Options _o;

    private readonly int _before, _after, _beforeRawMax, _afterRawMax, _maxWindow, _frameKeep;
    private readonly bool _countValid;
    private readonly string[] _triggers;
    private readonly string[] _verifyColumns;

    private TickRecord? _prev;

    private readonly List<Pending> _pending = [];

    private readonly Dictionary<long, long> _frames = [];

    private readonly Dictionary<long, (bool P1, bool P2)> _advanced = [];

    private bool _validEver;

    private long? _sessionId;
    private long _windowNo;

    public Func<TickRecord, string?>? ScopeGate { get; set; }

    public Dictionary<string, long> HitsSkippedByScope { get; } = new(StringComparer.Ordinal);

    public long WindowsHeadCapped { get; private set; }

    public long WindowsTailCapped { get; private set; }

    public long WindowsMergeCapped { get; private set; }

    public long WindowsTailUnfinished { get; private set; }

    public long WindowsWritten { get; private set; }
    public long TicksWritten { get; private set; }
    public long BytesWritten { get; private set; }
    public long HitsSeen { get; private set; }

    public Dictionary<string, long> HitsByTrigger { get; } = new(StringComparer.Ordinal);

    public long LostTicks { get; private set; }

    public long VerifyFailures { get; private set; }

    public long VerifyChecked { get; private set; }

    public long? MinSlack { get; private set; }

    public int PendingCount => _pending.Count;

    public long? SessionId => _sessionId;

    public long HitsSkipped => HitsSkippedByScope.Values.Sum();

    public HitWindowCollector(CoordRingReader reader, Layer0Writer sink,
                              Options? options = null, Action<string>? log = null)
    {
        _reader = reader;
        _sink = sink;
        _o = options ?? new Options();
        _log = log ?? (static _ => { });
        _before = _o.Before;
        _after = _o.After;
        _countValid = _o.CountValid;
        _beforeRawMax = Math.Max(_before, _o.BeforeRawMax ?? HitWindowLengths.HeadRawMax(_before));
        _afterRawMax = Math.Max(_after, _o.AfterRawMax ?? HitWindowLengths.TailRawMax(_after));
        _maxWindow = _o.MaxWindow ?? HitWindowLengths.MaxWindowTicks(_before, _after);
        _triggers = [.. _o.Triggers ?? HitWindowConst.DefaultTriggers];
        foreach (var t in _triggers) HitsByTrigger[t] = 0;
        _frameKeep = _maxWindow + _afterRawMax + _beforeRawMax + 256;
        _verifyColumns = CoordColumnNames.SlotColumns(0);

        _log($"[hitwin] 窓長  {HitWindowLengths.Note(_before, _after, _countValid)} "
             + $"（生上限 {_beforeRawMax} / {_afterRawMax}・マージ上限 {_maxWindow}）");
        foreach (var w in HitWindowLengths.Warnings(_before, _after, _maxWindow))
            _log("[hitwin] " + w);
    }


    public void BeginSession(long sessionId)
    {
        try
        {
            if (_sessionId is not null && sessionId != _sessionId)
            {
                Pump(force: true);
                _windowNo = 0;
            }
            _sessionId = sessionId;
            _windowNo = _sink.NextHitWindowNo(sessionId);
        }
        catch (Exception exc)
        {
            _log($"[hitwin] session={sessionId} の窓を開始できません: {exc.Message}");
        }
    }

    public void EndSession()
    {
        Pump(force: true);
        _sessionId = null;
        _prev = null;
        _frames.Clear();
        _advanced.Clear();
    }


    public void Feed(IReadOnlyList<TickRecord> records)
    {
        try
        {
            foreach (var rec in records)
            {
                long seq = rec.At(R.SeqBeginOffset);
                _frames[seq] = rec.At(R.RoundFramesOffset);
                var row = (HitTriggers.RingAdvanced(rec, 1), HitTriggers.RingAdvanced(rec, 2));
                _advanced[seq] = row;
                if (!_validEver && (row.Item1 || row.Item2)) _validEver = true;

                var hits = HitTriggers.HitsIn(rec, _prev, _triggers);
                string? skip = hits.Count == 0 || ScopeGate is null ? null : ScopeGate(rec);
                if (skip is not null)
                {
                    HitsSkippedByScope[skip] = HitsSkippedByScope.GetValueOrDefault(skip) + hits.Count;
                    hits = [];
                }
                foreach (var h in hits) NoteHit(seq, h.Side, h.Trigger);
                AdvanceTails(seq);
                _prev = rec;
            }
            PruneFrames();
        }
        catch (Exception exc)
        {
            _log("[hitwin] レコードの取り込みに失敗しました: " + exc.Message);
        }
    }

    private bool AdvancedAt(long seq, int side)
    {
        if (!_validEver) return true;
        if (!_advanced.TryGetValue(seq, out var row)) return true;
        return side == 1 ? row.P1 : row.P2;
    }

    private (long First, bool Capped) HeadSeq(long at, int side)
    {
        if (!_countValid) return (Math.Max(0, at - _before), false);
        int need = _before;
        long seq = at;
        long limit = at - _beforeRawMax;
        while (need > 0 && seq > 0 && seq > limit)
        {
            seq--;
            if (AdvancedAt(seq, side)) need--;
        }
        return (Math.Max(0, seq), need > 0);
    }

    private Tail NewTail(long at, int side) =>
        _countValid
            ? new Tail { Anchor = at, Side = side, Need = _after, Cursor = at, Last = null }
            : new Tail { Anchor = at, Side = side, Need = 0, Cursor = at, Last = at + _after - 1 };

    private void AdvanceTails(long seq)
    {
        foreach (var win in _pending)
        {
            foreach (var tail in win.Tails)
            {
                if (tail.Need <= 0) continue;
                while (tail.Need > 0 && tail.Cursor <= seq)
                {
                    long s = tail.Cursor;
                    if (s - tail.Anchor >= _afterRawMax)
                    {
                        tail.Capped = true;
                        tail.Last = tail.Anchor + _afterRawMax - 1;
                        tail.Need = 0;
                        break;
                    }
                    if (AdvancedAt(s, tail.Side))
                    {
                        tail.Need--;
                        if (tail.Need == 0) tail.Last = s;
                    }
                    tail.Cursor = s + 1;
                }
                if (tail.Need <= 0 && tail.Last is long last) Extend(win, last);
            }
        }
    }

    private void Extend(Pending win, long last)
    {
        long want = Math.Max(win.Last, last);
        long cap = win.First + _maxWindow - 1;
        if (want > cap && !win.MergeCapped)
        {
            win.MergeCapped = true;
            WindowsMergeCapped++;
            bool solo = win.Hits.Count <= 1;
            _log($"[hitwin] 窓の後端をマージ上限（{_maxWindow} 生 tick）で切りました"
                 + $"（first={win.First} / 被弾 {win.Hits.Count} 件・{want - cap} tick ぶん）。"
                 + (solo ? "★被弾 1 件でも当たっています ＝ 上限の置き方が誤りです"
                         : "被弾が続いたので切りました（元からの役目）"));
        }
        win.Last = Math.Min(want, cap);
    }

    private static bool TailsOpen(Pending win) => win.Tails.Any(t => t.Need > 0);

    private void CloseTails(Pending win)
    {
        foreach (var tail in win.Tails)
        {
            if (tail.Need <= 0) continue;
            tail.Unfinished = true;
            tail.Last = Math.Min(Math.Max(tail.Anchor, tail.Cursor - 1 + tail.Need),
                                 tail.Anchor + _afterRawMax - 1);
            tail.Need = 0;
            Extend(win, tail.Last.Value);
        }
    }

    private void NoteHit(long seq, int side, string trigger)
    {
        HitsSeen++;
        HitsByTrigger[trigger] = HitsByTrigger.GetValueOrDefault(trigger) + 1;
        long at = seq - (string.Equals(trigger, HitWindowConst.TriggerHitKind, StringComparison.Ordinal)
                         ? HitWindowConst.HitDelayTicks : 0);
        var (first, headCapped) = HeadSeq(at, side);
        var tail = NewTail(at, side);
        long last = tail.Last ?? (at + _after - 1);
        var hit = new Hit { Seq = at, Side = side, Trigger = trigger };
        if (headCapped)
        {
            hit.HeadCapped = true;
            WindowsHeadCapped++;
            _log($"[hitwin] 窓の頭が生 tick の上限（{_beforeRawMax}）に当たりました"
                 + $"（seq={at} / VALID {_before} tick ぶん遡れていません）。凍結が長く続いた区間です");
        }
        if (_pending.Count > 0)
        {
            var cur = _pending[^1];
            if (first <= cur.Last + 1 && Math.Max(cur.Last, last) - cur.First + 1 <= _maxWindow)
            {
                cur.Last = Math.Max(cur.Last, last);
                cur.Hits.Add(hit);
                cur.Tails.Add(tail);
                return;
            }
        }
        _pending.Add(new Pending { First = first, Last = last, Hits = [hit], Tails = [tail] });
        if (_pending.Count > HitWindowConst.MaxPendingWindows)
        {
            var dropped = _pending[0];
            _pending.RemoveAt(0);
            LostTicks += dropped.Last - dropped.First + 1;
            _log($"[hitwin] 書き出し待ちが {HitWindowConst.MaxPendingWindows} 本を超えたので"
                 + $"最古の窓を捨てました（first={dropped.First}）。"
                 + "座標リングに追い越されて読めない範囲です");
        }
    }

    private void PruneFrames()
    {
        if (_frames.Count <= _frameKeep * 2) return;
        long newest = long.MinValue;
        foreach (var k in _frames.Keys) if (k > newest) newest = k;
        long cutoff = newest - _frameKeep;
        foreach (var k in _frames.Keys.Where(k => k < cutoff).ToList()) _frames.Remove(k);
        foreach (var k in _advanced.Keys.Where(k => k < cutoff).ToList()) _advanced.Remove(k);
    }


    public int Pump(bool force = false)
    {
        if (_pending.Count == 0) return 0;
        long writeIndex;
        try
        {
            writeIndex = _reader.WriteIndex;
        }
        catch (Exception exc)
        {
            _log("[hitwin] 座標リングのヘッダを読めません: " + exc.Message);
            return 0;
        }
        long lastWritten = (writeIndex - 1) & 0xFFFFFFFFL;
        int written = 0;
        var rest = new List<Pending>();
        foreach (var win in _pending)
        {
            if (TailsOpen(win))
            {
                if (!force) { rest.Add(win); continue; }
                CloseTails(win);
            }
            if (!force && win.Last > lastWritten) { rest.Add(win); continue; }
            if (_sessionId is null) { rest.Add(win); continue; }
            if (WriteWindow(win)) written++;
        }
        _pending.Clear();
        _pending.AddRange(rest);
        return written;
    }

    private string SlackNote() =>
        _reader.LastSlack is long v ? $"（直前の余裕 {v} tick）" : "";

    private List<string> LossLines(Pending win, int count, CoordWindow got)
    {
        int lost = count - got.Count;
        var lines = new List<string>();
        if (lost == 0) return lines;
        int skipped = got.SkippedHead, torn = got.StoppedTicks, tail = got.UnwrittenTail;
        if (skipped + torn + tail != lost)
            lines.Add($"[hitwin] ★欠けの内訳が合いません（欠け {lost} tick に対し"
                      + $"頭 {skipped} + 途中 {torn} + 末尾 {tail}）。"
                      + "read_window() の戻り値と食い違っています");
        if (skipped > 0)
        {
            var note = SlackNote();
            if (got.Slack is long s && s > 0)
                note += "。★ただし余裕が正なので追い越しでは説明が付きません";
            lines.Add($"[hitwin] 窓の頭が {skipped} tick 欠けました（first={win.First}→{got.First} / "
                      + $"{count}→{got.Count} tick）。座標リングに追い越されています{note}");
        }
        if (torn > 0)
            lines.Add($"[hitwin] ★窓が途中で途切れました（{torn} tick / stopped_at="
                      + (got.StoppedAt?.ToString(CultureInfo.InvariantCulture) ?? "None")
                      + "）。seq が壊れているので穴を繋がずに打ち切りました");
        if (tail > 0)
            lines.Add($"[hitwin] 窓の末尾 {tail} tick はまだ書かれていませんでした"
                      + $"（first={got.First} / {count}→{got.Count} tick）。"
                      + "セッション終端で、書き手がまだ書いていない tick に窓の末尾が掛かったためで、"
                      + "追い越しではありません（リングを大きくしても直りません）");
        return lines;
    }

    private void NoteSlack(long? slack, bool warn = true)
    {
        if (slack is not long v) return;
        if (MinSlack is null || v < MinSlack) MinSlack = v;
        if (warn && v < _o.Margin)
            _log($"[hitwin] ★窓を読み終えた時点で余裕が {v} tick しかありません"
                 + $"（margin={_o.Margin}）。座標リングの上書きまで間がありません");
    }

    private void MarkTailCapped(Pending win)
    {
        int n = Math.Min(win.Hits.Count, win.Tails.Count);
        for (int i = 0; i < n; i++)
        {
            var tail = win.Tails[i];
            if (tail.Capped)
            {
                win.Hits[i].TailCapped = true;
                WindowsTailCapped++;
                _log($"[hitwin] 窓の尾が生 tick の上限（{_afterRawMax}）で打ち切られました"
                     + $"（seq={tail.Anchor} / VALID {_after} tick ぶん採れていません）");
            }
            else if (tail.Unfinished)
            {
                win.Hits[i].TailUnfinished = true;
                WindowsTailUnfinished++;
            }
        }
    }

    private bool WriteWindow(Pending win)
    {
        MarkTailCapped(win);
        if (win.MergeCapped && win.Hits.Count > 0) win.Hits[0].MergeCapped = true;

        int count = (int)(win.Last - win.First + 1);
        CoordWindow? got;
        try
        {
            got = _reader.ReadWindow((uint)win.First, count);
        }
        catch (Exception exc)
        {
            _log($"[hitwin] 窓を読めません（first={win.First} count={count}）: {exc.Message}");
            return false;
        }
        if (got is null || got.Count == 0)
        {
            LostTicks += count;
            if (_reader.LastMiss == CoordMiss.Unwritten)
                _log($"[hitwin] 窓が丸ごと取れませんでした（first={win.First} count={count}）。"
                     + "その範囲はまだ書かれていません（追い越しではありません）");
            else
                _log($"[hitwin] 窓が丸ごと取れませんでした（first={win.First} count={count}）。"
                     + "座標リングに追い越されています" + SlackNote());
            NoteSlack(_reader.LastSlack, warn: false);
            return false;
        }

        int lost = count - got.Count;
        LostTicks += lost;
        foreach (var line in LossLines(win, count, got)) _log(line);
        NoteSlack(got.Slack);

        Layer0Writer.HitWindowInfo info;
        try
        {
            var columns = CoordColumnNames.WindowColumns(got.Raw, got.Count);
            info = _sink.WriteHitWindow(
                _sessionId!.Value, _windowNo, got.First, columns, CoordColumnNames.All,
                HitsJson(win.Hits),
                _frames.TryGetValue(got.First, out var ff) ? ff : null,
                lost, _o.SlotCount, _o.Quant, _verifyColumns);
        }
        catch (Exception exc)
        {
            _log($"[hitwin] 窓を保存できません（first={got.First}）: {exc.Message}");
            return false;
        }
        if (info.Verified) VerifyChecked++;
        if (info.VerifyError is not null)
        {
            VerifyFailures++;
            _log($"[hitwin] ★書いた窓を読み戻せません（window_no={_windowNo}）: {info.VerifyError}");
        }
        _windowNo++;
        WindowsWritten++;
        TicksWritten += info.TickCount;
        BytesWritten += info.CompressedBytes + info.FieldOrderBytes;
        return true;
    }

    public static string HitsJson(IReadOnlyList<Hit> hits)
    {
        var sb = new StringBuilder("[");
        for (int i = 0; i < hits.Count; i++)
        {
            var h = hits[i];
            if (i > 0) sb.Append(',');
            sb.Append("{\"seq\":").Append(h.Seq.ToString(CultureInfo.InvariantCulture))
              .Append(",\"side\":").Append(h.Side.ToString(CultureInfo.InvariantCulture))
              .Append(",\"trigger\":\"").Append(h.Trigger).Append('"');
            if (h.HeadCapped) sb.Append(",\"head_capped\":true");
            if (h.TailCapped) sb.Append(",\"tail_capped\":true");
            if (h.TailUnfinished) sb.Append(",\"tail_unfinished\":true");
            if (h.MergeCapped) sb.Append(",\"merge_capped\":true");
            sb.Append('}');
        }
        return sb.Append(']').ToString();
    }

    public string SkippedNote()
    {
        if (HitsSkippedByScope.Count == 0) return "";
        var detail = string.Join("・", HitsSkippedByScope.OrderBy(kv => kv.Key, StringComparer.Ordinal)
                                        .Select(kv => CaptureScope.Label(kv.Key) + " "
                                                      + kv.Value.ToString(CultureInfo.InvariantCulture)));
        return $"設定により起点 {HitsSkipped} 件ぶんを取りませんでした（{detail}）";
    }

    public string Status()
    {
        var detail = string.Join("・", _triggers.Select(
            t => t + " " + HitsByTrigger.GetValueOrDefault(t).ToString(CultureInfo.InvariantCulture)));
        var slack = MinSlack is long v ? v.ToString(CultureInfo.InvariantCulture) + " tick" : "—";
        var capped = $"上限 頭 {WindowsHeadCapped} 本・尾 {WindowsTailCapped} 本"
                     + $"・マージ {WindowsMergeCapped} 本・尾が未了 {WindowsTailUnfinished} 本";
        var skipped = SkippedNote();
        return $"窓 {WindowsWritten} 本 / {TicksWritten} tick / {BytesWritten:N0} B"
               + $"（起点 {HitsSeen} 件［{detail}］・欠け {LostTicks} tick・保留 {_pending.Count} 本"
               + $"・読み戻し失敗 {VerifyFailures} 本・最小余裕 {slack}"
               + $"・コピー中の追い越し {_reader.RacedReads} 回・{capped}）"
               + (skipped.Length > 0 ? "　" + skipped : "");
    }


    public sealed class Hit
    {
        public required long Seq { get; init; }
        public required int Side { get; init; }
        public required string Trigger { get; init; }

        public bool HeadCapped { get; set; }

        public bool TailCapped { get; set; }

        public bool TailUnfinished { get; set; }

        public bool MergeCapped { get; set; }
    }

    private sealed class Tail
    {
        public required long Anchor { get; init; }
        public required int Side { get; init; }
        public required int Need { get; set; }
        public required long Cursor { get; set; }
        public required long? Last { get; set; }
        public bool Capped { get; set; }
        public bool Unfinished { get; set; }
    }

    private sealed class Pending
    {
        public required long First { get; init; }
        public required long Last { get; set; }
        public required List<Hit> Hits { get; init; }
        public required List<Tail> Tails { get; init; }
        public bool MergeCapped { get; set; }
    }
}
