using TH09.Launch;
using TH09.Record;

namespace TH09.Shell.Data;

internal sealed record DriveProcessState(LaunchKind Kind, bool IsRunning, bool CanStart, int? LastExitCode,
                                         string? LastFailureLine, bool StoppedIntentionally = false,
                                         bool SessionOpen = false, string? LastNoticeLine = null,
                                         ScanProgressLines.Mark? ScanProgress = null,
                                         string? LastImportResultLine = null,
                                         ScanProgressLines.ScanPlanSummary? LastScanPlan = null,
                                         ScanProgressLines.BatchOutcome? LastScanBatchResult = null,
                                         string? LastScanStopReason = null);

internal sealed record DriveControlSnapshot(bool IsDisposed, IReadOnlyList<DriveProcessState> Processes)
{
    public DriveProcessState this[LaunchKind kind] => Processes[(int)kind];
}

internal sealed class DriveProcessController : IDisposable
{
    private const string Category = "起動";
    private readonly object gate = new();
    private readonly IDriveProcessLauncher launcher;
    private readonly Dictionary<LaunchKind, RunningChild> children = [];
    private readonly Dictionary<LaunchKind, int?> lastExitCodes = [];
    private readonly Dictionary<LaunchKind, string?> lastFailureLines = [];
    private readonly Dictionary<LaunchKind, bool> lastIntended = [];
    private readonly Dictionary<LaunchKind, bool> sessionOpen = [];
    private readonly Dictionary<LaunchKind, string?> lastNoticeLines = [];
    private readonly Dictionary<LaunchKind, ScanProgressLines.Mark?> scanProgress = [];
    private readonly Dictionary<LaunchKind, string?> importResultLines = [];
    private readonly Dictionary<LaunchKind, ScanProgressLines.ScanPlanSummary?> scanPlans = [];
    private readonly Dictionary<LaunchKind, ScanProgressLines.BatchOutcome?> scanBatchResults = [];
    private readonly Dictionary<LaunchKind, string?> scanStopReasons = [];
    private bool disposed;

    public DriveProcessController() : this(new LaunchProcessLauncher()) { }

    internal DriveProcessController(IDriveProcessLauncher launcher)
        => this.launcher = launcher ?? throw new ArgumentNullException(nameof(launcher));

    public event Action<DriveControlSnapshot>? StateChanged;

    public DriveControlSnapshot Snapshot()
    {
        lock (gate) return SnapshotLocked();
    }

    public void Start(LaunchKind kind, LaunchOptions? options = null)
    {
        RunningChild child;
        while (true)
        {
            RunningChild? finished;
            lock (gate)
            {
                ObjectDisposedException.ThrowIf(disposed, this);
                if (children.TryGetValue(kind, out var current))
                {
                    if (current.Handle.IsRunning)
                        throw new InvalidOperationException($"{kind} は既に動いています");
                    finished = current;
                }
                else
                {
                    var handle = launcher.Start(kind, options);
                    child = new RunningChild(kind, handle, ReceiveLine, ReceiveExit);
                    child.Attach();
                    children.Add(kind, child);
                    sessionOpen[kind] = false;
                    lastNoticeLines[kind] = null;
                    scanProgress[kind] = null;
                    importResultLines[kind] = null;
                    scanPlans[kind] = null;
                    scanBatchResults[kind] = null;
                    scanStopReasons[kind] = null;
                    break;
                }
            }

            Complete(kind, finished);
        }

        LogSource.Info(Category, Name(kind) + " を開始しました");
        Publish();
        if (!child.Handle.IsRunning)
            Complete(kind, child);
    }

    public bool Stop(LaunchKind kind)
    {
        RunningChild? child;
        lock (gate)
        {
            if (!children.TryGetValue(kind, out child)) return false;
            child.StopRequested = true;
        }

        LogSource.Info(Category, Name(kind) + " の停止を要求しました");
        bool stopped = child.Handle.Stop();
        if (!stopped)
            LogSource.Error(Category, Name(kind) + " を停止できませんでした");
        if (!child.Handle.IsRunning)
            Complete(kind, child);
        return stopped;
    }

    public void Dispose()
    {
        RunningChild[] active;
        lock (gate)
        {
            if (disposed) return;
            disposed = true;
            active = [.. children.Values];
            foreach (var child in active) child.StopRequested = true;
        }

        Publish();
        foreach (var child in active)
        {
            LogSource.Info(Category, "終了に伴い " + Name(child.Kind) + " の停止を要求しました");
            Complete(child.Kind, child);
        }
    }

    private void ReceiveLine(LaunchKind kind, RunningChild child, string line)
    {
        bool changed = false;
        lock (gate)
        {
            if (!IsCurrent(kind, child)) return;
            if (!string.IsNullOrWhiteSpace(line)) child.LastLine = line;
            if (SessionMark(line) is bool open)
            {
                sessionOpen[kind] = open;
                changed = true;
            }
            if (IsNotice(line))
            {
                lastNoticeLines[kind] = line;
                changed = true;
            }
            if (ScanProgress(line) is { } mark)
            {
                scanProgress[kind] = mark;
                changed = true;
            }
            if (ImportResult(line) is { } importLine)
            {
                importResultLines[kind] = importLine;
                changed = true;
            }
            if (ScanPlan(line) is { } scanPlan)
            {
                scanPlans[kind] = scanPlan;
                changed = true;
            }
            if (BatchOutcome(line) is { } batchOutcome)
            {
                scanBatchResults[kind] = batchOutcome;
                changed = true;
            }
            if (StopReason(line) is { } stopReason)
            {
                scanStopReasons[kind] = stopReason;
                changed = true;
            }
        }
        LogSource.Info(Category, Name(kind) + ": " + line);
        if (changed) Publish();
    }

    private static bool? SessionMark(string line)
        => OperatingSystem.IsWindows() ? MonitorLines.SessionMark(line) : null;

    private static bool IsNotice(string line)
        => OperatingSystem.IsWindows() && MonitorLines.IsNotice(line);

    private static ScanProgressLines.Mark? ScanProgress(string line)
        => OperatingSystem.IsWindows() ? ScanProgressLines.TryParse(line) : null;

    private static string? ImportResult(string line)
        => OperatingSystem.IsWindows() && ScanProgressLines.TryParseImportResult(line, out var count)
            ? ScanProgressLines.ImportResult(count)
            : null;

    private static ScanProgressLines.ScanPlanSummary? ScanPlan(string line)
        => OperatingSystem.IsWindows() && ScanProgressLines.TryParseScanPlan(line, out var summary)
            ? summary
            : null;

    private static ScanProgressLines.BatchOutcome? BatchOutcome(string line)
        => OperatingSystem.IsWindows() && ScanProgressLines.TryParseBatchOutcome(line, out var outcome)
            ? outcome
            : null;

    private static string? StopReason(string line)
        => OperatingSystem.IsWindows() && ScanProgressLines.TryParseStopReason(line, out var reason)
            ? reason
            : null;

    private void ReceiveExit(LaunchKind kind, RunningChild child) => Complete(kind, child);

    private void Complete(LaunchKind kind, RunningChild? child)
    {
        if (child is null) return;

        bool intended;
        int? code;
        lock (gate)
        {
            if (!IsCurrent(kind, child)) return;
            children.Remove(kind);
            intended = child.StopRequested;
            code = child.Handle.ExitCode;
            lastExitCodes[kind] = code;
            lastFailureLines[kind] = !intended && code is int failed && failed != 0
                ? child.LastLine
                : null;
            lastIntended[kind] = intended;
            sessionOpen[kind] = false;
            scanProgress[kind] = null;
        }

        child.DisposeOnce();
        string message = Name(kind) + " は終了しました（終了コード "
                         + (code?.ToString() ?? "不明") + "）";
        if (!intended && code is int nonzero && nonzero != 0)
            LogSource.Error(Category, message);
        else
            LogSource.Info(Category, message);
        Publish();
    }

    private bool IsCurrent(LaunchKind kind, RunningChild child)
        => children.TryGetValue(kind, out var current) && ReferenceEquals(current, child);

    private void Publish()
    {
        DriveControlSnapshot snapshot;
        lock (gate) snapshot = SnapshotLocked();
        StateChanged?.Invoke(snapshot);
    }

    private DriveControlSnapshot SnapshotLocked()
    {
        var states = new DriveProcessState[Enum.GetValues<LaunchKind>().Length];
        foreach (var kind in Enum.GetValues<LaunchKind>())
        {
            bool running = !disposed
                           && children.TryGetValue(kind, out var child)
                           && child.Handle.IsRunning;
            lastExitCodes.TryGetValue(kind, out var code);
            lastFailureLines.TryGetValue(kind, out var failureLine);
            lastIntended.TryGetValue(kind, out bool intended);
            sessionOpen.TryGetValue(kind, out bool open);
            lastNoticeLines.TryGetValue(kind, out var notice);
            scanProgress.TryGetValue(kind, out var progress);
            importResultLines.TryGetValue(kind, out var importResult);
            scanPlans.TryGetValue(kind, out var scanPlan);
            scanBatchResults.TryGetValue(kind, out var scanBatchResult);
            scanStopReasons.TryGetValue(kind, out var scanStopReason);
            states[(int)kind] = new DriveProcessState(kind, running, !disposed && !running, code,
                                                     failureLine, intended, running && open, notice,
                                                     running ? progress : null, importResult,
                                                     scanPlan, scanBatchResult, scanStopReason);
        }
        return new DriveControlSnapshot(disposed, states);
    }

    private static string Name(LaunchKind kind) => kind switch
    {
        LaunchKind.Monitor => "監視",
        LaunchKind.Watch => "Replay保存監視",
        LaunchKind.ImportOnly => "既存Replay一括登録",
        LaunchKind.Scan => "走査",
        _ => kind.ToString(),
    };

    private sealed class RunningChild
    {
        private readonly Action<string> output;
        private readonly Action exited;
        private int disposed;

        public RunningChild(LaunchKind kind, IDriveProcessHandle handle,
                            Action<LaunchKind, RunningChild, string> receiveLine,
                            Action<LaunchKind, RunningChild> receiveExit)
        {
            Kind = kind;
            Handle = handle;
            output = line => receiveLine(kind, this, line);
            exited = () => receiveExit(kind, this);
        }

        public LaunchKind Kind { get; }
        public IDriveProcessHandle Handle { get; }
        public bool StopRequested { get; set; }

        public string? LastLine { get; set; }

        public void Attach()
        {
            Handle.OutputLine += output;
            Handle.Exited += exited;
        }

        public void DisposeOnce()
        {
            if (Interlocked.Exchange(ref disposed, 1) != 0) return;
            Handle.OutputLine -= output;
            Handle.Exited -= exited;
            Handle.Dispose();
        }
    }
}

internal interface IDriveProcessLauncher
{
    IDriveProcessHandle Start(LaunchKind kind, LaunchOptions? options);
}

internal interface IDriveProcessHandle : IDisposable
{
    event Action<string>? OutputLine;
    event Action? Exited;
    bool IsRunning { get; }
    int? ExitCode { get; }
    bool Stop();
}

internal sealed class LaunchProcessLauncher : IDriveProcessLauncher
{
    public IDriveProcessHandle Start(LaunchKind kind, LaunchOptions? options)
        => new LaunchProcessHandle(DriveLauncher.Start(kind, options));
}

internal sealed class LaunchProcessHandle : IDriveProcessHandle
{
    private readonly LaunchHandle handle;

    public LaunchProcessHandle(LaunchHandle handle)
    {
        this.handle = handle;
        handle.OutputLine += (_, e) => OutputLine?.Invoke(e.Line);
        handle.Exited += (_, _) => Exited?.Invoke();
    }

    public event Action<string>? OutputLine;
    public event Action? Exited;
    public bool IsRunning => handle.IsRunning;
    public int? ExitCode => handle.ExitCode;
    public bool Stop() => handle.Stop();
    public void Dispose() => handle.Dispose();
}
