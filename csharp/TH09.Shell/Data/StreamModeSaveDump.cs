using System.Globalization;

namespace TH09.Shell.Data;

internal static class StreamModeSaveDump
{
    public const string Flag = "--dump-stream-view-save";

    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public static int Run(string configPath, double width, double height, double fontScale,
                          string background, bool topmost, bool borderless,
                          IReadOnlyList<string>? hidden = null)
    {
        try
        {
            AppSettingsSource.ReadFrom(configPath);
            var before = AppSettingsSource.Current;
            var options = new ViewModels.StreamPanelOptions(width, height, fontScale, background,
                                                            topmost, borderless);
            ViewModels.ShellViewModel.SaveStreamSettings(options, hidden ?? []);
            var after = AppSettingsSource.Current;
            Console.WriteLine("before-width\t" + N(before.StreamMode.Width));
            Console.WriteLine("after-width\t" + N(after.StreamMode.Width));
            Console.WriteLine("after-height\t" + N(after.StreamMode.Height));
            Console.WriteLine("after-font-scale\t" + after.StreamMode.FontScale.ToString(Inv));
            Console.WriteLine("after-background\t" + after.StreamMode.Background);
            Console.WriteLine("after-topmost\t" + (after.StreamMode.Topmost ? "1" : "0"));
            Console.WriteLine("after-borderless\t" + (after.StreamMode.Borderless ? "1" : "0"));
            Console.WriteLine("before-content\t" + before.StreamMode.Content);
            Console.WriteLine("after-content\t" + after.StreamMode.Content);
            Console.WriteLine("before-stream-hidden\t" + Join(before.StreamPanelHiddenItems));
            Console.WriteLine("after-stream-hidden\t" + Join(after.StreamPanelHiddenItems));
            Console.WriteLine("before-play-hidden\t" + Join(before.PlayTabHiddenItems));
            Console.WriteLine("after-play-hidden\t" + Join(after.PlayTabHiddenItems));
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.GetType().Name + ": " + ex.Message);
            return 1;
        }
    }

    private static string N(int v) => v.ToString(Inv);

    private static string Join(IReadOnlyList<string> items) => string.Join(",", items);
}
