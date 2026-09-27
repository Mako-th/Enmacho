using System.Runtime.Versioning;
using TH09.Layer0;
using TH09.Record;

namespace TH09.Drive;

public sealed record Layer0RestampPlan(string Layer0Db, Layer0StampState Shape,
                                       Layer0Restamp.Status Status, string? Backup,
                                       string? Blocked, bool AlreadyDone);

[SupportedOSPlatform("windows")]
public static class Layer0RestampCommand
{
    public const string Flag = "--restamp-layer0";

    public const string Help =
        "いま生きている Layer 0 の solid-brotli-v1 行を、検査和つきの solid-brotli-v2 へ付け直す"
        + "（★brotli は圧縮し直さない。★控えを取ってから。--dry-run で予定だけ）";

    public const string Warning =
        "★これから、いま生きている本物の Layer 0 の blob / encoding / compressed_bytes を"
        + "書き換えます（brotli は圧縮し直さず、見出しに検査和を足すだけです）。";

    public const string NoBackup =
        "★いまの Layer 0 をそのまま控えた「完成した控え」が見つかりません。"
        + "先に " + Layer0StampCommand.ScanCommandBackupFlag + " で控えを取ってから、"
        + "もう一度この 1 手を呼んでください。";

    public const string Busy =
        "★別のプロセスが同じ DB へセッションを書いています"
        + "（監視を OFF にするか、走査を止めてから実行してください）。";

    public const string AlreadyDoneLine = "★もう v1 の行がありません（付け直す必要がありません）: ";

    public const string NotRun = "★付け直していません（--dry-run）。";


    public static Layer0RestampPlan Plan(Paths? paths = null)
    {
        var p = paths ?? Paths.Default;
        var shape = Layer0Stamp.Inspect(p.Layer0Db);
        var status = shape.FileExists && shape.Readable && shape.IsLayer0
            ? Layer0Restamp.Inspect(p.Layer0Db)
            : new Layer0Restamp.Status([]);
        var alreadyDone = shape.IsLayer0 && status.TotalV1 == 0;
        var backup = ScanBackup.LatestBackupOf(p.BackupRoot, p.Layer0Db);
        string? blocked =
            !shape.FileExists ? Layer0Stamp.NoFile + p.Layer0Db
            : !shape.Readable ? "★Layer 0 を読めません（" + (shape.Unreadable ?? "理由不明") + "）: "
                                + p.Layer0Db
            : !shape.IsLayer0 ? Layer0Stamp.NotLayer0 + "（" + p.Layer0Db + " ／ 足りない表 "
                                + string.Join(" ", shape.MissingTables) + "）"
            : alreadyDone ? null
            : backup is null ? NoBackup
            : null;
        return new Layer0RestampPlan(p.Layer0Db, shape, status, backup, blocked, alreadyDone);
    }

    public static void Describe(TextWriter w, Layer0RestampPlan plan, bool dryRun)
    {
        ArgumentNullException.ThrowIfNull(w);
        ArgumentNullException.ThrowIfNull(plan);
        var rule = new string('=', ScanTargets.RuleWidth);
        w.Write(rule + Lf);
        w.Write("Layer 0 の brotli 行を v2（検査和つき）へ付け直します" + Lf);
        w.Write("  相手: " + plan.Layer0Db
                + (plan.Shape.FileExists ? "（" + ScanBackup.Size(plan.Shape.Bytes) + "）"
                                        : "（★ありません）") + Lf);
        foreach (var t in plan.Status.Tables)
            w.Write("  " + t.Table + ": v1 " + Num(t.V1) + " 行 / v2 " + Num(t.V2) + " 行"
                    + " / それ以外 " + Num(t.Other) + " 行（全 " + Num(t.Total) + " 行）" + Lf);
        if (plan.Status.Tables.Count > 0)
            w.Write("  見積の増分: 約 " + ScanBackup.Size(plan.Status.EstimatedGrowthBytes)
                    + "（見出しが 4 B → 8 B になるだけ。brotli は圧縮し直さない）" + Lf);
        w.Write("  控え: " + (plan.Backup ?? "（見つかりません）") + Lf);
        if (plan.Blocked is { } why)
        {
            w.Write("★付け直しません: " + why + Lf);
            w.Write(rule + Lf);
            return;
        }
        if (plan.AlreadyDone)
        {
            w.Write(AlreadyDoneLine + plan.Layer0Db + Lf);
            w.Write(rule + Lf);
            return;
        }
        w.Write(Warning + Lf);
        if (dryRun) w.Write(NotRun + Lf);
        w.Write(rule + Lf);
    }


    public static int Run(TextWriter w, Layer0RestampPlan plan, bool dryRun, Paths? paths = null,
                          string? spareWriterLock = null)
    {
        ArgumentNullException.ThrowIfNull(w);
        ArgumentNullException.ThrowIfNull(plan);
        Describe(w, plan, dryRun);
        if (plan.Blocked is not null) return 1;
        if (plan.AlreadyDone || dryRun) return 0;

        var p = paths ?? Paths.Default;
        using var writer = spareWriterLock is { } spare
            ? SessionWriter.OpenWithSpareLock(p.MainDb, spare, msg => w.Write(msg + Lf))
            : SessionWriter.Open(p.MainDb, msg => w.Write(msg + Lf));
        if (writer is null)
        {
            w.Write(Busy + Lf);
            return 1;
        }

        var result = Layer0Restamp.Run(new Layer0Restamp.Options(plan.Layer0Db), w);
        w.Write("付け直した: " + Num(result.TotalConverted) + " 行"
                + "（" + string.Join(" / ", result.Tables.Select(
                    t => t.Table + " " + Num(t.Converted))) + "）" + Lf);

        var (v1Remaining, v2Checked, v2Failed) = Layer0Restamp.VerifyAll(plan.Layer0Db, w);
        w.Write("確認: v1 " + Num(v1Remaining) + " 行 / v2 " + Num(v2Checked) + " 行のうち "
                + Num(v2Failed) + " 行が CRC 不一致" + Lf);
        if (v1Remaining != 0 || v2Failed != 0) return 1;
        return 0;
    }


    private static readonly string Lf = ((char)10).ToString();

    private static string Num(long value) =>
        value.ToString(System.Globalization.CultureInfo.InvariantCulture);
}
