namespace Gleeble.Engine;

using System;
using System.IO;
using System.Runtime.InteropServices;

public readonly struct Chunk<T> where T : unmanaged
{
    public Chunk(int width, int height, Span<T> buffer)
    {
        mWidth = width;
        mHeight = height;

        int blockCount = width * width * height;
        if (buffer.Length < blockCount)
        {
            throw new ArgumentException($"Allocated memory chunk is not at least {blockCount} elements large!");
        }

        mBufferCapacity = buffer.Length;
        unsafe
        {
            fixed (T* ptr = buffer)
            {
                // will this work? nora is skeptical
                mBuffer = ptr;
            }
        }
    }

    internal int CalculateBufferIndex(Coord position)
    {
        // we want each "layer" to be stored contiguously
        // also, +y is up

        int xOffset = position.X;
        int yOffset = mWidth * mWidth * position.Y;
        int zOffset = mWidth * position.Z;

        return xOffset + yOffset + zOffset;
    }

    public T this[Coord position]
    {
        get
        {
            int index = CalculateBufferIndex(position);
            unsafe
            {
                return mBuffer[index];
            }
        }
        set
        {
            int index = CalculateBufferIndex(position);
            unsafe
            {
                mBuffer[index] = value;
            }
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

    public int Width => mWidth;
    public int Height => mHeight;

    public unsafe Span<T> AsSpan() => new Span<T>(mBuffer, mBufferCapacity);

    private readonly int mWidth, mHeight;

    private unsafe readonly T* mBuffer;
    private readonly int mBufferCapacity;
}
