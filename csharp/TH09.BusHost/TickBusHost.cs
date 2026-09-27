using System.Buffers.Binary;
using System.Globalization;
using System.IO.MemoryMappedFiles;
using TH09.TickBus;
using W = TH09.Generated.TickWords;

namespace TH09.BusHost;

public static class TickBusHost
{
    public const int LiveProbeMilliseconds = 250;

    public static BusSeat EnsureHosted(Action<string> log, bool forceReset = false)
        => EnsureHosted(TickBusReader.DefaultName, log, forceReset);

    public static BusSeat EnsureHosted(string name, Action<string> log, bool forceReset = false)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentNullException.ThrowIfNull(log);
        var section = OpenSection(name, TickBusLayout.TotalSize, "共有メモリ");
        try
        {
            if (!forceReset)
            {
                string reason = LiveWriterProbe.TickBusLiveReason(section);
                if (reason.Length > 0)
                {
                    var live = new TickBusHeader(
                        LiveWriterProbe.ReadHeaderWords(section, TickBusLayout.HeaderSize));
                    string brief = LiveWriterProbe.TickBusBrief(live);
                    if (live.HookState == W.HookStates.Armed)
                    {
                        log($"警告: ARMED の Tick Bus に相乗りしました（初期化しません: {reason} / {brief}）。"
                            + "フックは接続済みですがレコードを publish していないので **tick は来ません**。"
                            + "th09_inject.exe --run で RUNNING にするか、"
                            + "この Bus を使っている別ツールを終了してください。");
                        return new BusSeat(name, TickBusLayout.TotalSize, SeatOutcome.JoinedArmed, section);
                    }
                    log($"稼働中の Tick Bus に相乗りしました（初期化しません: {reason} / {brief}）。");
                    return new BusSeat(name, TickBusLayout.TotalSize, SeatOutcome.Joined, section);
                }
            }
            var old = new TickBusHeader(
                LiveWriterProbe.ReadHeaderWords(section, TickBusLayout.HeaderSize));
            log($"Tick Bus を初期化します（{(forceReset ? "force_reset 指定 / " : "")}"
                + $"旧ヘッダ: {LiveWriterProbe.TickBusBrief(old)}）");
            WriteInitialHeader(section, InitialTickBusHeader());
            return new BusSeat(name, TickBusLayout.TotalSize,
                               forceReset ? SeatOutcome.ForcedReset : SeatOutcome.Initialized, section);
        }
        catch
        {
            section.Dispose();
            throw;
        }
    }

    public static BusSeat EnsureCoordHosted(Action<string> log, bool forceReset = false, bool enable = true)
        => EnsureCoordHosted(CoordLayout.DefaultName, log, forceReset, enable);

    public static BusSeat EnsureCoordHosted(string name, Action<string> log,
                                            bool forceReset = false, bool enable = true)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentNullException.ThrowIfNull(log);
        var section = OpenSection(name, CoordLayout.TotalSize, "座標リング");
        try
        {
            if (!forceReset)
            {
                string reason = LiveWriterProbe.CoordLiveReason(section);
                if (reason.Length > 0)
                {
                    var live = new CoordBusHeader(
                        LiveWriterProbe.ReadHeaderWords(section, CoordLayout.HeaderSize));
                    log($"稼働中の座標リングに相乗りしました（初期化しません: {reason}）");
                    return new BusSeat(name, CoordLayout.TotalSize,
                                       live.CoordState == W.HookStates.Armed
                                           ? SeatOutcome.JoinedArmed : SeatOutcome.Joined,
                                       section);
                }
            }
            var old = new CoordBusHeader(
                LiveWriterProbe.ReadHeaderWords(section, CoordLayout.HeaderSize));
            string mb = (CoordLayout.TotalSize / 1e6).ToString("F1", CultureInfo.InvariantCulture);
            log($"座標リングを初期化します（{CoordLayout.Slots} スロット / {mb} MB / enable={(enable ? 1 : 0)}）"
                + $"（{(forceReset ? "force_reset 指定 / " : "")}"
                + $"旧ヘッダ: {LiveWriterProbe.CoordBrief(old)}）");
            WriteInitialHeader(section, InitialCoordHeader(enable));
            return new BusSeat(name, CoordLayout.TotalSize,
                               forceReset ? SeatOutcome.ForcedReset : SeatOutcome.Initialized, section);
        }
        catch
        {
            section.Dispose();
            throw;
        }
    }


    private static MemoryMappedFile OpenSection(string name, long size, string what)
    {
        try
        {
            return MemoryMappedFile.CreateOrOpen(name, size);
        }
        catch (Exception exc) when (exc is IOException or UnauthorizedAccessException
                                        or ArgumentException)
        {
            throw new TickBusLayoutException($"{what}を開けません（{name}）: {exc.Message}");
        }
    }

    private static void WriteInitialHeader(MemoryMappedFile section, byte[] blob)
    {
        using var stream = section.CreateViewStream(0, blob.Length, MemoryMappedFileAccess.ReadWrite);
        stream.Write(blob, 0, blob.Length);
        stream.Flush();
    }

    private static byte[] InitialTickBusHeader()
    {
        var blob = new byte[TickBusLayout.HeaderSize];
        Put(blob, TickBusHeaderOffset(W.Header.Magic), TickBusLayout.Magic);
        Put(blob, TickBusHeaderOffset(W.Header.Version), TickBusLayout.Version);
        Put(blob, TickBusHeaderOffset(W.Header.RecordSize), (uint)TickBusLayout.RecordSize);
        Put(blob, TickBusHeaderOffset(W.Header.Capacity), (uint)TickBusLayout.Capacity);
        return blob;
    }

    private static byte[] InitialCoordHeader(bool enable)
    {
        var blob = new byte[CoordLayout.HeaderSize];
        Put(blob, CoordHeaderOffset("magic"), CoordLayout.Magic);
        Put(blob, CoordHeaderOffset("version"), CoordLayout.Version);
        Put(blob, CoordHeaderOffset("record_size"), (uint)CoordLayout.RecordSize);
        Put(blob, CoordHeaderOffset("capacity"), (uint)CoordLayout.Capacity);
        Put(blob, CoordHeaderOffset("slot_count"), (uint)CoordLayout.Slots);
        Put(blob, CoordHeaderOffset("enable"), enable ? 1u : 0u);
        return blob;
    }

    private static int TickBusHeaderOffset(string name)
        => Offset(TickBusLayout.HeaderFields, name, "Tick Bus");

    private static int CoordHeaderOffset(string name)
        => Offset(CoordLayout.HeaderFields, name, "座標リング");

    private static int Offset((string Name, int Offset)[] table, string name, string what)
    {
        foreach (var (n, off) in table)
            if (string.Equals(n, name, StringComparison.Ordinal))
                return off;
        throw new TickBusLayoutException($"{what}のヘッダに {name} という語がありません");
    }

    private static void Put(byte[] blob, int offset, uint value)
        => BinaryPrimitives.WriteUInt32LittleEndian(blob.AsSpan(offset, 4), value);
}
