using System.Globalization;
using System.Text;
using TH09.Record;
using TH09.Shell.ViewModels;

namespace TH09.Shell.Data;

internal static class StreamPanelViewDump
{
    public const string Flag = "--dump-stream-view";

    private const char Separator = '\t';

    public const string SeparatorMark = "――";

    public static readonly IReadOnlyList<string> MenuItems =
    [
        "出す項目",
        "背景色",
        "文字を大きく",
        "文字を小さく",
        SeparatorMark,
        "常に手前を切替（★排他フルスクリーンでは危険）",
        "枠なし表示を切替（ドラッグで移動）",
        ResetSizeLabel(),
        "画面の外へ逃がす",
        "画面へ戻す",
        SeparatorMark,
        "閉じる",
    ];

    private static string ResetSizeLabel() =>
        "サイズを " + (int)StreamPanelOptions.Default.Width + "x"
        + (int)StreamPanelOptions.Default.Height + " に戻す";

    public static int Run(string dbPath, IReadOnlyList<string>? hidden = null)
    {
        using var stdout = new StreamWriter(Console.OpenStandardOutput(),
                                            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        try
        {
            TrackerDb.MainDbPath = dbPath;
            if (!TrackerDb.MainDbExists)
            {
                Console.Error.WriteLine("本体 DB が見つかりません: " + dbPath);
                return 2;
            }

            var kept = hidden ?? [];
            var vm = new StreamPanelViewModel(StreamPanelOptions.Default, static (_, _) => { },
                                              () => kept);
            using var db = TrackerDb.OpenMainDb();
            var targets = Array.Empty<StreamTargetEntry>();
            StreamPanelView? panel = null;
            var progress = PlayLiveQuery.Load(db, dots: 0, narrow: true,
                blockLookup: sid => StreamBlockSource.Load(dbPath, sid, targets, out panel, kept,
                                                           narrow: true));

            var sb = new StringBuilder();
            Meta(sb, "default-width", D(StreamPanelOptions.Default.Width));
            Meta(sb, "default-height", D(StreamPanelOptions.Default.Height));
            Meta(sb, "default-font-scale", D(StreamPanelOptions.Default.FontScale));
            Meta(sb, "default-background", StreamPanelOptions.Default.Background);
            Meta(sb, "default-topmost", StreamPanelOptions.Default.Topmost ? "1" : "0");
            Meta(sb, "default-borderless", StreamPanelOptions.Default.Borderless ? "1" : "0");

            Meta(sb, "font-step", D(StreamPanelViewModel.FontScaleStep));
            Meta(sb, "font-min", D(StreamPanelViewModel.MinFontScale));
            Meta(sb, "font-max", D(StreamPanelViewModel.MaxFontScale));
            Meta(sb, "font-walk-up", string.Join(",", FontWalk(2.5, up: true, steps: 4)));
            Meta(sb, "font-walk-down", string.Join(",", FontWalk(0.9, up: false, steps: 5)));

            Meta(sb, "menu-count", MenuItems.Count.ToString(CultureInfo.InvariantCulture));
            foreach (var item in MenuItems)
                sb.Append("@menu").Append(Separator).Append(item).Append('\n');

            Meta(sb, "bg-count", StreamPanelViewModel.Backgrounds.Count.ToString(CultureInfo.InvariantCulture));
            foreach (var bg in StreamPanelViewModel.Backgrounds)
                sb.Append("@bg").Append(Separator).Append(bg.Label).Append(Separator).Append(bg.Hex)
                  .Append('\n');

            Meta(sb, "session", progress.SessionId?.ToString(CultureInfo.InvariantCulture) ?? "");
            Meta(sb, "lines", progress.Lines.Count.ToString(CultureInfo.InvariantCulture));
            foreach (var line in progress.Lines) sb.Append(line).Append('\n');
            Meta(sb, "vm-reset-size-label", vm.ResetSizeLabel);
            Meta(sb, "hidden", string.Join(",", kept));
            Meta(sb, "vm-hidden", string.Join(",", vm.HiddenItems));
            Meta(sb, "vm-show-sb", vm.ShowSb ? "1" : "0");
            Meta(sb, "vm-show-pb", vm.ShowPb ? "1" : "0");
            Meta(sb, "vm-show-target", vm.ShowTarget ? "1" : "0");
            Meta(sb, "vm-show-wr", vm.ShowWr ? "1" : "0");

            Meta(sb, "target-count", targets.Length.ToString(CultureInfo.InvariantCulture));
            Meta(sb, "block-session", panel?.Session.ToString(CultureInfo.InvariantCulture) ?? "");
            if (panel is null)
            {
                Meta(sb, "block-lines", "0");
                Meta(sb, "item-count", "0");
                Meta(sb, "block-time", "~");
            }
            else
            {
                var spots = StreamCompareFormat.PickSpots(panel);
                Meta(sb, "block-time", spots.Count == 0 ? "~" : spots[0].IsTime ? "1" : "0");
                var blockLines = spots
                    .SelectMany(s => StreamCompareFormat.ComposeItemLines(s, kept, narrow: true))
                    .ToList();
                Meta(sb, "block-lines", blockLines.Count.ToString(CultureInfo.InvariantCulture));
                foreach (var line in blockLines)
                    sb.Append("@blockline").Append(Separator).Append(line.Text).Append('\n');

                var items = spots.SelectMany(s => s.Items).ToList();
                Meta(sb, "item-count", items.Count.ToString(CultureInfo.InvariantCulture));
                foreach (var item in items)
                    sb.Append("@item").Append(Separator).Append(item.Kind).Append(Separator)
                      .Append(N(item.Value)).Append(Separator).Append(N(item.Delta)).Append(Separator)
                      .Append(item.Source is null ? "~" : item.Source.Text()).Append(Separator)
                      .Append(N(item.UsedRound)).Append(Separator).Append(item.Fallback ? "1" : "0")
                      .Append('\n');
            }

            stdout.Write(sb.ToString());
            stdout.Flush();
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.GetType().Name + ": " + ex.Message);
            return 1;
        }
    }

    private static string D(double v) => v.ToString(CultureInfo.InvariantCulture);

    private static List<string> FontWalk(double start, bool up, int steps)
    {
        var vm = new StreamPanelViewModel(StreamPanelOptions.Default with { FontScale = start },
                                          static (_, _) => { }, static () => []);
        var walk = new List<string>(steps);
        for (var i = 0; i < steps; i++)
        {
            if (up) vm.EnlargeFontCommand.Execute(null);
            else vm.ShrinkFontCommand.Execute(null);
            walk.Add(D(vm.Options.FontScale));
        }
        return walk;
    }

    private static string N(long? v) => v is long i ? i.ToString(CultureInfo.InvariantCulture) : "~";

    private static void Meta(StringBuilder sb, string name, string value)
        => sb.Append('@').Append(name).Append(Separator).Append(value).Append('\n');
}
