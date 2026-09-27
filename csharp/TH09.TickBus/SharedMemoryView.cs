using System.IO.MemoryMappedFiles;
using System.Runtime.Versioning;

[assembly: SupportedOSPlatform("windows")]

namespace TH09.TickBus;

public sealed unsafe class SharedMemoryView : IDisposable
{
    private readonly MemoryMappedFile _mmf;
    private readonly MemoryMappedViewAccessor _view;
    private byte* _ptr;
    private bool _disposed;

    private SharedMemoryView(MemoryMappedFile mmf, MemoryMappedViewAccessor view)
    {
        _mmf = mmf;
        _view = view;
        _view.SafeMemoryMappedViewHandle.AcquirePointer(ref _ptr);
        _ptr += _view.PointerOffset;
    }

    public static SharedMemoryView OpenReadOnly(string name)
    {
        MemoryMappedFile mmf;
        try
        {
            mmf = MemoryMappedFile.OpenExisting(name, MemoryMappedFileRights.Read);
        }
        catch (FileNotFoundException exc)
        {
            throw new TickBusLayoutException(
                $"共有メモリがありません（{name}）: {exc.Message}。" +
                "★作りません ——Python 側（所有者）が動いていない状態で作ると、DLL が偽の Bus を掴みます。");
        }
        MemoryMappedViewAccessor view;
        try
        {
            view = mmf.CreateViewAccessor(0, 0, MemoryMappedFileAccess.Read);
        }
        catch
        {
            mmf.Dispose();
            throw;
        }
        return new SharedMemoryView(mmf, view);
    }

    public long Capacity => _view.Capacity;

    public uint ReadUInt32(long offset)
    {
        EnsureRange(offset, 4);
        return *(uint*)(_ptr + offset);
    }

    public void ReadWords(long offset, Span<uint> dest)
    {
        EnsureRange(offset, (long)dest.Length * 4);
        new ReadOnlySpan<uint>(_ptr + offset, dest.Length).CopyTo(dest);
    }

    public void ReadBytes(long offset, Span<byte> dest)
    {
        EnsureRange(offset, dest.Length);
        new ReadOnlySpan<byte>(_ptr + offset, dest.Length).CopyTo(dest);
    }

    private void EnsureRange(long offset, long length)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (offset < 0 || length < 0 || offset + length > Capacity)
            throw new TickBusLayoutException(
                $"共有メモリの範囲外を読もうとしました（offset={offset} length={length} capacity={Capacity}）");
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (_ptr != null)
        {
            _view.SafeMemoryMappedViewHandle.ReleasePointer();
            _ptr = null;
        }
        _view.Dispose();
        _mmf.Dispose();
    }
}
