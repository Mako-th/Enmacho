using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using TH09.ProcView.Interop;

namespace TH09.ProcView;

public enum ModuleState
{
    Unknown = 0,

    NotLoaded = 1,

    Loaded = 2,
}

public readonly record struct ProcessEntry(int Pid, string Name);

[SupportedOSPlatform("windows")]
public static class ProcessLookup
{
    public const int NotProbed = -1;

    public static int FindPid(string exeName)
    {
        var entries = ListProcesses();
        return entries is null ? NotProbed : SelectPid(entries, exeName);
    }

    public static int SelectPid(IReadOnlyList<ProcessEntry> entries, string exeName)
    {
        for (var i = 0; i < entries.Count; i++)
        {
            if (!entries[i].Name.Equals(exeName, StringComparison.OrdinalIgnoreCase)) continue;
            return entries[i].Pid;
        }
        return 0;
    }

    public static ModuleState FindModule(int pid, string moduleName)
    {
        if (pid <= 0) return ModuleState.Unknown;
        var snapshot = NativeMethods.CreateToolhelp32Snapshot(
            NativeMethods.Th32csSnapModule | NativeMethods.Th32csSnapModule32, (uint)pid);
        if (snapshot == NativeMethods.InvalidHandle) return ModuleState.Unknown;
        try
        {
            var entry = default(NativeMethods.ModuleEntry32W);
            entry.Size = (uint)Unsafe.SizeOf<NativeMethods.ModuleEntry32W>();
            var ok = NativeMethods.Module32FirstW(snapshot, ref entry) != 0;
            if (!ok) return ModuleState.Unknown;
            while (ok)
            {
                if (Name(entry.Module).Equals(moduleName, StringComparison.OrdinalIgnoreCase))
                    return ModuleState.Loaded;
                ok = NativeMethods.Module32NextW(snapshot, ref entry) != 0;
            }
            return ModuleState.NotLoaded;
        }
        finally
        {
            NativeMethods.CloseHandle(snapshot);
        }
    }

    public static string? GameExePath(int pid)
    {
        if (pid <= 0) return null;
        var snapshot = NativeMethods.CreateToolhelp32Snapshot(
            NativeMethods.Th32csSnapModule | NativeMethods.Th32csSnapModule32, (uint)pid);
        if (snapshot == NativeMethods.InvalidHandle) return null;
        try
        {
            var entry = default(NativeMethods.ModuleEntry32W);
            entry.Size = (uint)Unsafe.SizeOf<NativeMethods.ModuleEntry32W>();
            if (NativeMethods.Module32FirstW(snapshot, ref entry) == 0) return null;
            var path = Name(entry.ExePath).ToString();
            return path.Length == 0 ? null : path;
        }
        finally
        {
            NativeMethods.CloseHandle(snapshot);
        }
    }

    private static List<ProcessEntry>? ListProcesses()
    {
        var snapshot = NativeMethods.CreateToolhelp32Snapshot(NativeMethods.Th32csSnapProcess, 0);
        if (snapshot == NativeMethods.InvalidHandle) return null;
        try
        {
            var entry = default(NativeMethods.ProcessEntry32W);
            entry.Size = (uint)Unsafe.SizeOf<NativeMethods.ProcessEntry32W>();
            if (NativeMethods.Process32FirstW(snapshot, ref entry) == 0) return null;
            var found = new List<ProcessEntry>(400);
            do
            {
                found.Add(new ProcessEntry((int)entry.ProcessId, Name(entry.ExeFile).ToString()));
            }
            while (NativeMethods.Process32NextW(snapshot, ref entry) != 0);
            return found;
        }
        finally
        {
            NativeMethods.CloseHandle(snapshot);
        }
    }

    private static ReadOnlySpan<char> Name(ReadOnlySpan<ushort> buffer)
    {
        var chars = MemoryMarshal.Cast<ushort, char>(buffer);
        var end = chars.IndexOf('\0');
        return end < 0 ? chars : chars[..end];
    }
}
