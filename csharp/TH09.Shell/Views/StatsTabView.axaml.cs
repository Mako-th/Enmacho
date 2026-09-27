using Avalonia;
using Avalonia.Controls;
using Avalonia.LogicalTree;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using TH09.Shell.ViewModels;

namespace TH09.Shell.Views;

internal partial class StatsTabView : UserControl
{
    private static readonly string[] ScrollHomeTargets = ["GroupsScroll", "MatrixView", "LeafScroll"];

    private StatsTabViewModel? _vm;

    public StatsTabView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => Rebind();
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    private void Rebind()
    {
        if (_vm is not null) _vm.ScrollToHomeRequested -= OnScrollToHomeRequested;
        _vm = DataContext as StatsTabViewModel;
        if (_vm is not null) _vm.ScrollToHomeRequested += OnScrollToHomeRequested;
    }

    private void OnScrollToHomeRequested(object? sender, System.EventArgs e)
        => Dispatcher.UIThread.Post(ResetScrollHome, DispatcherPriority.Background);

    private void ResetScrollHome()
    {
        foreach (var name in ScrollHomeTargets)
        {
            if (this.GetLogicalDescendants().OfType<ScrollViewer>()
                    .FirstOrDefault(sv => sv.Name == name) is not { } sv)
                continue;
            if (sv.Offset.X != 0) sv.Offset = new Vector(0, sv.Offset.Y);
        }
    }
}
