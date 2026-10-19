using System.Buffers;
using System.Buffers.Binary;

namespace LastEpochLAN.Core;

// One rented frame including its prefix; avoids SerializeToUtf8Bytes followed
// by another equally large payload copy for every world chunk.
internal sealed class PooledFrameBuffer : IBufferWriter<byte>,IDisposable
{
    private byte[] buffer=ArrayPool<byte>.Shared.Rent(4096);
    private int written=4;
    public void Advance(int count)
    {if(count<0 || count>buffer.Length-written) throw new ArgumentOutOfRangeException(nameof(count));written+=count;}
    public Memory<byte> GetMemory(int sizeHint=0) {Ensure(sizeHint);return buffer.AsMemory(written);}
    public Span<byte> GetSpan(int sizeHint=0) {Ensure(sizeHint);return buffer.AsSpan(written);}
    private void Ensure(int size)
    {
        size=Math.Max(1,size);
        if(size>Wire.MaximumFrameBytes+4096-written) throw new InvalidDataException("Frame too large.");
        if(buffer.Length-written>=size) return;
        var grown=ArrayPool<byte>.Shared.Rent(Math.Max(written+size,Math.Min(buffer.Length*2,Wire.MaximumFrameBytes+4096)));
        buffer.AsSpan(0,written).CopyTo(grown);ArrayPool<byte>.Shared.Return(buffer);buffer=grown;
    }
    public ReadOnlyMemory<byte> Frame()
    {
        int size=written-4;
        if(size>Wire.MaximumFrameBytes) throw new InvalidDataException("Frame too large.");
        BinaryPrimitives.WriteInt32BigEndian(buffer.AsSpan(0,4),size);return buffer.AsMemory(0,written);
    }
    public void Dispose() {var old=buffer;buffer=Array.Empty<byte>();if(old.Length>0)ArrayPool<byte>.Shared.Return(old);}
}
