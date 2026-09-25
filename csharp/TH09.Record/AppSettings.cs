using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace TH09.Record;

public enum TickHookMode
{
    Auto,

    On,

    Off,
}

public sealed record SettingNote(string Key, string Kind, string Text);

public sealed record StatsOrderSection(string Name, IReadOnlyList<string> Items);

public sealed record StatsOrderPage(string Name, IReadOnlyList<StatsOrderSection> Sections);

public sealed record StatsOrderMap(IReadOnlyList<StatsOrderPage> Pages)
{
    public static readonly StatsOrderMap Empty = new([]);

    public bool IsEmpty
    {
        get
        {
            foreach (var page in Pages)
            {
                foreach (var section in page.Sections)
                {
                    if (section.Items.Count > 0) return false;
                }
            }
            return true;
        }
    }

    public IReadOnlyList<string> Of(string page, string section)
    {
        foreach (var p in Pages)
        {
            if (!string.Equals(p.Name, page, StringComparison.Ordinal)) continue;
            foreach (var s in p.Sections)
            {
                if (string.Equals(s.Name, section, StringComparison.Ordinal)) return s.Items;
            }
        }
        return [];
    }

    public StatsOrderMap With(string page, string section, IReadOnlyList<string> items)
    {
        var pages = new List<StatsOrderPage>(Pages.Count + 1);
        var donePage = false;
        foreach (var p in Pages)
        {
            if (!string.Equals(p.Name, page, StringComparison.Ordinal)) { pages.Add(p); continue; }
            donePage = true;
            var sections = new List<StatsOrderSection>(p.Sections.Count + 1);
            var doneSection = false;
            foreach (var s in p.Sections)
            {
                if (string.Equals(s.Name, section, StringComparison.Ordinal))
                {
                    sections.Add(new StatsOrderSection(section, items));
                    doneSection = true;
                }
                else sections.Add(s);
            }
            if (!doneSection) sections.Add(new StatsOrderSection(section, items));
            pages.Add(new StatsOrderPage(p.Name, sections));
        }
        if (!donePage) pages.Add(new StatsOrderPage(page, [new StatsOrderSection(section, items)]));
        return new StatsOrderMap(pages);
    }

    public IEnumerable<(string Page, string Section, IReadOnlyList<string> Items)> All()
    {
        foreach (var p in Pages)
        {
            foreach (var s in p.Sections) yield return (p.Name, s.Name, s.Items);
        }
    }
}

public sealed record AppSettings(
    bool RecordReplayPlayback,
    string RecordReplayToggleKey,
    int HistoryRetentionAborted,
    int HistoryRetentionCompleted,
    TickHookMode TickHook,
    bool HitWindows,
    int HitWindowBefore,
    int HitWindowAfter,
    IReadOnlyDictionary<string, bool> HitWindowScopes,
    bool HitWindowQuick,
    bool AutoMonitorOnGame,
    bool WatchReplaysWithMonitor,
    bool BackupKeepOne,
    IReadOnlyList<string> StatsHiddenItems,
    StatsOrderMap StatsColumnOrder,
    StatsOrderMap StatsHiddenColumns,
    StreamModeSettings StreamMode,
    IReadOnlyList<string> StreamPanelHiddenItems,
    IReadOnlyList<string> PlayTabHiddenItems,
    IReadOnlyList<StreamTargetEntry> StreamTargets,
    IReadOnlyList<string> OwnPlayerNames,
    IReadOnlyList<SettingNote> Notes);

public sealed record StreamModeSettings(int Width, int Height, double FontScale, string Background,
                                        bool Topmost, bool Borderless, string Content)
{
    public static readonly StreamModeSettings Default =
        new(Width: 480, Height: 1080, FontScale: 1.5, Background: "#0b0b12",
            Topmost: false, Borderless: false, Content: "compare");
}

public sealed record SaveOutcome(bool Written, bool Created, string? Reason);

public static class ConfigStore
{

    public const string RecordReplayToggleKeyKey = "record_replay_toggle_key";

    public const string RecordReplayToggleKeyDefault = "<F9>";

    public const string HistoryRetentionAbortedKey = "history_retention_aborted";

    public const int HistoryRetentionAbortedDefault = 30;

    public const string HistoryRetentionCompletedKey = "history_retention_completed";

    public const int HistoryRetentionCompletedDefault = 0;

    public const string TickHookKey = "tick_hook";

    public const string HitWindowsKey = "hit_windows";

    public const bool HitWindowsDefault = true;

    public const string HitWindowBeforeKey = "hit_window_before";

    public const string HitWindowAfterKey = "hit_window_after";

    public const string HitWindowScopesKey = "hit_window_scopes";

    public const string HitWindowQuickKey = "hit_window_quick";

    public const bool HitWindowQuickDefault = true;

    public const string AutoMonitorOnGameKey = "auto_monitor_on_game";

    public const bool AutoMonitorOnGameDefault = false;

    public const string WatchReplaysWithMonitorKey = "watch_replays_with_monitor";

    public const bool WatchReplaysWithMonitorDefault = false;

    public const string BackupKeepOneKey = "backup_keep_one";

    public const bool BackupKeepOneDefault = true;

    public const string StatsHiddenItemsKey = "stats_hidden_items";

    public const string StatsColumnOrderKey = "stats_column_order";

    public const string StatsHiddenColumnsKey = "stats_hidden_columns";

    public const string StatsSeparatorPrefix = "区切り:";

    public const string StreamModeKey = "stream_mode";

    public const string StreamPanelHiddenItemsKey = "stream_panel_hidden_items";

    public const string PlayTabHiddenItemsKey = "play_tab_hidden_items";

    public const string NoteMissing = "missing";

    public const string NoteType = "type";

    public const string NoteClamped = "clamped";

    public const string NoteWord = "word";

    public const string NoteUnreadable = "unreadable";

    public const string NoteUnknown = "unknown";

    public static readonly string[] OwnedKeys =
    [
        Paths.RecordReplayPlaybackKey,
        RecordReplayToggleKeyKey,
        HistoryRetentionAbortedKey,
        HistoryRetentionCompletedKey,
        TickHookKey,
        HitWindowsKey,
        HitWindowBeforeKey,
        HitWindowAfterKey,
        HitWindowScopesKey,
        HitWindowQuickKey,
        AutoMonitorOnGameKey,
        WatchReplaysWithMonitorKey,
        BackupKeepOneKey,
        StatsHiddenItemsKey,
        StatsColumnOrderKey,
        StatsHiddenColumnsKey,
        StreamModeKey,
        StreamPanelHiddenItemsKey,
        PlayTabHiddenItemsKey,
        StreamTargets.Key,
        Paths.OwnPlayerNamesKey,
    ];

    public static readonly string[] TickHookWords = ["auto", "on", "off"];

    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public static IReadOnlyList<string> HitWindowTriggers(bool quick) =>
        quick
            ? Generated.HitWindowConst.DefaultTriggers
            : [.. Generated.HitWindowConst.DefaultTriggers.Where(
                t => !string.Equals(t, Generated.HitWindowConst.TriggerQuick,
                                    StringComparison.Ordinal))];

    public static string Word(TickHookMode mode) => mode switch
    {
        TickHookMode.On => TickHookWords[1],
        TickHookMode.Off => TickHookWords[2],
        _ => TickHookWords[0],
    };


    public static AppSettings LoadDefault() => Load(Paths.Default.ConfigPath);

    public static AppSettings Load(string path)
    {
        var map = Paths.ReadConfig(path, out var loaded, out var extras);
        var notes = new List<SettingNote>();
        if (!loaded)
        {
            var exists = SafeExists(path);
            notes.Add(new SettingNote(
                "(config)", NoteUnreadable,
                exists
                    ? "★設定を読めませんでした（JSON が壊れている可能性があります）: " + path
                    : "設定がまだありません（既定で動きます）: " + path));
        }

        var toggleKey = Str(RecordReplayToggleKeyKey, RecordReplayToggleKeyDefault,
                            map, extras, notes);
        var aborted = NonNegative(HistoryRetentionAbortedKey,
                                  Int(HistoryRetentionAbortedKey, HistoryRetentionAbortedDefault,
                                      map, extras, notes), notes);
        var completed = NonNegative(HistoryRetentionCompletedKey,
                                    Int(HistoryRetentionCompletedKey,
                                        HistoryRetentionCompletedDefault, map, extras, notes),
                                    notes);

        var rawBefore = RawInt(HitWindowBeforeKey, map, extras, out var beforeKind);
        var rawAfter = RawInt(HitWindowAfterKey, map, extras, out var afterKind);
        var (before, after) = HitWindowLengths.Clamp(rawBefore, rawAfter);
        NoteFall(HitWindowBeforeKey, beforeKind, before, notes);
        NoteFall(HitWindowAfterKey, afterKind, after, notes);
        NoteClamp(HitWindowBeforeKey, rawBefore, before, notes);
        NoteClamp(HitWindowAfterKey, rawAfter, after, notes);

        return new AppSettings(
            RecordReplayPlayback: Truthy(Paths.RecordReplayPlaybackKey,
                                         Paths.RecordReplayPlaybackDefault, extras, notes),
            RecordReplayToggleKey: toggleKey,
            HistoryRetentionAborted: aborted,
            HistoryRetentionCompleted: completed,
            TickHook: Hook(map, extras, notes),
            HitWindows: Truthy(HitWindowsKey, HitWindowsDefault, extras, notes),
            HitWindowBefore: before,
            HitWindowAfter: after,
            HitWindowScopes: Scopes(extras, notes),
            HitWindowQuick: Truthy(HitWindowQuickKey, HitWindowQuickDefault, extras, notes),
            AutoMonitorOnGame: Truthy(AutoMonitorOnGameKey, AutoMonitorOnGameDefault,
                                      extras, notes),
            WatchReplaysWithMonitor: Truthy(WatchReplaysWithMonitorKey,
                                            WatchReplaysWithMonitorDefault, extras, notes),
            BackupKeepOne: Truthy(BackupKeepOneKey, BackupKeepOneDefault,
                                  extras, notes),
            StatsHiddenItems: NameList(StatsHiddenItemsKey, HideFallback, extras, notes),
            StatsColumnOrder: OrderMap(path, StatsColumnOrderKey, OrderFallback, notes),
            StatsHiddenColumns: OrderMap(path, StatsHiddenColumnsKey, HideFallback, notes),
            StreamMode: StreamMode(path, notes),
            StreamPanelHiddenItems: NameList(StreamPanelHiddenItemsKey, HideFallback, extras, notes),
            PlayTabHiddenItems: NameList(PlayTabHiddenItemsKey, HideFallback, extras, notes),
            StreamTargets: Targets(path, notes),
            OwnPlayerNames: NameList(Paths.OwnPlayerNamesKey, OwnNameFallback, extras, notes),
            Notes: notes);
    }

    private static IReadOnlyList<StreamTargetEntry> Targets(string path, List<SettingNote> notes)
    {
        var loaded = StreamTargets.Load(path);
        foreach (var note in loaded.Notes)
        {
            if (string.Equals(note.Kind, NoteUnreadable, StringComparison.Ordinal)) continue;
            notes.Add(note);
        }
        return loaded.Entries;
    }

    private static bool SafeExists(string path)
    {
        try { return File.Exists(path); }
        catch (Exception) { return false; }
    }

    private static bool Truthy(string key, bool dflt, Paths.ConfigExtras extras,
                               List<SettingNote> notes)
    {
        if (extras.Truthy.TryGetValue(key, out var v)) return v;
        notes.Add(Missing(key, dflt ? "ON" : "OFF"));
        return dflt;
    }

    private static string Str(string key, string dflt, Dictionary<string, string> map,
                              Paths.ConfigExtras extras, List<SettingNote> notes)
    {
        if (!extras.Keys.Contains(key)) { notes.Add(Missing(key, dflt)); return dflt; }
        if (extras.Strings.Contains(key) && map.TryGetValue(key, out var v)) return v;
        notes.Add(new SettingNote(key, NoteType,
                                  $"{key} が字ではないので既定（{dflt}）にしました"));
        return dflt;
    }

    private static int Int(string key, int dflt, Dictionary<string, string> map,
                           Paths.ConfigExtras extras, List<SettingNote> notes)
    {
        var v = RawInt(key, map, extras, out var kind);
        NoteFall(key, kind, v ?? dflt, notes);
        return v ?? dflt;
    }

    private static int? RawInt(string key, Dictionary<string, string> map,
                               Paths.ConfigExtras extras, out string? kind)
    {
        if (!extras.Keys.Contains(key)) { kind = NoteMissing; return null; }
        if (!extras.Strings.Contains(key) && map.TryGetValue(key, out var raw)
            && double.TryParse(raw, NumberStyles.Float, Inv, out var d)
            && d >= int.MinValue && d <= int.MaxValue)
        {
            kind = null;
            return (int)Math.Truncate(d);
        }
        kind = NoteType;
        return null;
    }

    private static void NoteFall(string key, string? kind, int used, List<SettingNote> notes)
    {
        if (kind is null) return;
        notes.Add(string.Equals(kind, NoteMissing, StringComparison.Ordinal)
            ? Missing(key, used.ToString(Inv))
            : new SettingNote(key, NoteType,
                              $"{key} が数として読めないので {used.ToString(Inv)} にしました"));
    }

    private static int NonNegative(string key, int value, List<SettingNote> notes)
    {
        if (value >= 0) return value;
        notes.Add(new SettingNote(key, NoteClamped,
                                  $"{key} の {value.ToString(Inv)} は 0 未満なので 0（無制限）にしました"));
        return 0;
    }

    private static void NoteClamp(string key, int? raw, int used, List<SettingNote> notes)
    {
        if (raw is not int r || r == used) return;
        notes.Add(new SettingNote(key, NoteClamped,
                                  $"{key} の {r.ToString(Inv)} は範囲外なので"
                                  + $" {used.ToString(Inv)} にしました"));
    }

    private static TickHookMode Hook(Dictionary<string, string> map, Paths.ConfigExtras extras,
                                     List<SettingNote> notes)
    {
        if (!extras.Keys.Contains(TickHookKey))
        {
            notes.Add(Missing(TickHookKey, TickHookWords[0]));
            return TickHookMode.Auto;
        }
        var word = extras.Strings.Contains(TickHookKey) && map.TryGetValue(TickHookKey, out var v)
            ? v.ToLowerInvariant() : "";
        if (string.Equals(word, TickHookWords[1], StringComparison.Ordinal)) return TickHookMode.On;
        if (string.Equals(word, TickHookWords[2], StringComparison.Ordinal)) return TickHookMode.Off;
        if (string.Equals(word, TickHookWords[0], StringComparison.Ordinal)) return TickHookMode.Auto;
        notes.Add(new SettingNote(TickHookKey, NoteWord,
                                  $"{TickHookKey} は {string.Join(" / ", TickHookWords)} の"
                                  + $" 3 語だけです。{TickHookWords[0]} にしました"));
        return TickHookMode.Auto;
    }

    private static Dictionary<string, bool> Scopes(Paths.ConfigExtras extras,
                                                   List<SettingNote> notes)
    {
        if (!extras.Keys.Contains(HitWindowScopesKey))
        {
            notes.Add(Missing(HitWindowScopesKey, "全部取る"));
            return CaptureScope.Normalize(null);
        }
        if (!extras.Objects.TryGetValue(HitWindowScopesKey, out var raw))
        {
            notes.Add(new SettingNote(HitWindowScopesKey, NoteType,
                                      $"{HitWindowScopesKey} が"
                                      + " {\"net\":true,…} の形ではないので全部取ります"));
            return CaptureScope.Normalize(null);
        }
        foreach (var s in CaptureScope.All)
        {
            if (!raw.ContainsKey(s))
                notes.Add(new SettingNote(HitWindowScopesKey + "." + s, NoteMissing,
                                          $"{CaptureScope.Label(s)} は書かれていないので取ります"));
        }
        foreach (var name in raw.Keys)
        {
            if (!CaptureScope.All.Contains(name, StringComparer.Ordinal))
                notes.Add(new SettingNote(HitWindowScopesKey + "." + name, NoteUnknown,
                                          $"{name} は使っていない語なので読み飛ばしました"));
        }
        return CaptureScope.Normalize(raw);
    }

    private const string HideFallback = "何も隠さない";

    private const string OrderFallback = "もとの並び";

    private const string OwnNameFallback = "自分の名前は判定しない";

    private static string[] NameList(string key, string fallback, Paths.ConfigExtras extras,
                                     List<SettingNote> notes)
    {
        if (!extras.Keys.Contains(key))
        {
            notes.Add(Missing(key, fallback));
            return [];
        }
        if (!extras.Lists.TryGetValue(key, out var items))
        {
            notes.Add(new SettingNote(key, NoteType,
                                      $"{key} が字の並びではないので"
                                      + fallback + "にしました"));
            return [];
        }
        if (extras.Strings.Contains(key))
            notes.Add(new SettingNote(key, NoteType,
                                      $"{key} が字 1 つだったので"
                                      + "1 件の並びとして読みました"));
        return items;
    }

    private static StatsOrderMap OrderMap(string path, string key, string fallback,
                                          List<SettingNote> notes)
    {
        JsonDocument doc;
        try { doc = JsonDocument.Parse(File.ReadAllBytes(path)); }
        catch (Exception) { return StatsOrderMap.Empty; }
        using (doc)
        {
            if (doc.RootElement.ValueKind != JsonValueKind.Object
                || !doc.RootElement.TryGetProperty(key, out var obj))
            {
                notes.Add(Missing(key, fallback));
                return StatsOrderMap.Empty;
            }
            if (obj.ValueKind != JsonValueKind.Object)
            {
                notes.Add(new SettingNote(key, NoteType,
                                          $"{key} が {{…}} の形ではないので{fallback}にしました"));
                return StatsOrderMap.Empty;
            }
            var pages = new List<StatsOrderPage>();
            foreach (var page in obj.EnumerateObject())
            {
                if (page.Value.ValueKind != JsonValueKind.Object)
                {
                    notes.Add(new SettingNote(key + "." + page.Name, NoteType,
                        $"{key}.{page.Name} が {{…}} の形ではないので{fallback}にしました"));
                    continue;
                }
                var sections = new List<StatsOrderSection>();
                foreach (var section in page.Value.EnumerateObject())
                {
                    if (section.Value.ValueKind != JsonValueKind.Array)
                    {
                        notes.Add(new SettingNote(key + "." + page.Name + "." + section.Name, NoteType,
                            $"{key}.{page.Name}.{section.Name} が字の並びではないので"
                            + fallback + "にしました"));
                        continue;
                    }
                    var items = new List<string>();
                    foreach (var item in section.Value.EnumerateArray())
                    {
                        if (item.ValueKind == JsonValueKind.String) items.Add(item.GetString()!);
                    }
                    sections.Add(new StatsOrderSection(section.Name, items));
                }
                pages.Add(new StatsOrderPage(page.Name, sections));
            }
            return new StatsOrderMap(pages);
        }
    }

    private static StreamModeSettings StreamMode(string path, List<SettingNote> notes)
    {
        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(File.ReadAllBytes(path));
        }
        catch (Exception)
        {
            return StreamModeSettings.Default;
        }
        using (doc)
        {
            if (doc.RootElement.ValueKind != JsonValueKind.Object
                || !doc.RootElement.TryGetProperty(StreamModeKey, out var obj))
            {
                notes.Add(new SettingNote(StreamModeKey, NoteMissing,
                                          $"{StreamModeKey} は書かれていないので既定を使います"));
                return StreamModeSettings.Default;
            }
            if (obj.ValueKind != JsonValueKind.Object)
            {
                notes.Add(new SettingNote(StreamModeKey, NoteType,
                                          $"{StreamModeKey} が {{…}} の形ではないので既定を使います"));
                return StreamModeSettings.Default;
            }
            var d = StreamModeSettings.Default;
            return new StreamModeSettings(
                Width: StreamModeInt(obj, "width", d.Width, notes),
                Height: StreamModeInt(obj, "height", d.Height, notes),
                FontScale: StreamModeDouble(obj, "font_scale", d.FontScale, notes),
                Background: StreamModeString(obj, "bg", d.Background, notes),
                Topmost: StreamModeBool(obj, "topmost", d.Topmost, notes),
                Borderless: StreamModeBool(obj, "borderless", d.Borderless, notes),
                Content: StreamModeString(obj, "content", d.Content, notes));
        }
    }

    private static int StreamModeInt(JsonElement obj, string name, int dflt, List<SettingNote> notes)
    {
        var key = StreamModeKey + "." + name;
        if (!obj.TryGetProperty(name, out var v))
        {
            notes.Add(Missing(key, dflt.ToString(Inv)));
            return dflt;
        }
        if (v.ValueKind != JsonValueKind.Number || !v.TryGetDouble(out var d))
        {
            notes.Add(new SettingNote(key, NoteType, $"{key} が数ではないので {dflt.ToString(Inv)} にしました"));
            return dflt;
        }
        return (int)Math.Truncate(d);
    }

    private static double StreamModeDouble(JsonElement obj, string name, double dflt, List<SettingNote> notes)
    {
        var key = StreamModeKey + "." + name;
        if (!obj.TryGetProperty(name, out var v))
        {
            notes.Add(Missing(key, dflt.ToString(Inv)));
            return dflt;
        }
        if (v.ValueKind != JsonValueKind.Number || !v.TryGetDouble(out var d))
        {
            notes.Add(new SettingNote(key, NoteType, $"{key} が数ではないので {dflt.ToString(Inv)} にしました"));
            return dflt;
        }
        return d;
    }

    private static string StreamModeString(JsonElement obj, string name, string dflt, List<SettingNote> notes)
    {
        var key = StreamModeKey + "." + name;
        if (!obj.TryGetProperty(name, out var v))
        {
            notes.Add(Missing(key, dflt));
            return dflt;
        }
        if (v.ValueKind != JsonValueKind.String)
        {
            notes.Add(new SettingNote(key, NoteType, $"{key} が字ではないので {dflt} にしました"));
            return dflt;
        }
        return v.GetString() ?? dflt;
    }

    private static bool StreamModeBool(JsonElement obj, string name, bool dflt, List<SettingNote> notes)
    {
        var key = StreamModeKey + "." + name;
        if (!obj.TryGetProperty(name, out var v))
        {
            notes.Add(Missing(key, dflt ? "ON" : "OFF"));
            return dflt;
        }
        if (v.ValueKind != JsonValueKind.True && v.ValueKind != JsonValueKind.False)
        {
            notes.Add(new SettingNote(key, NoteType,
                                      $"{key} が真偽ではないので {(dflt ? "ON" : "OFF")} にしました"));
            return dflt;
        }
        return v.ValueKind == JsonValueKind.True;
    }

    private static SettingNote Missing(string key, string used) =>
        new(key, NoteMissing, $"{key} は書かれていないので既定（{used}）を使います");


    public static SaveOutcome Save(string path, AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        byte[]? originalBytes = null;
        var existed = SafeExists(path);
        if (existed)
        {
            try { originalBytes = File.ReadAllBytes(path); }
            catch (Exception exc)
            {
                return new SaveOutcome(false, false, "設定を読めないので書きません: " + exc.Message);
            }
        }

        var bom = originalBytes is { Length: >= 3 }
                  && originalBytes[0] == 0xEF && originalBytes[1] == 0xBB && originalBytes[2] == 0xBF;
        var original = originalBytes is null
            ? null
            : new UTF8Encoding(false).GetString(originalBytes, bom ? 3 : 0,
                                                originalBytes.Length - (bom ? 3 : 0));
        var crlf = original is null || original.Contains("\r\n", StringComparison.Ordinal);
        var trailing = original is not null
                       && original.EndsWith('\n');

        JsonDocument? doc = null;
        try
        {
            if (original is not null)
            {
                doc = JsonDocument.Parse(original);
                if (doc.RootElement.ValueKind != JsonValueKind.Object)
                {
                    return new SaveOutcome(false, false,
                        "★設定が { … } の形ではないので書きません（既定で上書きしません）: " + path);
                }
            }
            var text = Compose(doc, settings);
            text = text.Replace("\r\n", "\n", StringComparison.Ordinal);
            if (crlf) text = text.Replace("\n", "\r\n", StringComparison.Ordinal);
            if (trailing) text += crlf ? "\r\n" : "\n";
            var bytes = new UTF8Encoding(false).GetBytes(text);
            if (bom) bytes = [0xEF, 0xBB, 0xBF, .. bytes];
            return Place(path, bytes, existed);
        }
        catch (JsonException exc)
        {
            return new SaveOutcome(false, false,
                "★設定を読めない（JSON が壊れています）ので書きません: " + exc.Message);
        }
        finally
        {
            doc?.Dispose();
        }
    }

    private static string Compose(JsonDocument? doc, AppSettings settings)
    {
        var scopes = CaptureScope.Normalize(settings.HitWindowScopes);
        using var buffer = new MemoryStream();
        var options = new JsonWriterOptions
        {
            Indented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        };
        using (var w = new Utf8JsonWriter(buffer, options))
        {
            w.WriteStartObject();
            var written = new HashSet<string>(StringComparer.Ordinal);
            if (doc is not null)
            {
                foreach (var prop in doc.RootElement.EnumerateObject())
                {
                    if (Array.IndexOf(OwnedKeys, prop.Name) >= 0)
                    {
                        if (written.Add(prop.Name)) WriteOwned(w, prop.Name, settings, scopes);
                        continue;
                    }
                    w.WritePropertyName(prop.Name);
                    prop.Value.WriteTo(w);
                }
            }
            foreach (var key in OwnedKeys)
            {
                if (written.Add(key)) WriteOwned(w, key, settings, scopes);
            }
            w.WriteEndObject();
        }
        return Encoding.UTF8.GetString(buffer.ToArray());
    }

    private static void WriteOwned(Utf8JsonWriter w, string key, AppSettings s,
                                   Dictionary<string, bool> scopes)
    {
        switch (key)
        {
            case Paths.RecordReplayPlaybackKey:
                w.WriteBoolean(key, s.RecordReplayPlayback); break;
            case RecordReplayToggleKeyKey:
                w.WriteString(key, s.RecordReplayToggleKey); break;
            case HistoryRetentionAbortedKey:
                w.WriteNumber(key, s.HistoryRetentionAborted); break;
            case HistoryRetentionCompletedKey:
                w.WriteNumber(key, s.HistoryRetentionCompleted); break;
            case TickHookKey:
                w.WriteString(key, Word(s.TickHook)); break;
            case HitWindowsKey:
                w.WriteBoolean(key, s.HitWindows); break;
            case HitWindowBeforeKey:
                w.WriteNumber(key, s.HitWindowBefore); break;
            case HitWindowAfterKey:
                w.WriteNumber(key, s.HitWindowAfter); break;
            case HitWindowScopesKey:
                w.WriteStartObject(key);
                foreach (var name in CaptureScope.All) w.WriteBoolean(name, scopes[name]);
                w.WriteEndObject();
                break;
            case HitWindowQuickKey:
                w.WriteBoolean(key, s.HitWindowQuick); break;
            case AutoMonitorOnGameKey:
                w.WriteBoolean(key, s.AutoMonitorOnGame); break;
            case WatchReplaysWithMonitorKey:
                w.WriteBoolean(key, s.WatchReplaysWithMonitor); break;
            case BackupKeepOneKey:
                w.WriteBoolean(key, s.BackupKeepOne); break;
            case StatsHiddenItemsKey:
                w.WriteStartArray(key);
                foreach (var item in s.StatsHiddenItems) w.WriteStringValue(item);
                w.WriteEndArray();
                break;
            case StatsColumnOrderKey:
                WriteOrderMap(w, key, s.StatsColumnOrder); break;
            case StatsHiddenColumnsKey:
                WriteOrderMap(w, key, s.StatsHiddenColumns); break;
            case StreamPanelHiddenItemsKey:
                w.WriteStartArray(key);
                foreach (var item in s.StreamPanelHiddenItems) w.WriteStringValue(item);
                w.WriteEndArray();
                break;
            case PlayTabHiddenItemsKey:
                w.WriteStartArray(key);
                foreach (var item in s.PlayTabHiddenItems) w.WriteStringValue(item);
                w.WriteEndArray();
                break;
            case StreamModeKey:
                w.WriteStartObject(key);
                w.WriteNumber("width", s.StreamMode.Width);
                w.WriteNumber("height", s.StreamMode.Height);
                w.WriteNumber("font_scale", s.StreamMode.FontScale);
                w.WriteString("bg", s.StreamMode.Background);
                w.WriteBoolean("topmost", s.StreamMode.Topmost);
                w.WriteBoolean("borderless", s.StreamMode.Borderless);
                w.WriteString("content", s.StreamMode.Content);
                w.WriteEndObject();
                break;
            case StreamTargets.Key:
                StreamTargets.Write(w, key, StreamTargets.Normalize(s.StreamTargets));
                break;
            case Paths.OwnPlayerNamesKey:
                w.WriteStartArray(key);
                foreach (var item in s.OwnPlayerNames) w.WriteStringValue(item);
                w.WriteEndArray();
                break;
            default:
                throw new InvalidOperationException("書き方を決めていない鍵: " + key);
        }
    }

    private static void WriteOrderMap(Utf8JsonWriter w, string key, StatsOrderMap map)
    {
        w.WriteStartObject(key);
        foreach (var page in map.Pages)
        {
            var any = false;
            foreach (var section in page.Sections)
            {
                if (section.Items.Count == 0) continue;
                if (!any) { w.WriteStartObject(page.Name); any = true; }
                w.WriteStartArray(section.Name);
                foreach (var item in section.Items) w.WriteStringValue(item);
                w.WriteEndArray();
            }
            if (any) w.WriteEndObject();
        }
        w.WriteEndObject();
    }

    private static SaveOutcome Place(string path, byte[] bytes, bool existed)
    {
        var tmp = path + ".tmp";
        try
        {
            File.WriteAllBytes(tmp, bytes);
            File.Move(tmp, path, overwrite: true);
            return new SaveOutcome(true, !existed, null);
        }
        catch (Exception exc)
        {
            try { if (File.Exists(tmp)) File.Delete(tmp); }
            catch (Exception) { }
            return new SaveOutcome(false, false, "書けませんでした: " + exc.Message);
        }
    }
}

public sealed record StreamTargetEntry(long Mode, long Difficulty, long Character, long? Stage, long? Round,
                                       long? Target, long? Wr, long? ClearBonus = null);

public sealed record StreamTargetsResult(IReadOnlyList<StreamTargetEntry> Entries,
                                         IReadOnlyList<SettingNote> Notes);

public sealed record StreamTargetLookupValue(long Value, long? UsedRound, bool Fallback);

public sealed record StreamTargetLookupResult(StreamTargetLookupValue? Target, StreamTargetLookupValue? Wr);

public static class StreamTargets
{
    public const string Key = "stream_targets";

    public static StreamTargetsResult Load(string path)
    {
        var notes = new List<SettingNote>();
        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(File.ReadAllBytes(path));
        }
        catch (Exception)
        {
            notes.Add(new SettingNote(Key, ConfigStore.NoteUnreadable,
                                      $"設定を読めないので Target / WR は 0 件として扱います: {path}"));
            return new StreamTargetsResult([], notes);
        }
        using (doc)
        {
            if (doc.RootElement.ValueKind != JsonValueKind.Object
                || !doc.RootElement.TryGetProperty(Key, out var arr))
            {
                notes.Add(new SettingNote(Key, ConfigStore.NoteMissing,
                                          $"{Key} は書かれていないので Target / WR は 0 件です"));
                return new StreamTargetsResult([], notes);
            }
            if (arr.ValueKind != JsonValueKind.Array)
            {
                notes.Add(new SettingNote(Key, ConfigStore.NoteType,
                                          $"{Key} が配列ではないので読み飛ばしました"));
                return new StreamTargetsResult([], notes);
            }

            var entries = new List<StreamTargetEntry>();
            var i = 0;
            foreach (var item in arr.EnumerateArray())
            {
                var label = $"{Key}[{i}]";
                i++;
                if (item.ValueKind != JsonValueKind.Object)
                {
                    notes.Add(new SettingNote(label, ConfigStore.NoteType,
                                              $"{label} が {{…}} の形ではないので読み飛ばしました"));
                    continue;
                }
                if (!TryLong(item, ModeField, out var mode) || !TryLong(item, DifficultyField, out var diff)
                    || !TryLong(item, CharacterField, out var ch))
                {
                    notes.Add(new SettingNote(label, ConfigStore.NoteType,
                                              $"{label} は {ModeField} / {DifficultyField} /"
                                              + $" {CharacterField} が要ります。読み飛ばしました"));
                    continue;
                }
                var stage = TryLong(item, StageField, out var st) ? st : (long?)null;
                var round = TryLong(item, RoundField, out var rd) ? rd : (long?)null;
                var target = ReadValue(item, TargetField, mode, label, notes);
                var wr = ReadValue(item, WrField, mode, label, notes);
                var cb = ReadClearBonus(item, label, mode, round, notes);
                entries.Add(new StreamTargetEntry(mode, diff, ch, stage, round, target, wr, cb));
            }
            return new StreamTargetsResult(entries, notes);
        }
    }

    public static StreamTargetLookupResult Lookup(IReadOnlyList<StreamTargetEntry> entries,
                                                  long mode, long difficulty, long character,
                                                  long? stage, long? round)
    {
        ArgumentNullException.ThrowIfNull(entries);
        var byRound = new Dictionary<long, (long? Target, long? Wr)>();
        foreach (var e in entries)
        {
            if (e.Mode != mode || e.Difficulty != difficulty || e.Character != character || e.Stage != stage)
                continue;
            byRound[e.Round ?? WholeStageRound] = (e.Target, e.Wr);
        }
        return new StreamTargetLookupResult(
            Resolve(byRound, round, static v => v.Target),
            Resolve(byRound, round, static v => v.Wr));
    }

    public static long? ClearBonusOf(IReadOnlyList<StreamTargetEntry> entries,
                                     long mode, long difficulty, long character, long stage)
    {
        ArgumentNullException.ThrowIfNull(entries);
        long? found = null;
        foreach (var e in entries)
        {
            if (e.Mode != mode || e.Difficulty != difficulty || e.Character != character
                || e.Stage != stage || e.Round is not null)
                continue;
            if (e.ClearBonus is long cb) found = cb;
        }
        return found;
    }

    private const long WholeStageRound = 0;

    private static StreamTargetLookupValue? Resolve(
        Dictionary<long, (long? Target, long? Wr)> byRound, long? round,
        Func<(long? Target, long? Wr), long?> pick)
    {
        if (round is long r0 && byRound.TryGetValue(r0, out var here) && pick(here) is long v0)
            return new StreamTargetLookupValue(v0, r0, false);

        if (round is long r1)
        {
            for (var back = r1 - 1; back >= 1; back--)
            {
                if (byRound.TryGetValue(back, out var e) && pick(e) is long v)
                    return new StreamTargetLookupValue(v, back, true);
            }
        }

        if (byRound.TryGetValue(WholeStageRound, out var whole) && pick(whole) is long v2)
            return new StreamTargetLookupValue(v2, null, round is not null);

        return null;
    }

    private static bool TryLong(JsonElement obj, string name, out long value)
    {
        value = 0;
        if (!obj.TryGetProperty(name, out var v) || v.ValueKind != JsonValueKind.Number
            || !v.TryGetDouble(out var d))
            return false;
        value = (long)Math.Truncate(d);
        return true;
    }

    private static long? ReadValue(JsonElement obj, string field, long mode, string label,
                                   List<SettingNote> notes)
    {
        if (!obj.TryGetProperty(field, out var v) || v.ValueKind == JsonValueKind.Null) return null;
        if (v.ValueKind != JsonValueKind.Number || !v.TryGetDouble(out var d))
        {
            notes.Add(new SettingNote($"{label}.{field}", ConfigStore.NoteType,
                                      $"{label}.{field} が数ではないので読み飛ばしました"));
            return null;
        }
        return mode == 2
            ? (long)Math.Round(d * 60.0, MidpointRounding.AwayFromZero)
            : (long)Math.Truncate(d);
    }

    private static long? ReadClearBonus(JsonElement obj, string label, long mode, long? round,
                                        List<SettingNote> notes)
    {
        if (!obj.TryGetProperty(ClearBonusField, out var v) || v.ValueKind == JsonValueKind.Null)
            return null;
        if (mode == 2)
        {
            notes.Add(new SettingNote($"{label}.{ClearBonusField}", ConfigStore.NoteUnknown,
                                      $"{label}.{ClearBonusField} は Match には無いので読み飛ばしました"));
            return null;
        }
        if (round is not null)
        {
            notes.Add(new SettingNote($"{label}.{ClearBonusField}", ConfigStore.NoteUnknown,
                                      $"{label}.{ClearBonusField} は面ぜんたいの行"
                                      + "（round を書かない行）だけなので読み飛ばしました"));
            return null;
        }
        if (v.ValueKind != JsonValueKind.Number || !v.TryGetDouble(out var d))
        {
            notes.Add(new SettingNote($"{label}.{ClearBonusField}", ConfigStore.NoteType,
                                      $"{label}.{ClearBonusField} が数ではないので読み飛ばしました"));
            return null;
        }
        return (long)Math.Truncate(d);
    }


    public const string ModeField = "mode";

    public const string DifficultyField = "difficulty";

    public const string CharacterField = "character";

    public const string StageField = "stage";

    public const string RoundField = "round";

    public const string TargetField = "target";

    public const string WrField = "wr";

    public const string ClearBonusField = "clear_bonus";

    public const long MatchFramesPerSecond = 60;

    public static IReadOnlyList<StreamTargetEntry> Normalize(IEnumerable<StreamTargetEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        var byKey = new Dictionary<(long, long, long, long, long), StreamTargetEntry>();
        var order = new List<(long, long, long, long, long)>();
        foreach (var e in entries)
        {
            var key = (e.Mode, e.Difficulty, e.Character, e.Stage ?? WholeStageRound,
                       e.Round ?? WholeStageRound);
            if (!byKey.ContainsKey(key)) order.Add(key);
            byKey[key] = e;
        }
        var kept = new List<StreamTargetEntry>();
        foreach (var key in order)
        {
            var e = byKey[key];
            if (e.Target is null && e.Wr is null && e.ClearBonus is null) continue;
            kept.Add(e);
        }
        kept.Sort(static (a, b) =>
        {
            var c = a.Mode.CompareTo(b.Mode);
            if (c != 0) return c;
            c = a.Difficulty.CompareTo(b.Difficulty);
            if (c != 0) return c;
            c = a.Character.CompareTo(b.Character);
            if (c != 0) return c;
            c = (a.Stage ?? WholeStageRound).CompareTo(b.Stage ?? WholeStageRound);
            if (c != 0) return c;
            return (a.Round ?? WholeStageRound).CompareTo(b.Round ?? WholeStageRound);
        });
        return kept;
    }

    internal static void Write(Utf8JsonWriter w, string key, IReadOnlyList<StreamTargetEntry> entries)
    {
        w.WriteStartArray(key);
        foreach (var e in entries)
        {
            w.WriteStartObject();
            w.WriteNumber(ModeField, e.Mode);
            w.WriteNumber(DifficultyField, e.Difficulty);
            w.WriteNumber(CharacterField, e.Character);
            if (e.Stage is long st) w.WriteNumber(StageField, st);
            if (e.Round is long rd) w.WriteNumber(RoundField, rd);
            WriteValue(w, TargetField, e.Mode, e.Target);
            WriteValue(w, WrField, e.Mode, e.Wr);
            if (e.ClearBonus is long cb) w.WriteNumber(ClearBonusField, cb);
            w.WriteEndObject();
        }
        w.WriteEndArray();
    }

    private static void WriteValue(Utf8JsonWriter w, string field, long mode, long? value)
    {
        if (value is not long v) return;
        if (mode != 2) { w.WriteNumber(field, v); return; }
        if (v % MatchFramesPerSecond == 0) w.WriteNumber(field, v / MatchFramesPerSecond);
        else w.WriteNumber(field, v / (double)MatchFramesPerSecond);
    }
}
