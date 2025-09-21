namespace Gleeble.Engine;

using System;
using System.IO;
using System.Runtime.InteropServices;

public sealed class Chunk<T> where T : unmanaged
{
    public Chunk(int size, Span<T> buffer)
    {
        mSize = size;

        int blockCount = size * size * size;
        if (buffer.Length < blockCount)
        {
            throw new ArgumentException($"Allocated memory chunk is not at least {blockCount} elements large!");
        }

        unsafe
        {
            // should stay fixed because its arena-allocated
            fixed (T* ptr = buffer)
            {
                mBuffer = ptr;
            }
        }
    }

    public void Clear()
    {
        var span = AsSpan();
        span.Fill(default);
    }

    internal int CalculateBufferIndex(Coord position)
    {
        // we want each "layer" to be stored contiguously
        // also, +y is up

        int xOffset = position.X;
        int yOffset = mSize * mSize * position.Y;
        int zOffset = mSize * position.Z;

        return xOffset + yOffset + zOffset;
    }

    public T this[Coord position]
    {
        get
        {
            int index = CalculateBufferIndex(position);

            var span = AsSpan();
            return span[index];
        }
        set
        {
            int index = CalculateBufferIndex(position);

            var span = AsSpan();
            span[index] = value;
        }
    }

    public T this[int x, int y, int z]
    {
        get => this[(x, y, z)];
        set => this[(x, y, z)] = value;
    }

    public void Deserialize(Stream input)
    {
        var span = AsSpan();
        var bufferData = MemoryMarshal.Cast<T, byte>(span);

        do
        {
            int bytesRead = input.Read(bufferData);
            if (bytesRead <= 0)
            {
                throw new EndOfStreamException();
            }

            bufferData = bufferData.Slice(bytesRead);
        }
        while (bufferData.Length > 0);

        // todo: deserialize state data as well?
    }

    public void Serialize(Stream output)
    {
        var span = AsSpan();
        var bufferData = MemoryMarshal.Cast<T, byte>(span);

        output.Write(bufferData);
        output.Flush();

        // todo: serialize state data as well?
    }

    public int Size => mSize;

    public unsafe Span<T> AsSpan() => new Span<T>(mBuffer, mSize * mSize * mSize);

    private readonly int mSize;
    private readonly unsafe T* mBuffer;
}
