using System.Diagnostics;
using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;
using TH09.TickBus;
using W = TH09.Generated.TickWords;

namespace TH09.BusHost;

internal static class LiveWriterProbe
{
    internal static uint[] ReadHeaderWords(MemoryMappedFile section, int headerSize)
    {
        var bytes = new byte[headerSize];
        using (var stream = section.CreateViewStream(0, headerSize, MemoryMappedFileAccess.Read))
            stream.ReadExactly(bytes);
        return MemoryMarshal.Cast<byte, uint>(bytes).ToArray();
    }

    internal static string TickBusLiveReason(MemoryMappedFile section)
    {
        var h = new TickBusHeader(ReadHeaderWords(section, TickBusLayout.HeaderSize));
        if (TickBusLayout.RejectReason(h) is not null) return "";
        uint state = h.HookState;
        if ((state == W.HookStates.Running || state == W.HookStates.Armed) && PidAlive(h.GamePid))
            return $"hook_state={W.HookStates.Text(state)} かつ pid={h.GamePid} が生存";
        uint first = h.WriteIndex;
        Thread.Sleep(TickBusHost.LiveProbeMilliseconds);
        var again = new TickBusHeader(ReadHeaderWords(section, TickBusLayout.HeaderSize));
        if (again.WriteIndex != first) return "write_index が進行中";
        return "";
    }

    internal static string CoordLiveReason(MemoryMappedFile section)
    {
        var h = new CoordBusHeader(ReadHeaderWords(section, CoordLayout.HeaderSize));
        if (CoordLayout.RejectReason(h) is not null) return "";
        uint state = h.CoordState;
        if ((state == W.HookStates.Running || state == W.HookStates.Armed) && PidAlive(h.GamePid))
            return $"coord_state={W.HookStates.Text(state)} かつ pid={h.GamePid} が生存";
        uint first = h.WriteIndex;
        Thread.Sleep(TickBusHost.LiveProbeMilliseconds);
        var again = new CoordBusHeader(ReadHeaderWords(section, CoordLayout.HeaderSize));
        if (again.WriteIndex != first) return "write_index が進行中";
        return "";
    }

    internal static string TickBusBrief(TickBusHeader h)
        => $"magic=0x{h.Magic:X8} write_index={h.WriteIndex} "
           + $"hook_state={W.HookStates.Text(h.HookState)} game_pid={h.GamePid}";

    internal static string CoordBrief(CoordBusHeader h)
        => $"magic=0x{h.Magic:X8} write_index={h.WriteIndex} "
           + $"coord_state={W.HookStates.Text(h.CoordState)} game_pid={h.GamePid}";

    internal static bool PidAlive(uint pid)
    {
        if (pid == 0 || pid > int.MaxValue) return false;
        try
        {
            using var proc = Process.GetProcessById((int)pid);
            return !proc.HasExited;
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (Exception)
        {
            return true;
        }
    }
}
