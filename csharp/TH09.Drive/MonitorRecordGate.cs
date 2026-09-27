using System.Diagnostics;
using TH09.Record;
using TH09.Record.Generated;

namespace TH09.Drive;

public sealed class MonitorRecordGate
{
    public const double NoticeSeconds = 10.0;

    public const string ReplaySkipNotice =
        "リプレイ再生を検出しました（record_replay_playback=false のため記録しません）。"
        + " 記録したい場合は GUI の「再生を記録」トグル（既定 F9）を ON に。";

    public const string DemoSkipHead = "タイトルデモとみなして記録しません（";

    public const string DemoSkipTail =
        "）。 人が始めた再生ならこれは誤判定です。"
        + " 記録したい場合は config.json の record_title_demo を true に。";

    public const string StrictPrefix = "厳密判定: ";

    public const string DemoUnknownNotice =
        "タイトルデモか判定できないので記録します（この監視には「直前の画面」で見る材料がありません）: ";

    public const string ReplaySkipReason = "record_replay_playback=false";

    public const string DemoSkipReason = "title demo";

    private readonly Func<bool> _recordReplay;
    private readonly Func<bool> _recordTitleDemo;
    private readonly Func<DemoCheck>? _demo;
    private readonly Action<string> _log;
    private readonly Func<double> _clock;
    private readonly Notice _replayNotice = new(NoticeSeconds);
    private readonly Notice _demoNotice = new(NoticeSeconds);
    private bool _unknownLogged;

    public MonitorRecordGate(Func<bool> recordReplay, Func<bool> recordTitleDemo,
                             Func<DemoCheck>? demo, Action<string> log, Func<double>? clock = null)
    {
        ArgumentNullException.ThrowIfNull(recordReplay);
        ArgumentNullException.ThrowIfNull(recordTitleDemo);
        ArgumentNullException.ThrowIfNull(log);
        _recordReplay = recordReplay;
        _recordTitleDemo = recordTitleDemo;
        _demo = demo;
        _log = log;
        _clock = clock ?? (() => (double)Stopwatch.GetTimestamp() / Stopwatch.Frequency);
    }

    public string? Skip(Snapshot s)
    {
        ArgumentNullException.ThrowIfNull(s);
        if (!string.Equals(s.ExecutionType, RecordLabels.ExecReplay, StringComparison.Ordinal))
        {
            return Record();
        }
        if (!_recordReplay())
        {
            if (_replayNotice.Due(_clock())) _log(ReplaySkipNotice);
            return ReplaySkipReason;
        }
        if (_recordTitleDemo()) return Record();
        var check = _demo is null ? new DemoCheck(false, false) : _demo();
        if (!check.Known)
        {
            if (!_unknownLogged)
            {
                _unknownLogged = true;
                _log(DemoUnknownNotice + check.Text());
            }
            return Record();
        }
        if (!check.Demo) return Record();
        if (_demoNotice.Due(_clock())) _log(DemoSkipHead + StrictPrefix + check.Text() + DemoSkipTail);
        return DemoSkipReason;
    }

    private string? Record()
    {
        _replayNotice.Reset();
        _demoNotice.Reset();
        return null;
    }

    private sealed class Notice(double interval)
    {
        private double? _lastAt;

        public bool Due(double now)
        {
            if (_lastAt is { } last && now - last < interval) return false;
            _lastAt = now;
            return true;
        }

        public void Reset() => _lastAt = null;
    }
}
