using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using TH09.Analysis;
using TH09.Shell.ViewModels;

namespace TH09.Shell.Views;

internal partial class HitWindowView : UserControl
{
    private readonly DispatcherTimer _timer = new();

    public HitWindowView()
    {
        InitializeComponent();
        _timer.Interval = TimeSpan.FromMilliseconds(PollMs);
        _timer.Tick += (_, _) => Advance(Data.PlaybackTrace.DriverTimer);
        AddHandler(KeyDownEvent, OnKeyDownPreview, RoutingStrategies.Tunnel);
        DataContextChanged += (_, _) => Rewire();
        DetachedFromVisualTree += (_, _) =>
            { _timer.Stop(); StopPostClock(); EndFineTimer(); Data.PlaybackTrace.Dump(); };
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    private HitWindowViewModel? Vm => DataContext as HitWindowViewModel;

    internal static int ClockOverride { get; set; }

    internal void UseClock(int mode)
    {
        ClockOverride = mode;
        _timer.Stop();
        StopPostClock();
        _framePending = false;
        var vm = Vm;
        if (vm is null || !vm.Playing) return;
        _carry = 0.0;
        _clock.Restart();
        StartClock();
    }

    private void StartClock()
    {
        if (ClockOverride == 1)
        {
            if (TopLevel.GetTopLevel(this) is TopLevel top) RequestFrame(top);
            else _timer.Start();
            return;
        }
        if (ClockOverride == 2) { _timer.Start(); return; }
        StartPostClock();
    }

    private Data.FineClock? _postTimer;

    private int _posted;

    private void StartPostClock()
    {
        StopPostClock();
        var period = PollMs;
        _postTimer = new Data.FineClock(period, () =>
        {
            if (System.Threading.Interlocked.Exchange(ref _posted, 1) == 1) return;
            Dispatcher.UIThread.Post(() =>
            {
                System.Threading.Volatile.Write(ref _posted, 0);
                Advance(Data.PlaybackTrace.DriverPost);
            });
        });
        Data.PlaybackTrace.ClockKind = _postTimer.Kind;
    }

    private void StopPostClock()
    {
        _postTimer?.Dispose();
        _postTimer = null;
        System.Threading.Volatile.Write(ref _posted, 0);
    }

    private void Rewire()
    {
        _timer.Stop();
        StopPostClock();
        _pausedAt = -1;
        _resumeAtMs = 0;
        if (Vm is null) return;
        Vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(HitWindowViewModel.Playing)
                               or nameof(HitWindowViewModel.Speed)) ApplyPlaying();
        };
        ApplyPlaying();
    }

    private void ApplyPlaying()
    {
        var vm = Vm;
        if (vm is null) return;
        _carry = 0.0;
        _clock.Restart();
        if (!vm.Playing)
        {
            _timer.Stop();
            StopPostClock();
            EndFineTimer();
            Data.PlaybackTrace.Dump();
            return;
        }
        _pausedAt = -1;
        _resumeAtMs = 0;
        Data.PlaybackTrace.Reset();
        if (TopLevel.GetTopLevel(this) is TopLevel t)
            Data.PlaybackTrace.Note = string.Create(System.Globalization.CultureInfo.InvariantCulture,
                $"窓 {t.Bounds.Width:F0}x{t.Bounds.Height:F0} / 倒し具合 {t.RenderScaling:F2}"
                + $" / 盤面 {vm.BoardWidth:F0}x{vm.BoardHeight:F0}×{vm.Zoom:F2}"
                + $" / {vm.SubHeading}");
        BeginFineTimer();
        StartClock();
    }

    private bool _framePending;

    private bool _fineTimer;

    private void RequestFrame(TopLevel top)
    {
        if (_framePending) return;
        _framePending = true;
        top.RequestAnimationFrame(_ =>
        {
            _framePending = false;
            var vm = Vm;
            if (vm is null || !vm.Playing) return;
            if (TopLevel.GetTopLevel(this) is TopLevel again) RequestFrame(again);
            Advance(Data.PlaybackTrace.DriverRaf);
        });
    }

    private void Advance(byte driver)
    {
        var vm = Vm;
        if (vm is null) return;
        if (!vm.Playing) return;
        if (_wall.Elapsed.TotalMilliseconds < _resumeAtMs)
        {
            _clock.Restart();
            return;
        }
        if (vm.TickIndex >= vm.SeekMax)
        {
            if (string.Equals(vm.Loop, "on", StringComparison.Ordinal))
            {
                vm.TickIndex = vm.StartIndex();
                _carry = 0.0;
                _clock.Restart();
                _pausedAt = -1;
                return;
            }
            vm.Playing = false;
            _clock.Restart();
            return;
        }
        double ms = _clock.Elapsed.TotalMilliseconds;
        _clock.Restart();
        double per = vm.TickMs;
        if (per <= 0) return;
        _carry += ms / per;
        int steps = (int)_carry;
        Data.PlaybackTrace.Beat1(driver, steps, vm.TickIndex, vm.Speed);
        if (steps <= 0) return;
        _carry -= steps;
        if (steps > MaxCatchUp) { steps = MaxCatchUp; _carry = 0.0; }

        int to = Math.Min(vm.TickIndex + steps, vm.SeekMax);
        int holdAt = -1;
        for (int t = vm.TickIndex + 1; t <= to; t++)
        {
            if (t == _pausedAt) continue;
            if (HoldHere(vm, t)) { holdAt = t; break; }
        }
        if (holdAt >= 0)
        {
            vm.StepBy(holdAt - vm.TickIndex);
            _pausedAt = holdAt;
            _resumeAtMs = _wall.Elapsed.TotalMilliseconds + HoldMs;
            _carry = 0.0;
            return;
        }

        vm.StepBy(steps);
        var t0 = System.Diagnostics.Stopwatch.StartNew();
        Dispatcher.UIThread.Post(() => Data.PlaybackTrace.NoteUi(t0.Elapsed.TotalMilliseconds),
                                 DispatcherPriority.Background);
    }

    private static readonly double PollMs = PollMsAsked();

    private static double PollMsAsked()
    {
        var v = Environment.GetEnvironmentVariable("TH09_POLL_MS");
        return double.TryParse(v, System.Globalization.NumberStyles.Float,
                               System.Globalization.CultureInfo.InvariantCulture, out var ms)
               && ms >= 0.5 && ms <= 16.0 ? ms : DefaultPollMs;
    }

    internal const double DefaultPollMs = 4.0;

    private void BeginFineTimer()
    {
        if (_fineTimer) return;
        _fineTimer = true;
        Data.TimerResolution.Begin();
    }

    private void EndFineTimer()
    {
        if (!_fineTimer) return;
        _fineTimer = false;
        Data.TimerResolution.End();
    }

    private const int MaxCatchUp = 8;

    private readonly System.Diagnostics.Stopwatch _clock = System.Diagnostics.Stopwatch.StartNew();
    private double _carry;

    public const double HoldMs = 700;
    private int _pausedAt = -1;
    private readonly System.Diagnostics.Stopwatch _wall = System.Diagnostics.Stopwatch.StartNew();
    private double _resumeAtMs;

    private static bool HoldHere(HitWindowViewModel vm, int tick)
    {
        foreach (var e in vm.Events)
            if (e.Index == tick) return true;
        var w = vm.Window;
        if (w is null) return false;
        foreach (var a in CardEvents.For(w).Cards())
            if (a.Index == tick && a.Source == CardSource.Quick) return true;
        return false;
    }

    private void OnKeyDownPreview(object? sender, KeyEventArgs e)
    {
        var vm = Vm;
        if (vm is null) return;
        bool shift = e.KeyModifiers.HasFlag(KeyModifiers.Shift);
        switch (e.Key)
        {
            case Key.Left: vm.StepBy(shift ? -10 : -1); break;
            case Key.Right: vm.StepBy(shift ? 10 : 1); break;
            case Key.Space: vm.Playing = !vm.Playing; break;
            case Key.Escape: vm.StopPlayCommand.Execute(null); break;
            case Key.Home: vm.TickIndex = 0; break;
            case Key.H: vm.GoHitCommand.Execute(null); break;
            case Key.OemMinus or Key.Subtract: vm.SpeedBump(-1); break;
            case Key.OemPlus or Key.Add: vm.SpeedBump(1); break;
            default: return;
        }
        e.Handled = true;
    }
}
