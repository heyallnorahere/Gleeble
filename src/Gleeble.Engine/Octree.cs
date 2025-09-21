namespace Gleeble.Engine;

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

public sealed class Octree<T>
{
    private struct Node
    {
        public Coord Center;
        public Octree<T>? Child;
        public T? Value;
    }

    public Octree(int level)
    {
        mLevel = level;
        mNodes = new Node[8];

        int halfSize = GetNodeSize() / 2;
        for (int i = 0; i < mNodes.Length; i++)
        {
            bool isNegativeX = (i & 0x1) != 0;
            bool isNegativeY = (i & 0x2) != 0;
            bool isNegativeZ = (i & 0x4) != 0;

            mNodes[i] = new Node
            {
                Center = new Coord
                {
                    X = isNegativeX ? -halfSize : halfSize - 1,
                    Y = isNegativeY ? -halfSize : halfSize - 1,
                    Z = isNegativeZ ? -halfSize : halfSize - 1
                },

                Child = null,
                Value = default
            };
        }
    }

    private void ValidatePosition(Coord position)
    {
        int nodeSize = GetNodeSize();
        if (position.X >= nodeSize || position.X < -nodeSize ||
                position.Y >= nodeSize || position.Y < -nodeSize ||
                position.Z >= nodeSize || position.Z < -nodeSize)
        {
            throw new ArgumentOutOfRangeException();
        }
    }

    internal int GetNodeIndex(Coord position)
    {
        ValidatePosition(position);

        int xBit = position.X < 0 ? 0x1 : 0;
        int yBit = position.Y < 0 ? 0x2 : 0;
        int zBit = position.Z < 0 ? 0x4 : 0;

        return xBit | yBit | zBit;
    }

    public bool TryGet(Coord position, out T? value)
    {
        int nodeIndex = GetNodeIndex(position);
        ref var node = ref mNodes[nodeIndex];

        if (mLevel > 0)
        {
            if (node.Child is null)
            {
                value = default;
                return false;
            }

            var center = node.Center;
            var relativePosition = position - center;

            return node.Child.TryGet(relativePosition, out value);
        }
        else
        {
            value = node.Value;
            return value is not null;
        }
    }

    public T? Get(Coord position)
    {
        if (!TryGet(position, out T? value))
        {
            throw new KeyNotFoundException();
        }

        return value;
    }

    public void Set(Coord position, T? value)
    {
        int nodeIndex = GetNodeIndex(position);
        ref var node = ref mNodes[nodeIndex];

        if (mLevel > 0)
        {
            if (node.Child is null)
            {
                // subdivide
                int nodeSize = GetNodeSize();
                node.Child = new Octree<T>(nodeSize);
            }

            var center = node.Center;
            var relativePosition = position - center;

            node.Child.Set(relativePosition, value);
        }
        else
        {
            node.Value = value;
        }
    }

    public int Level => mLevel;

    public int GetNodeSize()
    {
        // probably a faster way to do this. dont really care right now tho

        int size = 1;
        for (int i = 0; i < mLevel; i++)
        {
            size *= 2;
        }

        return size;
    }

    private readonly int mLevel;
    private readonly Node[] mNodes;
}
