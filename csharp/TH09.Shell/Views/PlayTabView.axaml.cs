using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Threading;
using TH09.Shell.Data;
using TH09.Shell.ViewModels;

namespace TH09.Shell.Views;

internal partial class PlayTabView : UserControl
{
    private const int RefreshMs = 250;

    private static readonly IReadOnlyDictionary<PlayRole, string?> RoleBrushKeys =
        new Dictionary<PlayRole, string?>
        {
            [PlayRole.Body] = null,
            [PlayRole.Label] = "Sub",
            [PlayRole.Value] = "Fg",
            [PlayRole.Separator] = "Rule",
            [PlayRole.Title] = "PlayTitle",
            [PlayRole.Gain] = "Gain",
            [PlayRole.Loss] = "Loss",
        };

    private readonly DispatcherTimer _timer;

    private string? _shown;

    private SelectableTextBlock? _text;

    public PlayTabView()
    {
        InitializeComponent();
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(RefreshMs) };
        _timer.Tick += (_, _) => Refresh();
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    protected override void OnAttachedToVisualTree(Avalonia.VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Refresh();
        _timer.Start();
    }

    protected override void OnDetachedFromVisualTree(Avalonia.VisualTreeAttachmentEventArgs e)
    {
        _timer.Stop();
        base.OnDetachedFromVisualTree(e);
    }

    private void Refresh()
    {
        if (DataContext is not PlayTabViewModel vm) return;
        vm.Refresh();
        Paint(vm);
    }

    private void Paint(PlayTabViewModel vm)
    {
        if (_shown == vm.Text) return;
        _shown = vm.Text;

        _text ??= this.FindControl<SelectableTextBlock>("PlayText")
                  ?? throw new InvalidOperationException(
                      "PlayText が見つからない（PlayTabView.axaml の名前を変えたら、ここも直す）");

        var inlines = new InlineCollection();
        for (var i = 0; i < vm.Rows.Count; i++)
        {
            if (i > 0) inlines.Add(new LineBreak());
            foreach (var span in vm.Rows[i].Spans)
            {
                var run = new Run(span.Text);
                if (BrushFor(span.Role) is IBrush brush) run.Foreground = brush;
                inlines.Add(run);
            }
        }
        _text.Inlines = inlines;
    }

    private IBrush? BrushFor(PlayRole role)
        => RoleBrushKeys.TryGetValue(role, out var key) && key is not null
           && this.TryFindResource(key, out var found) && found is IBrush brush
            ? brush
            : null;
}
