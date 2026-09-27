using System.Globalization;
using System.Runtime.Versioning;
using TH09.Layer0;
using TH09.Record;

namespace TH09.Drive;

[SupportedOSPlatform("windows")]
public static class MonitorCommand
{
    internal enum Next
    {
        Stop,
        Immediately,
        Retry,
    }

    public const string Flag = "--monitor";

    public const string LoggerVersion = "integrated-v18";

    public const int GameAbsentDelayMilliseconds = 1000;

    public const int RetryDelayMilliseconds = 250;

    public const string GameGoneLine = MonitorLines.GameGone;

    public const string GameFoundPrefix = MonitorLines.GameFoundPrefix;

    public const string GameFoundSuffix = MonitorLines.GameFoundSuffix;

    public static readonly string TickHookOffReason =
        "監視を始められません: " + ConfigStore.TickHookKey + " が "
        + ConfigStore.TickHookWords[2] + " です" + CaptureParts.NoPollingFallback
        + "設定はここです: ";

    public const string TickHookNote = "[tick] tick_hook = ";

    public static readonly string NoStampReason =
        "監視を始められません: 印（" + Layer0Stamp.Table + "）の無い Layer 0 へは書き足しません"
        + "（誰かが意図して開けた場所にしか書きません）: ";

    public static readonly string NoStampHint =
        "先に " + ScanCommand.Flag + " " + Layer0StampCommand.ScanCommandBackupFlag
        + " で控えを取り、" + ScanCommand.Flag + " " + Layer0StampCommand.Flag
        + " で印を押してから、もう一度 " + Flag + " を呼んでください。";

    public const string NotLayer0Reason =
        "監視を始められません: 行き先が Layer 0 ではありません"
        + "（Layer 0 の表がそろっていません）: ";

    public const string UnreadableReason = "監視を始められません: Layer 0 を読めません: ";

    public static int Run(CancellationToken cancel, Action<string>? log = null)
    {
        var writeLog = log ?? Console.WriteLine;
        var paths = Paths.Default;
        var settings = ConfigStore.Load(paths.ConfigPath);
        if (BlockedByLayer0(Layer0Stamp.Inspect(paths.Layer0Db), paths.Layer0Db) is { } blocked)
        {
            writeLog(blocked);
            return 1;
        }
        if (settings.TickHook == TickHookMode.Off)
        {
            writeLog(TickHookOffReason + paths.ConfigPath);
            return 1;
        }
        writeLog(TickHookNote + ConfigStore.Word(settings.TickHook));
        using var writer = SessionWriter.Open(paths.MainDb, writeLog);
        if (writer is null) return 1;

        CaptureParts parts;
        try
        {
            parts = CaptureParts.Open(new CaptureParts.Options(
                Paths: paths,
                TickHook: ConfigStore.Word(settings.TickHook),
                Log: writeLog,
                Layer0Db: paths.Layer0Db,
                Layer0Append: File.Exists(paths.Layer0Db),
                Settings: settings));
        }
        catch (Exception exc)
        {
            writeLog("監視を始められません: " + exc.GetType().Name + ": " + exc.Message);
            return 1;
        }

        using (parts)
        {
            return Loop(parts,
                        () => parts.CaptureMonitor(writer, cancel, LoggerVersion,
                                                   paths.TickHookAttachDelaySec),
                        writeLog, cancel,
                        () => AutoDetectGameDir(parts.FindPid(), paths, writeLog));
        }
    }

    internal static int Loop(CaptureParts parts, Func<CaptureResult> capture,
                             Action<string> writeLog, CancellationToken cancel,
                             Action beforeCapture)
    {
        ArgumentNullException.ThrowIfNull(parts);
        ArgumentNullException.ThrowIfNull(capture);
        ArgumentNullException.ThrowIfNull(beforeCapture);
        {
            writeLog("th09.exe を待機しています。Ctrl+C で終了します。");
            var absenceLogged = false;
            var everSeen = false;
            while (!cancel.IsCancellationRequested)
            {
                try
                {
                    if (absenceLogged)
                    {
                        var found = parts.FindPid();
                        if (found <= 0)
                        {
                            Wait(cancel, GameAbsentDelayMilliseconds);
                            continue;
                        }
                        writeLog(FoundLine(found));
                        absenceLogged = false;
                        everSeen = true;
                    }
                    beforeCapture();
                    var result = capture();
                    absenceLogged = false;
                    everSeen = true;
                    var next = AfterCapture(result.Status);
                    if (next == Next.Stop) break;
                    if (next == Next.Retry)
                    {
                        writeLog("捕捉を再試行します: status=" + result.Status
                                 + (result.Error is null ? "" : " / " + result.Error));
                        Wait(cancel, RetryDelayMilliseconds);
                    }
                }
                catch (ScanSetupFailed exc) when (exc.Message == LivePorts.GameMissing)
                {
                    if (AbsenceLine(everSeen, absenceLogged) is { } line)
                    {
                        writeLog(line);
                        absenceLogged = true;
                    }
                    Wait(cancel, GameAbsentDelayMilliseconds);
                }
                catch (ScanSetupFailed exc)
                {
                    absenceLogged = false;
                    writeLog("捕捉を再試行します: " + exc.Message);
                    Wait(cancel, RetryDelayMilliseconds);
                }
                catch (Exception exc)
                {
                    writeLog("監視を続けられません: " + exc.GetType().Name + ": " + exc.Message);
                    return 1;
                }
            }
        }
        writeLog("監視を終了します。");
        return 0;
    }

    private static void Wait(CancellationToken cancel, int milliseconds) =>
        cancel.WaitHandle.WaitOne(milliseconds);

    internal static Next AfterCapture(string status) => status switch
    {
        CaptureStatus.Interrupted => Next.Stop,
        CaptureStatus.Captured or CaptureStatus.Disconnected => Next.Immediately,
        _ => Next.Retry,
    };

    internal static bool ShouldLogAbsence(bool loggedSinceProgress) => !loggedSinceProgress;

    internal static string? AbsenceLine(bool everSeen, bool loggedSinceProgress) =>
        !ShouldLogAbsence(loggedSinceProgress) ? null
        : everSeen ? GameGoneLine : LivePorts.GameMissing;

    internal static string FoundLine(int pid) => MonitorLines.GameFound(pid);

    internal static void AutoDetectGameDir(int pid, Paths paths, Action<string> writeLog)
    {
        try
        {
            var exe = GamePid.GameExePath(pid);
            if (exe is null) return;
            var dir = Path.GetDirectoryName(exe);
            if (string.IsNullOrEmpty(dir)) return;
            var before = paths.ReadGameDir();
            if (paths.TryWriteGameDir(dir))
            {
                writeLog("ゲームのフォルダを覚えました: " + dir
                         + (before is null ? "" : "（旧: " + before + "）"));
            }
        }
        catch (Exception)
        {
        }
    }

    internal static string? BlockedByLayer0(Layer0StampState state, string dbPath)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (!state.FileExists) return null;
        if (!state.Readable)
            return UnreadableReason + dbPath + "（" + (state.Unreadable ?? "理由不明") + "）";
        if (!state.IsLayer0)
            return NotLayer0Reason + dbPath + "（足りない表 "
                   + state.MissingTables.Count.ToString(CultureInfo.InvariantCulture) + " 個: "
                   + string.Join(" ", state.MissingTables) + "）";
        if (!state.Stamped) return NoStampReason + dbPath + "。" + NoStampHint;
        return null;
    }
}
