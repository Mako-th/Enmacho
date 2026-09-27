using TH09.Layer0;
using TH09.TickBus;

using ScanItems = TH09.Generated.DbColumns.ReplayScanItems;

namespace TH09.Record;

public sealed class HitWindows
{
    public const string SinkName = "hitwin";

    private readonly CoordBusReader? _bus;
    private readonly Action<string> _log;
    private readonly IReadOnlyList<string>? _triggers;
    private readonly int? _before, _after;
    private readonly int _margin;
    private readonly CoordRingReader? _reader;

    private HitWindowCollector? _collector;
    private LiveTickSource? _source;
    private long? _coordTicks0;

    private readonly IReadOnlyDictionary<string, bool>? _scopes;

    public string? ReplaySource { get; set; }

    public bool AdonisLoaded { get; set; }

    public IReadOnlyDictionary<string, long?> Health { get; private set; } =
        new Dictionary<string, long?>(StringComparer.Ordinal);

    public HitWindowCollector? Collector => _collector;

    public bool Enabled => _reader is not null;

    public HitWindows(CoordBusReader? bus, Action<string>? log = null,
                      IReadOnlyList<string>? triggers = null,
                      int? before = null, int? after = null,
                      IReadOnlyDictionary<string, bool>? scopes = null,
                      int margin = CoordRingReader.DefaultMargin)
    {
        _bus = bus;
        _log = log ?? (static _ => { });
        _triggers = triggers;
        _before = before;
        _after = after;
        _scopes = scopes;
        _margin = margin;
        _reader = bus is null ? null : new CoordRingReader(bus, margin);
    }

    public CoordRingReader? Reader => _reader;


    private string? ScopeGate(TickRecord rec)
    {
        if (_scopes is null) return null;
        try
        {
            var scope = CaptureScope.OfRecord(rec, AdonisLoaded, ReplaySource);
            return CaptureScope.IsEnabled(scope, _scopes) ? null : scope;
        }
        catch (Exception)
        {
            return null;
        }
    }


    public void Attach(LiveTickSource source, Layer0Writer? writer, string reason = "")
    {
        if (_reader is null) return;
        if (writer is null)
        {
            _source = null;
            _log("[hitwin] 書き先がありません（Layer 0 が無効）。被弾窓は採りません。"
                 + (reason.Length > 0 ? "（" + reason + "）" : ""));
            return;
        }
        try
        {
            if (_collector is null || !ReferenceEquals(source, _source))
            {
                _collector = new HitWindowCollector(_reader, writer, new HitWindowCollector.Options(
                    Before: _before ?? Generated.HitWindowConst.WindowBeforeTicks,
                    After: _after ?? Generated.HitWindowConst.WindowAfterTicks,
                    Triggers: _triggers,
                    Margin: _margin), _log);
                _collector.ScopeGate = ScopeGate;
            }
            source.AddSink(SinkName, recs =>
            {
                _collector.Feed(recs);
                _collector.Pump();
            });
            _source = source;
        }
        catch (Exception exc)
        {
            _log("[hitwin] 取得経路の切り替えに失敗しました: " + exc.Message);
        }
    }

    public void Detach()
    {
        _source = null;
        _collector = null;
        ReplaySource = null;
        Health = new Dictionary<string, long?>(StringComparer.Ordinal);
    }


    public void Begin(long sessionId)
    {
        if (_collector is null) return;
        try
        {
            _collector.BeginSession(sessionId);
            _coordTicks0 = CoordTicks();
        }
        catch (Exception exc)
        {
            _log($"[hitwin] session={sessionId} の窓を開始できません: {exc.Message}");
        }
    }

    public void End()
    {
        ReplaySource = null;
        if (_collector is null) return;
        try
        {
            _collector.EndSession();
            Health = BuildHealth(_collector);
            _log("[hitwin] " + _collector.Status());
        }
        catch (Exception exc)
        {
            _log("[hitwin] 窓を書き出せません: " + exc.Message);
        }
    }

    public void Close()
    {
        Detach();
        _bus?.Dispose();
    }


    private long? CoordTicks()
    {
        try
        {
            return _bus is null ? null : _bus.ReadHeader().CoordTicks;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private Dictionary<string, long?> BuildHealth(HitWindowCollector c)
    {
        long? now = CoordTicks();
        long? delta = now is null || _coordTicks0 is null ? null : now - _coordTicks0;
        return new Dictionary<string, long?>(StringComparer.Ordinal)
        {
            [ScanItems.HitWindows] = c.WindowsWritten,
            [ScanItems.HitWindowTicks] = c.TicksWritten,
            [ScanItems.HitWindowBytes] = c.BytesWritten,
            [ScanItems.HitWindowLostTicks] = c.LostTicks,
            [ScanItems.HitsSeen] = c.HitsSeen,
            [ScanItems.CoordTicks] = delta,
            [ScanItems.HitWindowSkippedHits] = c.HitsSkipped,
            [ScanItems.QuicksSeen] = c.HitsByTrigger.GetValueOrDefault(
                Generated.HitWindowConst.TriggerQuick),
            [ScanItems.HitWindowMinSlack] = c.MinSlack,
            [ScanItems.HitWindowVerifyFailures] = c.VerifyFailures,
        };
    }
}
