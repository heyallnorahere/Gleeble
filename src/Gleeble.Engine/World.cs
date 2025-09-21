namespace Gleeble.Engine;

using System;

using Varena;

public sealed class World : IDisposable
{
    public World()
    {
        mDisposed = false;

        mArenaManager = new VirtualArenaManager();
        mDataBuffer = mArenaManager.CreateBuffer("Chunk data", 1 << 30);
        mChunkArray = mArenaManager.CreateArray<Chunk<byte>>("Chunk metadata", 1 << 20);
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
            mChunkArray.Dispose();
            mDataBuffer.Dispose();
            mArenaManager.Dispose();
        }
    }

    private readonly VirtualArenaManager mArenaManager;
    private readonly VirtualBuffer mDataBuffer;
    private readonly VirtualArray<Chunk<byte>> mChunkArray;

    private bool mDisposed;
}
