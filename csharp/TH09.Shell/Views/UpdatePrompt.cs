using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using TH09.Shell.Data;

namespace TH09.Shell.Views;

internal static class UpdatePrompt
{
    public static Task<bool> AskAsync(Window owner) => Build().ShowDialog<bool>(owner);

    public static Window Build()
    {
        var dlg = new Window
        {
            Title = "幻想閻魔帳",
            Width = 420,
            SizeToContent = SizeToContent.Height,
            CanResize = false,
            CanMinimize = false,
            CanMaximize = false,
            ShowInTaskbar = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
        };
        var yes = new Button { Content = "はい", MinWidth = 80, IsDefault = true };
        var no = new Button { Content = "いいえ", MinWidth = 80, IsCancel = true };
        yes.Click += (_, _) => dlg.Close(true);
        no.Click += (_, _) => dlg.Close(false);
        dlg.Content = new StackPanel
        {
            Margin = new Thickness(20),
            Spacing = 20,
            Children =
            {
                new TextBlock { Text = UpdateNotice.PromptMessage, TextWrapping = TextWrapping.Wrap },
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Spacing = 10,
                    Children = { yes, no },
                },
            },
        };
        return dlg;
    }
}
