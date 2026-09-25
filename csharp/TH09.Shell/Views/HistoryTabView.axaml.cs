using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TH09.Shell.Data;
using TH09.Shell.ViewModels;

namespace TH09.Shell.Views;

internal partial class HistoryTabView : UserControl
{
    private const int AutoRefreshMs = 2000;

    private readonly DispatcherTimer _autoRefreshTimer;

    public HistoryTabView()
    {
        InitializeComponent();
        AddHandler(PointerPressedEvent, OnPointerPressedTunnel, RoutingStrategies.Tunnel);
        AddHandler(PointerReleasedEvent, OnPointerReleased, RoutingStrategies.Tunnel);

        _autoRefreshTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(AutoRefreshMs) };
        _autoRefreshTimer.Tick += (_, _) =>
        {
            if (!IsEffectivelyVisible) return;
            (DataContext as HistoryTabViewModel)?.AutoRefresh();
        };
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    protected override void OnAttachedToVisualTree(Avalonia.VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _autoRefreshTimer.Start();
    }

    protected override void OnDetachedFromVisualTree(Avalonia.VisualTreeAttachmentEventArgs e)
    {
        _autoRefreshTimer.Stop();
        base.OnDetachedFromVisualTree(e);
    }

    private void OnPointerPressedTunnel(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is not HistoryTabViewModel vm) return;
        var p = e.GetCurrentPoint(this).Properties;
        var right = p.IsRightButtonPressed;
        vm.ModifierHeld = right
                          || e.KeyModifiers.HasFlag(KeyModifiers.Control)
                          || e.KeyModifiers.HasFlag(KeyModifiers.Shift);
        if (right && RowUnder(e.Source) is HistoryRow row) vm.EnsureSelected(row);
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (DataContext is HistoryTabViewModel vm) vm.ModifierHeld = false;
    }

    private static HistoryRow? RowUnder(object? source)
    {
        if (source is not Visual v) return null;
        for (var c = v; c is not null; c = c.GetVisualParent())
            if (c is ListBoxItem item) return item.DataContext as HistoryRow;
        return null;
    }
}
