using Avalonia;
using Avalonia.Controls;

namespace TH09.Shell.Data;

internal static class OffscreenWindow
{
    public static readonly PixelPoint Offscreen = new(-32000, -32000);

    public const int SafeShowWidth = 640;

    public const int SafeShowHeight = 480;

    public const string ShowEnvVar = "TH09_SHOW_TEST_WINDOWS";

    public static bool ShowOnScreen
        => Environment.GetEnvironmentVariable(ShowEnvVar) == "1";

    public static void Prepare(Window window)
    {
        window.ShowActivated = false;
        window.ShowInTaskbar = false;
        if (ShowOnScreen) return;
        window.WindowStartupLocation = WindowStartupLocation.Manual;
        window.Position = Offscreen;
        Pin(window, Offscreen);
    }

    private static void Pin(Window window, PixelPoint target)
    {
        void OnPositionChanged(object? sender, PixelPointEventArgs e)
        {
            if (e.Point != target) window.Position = target;
        }
        window.PositionChanged += OnPositionChanged;
        window.Closed += (_, _) => window.PositionChanged -= OnPositionChanged;
    }

    public static void PrepareForGrowth(Window window)
    {
        if (!ShowOnScreen)
        {
            window.Width = SafeShowWidth;
            window.Height = SafeShowHeight;
        }
        Prepare(window);
    }

    public static void GrowAfterShow(Window window, double width, double height)
    {
        window.Width = width;
        window.Height = height;
        Settle(window);
    }

    public static void Settle(Window window)
    {
        if (ShowOnScreen) return;
        for (var i = 0; i < 8; i++)
        {
            window.Position = Offscreen;
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            System.Threading.Thread.Sleep(15);
        }
    }

    public static void Settle(Window window, PixelPoint position)
    {
        window.Position = position;
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
    }
}
