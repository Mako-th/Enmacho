using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace TH09.Shell.Views;

internal partial class TimelineView : UserControl
{
    public TimelineView() => InitializeComponent();

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
