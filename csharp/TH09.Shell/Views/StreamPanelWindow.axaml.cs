using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using TH09.Shell.ViewModels;

namespace TH09.Shell.Views;

internal partial class StreamPanelWindow : Window
{
    private const int RefreshMs = 250;

    private readonly DispatcherTimer _timer;
    private StreamPanelViewModel? _vm;

    private static readonly PixelPoint OffscreenPosition = new(-32000, -32000);
    private PixelPoint? _restorePosition;

    public bool IsOffscreen => _restorePosition is not null;

    public StreamPanelWindow()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => Attach();
        AddHandler(KeyDownEvent, OnKeyDownTunnel, RoutingStrategies.Tunnel);
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(RefreshMs) };
        _timer.Tick += (_, _) => Refresh();
        Opened += (_, _) =>
        {
            Refresh();
            _timer.Start();
        };
        Closed += (_, _) => _timer.Stop();
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    private void Refresh() => (DataContext as StreamPanelViewModel)?.Refresh();

    private void Attach()
    {
        if (_vm is not null)
        {
            _vm.CloseRequested -= Close;
            _vm.ResizeRequested -= OnResizeRequested;
            _vm.HideOffscreenRequested -= OnHideOffscreenRequested;
            _vm.RestoreOnscreenRequested -= OnRestoreOnscreenRequested;
        }
        _vm = DataContext as StreamPanelViewModel;
        if (_vm is null) return;
        _vm.CloseRequested += Close;
        _vm.ResizeRequested += OnResizeRequested;
        _vm.HideOffscreenRequested += OnHideOffscreenRequested;
        _vm.RestoreOnscreenRequested += OnRestoreOnscreenRequested;
        Width = _vm.Options.Width;
        Height = _vm.Options.Height;
    }

    private void OnResizeRequested(double width, double height)
    {
        Width = width;
        Height = height;
    }

    private void OnHideOffscreenRequested()
    {
        if (_restorePosition is not null) return;
        _restorePosition = Position;
        Position = OffscreenPosition;
    }

    private void OnRestoreOnscreenRequested() => RestoreOnscreen();

    public void RestoreOnscreen()
    {
        if (_restorePosition is not PixelPoint p) return;
        Position = p;
        _restorePosition = null;
    }

    private void OnKeyDownTunnel(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape || DataContext is not StreamPanelViewModel vm) return;
        vm.CloseCommand.Execute(null);
        e.Handled = true;
    }

    private void OnDragSurfacePressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) BeginMoveDrag(e);
    }
}
