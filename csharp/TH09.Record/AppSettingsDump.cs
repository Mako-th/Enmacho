using System.Globalization;

namespace TH09.Record;

public static class AppSettingsDump
{
    public const string DumpFlag = "--dump-settings";

    public const string WriteFlag = "--write-settings";

    public const char ListSeparator = '|';

    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public static int Dump(TextWriter w, string path)
    {
        ArgumentNullException.ThrowIfNull(w);
        if (Refuse(path)) return 3;
        Facts(w, path);
        Rows(w, ConfigStore.Load(path));
        return 0;
    }

    public static int Write(TextWriter w, string path, IReadOnlyList<string> assignments)
    {
        ArgumentNullException.ThrowIfNull(w);
        ArgumentNullException.ThrowIfNull(assignments);
        if (Refuse(path)) return 3;
        var settings = ConfigStore.Load(path);
        foreach (var one in assignments)
        {
            var i = one.IndexOf('=', StringComparison.Ordinal);
            if (i <= 0)
            {
                Console.Error.WriteLine("★<鍵>=<値> の形で指してください: " + one);
                return 1;
            }
            if (!Apply(ref settings, one[..i], one[(i + 1)..], out var why))
            {
                Console.Error.WriteLine(why);
                return 1;
            }
        }
        var outcome = ConfigStore.Save(path, settings);
        Facts(w, path);
        Row(w, "fact", "written", outcome.Written ? "1" : "0");
        Row(w, "fact", "created", outcome.Created ? "1" : "0");
        Row(w, "fact", "reason", outcome.Reason ?? "");
        Rows(w, ConfigStore.Load(path));
        return outcome.Written ? 0 : 4;
    }

    private static bool Refuse(string path)
    {
        if (!RealDbGuard.InConfigDir(path)) return false;
        Console.Error.WriteLine(
            "★本物の config.json のフォルダは受け付けません（合成の設定を渡してください）: " + path);
        return true;
    }

    private static void Facts(TextWriter w, string path)
    {
        Row(w, "fact", "config", Path.GetFullPath(path));
        Row(w, "fact", "exists", File.Exists(path) ? "1" : "0");
        Row(w, "fact", "real_config", Paths.Default.ConfigPath);
    }

    private static void Rows(TextWriter w, AppSettings s)
    {
        Row(w, "set", Paths.RecordReplayPlaybackKey, Flag01(s.RecordReplayPlayback));
        Row(w, "set", ConfigStore.RecordReplayToggleKeyKey, s.RecordReplayToggleKey);
        Row(w, "set", ConfigStore.HistoryRetentionAbortedKey, Num(s.HistoryRetentionAborted));
        Row(w, "set", ConfigStore.HistoryRetentionCompletedKey, Num(s.HistoryRetentionCompleted));
        Row(w, "set", ConfigStore.TickHookKey, ConfigStore.Word(s.TickHook));
        Row(w, "set", ConfigStore.HitWindowsKey, Flag01(s.HitWindows));
        Row(w, "set", ConfigStore.HitWindowBeforeKey, Num(s.HitWindowBefore));
        Row(w, "set", ConfigStore.HitWindowAfterKey, Num(s.HitWindowAfter));
        foreach (var name in CaptureScope.All)
        {
            Row(w, "set", ConfigStore.HitWindowScopesKey + "." + name,
                Flag01(s.HitWindowScopes[name]));
        }
        Row(w, "set", ConfigStore.HitWindowQuickKey, Flag01(s.HitWindowQuick));
        Row(w, "set", ConfigStore.AutoMonitorOnGameKey, Flag01(s.AutoMonitorOnGame));
        Row(w, "set", ConfigStore.WatchReplaysWithMonitorKey,
            Flag01(s.WatchReplaysWithMonitor));
        Row(w, "set", ConfigStore.BackupKeepOneKey, Flag01(s.BackupKeepOne));
        List(w, ConfigStore.StatsHiddenItemsKey, s.StatsHiddenItems);
        Map(w, ConfigStore.StatsColumnOrderKey, s.StatsColumnOrder);
        Map(w, ConfigStore.StatsHiddenColumnsKey, s.StatsHiddenColumns);
        List(w, ConfigStore.StreamPanelHiddenItemsKey, s.StreamPanelHiddenItems);
        List(w, ConfigStore.PlayTabHiddenItemsKey, s.PlayTabHiddenItems);
        Targets(w, StreamTargets.Key, s.StreamTargets);
        List(w, Paths.OwnPlayerNamesKey, s.OwnPlayerNames);
        Row(w, "fact", "notes", Num(s.Notes.Count));
        foreach (var note in s.Notes) Row(w, "note", note.Key, note.Kind, note.Text);
    }

    private static void List(TextWriter w, string key, IReadOnlyList<string> items)
    {
        Row(w, "set", key + ".count", Num(items.Count));
        for (var i = 0; i < items.Count; i++) Row(w, "set", key + "." + Num(i), items[i]);
    }

    private static void Map(TextWriter w, string key, StatsOrderMap map)
    {
        var sections = 0;
        foreach (var _ in map.All()) sections++;
        Row(w, "set", key + ".sections", Num(sections));
        foreach (var (page, section, items) in map.All())
            List(w, key + "." + page + "." + section, items);
    }

    private static void Targets(TextWriter w, string key, IReadOnlyList<StreamTargetEntry> entries)
    {
        var rows = StreamTargets.Normalize(entries);
        Row(w, "set", key + ".count", Num(rows.Count));
        for (var i = 0; i < rows.Count; i++)
        {
            var e = rows[i];
            Row(w, "target", Num(i), Long(e.Mode), Long(e.Difficulty), Long(e.Character),
                Opt(e.Stage), Opt(e.Round), Opt(e.Target), Opt(e.Wr), Opt(e.ClearBonus));
        }
    }

    private static bool Apply(ref AppSettings s, string key, string value, out string why)
    {
        why = "";
        if (key.StartsWith(ConfigStore.HitWindowScopesKey + ".", StringComparison.Ordinal))
        {
            var word = key[(ConfigStore.HitWindowScopesKey.Length + 1)..];
            if (!CaptureScope.All.Contains(word, StringComparer.Ordinal))
            {
                why = "★遊び方の語は " + string.Join(" / ", CaptureScope.All) + " だけです: " + word;
                return false;
            }
            if (Flag(value) is not bool on) { why = Why(key, value); return false; }
            var scopes = new Dictionary<string, bool>(s.HitWindowScopes, StringComparer.Ordinal)
            {
                [word] = on,
            };
            s = s with { HitWindowScopes = scopes };
            return true;
        }
        if (StatsMapKey(key) is string mapKey)
        {
            var rest = key[(mapKey.Length + 1)..];
            var dot = rest.IndexOf('.', StringComparison.Ordinal);
            if (dot <= 0 || dot == rest.Length - 1)
            {
                why = "★ページと節を指してください（" + mapKey + ".<ページ>.<節>）: " + key;
                return false;
            }
            var pageName = rest[..dot];
            var sectionName = rest[(dot + 1)..];
            var words = Words(value);
            s = mapKey == ConfigStore.StatsColumnOrderKey
                ? s with { StatsColumnOrder = s.StatsColumnOrder.With(pageName, sectionName, words) }
                : s with { StatsHiddenColumns = s.StatsHiddenColumns.With(pageName, sectionName, words) };
            return true;
        }
        switch (key)
        {
            case Paths.RecordReplayPlaybackKey when Flag(value) is bool v:
                s = s with { RecordReplayPlayback = v }; return true;
            case ConfigStore.RecordReplayToggleKeyKey:
                s = s with { RecordReplayToggleKey = value }; return true;
            case ConfigStore.HistoryRetentionAbortedKey when Number(value) is int v:
                s = s with { HistoryRetentionAborted = v }; return true;
            case ConfigStore.HistoryRetentionCompletedKey when Number(value) is int v:
                s = s with { HistoryRetentionCompleted = v }; return true;
            case ConfigStore.TickHookKey when Hook(value) is TickHookMode v:
                s = s with { TickHook = v }; return true;
            case ConfigStore.HitWindowsKey when Flag(value) is bool v:
                s = s with { HitWindows = v }; return true;
            case ConfigStore.HitWindowBeforeKey when Number(value) is int v:
                s = s with { HitWindowBefore = v }; return true;
            case ConfigStore.HitWindowAfterKey when Number(value) is int v:
                s = s with { HitWindowAfter = v }; return true;
            case ConfigStore.HitWindowQuickKey when Flag(value) is bool v:
                s = s with { HitWindowQuick = v }; return true;
            case ConfigStore.AutoMonitorOnGameKey when Flag(value) is bool v:
                s = s with { AutoMonitorOnGame = v }; return true;
            case ConfigStore.WatchReplaysWithMonitorKey when Flag(value) is bool v:
                s = s with { WatchReplaysWithMonitor = v }; return true;
            case ConfigStore.BackupKeepOneKey when Flag(value) is bool v:
                s = s with { BackupKeepOne = v }; return true;
            case ConfigStore.StatsHiddenItemsKey:
                s = s with { StatsHiddenItems = Words(value) };
                return true;
            case Paths.OwnPlayerNamesKey:
                s = s with { OwnPlayerNames = Words(value) };
                return true;
            case ConfigStore.StatsColumnOrderKey when value.Length == 0:
                s = s with { StatsColumnOrder = StatsOrderMap.Empty };
                return true;
            case ConfigStore.StatsHiddenColumnsKey when value.Length == 0:
                s = s with { StatsHiddenColumns = StatsOrderMap.Empty };
                return true;
            case ConfigStore.StatsColumnOrderKey:
            case ConfigStore.StatsHiddenColumnsKey:
                why = "★ページと節を指してください（" + key + ".<ページ>.<節>）: " + key + "=" + value;
                return false;
            case ConfigStore.StreamPanelHiddenItemsKey:
                s = s with { StreamPanelHiddenItems = Words(value) };
                return true;
            case ConfigStore.PlayTabHiddenItemsKey:
                s = s with { PlayTabHiddenItems = Words(value) };
                return true;
            case StreamTargets.Key when TargetRows(value) is { } rows:
                s = s with { StreamTargets = rows };
                return true;
            case StreamTargets.Key:
                why = "★" + StreamTargets.Key + " の行は "
                      + "mode:difficulty:character:stage:round:target:wr:clear_bonus の 8 つです"
                      + "（空欄 ＝ 未入力）: " + value;
                return false;
            default:
                why = Array.IndexOf(ConfigStore.OwnedKeys, key) >= 0
                    ? Why(key, value)
                    : "★設定画面が扱わない鍵です（この口では書けません）: " + key;
                return false;
        }
    }

    private static string? StatsMapKey(string key)
    {
        if (key.StartsWith(ConfigStore.StatsColumnOrderKey + ".", StringComparison.Ordinal))
            return ConfigStore.StatsColumnOrderKey;
        if (key.StartsWith(ConfigStore.StatsHiddenColumnsKey + ".", StringComparison.Ordinal))
            return ConfigStore.StatsHiddenColumnsKey;
        return null;
    }

    private static string[] Words(string value) =>
        value.Length == 0 ? [] : value.Split(ListSeparator);

    private static List<StreamTargetEntry>? TargetRows(string value)
    {
        var rows = new List<StreamTargetEntry>();
        if (value.Length == 0) return rows;
        foreach (var raw in value.Split(ListSeparator))
        {
            var cells = raw.Split(':');
            if (cells.Length != 8) return null;
            if (Long(cells[0]) is not long mode || Long(cells[1]) is not long diff
                || Long(cells[2]) is not long ch)
                return null;
            if (!OptLong(cells[3], out var stage) || !OptLong(cells[4], out var round)
                || !OptLong(cells[5], out var target) || !OptLong(cells[6], out var wr)
                || !OptLong(cells[7], out var cb))
                return null;
            rows.Add(new StreamTargetEntry(mode, diff, ch, stage, round, target, wr, cb));
        }
        return rows;
    }

    private static bool OptLong(string text, out long? value)
    {
        value = null;
        if (text.Length == 0) return true;
        if (Long(text) is not long v) return false;
        value = v;
        return true;
    }

    private static long? Long(string value) =>
        long.TryParse(value, NumberStyles.Integer, Inv, out var v) ? v : null;

    private static string Opt(long? v) => v is long x ? x.ToString(Inv) : "";

    private static string Long(long v) => v.ToString(Inv);

    private static string Why(string key, string value) =>
        $"★{key} に渡せない値です: {value}";

    private static bool? Flag(string value) => value switch
    {
        "true" or "1" => true,
        "false" or "0" => false,
        _ => null,
    };

    private static int? Number(string value) =>
        int.TryParse(value, NumberStyles.Integer, Inv, out var v) ? v : null;

    private static TickHookMode? Hook(string value)
    {
        if (string.Equals(value, "on", StringComparison.Ordinal)) return TickHookMode.On;
        if (string.Equals(value, "off", StringComparison.Ordinal)) return TickHookMode.Off;
        if (string.Equals(value, "auto", StringComparison.Ordinal)) return TickHookMode.Auto;
        return null;
    }

    private static string Flag01(bool v) => v ? "1" : "0";

    private static string Num(int v) => v.ToString(Inv);

    private static void Row(TextWriter w, params string[] cells) =>
        w.WriteLine(string.Join('\t', cells));
}
