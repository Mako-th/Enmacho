using System.Runtime.Versioning;
using TH09.Record;
using TH09.Record.Generated;
using TH09.Replay;

using Cols = TH09.Generated.DbColumns;

namespace TH09.Drive;

public enum ScanRunMode
{
    AddNew,

    Rescan,

    ResetScans,

    ResetAll,

    ImportOnly,
}

public sealed record ScanRunPlan(ScanRunMode Mode, string Name, string Label,
                                 bool IncludesDone, bool Capture, bool ImportReplays,
                                 bool ReplaceOlder, IReadOnlyList<string> Clearing,
                                 IReadOnlyList<string> Keeping, string? Blocked,
                                 string DoneNote);

[SupportedOSPlatform("windows")]
public static class ScanRunModes
{
    public const string Flag = "--run";

    public const string NameAddNew = "new";

    public const string NameRescan = "rescan";

    public const string NameResetScans = "reset-scans";

    public const string NameResetAll = "reset-all";

    public const string NameImportOnly = "import-only";

    public static readonly IReadOnlyList<string> Names =
        [NameAddNew, NameRescan, NameResetScans, NameResetAll, NameImportOnly];

    public static readonly IReadOnlyList<ScanRunMode> All =
        [ScanRunMode.AddNew, ScanRunMode.Rescan, ScanRunMode.ResetScans,
         ScanRunMode.ResetAll, ScanRunMode.ImportOnly];

    public static ScanRunMode Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        for (var i = 0; i < Names.Count; i++)
        {
            if (string.Equals(Names[i], text, StringComparison.Ordinal)) return All[i];
        }
        throw new ScanUsageError("取れない値です（" + Flag + " は "
                                 + string.Join(" / ", Names) + "）: " + text);
    }

    public static string Name(ScanRunMode mode)
    {
        for (var i = 0; i < All.Count; i++)
        {
            if (All[i] == mode) return Names[i];
        }
        throw new ScanUsageError("知らない走り方です: " + mode);
    }

    public static ScanRunPlan Plan(ScanRunMode mode, Paths? paths = null)
    {
        var p = paths ?? Paths.Default;
        return mode switch
        {
            ScanRunMode.AddNew => new ScanRunPlan(
                mode, NameAddNew, "新規のみ走査して DB 追加",
                IncludesDone: false, Capture: true, ImportReplays: false, ReplaceOlder: false,
                Clearing: [], Keeping: [], Blocked: null,
                DoneNote: Flag + " " + NameAddNew + "＝記録済みは除外"),

            ScanRunMode.Rescan => new ScanRunPlan(
                mode, NameRescan, "上書きして新たに走査",
                IncludesDone: true, Capture: true, ImportReplays: false, ReplaceOlder: true,
                Clearing: [], Keeping: [], Blocked: null,
                DoneNote: Flag + " " + NameRescan + "＝記録済みもやり直す"),

            ScanRunMode.ResetScans => new ScanRunPlan(
                mode, NameResetScans, "実プレイは残し、走査の結果だけ消して走査",
                IncludesDone: true, Capture: true, ImportReplays: false, ReplaceOlder: false,
                Clearing: ScanDerivedResults(), Keeping: [.. Registrations(), RealPlayKept],
                Blocked: null,
                DoneNote: Flag + " " + NameResetScans + "＝走査の結果を空にしてから全部やり直す"),

            ScanRunMode.ResetAll => new ScanRunPlan(
                mode, NameResetAll, "DB ごと空にして走査（実プレイも消えます）",
                IncludesDone: true, Capture: true, ImportReplays: true, ReplaceOlder: false,
                Clearing: [.. AllResults(p), .. Registrations()], Keeping: [],
                Blocked: null,
                DoneNote: Flag + " " + NameResetAll + "＝DB ごと空にして登録からやり直す"),

            ScanRunMode.ImportOnly => new ScanRunPlan(
                mode, NameImportOnly, "リプレイの登録のみ",
                IncludesDone: false, Capture: false, ImportReplays: true, ReplaceOlder: false,
                Clearing: [], Keeping: [], Blocked: null,
                DoneNote: Flag + " " + NameImportOnly + "＝走査しない"),

            _ => throw new ScanUsageError("知らない走り方です: " + mode),
        };
    }

    public static void Describe(TextWriter w, ScanRunPlan plan, ScanFilter? filter, Paths? paths = null)
    {
        ArgumentNullException.ThrowIfNull(w);
        ArgumentNullException.ThrowIfNull(plan);
        var rule = new string('=', ScanTargets.RuleWidth);
        w.Write(rule + "\n");
        w.Write("走り方: " + plan.Label + "（" + Flag + " " + plan.Name + "）\n");
        w.Write("  記録済みのリプレイ: " + (plan.IncludesDone ? "対象に含める" : "対象から外す") + "\n");
        w.Write("  走査: " + Yes(plan.Capture) + "\n");
        w.Write("  リプレイの登録: " + Yes(plan.ImportReplays) + "\n");
        w.Write("  古いセッションの置き換え: " + Yes(plan.ReplaceOlder) + "\n");
        Listing(w, "空にするもの", "空に", plan.Clearing);
        Listing(w, "残すもの", "残す", plan.Keeping);
        if (filter is not null) Filters(w, filter, paths ?? Paths.Default);
        w.Write(rule + "\n");
    }

    internal static bool OwnNamesEmpty(Paths paths)
    {
        var own = paths.OwnNames;
        return ReplayOwner.OwnNames(own.PlayerName, own.Names).Count == 0;
    }


    private static IReadOnlyList<string> ScanDerivedResults() =>
    [
        "本体 DB の走査由来のセッション（" + Cols.SessionReplays.LinkMethod + "=" + ScanLink.Method
            + "）と " + Cols.Sessions.Table + " の連なり（表の一覧は SessionDelete が DDL から集める）",
        "本体 DB の " + Cols.ReplayScanJobs.Table + " と " + Cols.ReplayScanItems.Table
            + "（走査の作業履歴。丸ごと）",
    ];

    private static IReadOnlyList<string> AllResults(Paths p) =>
    [
        "Layer 0 のファイル: " + p.Layer0Db + "（空から建てて、走査が最後まで行けば入れ替えます）",
        "本体 DB の " + Cols.Sessions.Table
            + "（実プレイも含め全部。とその連なり。表の一覧は SessionDelete が DDL から集める）",
        "本体 DB の " + Cols.ReplayScanJobs.Table + " と " + Cols.ReplayScanItems.Table
            + "（走査の作業履歴。丸ごと）",
    ];

    private static IReadOnlyList<string> Registrations() =>
    [
        "本体 DB の " + Cols.Replays.Table + "（リプレイの登録）",
        "本体 DB の " + Cols.ReplayPaths.Table + "（リプレイの置き場所）",
    ];

    private const string RealPlayKept = "実プレイ（走査由来ではないセッション。Layer 0 のファイルも含めて残ります）";

    private static void Listing(TextWriter w, string head, string mark,
                                IReadOnlyList<string> items)
    {
        w.Write("  " + head + ": " + items.Count.ToString(System.Globalization.CultureInfo.InvariantCulture)
                + " 件" + (items.Count == 0 ? "（無し）" : "") + "\n");
        foreach (var line in items) w.Write("    " + mark + ": " + line + "\n");
    }

    private static void Filters(TextWriter w, ScanFilter f, Paths paths)
    {
        var lines = new List<string>();
        if (f.Modes is { Count: > 0 } modes)
        {
            lines.Add("--mode " + string.Join(
                " ", modes.Select(m => Num(m) + "（" + ScanTargets.LabelOf(RecordLabels.Modes, m) + "）")));
        }
        if (f.Difficulties is { Count: > 0 } diffs)
        {
            lines.Add("--difficulty " + string.Join(
                " ", diffs.Select(d => Num(d) + "（" + ScanTargets.LabelOf(RecordLabels.Difficulties, d) + "）")));
        }
        if (f.Chars is { Count: > 0 } chars)
        {
            lines.Add("--chars " + string.Join(
                " ", chars.Select(c => Num(c) + "（" + MonitorRules.Char(c) + "）")));
        }
        if (f.MatchKinds is { Count: > 0 } kinds)
        {
            lines.Add("--match-kind " + string.Join(
                          " ", kinds.Select(k => Num((int)k) + "（" + ScanTargets.MatchKindWord(k) + "）"))
                      + "（Match だけに効く）");
        }
        if (f.Own) lines.Add("--own（自分のリプレイだけ）");
        if (f.MinStages != 0) lines.Add("--min-stages " + Num(f.MinStages) + "（面数の下限）");
        if (f.Dirs is { Count: > 0 } dirs)
        {
            lines.Add("--dir " + string.Join(", ", dirs)
                      + "（" + (f.Recurse ? "配下も含む" : "直下のみ") + "）");
        }
        w.Write("絞り（すべて AND）: " + Num(lines.Count) + " 件"
                + (lines.Count == 0 ? "（無し）" : "") + "\n");
        foreach (var line in lines) w.Write("  " + line + "\n");
        if (f.Own && OwnNamesEmpty(paths))
        {
            w.Write("★注意: --own ですが、自分の名前が設定されていません"
                    + "（config.json の own_names / player_name）。"
                    + "すべて他人のリプレイ扱いになり、対象は 0 件になります。\n");
        }
    }

    private static string Yes(bool v) => v ? "する" : "しない";

    private static string Num(long v) =>
        v.ToString(System.Globalization.CultureInfo.InvariantCulture);
}
