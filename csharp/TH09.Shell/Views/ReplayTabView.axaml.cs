using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.VisualTree;
using TH09.Shell.ViewModels;

namespace TH09.Shell.Views;

internal partial class ReplayTabView : UserControl
{
    public ReplayTabView()
    {
        InitializeComponent();
        AddHandler(PointerPressedEvent, OnPointerPressedTunnel, RoutingStrategies.Tunnel);
        AddHandler(PointerReleasedEvent, OnPointerReleased, RoutingStrategies.Tunnel);
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    private void OnPointerPressedTunnel(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is not ReplayTabViewModel vm) return;
        var props = e.GetCurrentPoint(this).Properties;
        vm.ContextHeld = props.IsRightButtonPressed;
        vm.CheckHeld = props.IsLeftButtonPressed && IsOnCheckBox(e.Source);
    }

    private static bool IsOnCheckBox(object? source)
        => source is Visual v && (v is CheckBox || v.FindAncestorOfType<CheckBox>() is not null);

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (DataContext is ReplayTabViewModel vm)
        {
            vm.ContextHeld = false;
            vm.CheckHeld = false;
        }
    }

    private async void OnRevealReplayRow(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not ReplayTabViewModel vm) return;
        var chosen = e.Source is Control { DataContext: RevealChoice choice } ? choice.Directory : null;
        await FolderOpener.RevealAsync(TopLevel.GetTopLevel(this), vm.RevealDirectory(vm.ContextRow, chosen));
    }
}
