using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.VisualTree;
using TH09.Shell.Data;
using TH09.Shell.ViewModels;

namespace TH09.Shell.Views;

internal partial class ReplayDetailView : UserControl
{
    private ReplayFileRow? _contextFile;

    public ReplayDetailView()
    {
        InitializeComponent();
        AddHandler(PointerPressedEvent, OnPointerPressedTunnel, RoutingStrategies.Tunnel);
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    private void OnPointerPressedTunnel(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsRightButtonPressed) return;
        _contextFile = RowUnder(e.Source);
    }

    private async void OnRevealFileRow(object? sender, RoutedEventArgs e) => await Reveal(_contextFile);

    private async void OnFileRowDoubleTapped(object? sender, TappedEventArgs e)
        => await Reveal(RowUnder(e.Source));

    private async Task Reveal(ReplayFileRow? row)
    {
        if (DataContext is not ReplayDetailViewModel vm) return;
        await FolderOpener.RevealAsync(TopLevel.GetTopLevel(this), vm.RevealDirectory(row));
    }

    private static ReplayFileRow? RowUnder(object? source)
    {
        if (source is not Visual v) return null;
        for (var c = v; c is not null; c = c.GetVisualParent())
            if (c is ListBoxItem item) return item.DataContext as ReplayFileRow;
        return null;
    }
}
