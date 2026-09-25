using System.Globalization;

namespace TH09.Launch;

public enum ScanRun
{
    AddNew,

    Rescan,

    ResetScans,

    ResetAll,
}

public enum ScanOrder
{
    Id,

    Size,

    Mtime,
}

public enum ScanMode
{
    Story = 0,

    Extra = 1,

    Match = 2,
}

public enum ScanMatchKind
{
    HumanVsHuman = 0,

    HumanVsCpu = 1,

    CpuVsHuman = 2,

    CpuVsCpu = 3,
}

public enum ScanScope
{
    Net,

    Local,

    Cpu,

    Story,

    Extra,
}

internal static class DriveFlags
{
    internal const string Monitor = "--monitor";
    internal const string Watch = "--watch";
    internal const string Scan = "--scan";

    internal const string RestoreSlots = "--restore-slots";
    internal const string Run = "--run";
    internal const string Batch = "--batch";
    internal const string DryRun = "--dry-run";
    internal const string Mode = "--mode";
    internal const string Difficulty = "--difficulty";
    internal const string MatchKind = "--match-kind";
    internal const string Chars = "--chars";
    internal const string Own = "--own";
    internal const string MinStages = "--min-stages";
    internal const string Dir = "--dir";
    internal const string NoRecurse = "--no-recurse";
    internal const string Order = "--order";
    internal const string Max = "--max";
    internal const string MaxMinutes = "--max-minutes";
    internal const string MinRecordVersion = "--min-record-version";
    internal const string NoBackup = "--no-backup";
    internal const string DropOldBackups = "--drop-old-backups";

    internal const string HitWindows = "--hit-windows";
    internal const string HitWindowBefore = "--hit-window-before";
    internal const string HitWindowAfter = "--hit-window-after";
    internal const string HitWindowQuick = "--hit-window-quick";
    internal const string HitWindowScopes = "--hit-window-scopes";

    internal const string On = "on";
    internal const string Off = "off";

    internal const string ScopeNet = "net";
    internal const string ScopeLocal = "local";
    internal const string ScopeCpu = "cpu";
    internal const string ScopeStory = "story";
    internal const string ScopeExtra = "extra";

    internal const string MatchHumanVsHuman = "human-vs-human";
    internal const string MatchHumanVsCpu = "human-vs-cpu";
    internal const string MatchCpuVsHuman = "cpu-vs-human";
    internal const string MatchCpuVsCpu = "cpu-vs-cpu";

    internal const string RunAddNew = "new";
    internal const string RunRescan = "rescan";
    internal const string RunResetScans = "reset-scans";
    internal const string RunResetAll = "reset-all";
    internal const string RunImportOnly = "import-only";

    internal const string OrderId = "id";
    internal const string OrderSize = "size";
    internal const string OrderMtime = "mtime";

    internal const string BuildLayer1 = "--build-layer1";
    internal const string Check = "--check";
    internal const string All = "--all";
    internal const string Session = "--session";
}

public sealed record ScanSettings
{
    public static ScanSettings Default { get; } = new();

    public ScanRun Run { get; init; } = ScanRun.AddNew;

    public IReadOnlyList<ScanMode> Modes { get; init; } = [];

    public IReadOnlyList<int> Difficulties { get; init; } = [];

    public IReadOnlyList<ScanMatchKind> MatchKinds { get; init; } = [];

    public IReadOnlyList<int> Characters { get; init; } = [];

    public bool OwnOnly { get; init; }

    public int MinStages { get; init; }

    public IReadOnlyList<string> Directories { get; init; } = [];

    public bool NoRecurse { get; init; }

    public ScanOrder Order { get; init; } = ScanOrder.Id;

    public int? MaxCount { get; init; }

    public double? MaxMinutes { get; init; }

    public int? MinRecordVersion { get; init; }

    public bool Backup { get; init; } = true;

    public bool DropOldBackups { get; init; }

    public bool DryRun { get; init; }

    public bool? HitWindows { get; init; }

    public int? HitWindowBefore { get; init; }

    public int? HitWindowAfter { get; init; }

    public bool? HitWindowQuick { get; init; }

    public IReadOnlyDictionary<ScanScope, bool>? HitWindowScopes { get; init; }

    internal void Validate()
    {
        if (!Enum.IsDefined(Run)) throw new ArgumentException("走り方の選択が範囲の外です: " + (int)Run);
        if (!Enum.IsDefined(Order)) throw new ArgumentException("処理順の選択が範囲の外です: " + (int)Order);
        foreach (ScanMode mode in Modes)
        {
            if (!Enum.IsDefined(mode))
                throw new ArgumentException("モードの選択が範囲の外です: " + (int)mode);
        }
        foreach (ScanMatchKind kind in MatchKinds)
        {
            if (!Enum.IsDefined(kind))
                throw new ArgumentException("対戦区分の選択が範囲の外です: " + (int)kind);
        }

        if (MinStages < 0) throw new ArgumentException("最小ステージ数は 0 以上です: " + MinStages);
        if (MaxCount is int max && max <= 0)
            throw new ArgumentException("対象の上限本数は 1 以上です: " + max);
        if (MinRecordVersion is int version && version < 0)
            throw new ArgumentException("Layer 0 の版は 0 以上です: " + version);
        if (MaxMinutes is double minutes && !(minutes > 0 && double.IsFinite(minutes)))
            throw new ArgumentException("総時間の上限は 0 より大きい数です: "
                                        + minutes.ToString("R", CultureInfo.InvariantCulture));

        foreach (int id in Difficulties)
        {
            if (id < 0) throw new ArgumentException("難易度の添字は 0 以上です: " + id);
        }
        foreach (int id in Characters)
        {
            if (id < 0) throw new ArgumentException("キャラの添字は 0 以上です: " + id);
        }
        if (HitWindowBefore is int before && before <= 0)
            throw new ArgumentException("窓の前は 1 以上です: " + before);
        if (HitWindowAfter is int after && after <= 0)
            throw new ArgumentException("窓の後は 1 以上です: " + after);
        if (HitWindowScopes is { } scopes)
        {
            foreach (ScanScope scope in scopes.Keys)
            {
                if (!Enum.IsDefined(scope))
                    throw new ArgumentException("遊び方の選択が範囲の外です: " + (int)scope);
            }
        }
        foreach (string dir in Directories) ValidateDirectory(dir);
    }

    private static void ValidateDirectory(string dir)
    {
        if (string.IsNullOrWhiteSpace(dir))
            throw new ArgumentException("フォルダが空です。");
        if (dir.StartsWith('-'))
            throw new ArgumentException("フォルダの指定が旗になっています: " + dir);
        foreach (char c in dir)
        {
            if (char.IsControl(c))
                throw new ArgumentException("フォルダに制御文字が入っています: " + dir);
        }
    }

    internal IReadOnlyList<string> ToArguments()
    {
        List<string> argv = [DriveFlags.Run, RunName(Run), DriveFlags.Batch];
        if (DryRun) argv.Add(DriveFlags.DryRun);
        if (Modes.Count > 0)
        {
            argv.Add(DriveFlags.Mode);
            argv.Add(string.Join(",", Modes.Select(m => Num((int)m))));
        }
        if (Difficulties.Count > 0)
        {
            argv.Add(DriveFlags.Difficulty);
            argv.Add(Join(Difficulties));
        }
        if (MatchKinds.Count > 0)
        {
            var picked = Enum.GetValues<ScanMatchKind>().Where(MatchKinds.Contains);
            argv.Add(DriveFlags.MatchKind);
            argv.Add(string.Join(",", picked.Select(MatchKindName)));
        }
        if (Characters.Count > 0)
        {
            argv.Add(DriveFlags.Chars);
            argv.Add(Join(Characters));
        }
        if (OwnOnly) argv.Add(DriveFlags.Own);
        if (MinStages > 0)
        {
            argv.Add(DriveFlags.MinStages);
            argv.Add(Num(MinStages));
        }
        foreach (string dir in Directories)
        {
            argv.Add(DriveFlags.Dir);
            argv.Add(dir);
        }
        if (NoRecurse) argv.Add(DriveFlags.NoRecurse);
        argv.Add(DriveFlags.Order);
        argv.Add(OrderName(Order));
        if (MaxCount is int max)
        {
            argv.Add(DriveFlags.Max);
            argv.Add(Num(max));
        }
        if (MaxMinutes is double minutes)
        {
            argv.Add(DriveFlags.MaxMinutes);
            argv.Add(minutes.ToString("R", CultureInfo.InvariantCulture));
        }
        if (MinRecordVersion is int version)
        {
            argv.Add(DriveFlags.MinRecordVersion);
            argv.Add(Num(version));
        }
        if (!Backup) argv.Add(DriveFlags.NoBackup);
        if (DropOldBackups) argv.Add(DriveFlags.DropOldBackups);
        if (HitWindows is bool windows)
        {
            argv.Add(DriveFlags.HitWindows);
            argv.Add(OnOff(windows));
        }
        if (HitWindowBefore is int before)
        {
            argv.Add(DriveFlags.HitWindowBefore);
            argv.Add(Num(before));
        }
        if (HitWindowAfter is int after)
        {
            argv.Add(DriveFlags.HitWindowAfter);
            argv.Add(Num(after));
        }
        if (HitWindowQuick is bool quick)
        {
            argv.Add(DriveFlags.HitWindowQuick);
            argv.Add(OnOff(quick));
        }
        if (HitWindowScopes is { Count: > 0 } scopes)
        {
            var pairs = Enum.GetValues<ScanScope>()
                            .Where(scopes.ContainsKey)
                            .Select(s => ScopeName(s) + "=" + OnOff(scopes[s]));
            argv.Add(DriveFlags.HitWindowScopes);
            argv.Add(string.Join(",", pairs));
        }
        return argv;
    }

    internal static string OnOff(bool value) => value ? DriveFlags.On : DriveFlags.Off;

    internal static string MatchKindName(ScanMatchKind kind) => kind switch
    {
        ScanMatchKind.HumanVsHuman => DriveFlags.MatchHumanVsHuman,
        ScanMatchKind.HumanVsCpu => DriveFlags.MatchHumanVsCpu,
        ScanMatchKind.CpuVsHuman => DriveFlags.MatchCpuVsHuman,
        ScanMatchKind.CpuVsCpu => DriveFlags.MatchCpuVsCpu,
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    internal static string ScopeName(ScanScope scope) => scope switch
    {
        ScanScope.Net => DriveFlags.ScopeNet,
        ScanScope.Local => DriveFlags.ScopeLocal,
        ScanScope.Cpu => DriveFlags.ScopeCpu,
        ScanScope.Story => DriveFlags.ScopeStory,
        ScanScope.Extra => DriveFlags.ScopeExtra,
        _ => throw new ArgumentOutOfRangeException(nameof(scope)),
    };

    internal static string RunName(ScanRun run) => run switch
    {
        ScanRun.AddNew => DriveFlags.RunAddNew,
        ScanRun.Rescan => DriveFlags.RunRescan,
        ScanRun.ResetScans => DriveFlags.RunResetScans,
        ScanRun.ResetAll => DriveFlags.RunResetAll,
        _ => throw new ArgumentOutOfRangeException(nameof(run)),
    };

    internal static string OrderName(ScanOrder order) => order switch
    {
        ScanOrder.Id => DriveFlags.OrderId,
        ScanOrder.Size => DriveFlags.OrderSize,
        ScanOrder.Mtime => DriveFlags.OrderMtime,
        _ => throw new ArgumentOutOfRangeException(nameof(order)),
    };

    private static string Join(IReadOnlyList<int> ids) => string.Join(",", ids.Select(Num));

    private static string Num(int value) => value.ToString(CultureInfo.InvariantCulture);
}

public sealed record Layer1Settings
{
    public static Layer1Settings Default { get; } = new();

    public bool CheckOnly { get; init; }

    public bool RebuildAll { get; init; }

    public long? OnlySession { get; init; }

    internal void Validate()
    {
        if (OnlySession is long sid && sid <= 0)
            throw new ArgumentException("session_id は 1 以上です: " + sid);
    }

    internal IReadOnlyList<string> ToArguments()
    {
        List<string> argv = [];
        if (CheckOnly) argv.Add(DriveFlags.Check);
        if (RebuildAll) argv.Add(DriveFlags.All);
        if (OnlySession is long sid)
        {
            argv.Add(DriveFlags.Session);
            argv.Add(sid.ToString(CultureInfo.InvariantCulture));
        }
        return argv;
    }
}
