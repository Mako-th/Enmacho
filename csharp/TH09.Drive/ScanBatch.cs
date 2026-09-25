using System.Diagnostics;
using System.Globalization;
using System.Runtime.Versioning;
using TH09.Record;

using IOPath = System.IO.Path;

namespace TH09.Drive;

public sealed record ScanBatchItem(int ExitCode, string? Problem);

public sealed record ScanBatchResult(int ExitCode, int Ok, int Mismatch, int Failed, int Empty,
                                     int Skipped, int Total, int Ran, string? StoppedBy);

[SupportedOSPlatform("windows")]
public static class ScanBatch
{
    public const int ConsecutiveLimit = 3;

    public const string NoTargets = "対象がありません（記録済みは既定で除外されます。--rescan で再実行）。";

    public sealed record Options(
        IReadOnlyList<ScanTarget> Targets,
        Func<ScanTarget, ScanBatchItem> RunOne,
        double? MaxMinutes = null,
        string? ReplayDir = null,
        Func<IDisposable?>? Exclusive = null,
        Func<double>? Clock = null,
        TextWriter? Out = null,
        Action<string>? Log = null);

    public static ScanBatchResult Run(Options o)
    {
        ArgumentNullException.ThrowIfNull(o);
        var w = o.Out ?? Console.Out;
        var log = o.Log ?? ReplaySlots.DefaultLog;
        var clock = o.Clock ?? DefaultClock;
        var targets = o.Targets;
        var total = targets.Count;
        if (total == 0)
        {
            w.Write(NoTargets + "\n");
            return new ScanBatchResult(0, 0, 0, 0, 0, 0, 0, 0, null);
        }

        var replayDir = o.ReplayDir ?? Paths.Default.GameReplayDir()
            ?? throw new ScanSetupFailed("エラー: ゲームの replay フォルダが見つかりません。");
        ReplaySlots.RestoreLeftoverBackups(replayDir, log);
        using var exclusive = o.Exclusive?.Invoke();

        double? deadline = o.MaxMinutes is double m && m != 0.0 ? clock() + m * 60.0 : null;
        var rule = new string('=', ScanTargets.RuleWidth);
        int ok = 0, mismatch = 0, failed = 0, empty = 0, skipped = 0, consecutive = 0, ran = 0;
        string? stoppedBy = null;

        w.Write(rule + "\n");
        w.Write("バッチ開始: " + Num(total)
                + " 件（Ctrl+C でいつでも中断できます。処理済みは残ります）\n");
        w.Write(rule + "\n");
        var started = clock();

        for (var i = 1; i <= total; i++)
        {
            var t = targets[i - 1];
            if (deadline is double limit && clock() >= limit)
            {
                stoppedBy = "時間上限（--max-minutes " + G(o.MaxMinutes!.Value) + "）";
                skipped = total - i + 1;
                break;
            }
            var elapsed = clock() - started;
            w.Write("\n");
            w.Write(ScanProgressLines.ScanHeader(i, total, t.ReplayId, IOPath.GetFileName(t.Path),
                                                 elapsed / 60.0) + "\n");

            int rc;
            string? problem = null;
            ran++;
            try
            {
                var item = o.RunOne(t);
                rc = item.ExitCode;
                problem = item.Problem;
            }
            catch (OperationCanceledException)
            {
                stoppedBy = "Ctrl+C";
                skipped = total - i;
                w.Write("\n");
                w.Write("中断しました。この1本は諦めます（スロットは復元済み）。\n");
                break;
            }
            catch (ScanSetupFailed exc)
            {
                rc = 1;
                log("この1本は飛ばします: " + exc.Message);
            }
            catch (Exception exc)
            {
                rc = 1;
                log("この1本で例外: " + exc.GetType().Name + ": " + exc.Message);
            }

            if (!string.IsNullOrEmpty(problem))
            {
                empty++;
                consecutive++;
                log("★中身が伴っていません: " + problem);
                if (consecutive >= ConsecutiveLimit)
                {
                    stoppedBy = Num(ConsecutiveLimit) + "本連続で中身が空（" + problem + "）";
                    skipped = total - i;
                    break;
                }
            }
            else if (rc == 0)
            {
                ok++;
                consecutive = 0;
            }
            else if (rc == 2)
            {
                mismatch++;
                consecutive = 0;
            }
            else
            {
                failed++;
                consecutive++;
                if (consecutive >= ConsecutiveLimit)
                {
                    stoppedBy = Num(ConsecutiveLimit) + "本連続の失敗";
                    skipped = total - i;
                    break;
                }
            }
        }

        var took = clock() - started;
        w.Write("\n");
        w.Write(rule + "\n");
        w.Write(ScanProgressLines.BatchOutcomeLine(ok, mismatch, failed, empty, skipped, total) + "\n");
        w.Write("所要 " + ScanTargets.F(took / 60.0, 1) + " 分\n");
        if (stoppedBy is not null)
        {
            w.Write(ScanProgressLines.StopReasonLine(stoppedBy) + "\n");
            w.Write("**処理済みは保存されています。** もう一度 --batch を実行すれば続きから進みます\n");
            w.Write("（記録済みは既定で除外されるので、二重には走りません）。\n");
        }
        w.Write(rule + "\n");

        var exitCode = failed != 0 || empty != 0 ? 1 : mismatch != 0 ? 2 : 0;
        return new ScanBatchResult(exitCode, ok, mismatch, failed, empty, skipped, total, ran,
                                   stoppedBy);
    }


    private static double DefaultClock() =>
        (double)Stopwatch.GetTimestamp() / Stopwatch.Frequency;

    internal static string G(double value)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
            return value.ToString(CultureInfo.InvariantCulture);
        if (value == 0.0) return "0";
        const int Precision = 6;
        var sci = value.ToString("E" + (Precision - 1).ToString(CultureInfo.InvariantCulture),
                                 CultureInfo.InvariantCulture);
        var at = sci.IndexOf('E', StringComparison.Ordinal);
        var mantissa = sci[..at];
        var exp = int.Parse(sci[(at + 1)..], NumberStyles.AllowLeadingSign,
                            CultureInfo.InvariantCulture);
        if (exp < -4 || exp >= Precision)
        {
            return Trim(mantissa) + "e" + (exp < 0 ? "-" : "+")
                 + Math.Abs(exp).ToString("00", CultureInfo.InvariantCulture);
        }
        var digits = Math.Max(0, Precision - 1 - exp);
        return Trim(value.ToString("F" + digits.ToString(CultureInfo.InvariantCulture),
                                   CultureInfo.InvariantCulture));
    }

    private static string Trim(string text)
    {
        if (!text.Contains('.', StringComparison.Ordinal)) return text;
        return text.TrimEnd('0').TrimEnd('.');
    }

    private static string Num(long value) => value.ToString(CultureInfo.InvariantCulture);
}
