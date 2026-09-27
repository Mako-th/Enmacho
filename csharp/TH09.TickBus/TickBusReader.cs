namespace TH09.TickBus;

public sealed class TickBusReader : IDisposable
{
    public const string DefaultName = @"Local\TH09TickBus";

    private readonly SharedMemoryView _view;

    public string Name { get; }

    private TickBusReader(string name, SharedMemoryView view)
    {
        Name = name;
        _view = view;
    }

    public static TickBusReader OpenVerified(string name)
    {
        var bus = Open(name);
        try
        {
            TickBusLayout.VerifyHeader(bus.ReadHeader());
        }
        catch
        {
            bus.Dispose();
            throw;
        }
        return bus;
    }

    public static TickBusReader Open(string name)
    {
        var view = SharedMemoryView.OpenReadOnly(name);
        try
        {
            if (view.Capacity < TickBusLayout.TotalSize)
                throw new TickBusLayoutException(
                    $"共有メモリが小さすぎます（{view.Capacity} B / 期待 {TickBusLayout.TotalSize} B 以上）。" +
                    "Tick Bus ではないか、世代が違います。");
        }
        catch
        {
            view.Dispose();
            throw;
        }
        return new TickBusReader(name, view);
    }

    public TickBusHeader ReadHeader()
    {
        var words = new uint[TickBusLayout.HeaderWordCount];
        _view.ReadWords(0, words);
        return new TickBusHeader(words);
    }

    public uint ReadWriteIndex()
        => _view.ReadUInt32(TH09.Generated.TickWords.Header.WriteIndexOffset);

    public void ReadRecordWords(uint index, Span<uint> dest)
        => _view.ReadWords(TickBusLayout.RecordOffset(index), dest);

    public void Dispose() => _view.Dispose();
}

public sealed class RingReader(TickBusReader bus, int margin = RingReader.DefaultMargin)
{
    public const int DefaultMargin = 64;

    private readonly TickBusReader _bus = bus;

    public uint? NextIndex { get; set; }

    public long GapEvents { get; private set; }

    public long LostRecords { get; private set; }

    public long TornRecords { get; private set; }

    public long ReadRecords { get; private set; }

    public int Rewound { get; private set; }

    public Action<string> Log { get; set; } = static msg => Console.Error.WriteLine(msg);

    private int Keep => Math.Max(0, TickBusLayout.Capacity - margin);

    public void StartAtHead(int rewind = 0)
    {
        uint head = _bus.ReadWriteIndex();
        int back = Math.Max(0, (int)Math.Min(Math.Min((long)rewind, Keep), head));
        Rewound = back;
        NextIndex = unchecked(head - (uint)back);
    }

    public List<TickRecord> Poll(int limit = 100000)
    {
        uint headWriteIndex = _bus.ReadWriteIndex();
        if (NextIndex is null)
        {
            NextIndex = headWriteIndex;
            return [];
        }

        uint behind = unchecked(headWriteIndex - NextIndex.Value);
        if (behind > Keep)
            Resync(headWriteIndex, $"reader が {behind} 件遅れています");

        var outList = new List<TickRecord>();
        while (NextIndex!.Value != headWriteIndex && outList.Count < limit)
        {
            uint idx = NextIndex.Value;
            if (idx == TickBusLayout.SeqBuilding)
            {
                NextIndex = 0;
                continue;
            }
            var words = new uint[TickBusLayout.RecordWordCount];
            _bus.ReadRecordWords(idx, words);
            var rec = new TickRecord(words);
            if (rec.Seq != idx || rec.SeqEnd != idx)
            {
                TornRecords++;
                behind = unchecked(headWriteIndex - idx);
                if (behind > Keep)
                {
                    Resync(headWriteIndex, $"idx={idx} の seq が不一致 (begin={rec.Seq} end={rec.SeqEnd})");
                    headWriteIndex = _bus.ReadWriteIndex();
                }
                else
                {
                    LostRecords++;
                    GapEvents++;
                    NextIndex = unchecked(idx + 1);
                    Log($"欠落を検出しました（idx={idx} の seq が不一致 begin={rec.Seq} end={rec.SeqEnd}）: この 1 件を捨てます。"
                        + $"累計 欠落{LostRecords}件 / 再同期{GapEvents}回");
                }
                continue;
            }
            outList.Add(rec);
            ReadRecords++;
            NextIndex = unchecked(idx + 1);
        }
        return outList;
    }

    private void Resync(uint writeIndex, string reason)
    {
        uint newNext = unchecked(writeIndex - (uint)Keep);
        uint behind = unchecked(writeIndex - NextIndex!.Value);
        long lost = Math.Max(0, (long)behind - Keep);
        LostRecords += lost;
        GapEvents++;
        NextIndex = newNext;
        Log($"欠落を検出しました（{reason}）: write_index={writeIndex} → {lost} 件を捨てて idx {newNext} から再同期します。"
            + $"累計 欠落{LostRecords}件 / 再同期{GapEvents}回");
    }
}
