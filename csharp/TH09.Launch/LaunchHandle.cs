using System.Diagnostics;

namespace TH09.Launch;

public sealed class LaunchHandle : IDisposable
{
    private readonly Process process;
    private readonly TimeSpan stopTimeout;
    private readonly Action<LaunchHandle> release;
    private int disposed;
    private int openStreams = 2;
    private readonly ManualResetEventSlim outputDrained = new(false);
    private int deliveredLines;
    private volatile bool exitDelivered;

    internal LaunchHandle(Process process, TimeSpan stopTimeout, Action<LaunchHandle> release)
    {
        this.process = process;
        this.stopTimeout = stopTimeout;
        this.release = release;

        process.OutputDataReceived += ReceiveLine;
        process.ErrorDataReceived += ReceiveLine;
        process.Exited += ProcessExited;
    }

    public event EventHandler<LaunchOutputEventArgs>? OutputLine;
    public event EventHandler? Exited;

    public bool IsRunning
    {
        get
        {
            if (exitDelivered) return false;
            try { return !process.HasExited || !outputDrained.IsSet; }
            catch (InvalidOperationException) { return false; }
        }
    }

    public int? ExitCode
    {
        get
        {
            try { return process.HasExited ? process.ExitCode : null; }
            catch (InvalidOperationException) { return null; }
        }
    }

    public bool Stop()
    {
        if (!IsRunning) return true;

        bool hasWindow = !process.StartInfo.CreateNoWindow;
        if (hasWindow)
        {
            try { process.CloseMainWindow(); }
            catch (InvalidOperationException) { }

            try
            {
                if (process.WaitForExit((int)stopTimeout.TotalMilliseconds)) return true;
            }
            catch (InvalidOperationException) { return true; }
        }

        try
        {
            process.Kill(entireProcessTree: true);
            return process.WaitForExit((int)KillConfirmTimeout.TotalMilliseconds);
        }
        catch (InvalidOperationException) { return true; }
        catch (System.ComponentModel.Win32Exception) { return false; }
    }

    private static readonly TimeSpan KillConfirmTimeout = TimeSpan.FromSeconds(1);

    private static readonly TimeSpan OutputDrainTimeout = TimeSpan.FromSeconds(2);

    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0) return;
        Stop();
        release(this);
        process.Dispose();
    }

    internal void Activate()
    {
        process.EnableRaisingEvents = true;
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
    }

    private void ReceiveLine(object sender, DataReceivedEventArgs args)
    {
        if (args.Data is not null)
        {
            OutputLine?.Invoke(this, new LaunchOutputEventArgs(args.Data));
            Interlocked.Increment(ref deliveredLines);
        }
        else if (Interlocked.Decrement(ref openStreams) == 0)
            outputDrained.Set();
    }

    private void ProcessExited(object? sender, EventArgs args)
    {
        int seen = Volatile.Read(ref deliveredLines);
        while (!outputDrained.Wait(OutputDrainTimeout))
        {
            int now = Volatile.Read(ref deliveredLines);
            if (now == seen) break;
            seen = now;
        }
        exitDelivered = true;
        release(this);
        Exited?.Invoke(this, EventArgs.Empty);
    }
}
