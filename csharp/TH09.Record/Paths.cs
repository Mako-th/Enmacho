using System.Globalization;
using System.Text;
using System.Text.Json;

namespace TH09.Record;

public enum PathMode
{
    Portable,

    Repository,
}

public enum PathSeverity
{
    Ok,

    Warn,

    Fatal,
}

public sealed record PathProbe(string Directory, bool? Writable, string? Reason,
                               string? VirtualStoreShadow, string? Probed = null);

public sealed record PathReport(PathSeverity Severity, IReadOnlyList<PathProbe> Probes,
                                IReadOnlyList<string> Lines);

public sealed record OwnNameSettings(string? PlayerName, IReadOnlyList<string>? Names,
                                     bool? IgnoreCase, bool? Partial);

public sealed class Paths
{
    private static readonly string[] RepositoryMarkers = ["python", "project_material_documents"];

    public const string MainDbDefault = "th09_tracker.sqlite3";

    public const string Layer0DbDefault = @"data\layer0\th09_ticks.db";

    public const string ReplayPathsFileName = "replay_paths.txt";

    public const string NativeDirName = "native";

    public const string GameReplayDirName = "replay";

    public const string GameDirFileName = "game_dir.txt";

    public const string AdonisWatchDirName = "ReplayAutoSaveTest";

    public const string OwnPlayerNamesKey = "own_player_names";

    public const string OwnNameLegacyKey = "player_name";

    public const string OwnNameIgnoreCaseKey = "own_name_ignore_case";

    public const string OwnNamePartialMatchKey = "own_name_partial_match";

    public const string TickHookAttachDelayKey = "tick_hook_attach_delay_sec";

    public const double TickHookAttachDelaySecDefault = 2.0;

    public const string RecordReplayPlaybackKey = "record_replay_playback";

    public const bool RecordReplayPlaybackDefault = true;

    public const string RecordTitleDemoKey = "record_title_demo";

    public const bool RecordTitleDemoDefault = false;

    public const string ReplayScanSlotKey = "replay_scan_slot";

    public const int ReplayScanSlotDefault = 25;

    private static readonly string[] BackupDirSegments = ["data", "backup"];

    private static readonly string[] LogsDirSegments = ["data", "logs"];

    private static readonly string[] VirtualStoreReplaySegments =
        ["VirtualStore", "Program Files (x86)", "上海アリス幻樂団", "東方花映塚", GameReplayDirName];

    public static Paths Default { get; } = Resolve(AppContext.BaseDirectory);

    public string ExeDir { get; }

    public PathMode Mode { get; }

    public string DataRoot { get; }

    public string ConfigPath { get; }

    public bool ConfigLoaded { get; }

    public string MainDb { get; }

    public string Layer0Db { get; }

    public string ReplayPathsFile { get; }

    public string GameDirFile { get; }

    public string BackupRoot { get; }

    public string LogsDir { get; }

    public string NativeDir { get; }

    public bool MainDbFromConfig { get; }

    public bool Layer0DbFromConfig { get; }

    public double TickHookAttachDelaySec { get; }

    public bool TickHookAttachDelayFromConfig { get; }

    public int ReplayScanSlot { get; }

    public bool ReplayScanSlotFromConfig { get; }

    public OwnNameSettings OwnNames { get; }

    public bool ReadRecordReplayPlayback() =>
        ReadTruthyNow(RecordReplayPlaybackKey, RecordReplayPlaybackDefault);

    public bool ReadRecordTitleDemo() => ReadTruthyNow(RecordTitleDemoKey, RecordTitleDemoDefault);

    private bool ReadTruthyNow(string key, bool fallback)
    {
        ReadConfig(ConfigPath, out _, out var extras);
        return extras.Truthy.TryGetValue(key, out var value) ? value : fallback;
    }

    private Paths(string exeDir, PathMode mode, string dataRoot, string configPath,
                  bool configLoaded, string mainDb, bool mainFromConfig,
                  string layer0Db, bool layer0FromConfig,
                  double attachDelaySec, bool attachDelayFromConfig,
                  int scanSlot, bool scanSlotFromConfig, OwnNameSettings ownNames)
    {
        OwnNames = ownNames;
        ExeDir = exeDir;
        Mode = mode;
        DataRoot = dataRoot;
        ConfigPath = configPath;
        ConfigLoaded = configLoaded;
        MainDb = mainDb;
        MainDbFromConfig = mainFromConfig;
        Layer0Db = layer0Db;
        Layer0DbFromConfig = layer0FromConfig;
        ReplayPathsFile = Path.Combine(dataRoot, ReplayPathsFileName);
        GameDirFile = Path.Combine(dataRoot, GameDirFileName);
        NativeDir = Path.Combine(dataRoot, NativeDirName);
        BackupRoot = Path.Combine([dataRoot, .. BackupDirSegments]);
        LogsDir = Path.Combine([dataRoot, .. LogsDirSegments]);
        TickHookAttachDelaySec = attachDelaySec;
        TickHookAttachDelayFromConfig = attachDelayFromConfig;
        ReplayScanSlot = scanSlot;
        ReplayScanSlotFromConfig = scanSlotFromConfig;
    }

    public static Paths Resolve(string exeDir)
    {
        exeDir = Path.GetFullPath(exeDir).TrimEnd(Path.DirectorySeparatorChar);
        var repo = FindRepositoryRoot(exeDir);
        var mode = repo is null ? PathMode.Portable : PathMode.Repository;
        var dataRoot = repo ?? exeDir;

        var configPath = mode == PathMode.Repository
            ? Path.Combine(dataRoot, "python", "config.json")
            : Path.Combine(exeDir, "config.json");
        var config = ReadConfig(configPath, out var loaded, out var extras);

        var database = Value(config, "database");
        var layer0 = Value(config, "layer0_db_path");
        var delayRaw = Value(config, TickHookAttachDelayKey);
        double delay = TickHookAttachDelaySecDefault;
        var delayOk = delayRaw is not null
                      && double.TryParse(delayRaw, NumberStyles.Float, CultureInfo.InvariantCulture,
                                         out delay);
        if (!delayOk) delay = TickHookAttachDelaySecDefault;
        var slotRaw = Value(config, ReplayScanSlotKey);
        int slot = ReplayScanSlotDefault;
        var slotOk = slotRaw is not null
                     && int.TryParse(slotRaw, NumberStyles.Integer, CultureInfo.InvariantCulture,
                                     out slot);
        if (!slotOk) slot = ReplayScanSlotDefault;
        var own = new OwnNameSettings(
            PlayerName: extras.Strings.Contains(OwnNameLegacyKey) ? Value(config, OwnNameLegacyKey) : null,
            Names: extras.Lists.TryGetValue(OwnPlayerNamesKey, out var ownList) ? ownList : null,
            IgnoreCase: extras.Truthy.TryGetValue(OwnNameIgnoreCaseKey, out var ic) ? ic : null,
            Partial: extras.Truthy.TryGetValue(OwnNamePartialMatchKey, out var pt) ? pt : null);
        return new Paths(
            exeDir, mode, dataRoot, configPath, loaded,
            ResolveAgainst(dataRoot, database, MainDbDefault), database is not null,
            ResolveAgainst(dataRoot, layer0, Layer0DbDefault), layer0 is not null,
            delay, delayOk, slot, slotOk, own);
    }

    private static string? FindRepositoryRoot(string start)
    {
        var dir = new DirectoryInfo(start);
        while (dir is not null)
        {
            var all = true;
            foreach (var marker in RepositoryMarkers)
            {
                if (!Directory.Exists(Path.Combine(dir.FullName, marker))) { all = false; break; }
            }
            if (all) return dir.FullName.TrimEnd(Path.DirectorySeparatorChar);
            dir = dir.Parent;
        }
        return null;
    }

    internal static Dictionary<string, string> ReadConfig(string path, out bool loaded,
                                                          out ConfigExtras extras)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        extras = new ConfigExtras(
            new Dictionary<string, string[]>(StringComparer.Ordinal),
            new Dictionary<string, bool>(StringComparer.Ordinal),
            new HashSet<string>(StringComparer.Ordinal),
            new Dictionary<string, Dictionary<string, bool>>(StringComparer.Ordinal),
            new HashSet<string>(StringComparer.Ordinal));
        loaded = false;
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllBytes(path));
            if (doc.RootElement.ValueKind != JsonValueKind.Object) return map;
            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                if (prop.Value.ValueKind == JsonValueKind.String)
                {
                    map[prop.Name] = prop.Value.GetString()!;
                    extras.Strings.Add(prop.Name);
                    extras.Lists[prop.Name] = [prop.Value.GetString()!];
                }
                else if (prop.Value.ValueKind == JsonValueKind.Number)
                {
                    map[prop.Name] = prop.Value.GetRawText();
                }
                else if (prop.Value.ValueKind == JsonValueKind.Array)
                {
                    var items = new List<string>();
                    foreach (var item in prop.Value.EnumerateArray())
                    {
                        if (item.ValueKind == JsonValueKind.String) items.Add(item.GetString()!);
                    }
                    extras.Lists[prop.Name] = [.. items];
                }
                else if (prop.Value.ValueKind == JsonValueKind.Object)
                {
                    var inner = new Dictionary<string, bool>(StringComparer.Ordinal);
                    foreach (var child in prop.Value.EnumerateObject())
                        inner[child.Name] = Truthy(child.Value);
                    extras.Objects[prop.Name] = inner;
                }
                extras.Keys.Add(prop.Name);
                if (prop.Value.ValueKind != JsonValueKind.Undefined)
                    extras.Truthy[prop.Name] = Truthy(prop.Value);
            }
            loaded = true;
        }
        catch (Exception)
        {
        }
        return map;
    }

    private static bool Truthy(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        JsonValueKind.Null => false,
        JsonValueKind.String => value.GetString()!.Length > 0,
        JsonValueKind.Number => value.TryGetDouble(out var d) ? d != 0.0 : true,
        JsonValueKind.Array => value.GetArrayLength() > 0,
        JsonValueKind.Object => value.EnumerateObject().Any(),
        _ => false,
    };

    internal sealed record ConfigExtras(Dictionary<string, string[]> Lists,
                                        Dictionary<string, bool> Truthy,
                                        HashSet<string> Strings,
                                        Dictionary<string, Dictionary<string, bool>> Objects,
                                        HashSet<string> Keys);

    private static string? Value(Dictionary<string, string> config, string key) =>
        config.TryGetValue(key, out var v) && !string.IsNullOrWhiteSpace(v) ? v : null;

    private static string ResolveAgainst(string root, string? value, string fallback)
    {
        var path = value ?? fallback;
        return Path.GetFullPath(Path.IsPathRooted(path) ? path : Path.Combine(root, path));
    }


    public IReadOnlyList<string> ReplayExtraDirs()
    {
        var outList = new List<string>();
        foreach (var raw in ReadReplayPathsLines())
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith('#')) continue;
            outList.Add(ExpandUser(Environment.ExpandEnvironmentVariables(line)));
        }
        return outList;
    }

    public IReadOnlyList<string> ReplayExtraDirsRaw()
    {
        var outList = new List<string>();
        foreach (var raw in ReadReplayPathsLines())
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith('#')) continue;
            outList.Add(line);
        }
        return outList;
    }

    private IReadOnlyList<string> ReadReplayPathsLines()
    {
        try
        {
            if (!File.Exists(ReplayPathsFile)) return [];
            return File.ReadAllLines(ReplayPathsFile, new UTF8Encoding(false));
        }
        catch (Exception)
        {
            return [];
        }
    }

    public bool TryWriteReplayExtraDirs(IReadOnlyList<string> lines)
    {
        var cleaned = lines.Select(l => l.Trim()).Where(l => l.Length > 0).ToList();
        var current = ReplayExtraDirsRaw();
        if (current.Count == cleaned.Count && current.SequenceEqual(cleaned, StringComparer.Ordinal))
            return false;
        var kept = new List<string>();
        foreach (var raw in ReadReplayPathsLines())
        {
            var t = raw.Trim();
            if (t.Length == 0 || t.StartsWith('#')) kept.Add(raw);
        }
        kept.AddRange(cleaned);
        var tmp = ReplayPathsFile + ".tmp";
        try
        {
            Directory.CreateDirectory(DataRoot);
            File.WriteAllLines(tmp, kept, new UTF8Encoding(false));
            File.Move(tmp, ReplayPathsFile, overwrite: true);
            return true;
        }
        catch (Exception)
        {
            try { if (File.Exists(tmp)) File.Delete(tmp); }
            catch (Exception) { }
            return false;
        }
    }

    public string? ReadGameDir()
    {
        try
        {
            if (!File.Exists(GameDirFile)) return null;
            var text = File.ReadAllText(GameDirFile, new UTF8Encoding(false)).Trim();
            return text.Length == 0 ? null : text;
        }
        catch (Exception)
        {
            return null;
        }
    }

    public bool TryWriteGameDir(string dir)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dir);
        if (string.Equals(ReadGameDir(), dir, StringComparison.OrdinalIgnoreCase)) return false;
        var tmp = GameDirFile + ".tmp";
        try
        {
            Directory.CreateDirectory(DataRoot);
            File.WriteAllText(tmp, dir, new UTF8Encoding(false));
            File.Move(tmp, GameDirFile, overwrite: true);
            return true;
        }
        catch (Exception)
        {
            try { if (File.Exists(tmp)) File.Delete(tmp); }
            catch (Exception) { }
            return false;
        }
    }

    public string? UserDataDir(string? virtualStoreRoot = null)
    {
        var gameDir = ReadGameDir();
        return gameDir is null ? null : UserDataDirFor(gameDir, virtualStoreRoot);
    }

    public static string? UserDataDirFor(string gameDir, string? virtualStoreRoot = null)
    {
        var shadow = FindVirtualStoreShadow(gameDir, virtualStoreRoot);
        return shadow is not null && SafeDirectoryExists(shadow) ? shadow : null;
    }

    public string? GameReplayDir(string? currentDirectory = null, string? virtualStoreRoot = null)
    {
        var gameDir = ReadGameDir();
        if (gameDir is not null)
        {
            if (UserDataDir(virtualStoreRoot) is { } userData)
            {
                var userReplay = Path.Combine(userData, GameReplayDirName);
                if (SafeDirectoryExists(userReplay)) return userReplay;
            }
            return Path.Combine(gameDir, GameReplayDirName);
        }

        var candidates = new List<string>();
        var local = Environment.GetEnvironmentVariable("LOCALAPPDATA");
        if (!string.IsNullOrEmpty(local))
        {
            var parts = new string[VirtualStoreReplaySegments.Length + 1];
            parts[0] = local;
            VirtualStoreReplaySegments.CopyTo(parts, 1);
            candidates.Add(Path.Combine(parts));
        }
        candidates.Add(Path.Combine(currentDirectory ?? Directory.GetCurrentDirectory(),
                                    GameReplayDirName));
        candidates.AddRange(ReplayExtraDirs());
        foreach (var c in candidates)
        {
            if (SafeDirectoryExists(c)) return c;
        }
        return null;
    }

    private static bool SafeDirectoryExists(string path)
    {
        try
        {
            return Directory.Exists(path);
        }
        catch (Exception)
        {
            return false;
        }
    }

    public IReadOnlyList<string> ReplayWatchDirs(string? currentDirectory = null,
                                                 string? virtualStoreRoot = null)
    {
        var cwd = currentDirectory ?? Directory.GetCurrentDirectory();
        var candidates = new List<string>();

        var gameDir = ReadGameDir();
        if (gameDir is not null)
        {
            candidates.Add(gameDir);
            if (UserDataDir(virtualStoreRoot) is { } userData) candidates.Add(userData);
        }
        candidates.AddRange(ReplayExtraDirs());

        candidates.Add(Path.Combine(cwd, GameReplayDirName));
        candidates.Add(Path.Combine(cwd, AdonisWatchDirName));
        var local = Environment.GetEnvironmentVariable("LOCALAPPDATA");
        if (!string.IsNullOrEmpty(local))
        {
            var fixedGameDir = Path.Combine([local, .. VirtualStoreReplaySegments[..^1]]);
            candidates.Add(Path.Combine(fixedGameDir, GameReplayDirName));
            candidates.Add(Path.Combine(fixedGameDir, AdonisWatchDirName));
        }

        var kept = new List<string>();
        foreach (var raw in candidates)
        {
            var path = PathlibString(raw);
            if (kept.Any(k => IsAncestorOrSame(k, path))) continue;
            kept.RemoveAll(k => IsAncestorOrSame(path, k));
            kept.Add(path);
        }
        return kept;
    }

    private static bool IsAncestorOrSame(string ancestor, string path)
    {
        if (string.Equals(ancestor, path, StringComparison.OrdinalIgnoreCase)) return true;
        var prefix = ancestor.TrimEnd('\\') + '\\';
        return path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
    }

    private static string PathlibString(string path)
    {
        var s = path.Replace('/', '\\');
        string drive = "", root = "";
        int rest = 0;
        if (s.Length > 2 && s[0] == '\\' && s[1] == '\\' && s[2] != '\\')
        {
            int server = s.IndexOf('\\', 2);
            int share = server < 0 ? -1 : s.IndexOf('\\', server + 1);
            if (server > 2 && server + 1 < s.Length && (share < 0 || share > server + 1))
            {
                rest = share < 0 ? s.Length : share;
                drive = s[..rest];
                root = "\\";
            }
        }
        if (drive.Length == 0)
        {
            if (s.Length >= 2 && s[1] == ':') { drive = s[..2]; rest = 2; }
            if (rest < s.Length && s[rest] == '\\') root = "\\";
        }
        var parts = new List<string>();
        foreach (var part in s[rest..].Split('\\'))
        {
            if (part.Length > 0 && part != ".") parts.Add(part);
        }
        var text = drive + root + string.Join('\\', parts);
        return text.Length == 0 ? "." : text;
    }

    public static IReadOnlyList<string> ReplayWatchDumpLines(IReadOnlyList<string> args)
    {
        string? root = null, cwd = null, vroot = null;
        for (int i = 0; i + 1 < args.Count; i++)
        {
            if (args[i] == "--root") root = args[i + 1];
            else if (args[i] == "--cwd") cwd = args[i + 1];
            else if (args[i] == "--virtual-store-root") vroot = args[i + 1];
        }
        var paths = root is null ? Default : Resolve(root);
        var dirs = paths.ReplayWatchDirs(cwd, vroot);
        var own = paths.OwnNames;
        string Opt(string? v) => v is null ? "-" : "=" + v;
        string Flag(bool? v) => v is null ? "-" : v.Value ? "1" : "0";
        var lines = new List<string>
        {
            "fact\tmode\t" + paths.Mode,
            "fact\tdata_root\t" + paths.DataRoot,
            "fact\tconfig_path\t" + paths.ConfigPath,
            "fact\tconfig_loaded\t" + (paths.ConfigLoaded ? "1" : "0"),
            "fact\treplay_paths_file\t" + paths.ReplayPathsFile,
            "fact\treplay_paths_exists\t" + (File.Exists(paths.ReplayPathsFile) ? "1" : "0"),
            "fact\tlocalappdata\t" + (Environment.GetEnvironmentVariable("LOCALAPPDATA") ?? ""),
            "fact\tcwd\t" + (cwd ?? Directory.GetCurrentDirectory()),
            "fact\textra\t" + paths.ReplayExtraDirs().Count.ToString(CultureInfo.InvariantCulture),
            "fact\tlogs_dir\t" + paths.LogsDir,
            "fact\tgame_dir\t" + (paths.ReadGameDir() ?? ""),
            "fact\tuser_data_dir\t" + (paths.UserDataDir(vroot) ?? ""),
            "fact\tgame_replay_dir\t" + (paths.GameReplayDir(cwd, vroot) ?? ""),
            "own\tplayer_name\t" + Opt(own.PlayerName),
            "own\tnames\t" + (own.Names is null ? "-"
                              : own.Names.Count.ToString(CultureInfo.InvariantCulture)),
            "own\tignore_case\t" + Flag(own.IgnoreCase),
            "own\tpartial\t" + Flag(own.Partial),
        };
        for (int i = 0; i < (own.Names?.Count ?? 0); i++)
            lines.Add("ownname\t" + i.ToString(CultureInfo.InvariantCulture) + "\t=" + own.Names![i]);
        for (int i = 0; i < dirs.Count; i++)
        {
            lines.Add("dir\t" + i.ToString(CultureInfo.InvariantCulture) + "\t" + dirs[i]
                      + "\t" + (Directory.Exists(dirs[i]) ? "1" : "0"));
        }
        lines.Add("end\t" + dirs.Count.ToString(CultureInfo.InvariantCulture));
        return lines;
    }

    private static string ExpandUser(string path)
    {
        if (path.Length == 0 || path[0] != '~') return path;
        if (path.Length > 1 && path[1] != '/' && path[1] != Path.DirectorySeparatorChar) return path;
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (home.Length == 0) return path;
        return path.Length == 1 ? home : Path.Combine(home, path[2..]);
    }


    private static IEnumerable<string> ProtectedRoots()
    {
        foreach (var v in new[] { "ProgramFiles", "ProgramFiles(x86)", "ProgramW6432", "SystemRoot" })
        {
            var p = Environment.GetEnvironmentVariable(v);
            if (!string.IsNullOrWhiteSpace(p)) yield return Path.GetFullPath(p);
        }
    }

    public static bool IsUnderProtectedRoot(string path)
    {
        var full = Path.GetFullPath(path);
        foreach (var root in ProtectedRoots())
        {
            if (full.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
                || string.Equals(full, root, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    public static string? FindVirtualStoreShadow(string path, string? virtualStoreRoot = null)
    {
        try
        {
            var root0 = virtualStoreRoot ?? DefaultVirtualStoreRoot();
            if (string.IsNullOrEmpty(root0)) return null;
            var full = Path.GetFullPath(path);
            var root = Path.GetPathRoot(full);
            if (string.IsNullOrEmpty(root)) return null;
            var shadow = Path.Combine(root0, full[root.Length..]);
            return File.Exists(shadow) || Directory.Exists(shadow) ? shadow : null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    public static string DefaultVirtualStoreRoot()
    {
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return string.IsNullOrEmpty(local) ? "" : Path.Combine(local, "VirtualStore");
    }

    public static PathProbe Probe(string directory, bool probe = true, string? virtualStoreRoot = null)
    {
        var shadow = FindVirtualStoreShadow(directory, virtualStoreRoot);
        if (!probe) return new PathProbe(directory, null, null, shadow);

        var probeDir = NearestExisting(directory, out var blocker);
        if (blocker is not null)
            return new PathProbe(directory, false,
                                 "同じ名前のファイルが途中にある: " + blocker, shadow, blocker);
        if (probeDir is null)
            return new PathProbe(directory, false, "存在するフォルダが 1 つも見つからない", shadow, null);

        var file = Path.Combine(probeDir, ".th09-write-probe-" + Guid.NewGuid().ToString("N"));
        try
        {
            File.WriteAllBytes(file, [0x54, 0x48, 0x30, 0x39]);
            return new PathProbe(directory, true, null, shadow, probeDir);
        }
        catch (Exception e)
        {
            return new PathProbe(directory, false, e.GetType().Name + ": " + e.Message, shadow, probeDir);
        }
        finally
        {
            try { if (File.Exists(file)) File.Delete(file); }
            catch (Exception) { }
        }
    }

    public PathReport Diagnose(bool probe = true, string? virtualStoreRoot = null)
    {
        var dirs = new List<string> { DataRoot };
        foreach (var p in new[] { MainDb, Layer0Db })
        {
            var d = Path.GetDirectoryName(Path.GetFullPath(p));
            if (d is not null && !dirs.Contains(d, StringComparer.OrdinalIgnoreCase)) dirs.Add(d);
        }

        var probes = dirs.Select(d => Probe(d, probe, virtualStoreRoot)).ToList();
        var severity = PathSeverity.Ok;
        var lines = new List<string>
        {
            $"置き場所  : {DataRoot}",
            $"  決め方  : {(Mode == PathMode.Portable
                ? "★exe の隣（ポータブル。配布時の既定）"
                : "開発中のリポジトリ直下（目印 " + string.Join(" と ", RepositoryMarkers) + " が揃っている）")}",
            $"  exe     : {ExeDir}",
            $"  設定    : {ConfigPath}{(ConfigLoaded ? "" : "  ★読めなかったので既定値で動く")}",
            $"  本体 DB : {MainDb}{(MainDbFromConfig ? "  ←config.json の database" : "")}",
            $"  Layer 0 : {Layer0Db}{(Layer0DbFromConfig ? "  ←config.json の layer0_db_path" : "")}",
            $"  監視一覧: {ReplayPathsFile}",
            $"  native  : {NativeDir}",
        };

        if (!IsUnder(DataRoot, MainDb) || !IsUnder(DataRoot, Layer0Db))
        {
            severity = PathSeverity.Warn;
            lines.Add("★アプリのデータが置き場所の外にあります（config.json で指しているはず）。");
            lines.Add("  ★既定は『分けない』です——意図した設定かを確かめてください。");
        }

        foreach (var p in probes)
        {
            if (p.Writable == false)
            {
                severity = PathSeverity.Fatal;
                lines.Add($"★書けません: {p.Directory}");
                lines.Add($"  理由: {p.Reason}");
                if (IsUnderProtectedRoot(p.Directory))
                    lines.Add("  ★Windows が書き込みを守っているフォルダです"
                              + "（Program Files / Windows の下）。");
                lines.Add("  ★このまま記録は始めません。★別の場所へ黙って書くこともしません。");
                lines.Add("  直し方は 2 つ:");
                lines.Add("    (1) この一式を書ける場所（デスクトップ・ドキュメント等）へ移す ——★これが素直");
                lines.Add($"    (2) {ConfigPath} の database / layer0_db_path に★絶対パスを書く");
            }
            else if (p.Writable is null && IsUnderProtectedRoot(p.Directory))
            {
                severity = severity == PathSeverity.Fatal ? severity : PathSeverity.Warn;
                lines.Add($"★Windows が守っているフォルダの下にあります: {p.Directory}");
            }

            if (p.VirtualStoreShadow is not null)
            {
                severity = PathSeverity.Fatal;
                lines.Add($"★★{p.Directory} へ書いたはずのものが、別の場所に居ます:");
                lines.Add($"    {p.VirtualStoreShadow}");
                lines.Add("  ★Windows の VirtualStore による付け替えです。"
                          + "★どちらが本物かをこちらで決めることはしません。");
            }
        }

        if (severity == PathSeverity.Ok)
        {
            var made = probes.Where(x => x.Probed is not null
                                      && !string.Equals(x.Probed, x.Directory, StringComparison.OrdinalIgnoreCase))
                             .Select(x => $"{x.Directory} はまだ無い（{x.Probed} で試した）").ToList();
            lines.Add("書き込みの確認: ok" + (made.Count > 0 ? "  ※" + string.Join(" / ", made) : ""));
        }
        return new PathReport(severity, probes, lines);
    }

    private static string? NearestExisting(string directory, out string? blocker)
    {
        blocker = null;
        var dir = new DirectoryInfo(Path.GetFullPath(directory));
        while (dir is not null)
        {
            if (Directory.Exists(dir.FullName)) return dir.FullName;
            if (File.Exists(dir.FullName)) { blocker = dir.FullName; return null; }
            dir = dir.Parent;
        }
        return null;
    }

    private static bool IsUnder(string root, string path)
    {
        var r = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar);
        var f = Path.GetFullPath(path);
        return f.StartsWith(r + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }

    public void EnsureWritable()
    {
        var report = Diagnose();
        if (report.Severity == PathSeverity.Fatal)
            throw new IOException(string.Join(Environment.NewLine, report.Lines));
    }

    public string Describe()
    {
        var sb = new StringBuilder();
        foreach (var line in Diagnose(probe: false).Lines) sb.AppendLine(line);
        return sb.ToString();
    }
}
