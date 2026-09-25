using System.Buffers.Binary;
using System.IO.MemoryMappedFiles;
using System.Runtime.Versioning;
using TH09.TickBus;
using W = TH09.Generated.TickWords;

namespace TH09.Drive;

public interface IInputChannel
{
    (uint Seq, bool Acked) Send(uint mask, int ticks, uint fields);

    (uint Seq, bool Acked) Cancel();

    uint? HookState();
}

[SupportedOSPlatform("windows")]
public sealed class TickBusChannel : IInputChannel, IDisposable
{
    private const int PollMilliseconds = 1;

    private readonly double _timeoutSeconds;
    private MemoryMappedFile? _section;
    private MemoryMappedViewAccessor? _header;

    public TickBusChannel(double timeoutSeconds = 1.0)
        : this(TickBusReader.DefaultName, timeoutSeconds) { }

    public TickBusChannel(string name, double timeoutSeconds = 1.0)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        _timeoutSeconds = timeoutSeconds;
        try
        {
            _section = MemoryMappedFile.OpenExisting(name, MemoryMappedFileRights.ReadWrite);
            _header = _section.CreateViewAccessor(0, TickBusLayout.HeaderSize,
                                                  MemoryMappedFileAccess.ReadWrite);
        }
        catch (Exception exc) when (exc is FileNotFoundException or IOException
                                        or UnauthorizedAccessException)
        {
            Dispose();
            throw new TickBusLayoutException(
                $"入力を書く口を開けません（{name}）: {exc.Message}"
                + "。席を作る側（監視の tickフック、または走査）が先に居る必要があります。");
        }
    }

    public (uint Seq, bool Acked) Send(uint mask, int ticks, uint fields)
    {
        var header = _header ?? throw new ObjectDisposedException(nameof(TickBusChannel));
        uint seq = Read(W.Header.CmdSeqOffset) + 1;
        if (seq == 0) seq = 1;
        Write(W.Header.CmdFieldsOffset, fields);
        Write(W.Header.CmdMaskOffset, mask);
        Write(W.Header.CmdTicksOffset, unchecked((uint)ticks));
        Write(W.Header.CmdSeqOffset, seq);
        if (_timeoutSeconds <= 0) return (seq, false);

        long deadline = Environment.TickCount64 + (long)(_timeoutSeconds * 1000);
        while (Environment.TickCount64 < deadline)
        {
            if (Read(W.Header.CmdAckOffset) == seq) return (seq, true);
            Thread.Sleep(PollMilliseconds);
        }
        return (seq, false);
        void Write(int offset, uint value) => header.Write(offset, value);
    }

    public (uint Seq, bool Acked) Cancel() => Send(0, 0, 0);

    public uint? HookState()
    {
        try
        {
            return _header is null ? null : Read(W.Header.HookStateOffset);
        }
        catch (Exception)
        {
            return null;
        }
    }

    private uint Read(int offset)
        => _header is null ? 0u : _header.ReadUInt32(offset);

    public void Dispose()
    {
        _header?.Dispose();
        _header = null;
        _section?.Dispose();
        _section = null;
    }
}

public sealed class TickBusInput : IInputSink
{
    private readonly IInputChannel _channel;
    private readonly Action<int> _sleep;

    public TickBusInput(IInputChannel channel, Action<int>? sleep = null)
    {
        ArgumentNullException.ThrowIfNull(channel);
        _channel = channel;
        _sleep = sleep ?? Thread.Sleep;
    }

    public int TapTicks { get; init; } = 6;

    public int PressMilliseconds { get; init; } = 50;

    public int GapMilliseconds { get; init; } = 33;

    public int HoldTicks { get; init; } = 100000;

    public uint Hold { get; private set; }

    public uint HoldFields { get; private set; }

    public const uint LockField = W.CmdFieldBits.Exclusive;

    public bool LockInput { get; set; }

    public int Sent { get; private set; }

    public bool Tap(uint mask, int ticks = 0)
    {
        int use = ticks == 0 ? TapTicks : ticks;
        bool acked = Write(Hold | mask, use, 0);
        _sleep(PressMilliseconds);
        Write(Hold, HoldTicks, HoldFields);
        _sleep(GapMilliseconds);
        return acked;
    }

    public bool SetHold(uint mask, uint fields = 0)
    {
        Hold = mask;
        HoldFields = mask != 0 ? fields : 0;
        return Write(mask, HoldTicks, HoldFields);
    }

    public bool Renew()
        => (Hold == 0 && !LockInput) || Write(Hold, HoldTicks, HoldFields);

    public bool ReleaseAll()
    {
        Hold = 0;
        HoldFields = 0;
        LockInput = false;
        return Write(0, 0, 0);
    }

    public uint? HookState() => _channel.HookState();

    private bool Write(uint mask, int ticks, uint fields)
    {
        Sent++;
        if (LockInput) fields |= LockField;
        bool cancel = ticks == 0 || (mask == 0 && !LockInput);
        var (_, acked) = cancel ? _channel.Cancel() : _channel.Send(mask, ticks, fields);
        return acked;
    }
}
