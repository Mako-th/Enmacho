using System.Globalization;
using System.Text.RegularExpressions;

namespace TH09.Record;

public static class ScanProgressLines
{
    public const string ScanHeaderPrefix = "―― [";

    public const string ScanHeaderReplayMarker = "] replay_id=";

    public static string ScanHeader(int index, int total, long replayId, string fileName,
                                    double elapsedMinutes)
    {
        ArgumentNullException.ThrowIfNull(fileName);
        return ScanHeaderPrefix + Num(index) + "/" + Num(total) + ScanHeaderReplayMarker
             + Num(replayId) + "  " + fileName + "  経過 " + ElapsedMinutesText(elapsedMinutes)
             + " 分 ――";
    }

    public static bool TryParseScanHeader(string? line, out int index, out int total)
    {
        index = 0;
        total = 0;
        if (string.IsNullOrEmpty(line)) return false;
        var text = StripTimestamp(line);
        if (!text.StartsWith(ScanHeaderPrefix, StringComparison.Ordinal)) return false;
        var markerAt = text.IndexOf(ScanHeaderReplayMarker, ScanHeaderPrefix.Length,
                                    StringComparison.Ordinal);
        if (markerAt < 0) return false;
        var body = text[ScanHeaderPrefix.Length..markerAt];
        return TryParseFraction(body, out index, out total);
    }

    public const string Layer1ProgressInfix = " セッション / ";

    public static string Layer1Progress(int n, int total, int rows, double seconds) =>
        "  " + Num(n) + "/" + Num(total) + Layer1ProgressInfix + Num(rows) + " 行 / "
        + SecondsText(seconds) + " 秒";

    public static bool TryParseLayer1Progress(string? line, out int n, out int total)
    {
        n = 0;
        total = 0;
        if (string.IsNullOrEmpty(line)) return false;
        var text = StripTimestamp(line);
        var infixAt = text.IndexOf(Layer1ProgressInfix, StringComparison.Ordinal);
        if (infixAt < 0) return false;
        var head = text[..infixAt].TrimStart(' ');
        return TryParseFraction(head, out n, out total);
    }

    public const string ImportResultPrefix = "既存リプレイ一括登録: ";

    public const string ImportResultSuffix = " 件を処理しました。";

    public static string ImportResult(int count) => ImportResultPrefix + Num(count) + ImportResultSuffix;

    public static bool TryParseImportResult(string? line, out int count)
    {
        count = 0;
        if (string.IsNullOrEmpty(line)) return false;
        var text = StripTimestamp(line);
        if (!text.StartsWith(ImportResultPrefix, StringComparison.Ordinal)) return false;
        if (!text.EndsWith(ImportResultSuffix, StringComparison.Ordinal)) return false;
        var body = text[ImportResultPrefix.Length..^ImportResultSuffix.Length];
        return int.TryParse(body, NumberStyles.None, CultureInfo.InvariantCulture, out count);
    }

    public const string SweptStalePathsPrefix = "移動したリプレイの片付け: ";

    public const string SweptStalePathsSuffix = " 件を片付けました。";

    public static string SweptStalePaths(int count) =>
        SweptStalePathsPrefix + Num(count) + SweptStalePathsSuffix;

    public static bool TryParseSweptStalePaths(string? line, out int count)
    {
        count = 0;
        if (string.IsNullOrEmpty(line)) return false;
        var text = StripTimestamp(line);
        if (!text.StartsWith(SweptStalePathsPrefix, StringComparison.Ordinal)) return false;
        if (!text.EndsWith(SweptStalePathsSuffix, StringComparison.Ordinal)) return false;
        var body = text[SweptStalePathsPrefix.Length..^SweptStalePathsSuffix.Length];
        return int.TryParse(body, NumberStyles.None, CultureInfo.InvariantCulture, out count);
    }

    public enum Phase
    {
        Scanning,

        RebuildingLayer1,
    }

    public readonly record struct Mark(Phase Phase, int Current, int Total);

    public static Mark? TryParse(string? line)
    {
        if (TryParseScanHeader(line, out var index, out var scanTotal))
            return new Mark(Phase.Scanning, index, scanTotal);
        if (TryParseLayer1Progress(line, out var n, out var layerTotal))
            return new Mark(Phase.RebuildingLayer1, n, layerTotal);
        return null;
    }

    public readonly record struct ScanPlanSummary(int TargetCount, string DoneNote, double Hours,
                                                   int CopyCount, int InSlotCount);

    public const string ScanPlanPrefix = "走査予定: 対象 ";
    private const string ScanPlanCountToDone = " 件（";
    private const string ScanPlanDoneToHours = "） ／ 所要見積 ";
    private const string ScanPlanHoursToCopy = " 時間 ／ ファイル操作: コピー ";
    private const string ScanPlanCopyToInSlot = " 件 / 在中 ";
    private const string ScanPlanInSlotSuffix = " 件";

    public static string ScanPlanLine(int targetCount, string doneNote, double hours,
                                      int copyCount, int inSlotCount)
        => ScanPlanPrefix + Num(targetCount) + ScanPlanCountToDone + doneNote
         + ScanPlanDoneToHours + hours.ToString("F1", CultureInfo.InvariantCulture)
         + ScanPlanHoursToCopy + Num(copyCount) + ScanPlanCopyToInSlot + Num(inSlotCount)
         + ScanPlanInSlotSuffix;

    public static bool TryParseScanPlan(string? line, out ScanPlanSummary summary)
    {
        summary = default;
        if (string.IsNullOrEmpty(line)) return false;
        var text = StripTimestamp(line);
        if (!text.StartsWith(ScanPlanPrefix, StringComparison.Ordinal)) return false;
        var body = text[ScanPlanPrefix.Length..];
        if (!TryCutInt(ref body, ScanPlanCountToDone, out var targetCount)) return false;
        var doneAt = body.IndexOf(ScanPlanDoneToHours, StringComparison.Ordinal);
        if (doneAt < 0) return false;
        var doneNote = body[..doneAt];
        body = body[(doneAt + ScanPlanDoneToHours.Length)..];
        var hoursAt = body.IndexOf(ScanPlanHoursToCopy, StringComparison.Ordinal);
        if (hoursAt < 0) return false;
        if (!double.TryParse(body[..hoursAt], NumberStyles.Float, CultureInfo.InvariantCulture,
                             out var hours))
            return false;
        body = body[(hoursAt + ScanPlanHoursToCopy.Length)..];
        if (!TryCutInt(ref body, ScanPlanCopyToInSlot, out var copyCount)) return false;
        if (!body.EndsWith(ScanPlanInSlotSuffix, StringComparison.Ordinal)) return false;
        if (!int.TryParse(body[..^ScanPlanInSlotSuffix.Length], NumberStyles.None,
                          CultureInfo.InvariantCulture, out var inSlotCount))
            return false;
        summary = new ScanPlanSummary(targetCount, doneNote, hours, copyCount, inSlotCount);
        return true;
    }

    public readonly record struct BatchOutcome(int Ok, int Mismatch, int Failed, int Empty,
                                               int Skipped, int Total);

    public const string BatchOutcomePrefix = "バッチ終了: ok ";
    private const string BatchOkToMismatch = " / mismatch ";
    private const string BatchMismatchToFailed = " / failed ";
    private const string BatchFailedToEmpty = " / 中身が空 ";
    private const string BatchEmptyToSkipped = " / 未処理 ";
    private const string BatchSkippedToTotal = "（全 ";
    private const string BatchTotalSuffix = " 件）";

    public static string BatchOutcomeLine(int ok, int mismatch, int failed, int empty, int skipped,
                                          int total)
        => BatchOutcomePrefix + Num(ok) + BatchOkToMismatch + Num(mismatch) + BatchMismatchToFailed
         + Num(failed) + BatchFailedToEmpty + Num(empty) + BatchEmptyToSkipped + Num(skipped)
         + BatchSkippedToTotal + Num(total) + BatchTotalSuffix;

    public static bool TryParseBatchOutcome(string? line, out BatchOutcome outcome)
    {
        outcome = default;
        if (string.IsNullOrEmpty(line)) return false;
        var text = StripTimestamp(line);
        if (!text.StartsWith(BatchOutcomePrefix, StringComparison.Ordinal)) return false;
        var body = text[BatchOutcomePrefix.Length..];
        if (!TryCutInt(ref body, BatchOkToMismatch, out var ok)) return false;
        if (!TryCutInt(ref body, BatchMismatchToFailed, out var mismatch)) return false;
        if (!TryCutInt(ref body, BatchFailedToEmpty, out var failed)) return false;
        if (!TryCutInt(ref body, BatchEmptyToSkipped, out var empty)) return false;
        if (!TryCutInt(ref body, BatchSkippedToTotal, out var skipped)) return false;
        if (!body.EndsWith(BatchTotalSuffix, StringComparison.Ordinal)) return false;
        if (!int.TryParse(body[..^BatchTotalSuffix.Length], NumberStyles.None,
                          CultureInfo.InvariantCulture, out var total))
            return false;
        outcome = new BatchOutcome(ok, mismatch, failed, empty, skipped, total);
        return true;
    }

    public const string StopReasonPrefix = "停止理由: ";

    public static string StopReasonLine(string reason) => StopReasonPrefix + reason;

    public static bool TryParseStopReason(string? line, out string reason)
    {
        reason = "";
        if (string.IsNullOrEmpty(line)) return false;
        var text = StripTimestamp(line);
        if (!text.StartsWith(StopReasonPrefix, StringComparison.Ordinal)) return false;
        reason = text[StopReasonPrefix.Length..];
        return true;
    }

    private static bool TryCutInt(ref string body, string infix, out int value)
    {
        value = 0;
        var at = body.IndexOf(infix, StringComparison.Ordinal);
        if (at < 0) return false;
        if (!int.TryParse(body[..at], NumberStyles.None, CultureInfo.InvariantCulture, out value))
            return false;
        body = body[(at + infix.Length)..];
        return true;
    }


    private static readonly Regex TimestampPrefix =
        new(@"^\[\d{2}:\d{2}:\d{2}\] ", RegexOptions.Compiled);

    private static string StripTimestamp(string line) => TimestampPrefix.Replace(line, "", 1);

    private static bool TryParseFraction(string text, out int index, out int total)
    {
        index = 0;
        total = 0;
        var slash = text.IndexOf('/');
        if (slash < 0) return false;
        if (!int.TryParse(text[..slash], NumberStyles.None, CultureInfo.InvariantCulture, out index))
            return false;
        if (!int.TryParse(text[(slash + 1)..], NumberStyles.None, CultureInfo.InvariantCulture,
                          out total))
            return false;
        return index >= 1 && total >= 1 && index <= total;
    }

    private static string Num(long value) => value.ToString(CultureInfo.InvariantCulture);

    private static string ElapsedMinutesText(double value) =>
        value.ToString("F0", CultureInfo.InvariantCulture);

    private static string SecondsText(double value) =>
        value.ToString("F1", CultureInfo.InvariantCulture);
}
