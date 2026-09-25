using W = TH09.Generated.TickWords;

namespace TH09.TickBus;

public readonly struct TickBusHeader(uint[] words)
{
    public uint[] Words { get; } = words;

    public uint At(int byteOffset) => Words[byteOffset >> 2];

    public uint Magic => At(W.Header.MagicOffset);
    public uint Version => At(W.Header.VersionOffset);

    public uint RecordSizeWord => At(W.Header.RecordSizeOffset);

    public uint CapacityWord => At(W.Header.CapacityOffset);

    public uint WriteIndex => At(W.Header.WriteIndexOffset);
    public uint HookState => At(W.Header.HookStateOffset);
    public uint LastError => At(W.Header.LastErrorOffset);
    public uint GamePid => At(W.Header.GamePidOffset);
    public uint CmdSeq => At(W.Header.CmdSeqOffset);
    public uint CmdAck => At(W.Header.CmdAckOffset);
}

public readonly struct TickRecord(uint[] words)
{
    public uint[] Words { get; } = words;

    public uint At(int byteOffset) => Words[byteOffset >> 2];

    public uint Seq => At(W.Record.SeqBeginOffset);

    public uint SeqEnd => At(W.Record.SeqEndOffset);
}
