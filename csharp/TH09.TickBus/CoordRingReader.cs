namespace TH09.TickBus;

public sealed class CoordBusReader : IDisposable
{
    private readonly SharedMemoryView _view;

    public string Name { get; }

    private CoordBusReader(string name, SharedMemoryView view)
    {
        Name = name;
        _view = view;
    }

    public static CoordBusReader Open(string name)
    {
        var view = SharedMemoryView.OpenReadOnly(name);
        try
        {
            if (view.Capacity < CoordLayout.TotalSize)
                throw new TickBusLayoutException(
                    $"座標リングが小さすぎます（{view.Capacity} B / 期待 {CoordLayout.TotalSize} B 以上）。");
        }
        catch
        {
            view.Dispose();
            throw;
        }
        return new CoordBusReader(name, view);
    }

    public static CoordBusReader OpenVerified(string name)
    {
        var bus = Open(name);
        try
        {
            CoordLayout.VerifyHeader(bus.ReadHeader());
        }
        catch
        {
            bus.Dispose();
            throw;
        }
        return bus;
    }

    public CoordBusHeader ReadHeader()
    {
        var words = new uint[CoordLayout.HeaderSize / 4];
        _view.ReadWords(0, words);
        return new CoordBusHeader(words);
    }

    public uint ReadWriteIndex() => _view.ReadUInt32(0x10);

    public (uint Begin, uint End) ReadSeqPair(uint index)
    {
        long off = CoordLayout.RecordOffset(index);
        return (_view.ReadUInt32(off), _view.ReadUInt32(off + CoordLayout.RecordSize - 4));
    }

    public void ReadRecordBytes(uint index, Span<byte> dest) => _view.ReadBytes(CoordLayout.RecordOffset(index), dest);

    public byte[] ReadBytesRun(uint first, int count)
    {
        var buf = new byte[(long)count * CoordLayout.RecordSize];
        int head = (int)(first & CoordLayout.IndexMask);
        int room = CoordLayout.Capacity - head;
        long off = CoordLayout.RecordOffset(first);
        if (count <= room)
        {
            _view.ReadBytes(off, buf);
            return buf;
        }
        int firstBytes = room * CoordLayout.RecordSize;
        _view.ReadBytes(off, buf.AsSpan(0, firstBytes));
        int rest = count - room;
        _view.ReadBytes(CoordLayout.HeaderSize, buf.AsSpan(firstBytes, rest * CoordLayout.RecordSize));
        return buf;
    }

    public void Dispose() => _view.Dispose();
}

public enum CoordMiss
{
    Overrun,

    Torn,

    Unwritten,
}

public sealed class CoordWindow
{
    public required uint First { get; init; }
    public required int Count { get; init; }
    public required byte[] Raw { get; init; }
    public required uint RequestedFirst { get; init; }
    public required int RequestedCount { get; init; }
    public required int SkippedHead { get; init; }
    public required int StoppedTicks { get; init; }
    public required int UnwrittenTail { get; init; }
    public required long? Slack { get; init; }
    public required uint? StoppedAt { get; init; }
}

public sealed class CoordRingReader(CoordBusReader bus, int margin = CoordRingReader.DefaultMargin)
{
    public const int DefaultMargin = 64;

    private const uint U32Max = 0xFFFFFFFFu;
    private const uint HalfSpace = U32Max / 2;

    private readonly CoordBusReader _bus = bus;

    public long TornRecords { get; private set; }
    public long ReadTicks { get; private set; }

    public long RacedReads { get; private set; }

    public long? MinSlack { get; private set; }
    public long? LastSlack { get; private set; }

    public CoordMiss? LastMiss { get; private set; }

    public uint WriteIndex => _bus.ReadWriteIndex();

    public long SlackOf(uint first, uint writeIndex)
    {
        uint behind = unchecked(writeIndex - first);
        if (behind >= HalfSpace) behind = 0;
        return CoordLayout.Capacity - (long)behind;
    }

    private void NoteSlack(long slack)
    {
        LastSlack = slack;
        if (MinSlack is null || slack < MinSlack) MinSlack = slack;
    }

    public uint OldestIndex(uint? writeIndex = null)
    {
        uint wi = writeIndex ?? WriteIndex;
        long keep = Math.Max(0, CoordLayout.Capacity - margin);
        return unchecked(wi - (uint)Math.Min(wi, keep));
    }

    private bool SeqOk(uint idx)
    {
        var (begin, end) = _bus.ReadSeqPair(idx);
        return begin == idx && end == idx;
    }

    public CoordWindow? ReadWindow(uint first, int count)
    {
        LastSlack = null;
        LastMiss = null;
        int want = count;
        if (want <= 0) return null;

        uint wi = WriteIndex;
        uint lastWritten = unchecked(wi - 1);
        uint avail = unchecked(lastWritten - first);
        if (wi == 0 || avail >= HalfSpace)
        {
            LastMiss = CoordMiss.Unwritten;
            return null;
        }
        int usable = (int)Math.Min(want, (long)avail + 1);
        int unwrittenTail = want - usable;

        uint start = first;
        int skipped = 0;
        while (skipped < usable && !SeqOk(start))
        {
            TornRecords++;
            start = unchecked(start + 1);
            skipped++;
        }
        if (skipped >= usable)
        {
            NoteSlack(SlackOf(first, wi));
            LastMiss = CoordMiss.Overrun;
            return null;
        }

        int limit = usable - skipped;
        int got = 0;
        while (got < limit && SeqOk(unchecked(start + (uint)got))) got++;
        bool stopped = got < limit;
        if (stopped) TornRecords++;

        byte[] raw = _bus.ReadBytesRun(start, got);

        long? slack;
        try
        {
            slack = SlackOf(start, WriteIndex);
        }
        catch (Exception)
        {
            slack = null;
        }
        if (!SeqOk(start))
        {
            RacedReads++;
            if (slack is not null) NoteSlack(slack.Value);
            LastMiss = CoordMiss.Overrun;
            return null;
        }
        if (slack is not null) NoteSlack(slack.Value);
        ReadTicks += got;
        return new CoordWindow
        {
            First = start,
            Count = got,
            Raw = raw,
            RequestedFirst = first,
            RequestedCount = want,
            SkippedHead = skipped,
            Slack = slack,
            StoppedTicks = limit - got,
            UnwrittenTail = unwrittenTail,
            StoppedAt = stopped ? unchecked(start + (uint)got) : null,
        };
    }
}
