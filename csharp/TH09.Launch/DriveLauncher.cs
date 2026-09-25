using System.Diagnostics;
using System.Text;

namespace TH09.Launch;

public static class DriveLauncher
{
    private static readonly object Gate = new();
    private static readonly Dictionary<LaunchKind, LaunchHandle> Active = [];

    public static LaunchHandle Start(LaunchKind kind, LaunchOptions? options = null)
    {
        options ??= LaunchOptions.Default;
        options.Validate(kind);

        lock (Gate)
        {
            if (Active.TryGetValue(kind, out var old) && old.IsRunning)
                throw new InvalidOperationException($"{kind} は既に動いています");
            if (old is not null)
                Active.Remove(kind);

            string executable = DriveExecutable.Path;
            if (!File.Exists(executable))
                throw new FileNotFoundException($"{DriveExecutable.FileName} がありません", executable);

            var info = new ProcessStartInfo(executable)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = new UTF8Encoding(false),
                StandardErrorEncoding = new UTF8Encoding(false),
                WorkingDirectory = AppContext.BaseDirectory,
            };
            foreach (string argument in Arguments(kind, options))
                info.ArgumentList.Add(argument);

            Process process = Process.Start(info)
                ?? throw new InvalidOperationException($"{DriveExecutable.FileName} を起動できません");
            try
            {
                JobObject.Shared.Assign(process);
                var handle = new LaunchHandle(process, options.StopTimeout, h => Release(kind, h));
                Active[kind] = handle;
                handle.Activate();
                return handle;
            }
            catch
            {
                try { process.Kill(entireProcessTree: true); }
                catch (Exception) { }
                process.Dispose();
                throw;
            }
        }
    }

    public static IReadOnlyList<string> PreviewArguments(LaunchKind kind, LaunchOptions? options = null)
    {
        options ??= LaunchOptions.Default;
        options.Validate(kind);
        return Arguments(kind, options);
    }

    private static IReadOnlyList<string> Arguments(LaunchKind kind, LaunchOptions options) => kind switch
    {
        LaunchKind.Monitor => [DriveFlags.Monitor],
        LaunchKind.Watch => [DriveFlags.Watch],
        LaunchKind.ImportOnly => [DriveFlags.Scan, DriveFlags.Run, DriveFlags.RunImportOnly],
        LaunchKind.Scan => [DriveFlags.Scan, .. options.Scan!.ToArguments()],
        LaunchKind.BuildLayer1 => [DriveFlags.BuildLayer1, .. options.Layer1!.ToArguments()],
        LaunchKind.RestoreSlots => [DriveFlags.Scan, DriveFlags.RestoreSlots],
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    private static void Release(LaunchKind kind, LaunchHandle handle)
    {
        lock (Gate)
        {
            if (Active.TryGetValue(kind, out var current) && ReferenceEquals(current, handle))
                Active.Remove(kind);
        }
    }
}
