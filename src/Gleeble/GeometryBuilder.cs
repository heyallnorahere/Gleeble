namespace Gleeble;

using Gleeble.Engine;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

// todo: optimize! this structure is BIG (34 bytes?)
public struct VoxelVertex
{
    public Vector3 Position;
    public Vector3 Normal;
    public Vector2 TextureCoordinates;
    public uint VoxelID;
}

public interface IVoxelTextureProvider
{
    public Vector2 GetUVForVertex(byte voxelID, int vertex, int face);
}

public static class GeometryBuilder
{
    private static IVoxelTextureProvider? sTextureProvider = null;

    private static readonly uint[] sFaceIndices = new uint[]
    {
        0, 1, 2, 0, 2, 3
    };

    public static IVoxelTextureProvider? TextureProvider
    {
        get => sTextureProvider;
        set => sTextureProvider = value;
    }

    private static bool IsOutOfBounds(Coord position, int chunkSize) =>
        position.X < 0 || position.X >= chunkSize ||
        position.Y < 0 || position.Y >= chunkSize ||
        position.Z < 0 || position.Z >= chunkSize;

    private static void GenerateFace(byte voxelID, int face, Vector3 normal, Vector3 tangent, Vector3 center, List<VoxelVertex> vertices, List<uint> indices)
    {
        uint vertexOffset = (uint)vertices.Count;
        indices.AddRange(sFaceIndices.Select(index => index + vertexOffset));

        var bitangent = Vector3.Cross(normal, tangent);
        for (int i = 0; i < 4; i++)
        {
            float theta = MathF.PI * ((float)i / 2f + 1f / 4f);
            float sinTheta = MathF.Sin(theta);
            float cosTheta = MathF.Cos(theta);

            float magnitude = MathF.Sqrt(2f);
            var basePosition = new Vector2(sinTheta, cosTheta) * magnitude;

            var voxelSpacePosition = basePosition.X * tangent * basePosition.Y * bitangent + normal;
            var chunkSpacePosition = voxelSpacePosition * 0.5f + center;

            Vector2 uv = sTextureProvider?.GetUVForVertex(voxelID, i, face) ?? basePosition;
            var vertex = new VoxelVertex
            {
                Position = chunkSpacePosition,
                Normal = normal,
                TextureCoordinates = uv,
                VoxelID = (uint)voxelID,
            };

            vertices.Add(vertex);
        }
    }

    private static void BuildVoxel(Chunk<byte> chunk, Coord position, Vector3 voxelCenter, List<VoxelVertex> vertices, List<uint> indices)
    {
        byte id = chunk[position];
        if (id == 0)
        {
            return;
        }

        for (int i = 0; i < 6; i++)
        {
            int axisIndex = i / 2;
            bool negative = i % 2 != 0;

            var offset = new Coord(0)
            {
                [axisIndex] = negative ? -1 : 1
            };

            var adjacentPosition = position + offset;
            if (!IsOutOfBounds(adjacentPosition, chunk.Size))
            {
                byte adjacentID = chunk[adjacentPosition];
                if (adjacentID > 0)
                {
                    // we dont want duplicate faces
                    continue;
                }
            }

            var normal = new Vector3(offset.X, offset.Y, offset.Z);
            var tangent = new Vector3(0f)
            {
                [(axisIndex + 1) % 3] = negative ? -1f : 1f
            };

            GenerateFace(id, i, normal, tangent, voxelCenter, vertices, indices);
        }
    }

    public static void Build(Chunk<byte> chunk, out VoxelVertex[] vertices, out uint[] indices)
    {
        var vertexList = new List<VoxelVertex>();
        var indexList = new List<uint>();

        int chunkSize = chunk.Size;
        for (int y = 0; y < chunkSize; y++)
        {
            for (int x = 0; x < chunkSize; x++)
            {
                for (int z = 0; z < chunkSize; z++)
                {
                    Coord chunkPosition = (x, y, z);
                    var voxelCenter = new Vector3(x, y, z) + Vector3.One * 0.5f;

                    BuildVoxel(chunk, chunkPosition, voxelCenter, vertexList, indexList);
                }
            }
        }

        vertices = vertexList.ToArray();
        indices = indexList.ToArray();
    }
}
