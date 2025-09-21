namespace Gleeble.Engine;

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;

using Varena;

public interface IChunkGeneration<T> where T : unmanaged
{
    public void GenerateChunk(Coord position, Chunk<T> chunk);
}

public interface IChunkSerializer
{
    public Stream OpenChunkForReading(Coord position);
    public Stream OpenChunkForWriting(Coord position);
}

public class World<T> : IDisposable where T : unmanaged
{
    public World(int octreeLevels, int chunkSize)
    {
        mDisposed = false;

        // 8 gigs is reasonable?
        mArenaManager = new VirtualArenaManager();
        mDataBuffer = mArenaManager.CreateArray<T>("Chunk data", (nuint)8 << 30);
        mAllocatedChunks = new Queue<int>();

        mLoadedChunks = new Dictionary<Coord, int>();
        mChunks = new Octree<Chunk<T>>(octreeLevels);
        mChunkSize = chunkSize;
    }

    ~World()
    {
        if (!mDisposed)
        {
            Dispose(false);
        }
    }

    public void Dispose()
    {
        if (mDisposed)
        {
            return;
        }

        Dispose(true);
        GC.SuppressFinalize(this);

        mDisposed = true;
    }

    private void Dispose(bool disposing)
    {
        if (disposing)
        {
            mDataBuffer.Dispose();
            mArenaManager.Dispose();
        }
    }

    public IChunkGeneration<T>? Generation
    {
        get => mGeneration;
        set => mGeneration = value;
    }

    public IChunkSerializer? Serializer
    {
        get => mSerializer;
        set => mSerializer = value;
    }

    private Span<T> GetMemoryChunk(out int arrayOffset)
    {
        int blockCount = mChunkSize * mChunkSize * mChunkSize;
        if (mAllocatedChunks.Count > 0)
        {
            arrayOffset = mAllocatedChunks.Dequeue();
            return mDataBuffer.AsSpan(arrayOffset, blockCount);
        }

        return mDataBuffer.AllocateRange(blockCount, out arrayOffset);
    }

    private bool TryLoadChunk(Coord position, Chunk<T> chunk)
    {
        if (mSerializer is null)
        {
            return false;
        }

        try
        {
            using var input = mSerializer.OpenChunkForReading(position);
            chunk.Deserialize(input);
        }
        catch (FileNotFoundException)
        {
            return false;
        }
        catch (EndOfStreamException)
        {
            return false;
        }

        return true;
    }

    private void GenerateNewChunk(Coord position, Chunk<T> chunk)
    {
        if (mGeneration is null)
        {
            chunk.Clear();
            return;
        }

        mGeneration.GenerateChunk(position, chunk);
    }

    private void SaveChunk(Coord position, Chunk<T> chunk)
    {
        if (mSerializer is null)
        {
            return;
        }

        using var stream = mSerializer.OpenChunkForWriting(position);
        chunk.Serialize(stream);
    }

    public void LoadChunk(Coord position)
    {
        var memoryChunk = GetMemoryChunk(out int arrayOffset);
        var chunk = new Chunk<T>(mChunkSize, memoryChunk);

        if (!TryLoadChunk(position, chunk))
        {
            GenerateNewChunk(position, chunk);
            SaveChunk(position, chunk);
        }

        mLoadedChunks.Add(position, arrayOffset);
    }

    public bool TryGetChunk(Coord position, [NotNullWhen(true)] out Chunk<T>? chunk)
    {
        if (!mChunks.TryGet(position, out chunk))
        {
            return false;
        }

        return chunk is not null;
    }

    public Chunk<T> GetChunk(Coord position)
    {
        if (!TryGetChunk(position, out Chunk<T>? chunk))
        {
            throw new KeyNotFoundException();
        }

        return chunk;
    }

    public IEnumerable<Coord> LoadedChunks => mLoadedChunks.Keys;
    public int ChunkSize => mChunkSize;

    private readonly VirtualArenaManager mArenaManager;
    private readonly VirtualArray<T> mDataBuffer;
    private readonly Queue<int> mAllocatedChunks;

    private readonly Dictionary<Coord, int> mLoadedChunks;
    private readonly Octree<Chunk<T>> mChunks;
    private readonly int mChunkSize;

    private IChunkGeneration<T>? mGeneration;
    private IChunkSerializer? mSerializer;

    private bool mDisposed;
}
