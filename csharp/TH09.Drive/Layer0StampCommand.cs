using System.Runtime.Versioning;
using TH09.Layer0;
using TH09.Record;

namespace TH09.Drive;

public sealed record Layer0StampPlan(string Layer0Db, Layer0StampState State, string? Backup,
                                     string? Blocked, bool AlreadyStamped);

[SupportedOSPlatform("windows")]
public static class Layer0StampCommand
{
    public const string Flag = "--stamp-layer0";

    public const string Help =
        "いま生きている Layer 0 に「書き足してよい」の印を押す"
        + "（★追記の走り方に 1 度だけ要る。★控えを取ってから。--dry-run で予定だけ）";

    public const string ToolName = "th09_drive --scan " + Flag;

    public const string Warning =
        "★これから、いま生きている本物の Layer 0 に書きます"
        + "（表を 1 つと行を 1 本足すだけで、既存の行には 1 行も触りません）。";

    public const string NoBackup =
        "★いまの Layer 0 をそのまま控えた「完成した控え」が見つかりません。"
        + "先に " + ScanCommandBackupFlag + " で控えを取ってから、もう一度この 1 手を呼んでください。";

    public const string ScanCommandBackupFlag = "--backup";

    public const string NotPressed = "★印は押していません（--dry-run）。";


    public static Layer0StampPlan Plan(Paths? paths = null)
    {
        var p = paths ?? Paths.Default;
        var state = Layer0Stamp.Inspect(p.Layer0Db);
        var backup = ScanBackup.LatestBackupOf(p.BackupRoot, p.Layer0Db);
        string? blocked =
            !state.FileExists ? Layer0Stamp.NoFile + p.Layer0Db
            : !state.Readable ? "★Layer 0 を読めません（" + (state.Unreadable ?? "理由不明") + "）: "
                                + p.Layer0Db
            : !state.IsLayer0 ? Layer0Stamp.NotLayer0 + "（" + p.Layer0Db + " ／ 足りない表 "
                                + string.Join(" ", state.MissingTables) + "）"
            : state.Stamped ? null
            : backup is null ? NoBackup
            : null;
        return new Layer0StampPlan(p.Layer0Db, state, backup, blocked, state.Stamped);
    }

    public static void Describe(TextWriter w, Layer0StampPlan plan, bool dryRun)
    {
        ArgumentNullException.ThrowIfNull(w);
        ArgumentNullException.ThrowIfNull(plan);
        var rule = new string('=', ScanTargets.RuleWidth);
        w.Write(rule + Lf);
        w.Write("Layer 0 に印を押します（" + Layer0Stamp.Table + "）" + Lf);
        w.Write("  相手: " + plan.Layer0Db
                + (plan.State.FileExists ? "（" + ScanBackup.Size(plan.State.Bytes) + "）"
                                         : "（★ありません）") + Lf);
        w.Write("  Layer 0 の表: " + (plan.State.IsLayer0 ? "そろっています" : "足りません")
                + "（足りない表 " + Num(plan.State.MissingTables.Count) + " 個"
                + (plan.State.MissingTables.Count == 0
                   ? "" : "：" + string.Join(" ", plan.State.MissingTables)) + "）" + Lf);
        w.Write("  いまの印: " + (plan.State.Stamped
                                  ? "あります（" + Num(plan.State.StampRows) + " 行）"
                                  : "ありません") + Lf);
        w.Write("  控え: " + (plan.Backup ?? "（見つかりません）") + Lf);
        if (plan.Blocked is { } why)
        {
            w.Write("★押しません: " + why + Lf);
            w.Write(rule + Lf);
            return;
        }
        if (plan.AlreadyStamped)
        {
            w.Write(Layer0Stamp.Already + plan.Layer0Db + Lf);
            w.Write(rule + Lf);
            return;
        }
        w.Write(Warning + Lf);
        w.Write("  足すもの: 表 " + Layer0Stamp.Table + " 1 つ ／ 行 1 本（state="
                + Layer0Stamp.StatePressed + " / tool=" + ToolName + "）" + Lf);
        if (dryRun) w.Write(NotPressed + Lf);
        w.Write(rule + Lf);
    }


    public static int Run(TextWriter w, Layer0StampPlan plan, bool dryRun)
    {
        ArgumentNullException.ThrowIfNull(w);
        ArgumentNullException.ThrowIfNull(plan);
        Describe(w, plan, dryRun);
        if (plan.Blocked is not null) return 1;
        if (plan.AlreadyStamped || dryRun) return 0;
        var pressed = Layer0Stamp.Press(plan.Layer0Db, ToolName);
        w.Write(pressed
                ? "印を押しました: " + plan.Layer0Db + "（" + Layer0Stamp.Table + "）" + Lf
                : Layer0Stamp.Already + plan.Layer0Db + Lf);
        return 0;
    }


    private static readonly string Lf = ((char)10).ToString();

    private static string Num(long value) =>
        value.ToString(System.Globalization.CultureInfo.InvariantCulture);
}
