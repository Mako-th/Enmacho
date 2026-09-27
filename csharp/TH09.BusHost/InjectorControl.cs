using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using W = TH09.Generated.TickWords;

namespace TH09.BusHost;

public sealed class InjectorException : Exception
{
    public InjectorException(string message) : base(message) { }

    public InjectorException(string message, Exception inner) : base(message, inner) { }
}

public sealed record HookStatus(uint? State, uint? Error, string? ErrorName,
                                bool? Mapped, uint? Ticks, string Raw)
{
    public bool Running => State == W.HookStates.Running && Mapped == true;

    public string ErrorText()
        => Error is null ? "?"
           : Error.Value.ToString(CultureInfo.InvariantCulture)
             + (string.IsNullOrEmpty(ErrorName) ? "" : $"（{ErrorName}）");

    public override string ToString()
        => State is null
           ? "状態不明"
           : $"hook_state={W.HookStates.Text(State.Value)} / last_error={ErrorText()}"
             + $" / bus={(Mapped == true ? "mapped" : "not mapped")}";
}

public sealed record HookRun(bool Ok, string Detail, HookStatus? Status);

public static partial class InjectorControl
{
    public const int TimeoutSeconds = 25;

    private const uint ErrorAlready = 1u;

    private static readonly Encoding OutputEncoding = MakeOutputEncoding();

    private static Encoding MakeOutputEncoding()
    {
        try
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            return Encoding.GetEncoding(932, EncoderFallback.ReplacementFallback,
                                        DecoderFallback.ReplacementFallback);
        }
        catch (Exception)
        {
            return new UTF8Encoding(false);
        }
    }

    [GeneratedRegex(@"hook_state\s*:\s*(\d+)(?:\s*\(([^)]*)\))?", RegexOptions.CultureInvariant)]
    private static partial Regex StateRegex();

    [GeneratedRegex(@"last_error\s*:\s*(\d+)(?:\s*\(([^)]*)\))?", RegexOptions.CultureInvariant)]
    private static partial Regex ErrorRegex();

    [GeneratedRegex(@"tick bus\s*:\s*(mapped|not mapped)", RegexOptions.CultureInvariant)]
    private static partial Regex MappedRegex();

    [GeneratedRegex(@"ticks\s*:\s*(\d+)", RegexOptions.CultureInvariant)]
    private static partial Regex TicksRegex();

    [GeneratedRegex(@"result\s*:\s*(\d+)(?:\s*\(([^)]*)\))?", RegexOptions.CultureInvariant)]
    private static partial Regex ResultRegex();

    [GeneratedRegex(@"vpatch_skip\s*:\s*(\d+)(?:\s*\((.*)\))?", RegexOptions.CultureInvariant)]
    private static partial Regex VpatchRegex();

    public static HookStatus QueryStatus(string nativeDir, int pid)
        => ParseStatus(Run(nativeDir, ["--status", "--pid", Num(pid)]).Output);

    public static (bool Ok, string Detail) Detach(string nativeDir, int pid)
    {
        var (code, output) = Run(nativeDir, ["--detach", "--pid", Num(pid)]);
        return (code == 0, output.Trim());
    }

    public static (bool Ok, string Detail) SetVpatchSkip(string nativeDir, int pid, bool on)
    {
        var (code, output) = Run(nativeDir,
                                 [on ? "--vpatch-on" : "--vpatch-off", "--pid", Num(pid)]);
        var state = VpatchRegex().Match(output);
        var error = ErrorRegex().Match(output);
        string where = state.Success
            ? state.Groups[1].Value
              + (state.Groups[2].Success ? $"（{state.Groups[2].Value}）" : "")
            : "状態不明";
        if (code == 0 && state.Success)
            return (true, (on ? "vpatch の参照先を差し替えました: " : "vpatch の参照先を戻しました: ") + where);
        string why = error.Success
            ? error.Groups[1].Value
              + (error.Groups[2].Success ? $"（{error.Groups[2].Value}）" : "")
            : "理由不明";
        return (false, (on ? "vpatch の参照先を差し替えられません" : "vpatch の参照先を戻せません")
                       + $"（いまの参照先 {where} / last_error={why}）");
    }

    public static HookRun EnsureHookRunning(BusSeat seat, int pid, string nativeDir)
    {
        ArgumentNullException.ThrowIfNull(seat);
        ObjectDisposedException.ThrowIf(!seat.IsOpen, seat);

        HookStatus status;
        try
        {
            status = QueryStatus(nativeDir, pid);
        }
        catch (InjectorException exc)
        {
            return new HookRun(false, exc.Message, null);
        }

        if (status.Running) return new HookRun(true, $"既に稼働中（{status}）", status);

        if (status.State != W.HookStates.Armed && status.State != W.HookStates.Running)
        {
            int code;
            string output;
            try
            {
                (code, output) = Run(nativeDir, ["--attach", "--pid", Num(pid)]);
            }
            catch (InjectorException exc)
            {
                return new HookRun(false, exc.Message, status);
            }
            status = ParseStatus(output);
            if (status.Running) return new HookRun(true, $"注入しました（{status}）", status);

            var result = ResultRegex().Match(output);
            uint? resultCode = result.Success
                ? uint.Parse(result.Groups[1].Value, CultureInfo.InvariantCulture) : null;
            if (code != 0 && resultCode is not null
                && resultCode != 0 && resultCode != ErrorAlready)
            {
                string name = result.Groups[2].Success ? result.Groups[2].Value : "";
                return new HookRun(false, "注入に失敗しました: "
                    + resultCode.Value.ToString(CultureInfo.InvariantCulture)
                    + (name.Length > 0 ? $"（{name}）" : ""), status);
            }
            if (code != 0 && resultCode is null)
                return new HookRun(false, "注入に失敗しました:" + Environment.NewLine + Indent(output),
                                   status);
        }

        try
        {
            Run(nativeDir, ["--run", "--pid", Num(pid)]);
            status = QueryStatus(nativeDir, pid);
        }
        catch (InjectorException exc)
        {
            return new HookRun(false, exc.Message, status);
        }
        return status.Running
            ? new HookRun(true, $"RUNNING へ切り替えました（{status}）", status)
            : new HookRun(false, $"RUNNING にできませんでした（{status}）", status);
    }


    internal static HookStatus ParseStatus(string text)
    {
        var state = StateRegex().Match(text);
        var error = ErrorRegex().Match(text);
        var mapped = MappedRegex().Match(text);
        var ticks = TicksRegex().Match(text);
        return new HookStatus(
            state.Success ? uint.Parse(state.Groups[1].Value, CultureInfo.InvariantCulture) : null,
            error.Success ? uint.Parse(error.Groups[1].Value, CultureInfo.InvariantCulture) : null,
            error.Success && error.Groups[2].Success ? error.Groups[2].Value : null,
            mapped.Success ? mapped.Groups[1].Value == "mapped" : null,
            ticks.Success ? uint.Parse(ticks.Groups[1].Value, CultureInfo.InvariantCulture) : null,
            text.Trim());
    }

    private static (int Code, string Output) Run(string nativeDir, string[] args)
    {
        string exe = InjectorPaths.ExistingInjectorExe(nativeDir)
            ?? throw new InjectorException(
                $"{InjectorPaths.InjectorExeName} がありません: {InjectorPaths.InjectorExe(nativeDir)}"
                + Environment.NewLine
                + "       native の th09_inject の build.bat を実行してください。");

        var info = new ProcessStartInfo(exe)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = OutputEncoding,
            StandardErrorEncoding = OutputEncoding,
            WorkingDirectory = InjectorPaths.InjectorDir(nativeDir),
        };
        foreach (var arg in args) info.ArgumentList.Add(arg);

        Process? proc;
        try
        {
            proc = Process.Start(info);
        }
        catch (Exception exc) when (exc is System.ComponentModel.Win32Exception
                                        or InvalidOperationException or IOException)
        {
            throw new InjectorException(
                $"{InjectorPaths.InjectorExeName} を起動できません: {exc.Message}", exc);
        }
        if (proc is null)
            throw new InjectorException($"{InjectorPaths.InjectorExeName} を起動できません（プロセスが返りませんでした）");

        using (proc)
        {
            var outText = proc.StandardOutput.ReadToEndAsync();
            var errText = proc.StandardError.ReadToEndAsync();
            if (!proc.WaitForExit(TimeoutSeconds * 1000))
            {
                try { proc.Kill(entireProcessTree: true); }
                catch (Exception) { }
                throw new InjectorException(
                    $"{InjectorPaths.InjectorExeName} が {TimeoutSeconds} 秒で終わりませんでした"
                    + $"（{string.Join(" ", args)}）");
            }
            proc.WaitForExit();
            return (proc.ExitCode, outText.GetAwaiter().GetResult() + errText.GetAwaiter().GetResult());
        }
    }

    private static string Num(int value) => value.ToString(CultureInfo.InvariantCulture);

    private static string Indent(string text)
        => string.Join(Environment.NewLine,
                       text.Trim().Split('\n').Select(line => "       " + line.TrimEnd('\r')));
}
