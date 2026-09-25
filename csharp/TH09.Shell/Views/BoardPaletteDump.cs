using System.Globalization;
using TH09.Analysis;

namespace TH09.Shell.Views;


internal static class BoardPaletteDump
{
    public static int Run()
    {
        int n = 0;
        Console.WriteLine("# th09-board-colors");
        foreach (var light in new[] { false, true })
        {
            var theme = light ? "light" : "dark";
            var pal = BoardPalette.Of(theme);

            foreach (var key in BoardPalette.PaletteRows.Keys.OrderBy(x => x, StringComparer.Ordinal))
                n += Row(theme, "palette:" + key,
                         BoardPalette.PaletteColor(key, 0.0, light)
                         ?? throw new InvalidOperationException("段の色が引けない: " + key));

            n += Row(theme, "fade:card", BoardPalette.FadeUnknown(BoardPalette.PaletteColor("card", 0.0, light)!));
            n += Row(theme, "fade:boss", BoardPalette.FadeUnknown(BoardPalette.PaletteColor("boss", 0.0, light)!));
            n += Row(theme, "fade:ex", BoardPalette.FadeUnknown(BoardPalette.PaletteColor("ex", 0.0, light)!));

            for (int sub = 1; sub <= 9; sub++)
                n += Row(theme, "boss_sub:" + sub.ToString(CultureInfo.InvariantCulture),
                         BoardPalette.BossSubColor(sub, light));
            n += Row(theme, "boss_sub:null", BoardPalette.BossSubColor(null, light));

            foreach (var lv in new int?[] { 2, 3, 4, null })
                n += Row(theme, "card_level:" + (lv?.ToString(CultureInfo.InvariantCulture) ?? "null"),
                         BoardPalette.CardLevelColor(lv, light));

            foreach (var sp in new[] { 0, 1, 2, 3, 5, 6, 7, 8, 9, 11, 12, 13, 16, 18, 19, 20, 21, 22, 4 })
                n += Row(theme, "sprite:" + sp.ToString(CultureInfo.InvariantCulture),
                         HexOf(pal.BulletSpriteBrush(sp)));

            (string? O, string? C, int? L, int? S)[] origins =
            [
                (null, null, null, null),
                ("enemy", "boss", null, 5), ("enemy", "boss", null, null),
                ("enemy", "c2c3", 2, null), ("enemy", "c2c3", 3, null), ("enemy", "c2c3", null, null),
                ("enemy", "lily", null, null), ("enemy", "fairy", null, null), ("enemy", null, null, null),
                ("white_bullet", null, null, null), ("ghost_penalty", null, null, null),
                ("bullet_item", null, null, null), ("ex", null, null, null),
            ];
            foreach (var (o, c, l, s) in origins)
                n += Row(theme, $"origin:{o ?? "-"}/{c ?? "-"}/{l?.ToString(CultureInfo.InvariantCulture) ?? "-"}/{s?.ToString(CultureInfo.InvariantCulture) ?? "-"}",
                         BoardPalette.OriginColor(o, c, l, s, light));

            foreach (var kv in BoardPalette.EnemyColors(light).OrderBy(x => x.Key, StringComparer.Ordinal))
                n += Row(theme, "enemy:" + kv.Key, kv.Value);

            foreach (var sd in new[] { 1, 2 })
                foreach (var (c1, name) in new (bool?, string)[]
                         { (false, "normal"), (true, "c1"), (null, "unknown") })
                    n += Row(theme, $"shot.{sd}.{name}", HexOf(pal.ShotBrush(sd, c1)));
        }
        Console.WriteLine($"# rows={n}");
        return n > 0 ? 0 : 2;
    }

    private static int Row(string theme, string key, string hex)
    {
        Console.WriteLine(theme + "\t" + key + "\t" + hex);
        return 1;
    }

    private static string HexOf(Avalonia.Media.IBrush b)
    {
        if (b is not Avalonia.Media.ISolidColorBrush s)
            throw new InvalidOperationException("単色でないブラシが盤面に混ざっている: " + b.GetType().Name);
        return string.Format(CultureInfo.InvariantCulture, "#{0:x2}{1:x2}{2:x2}",
                             s.Color.R, s.Color.G, s.Color.B);
    }
}
