namespace Gleeble.Engine;

using System;
using System.Diagnostics.CodeAnalysis;

public struct Coord : IEquatable<Coord>
{
    public Coord(int scalar)
    {
        X = Y = Z = scalar;
    }

    public Coord(int x, int y, int z)
    {
        X = x;
        Y = y;
        Z = z;
    }

    public int this[int index]
    {
        get => index switch
        {
            0 => X,
            1 => Y,
            2 => Z,
            _ => throw new IndexOutOfRangeException()
        };
        set
        {
            switch (index)
            {
                case 0:
                    X = value;
                    break;
                case 1:
                    Y = value;
                    break;
                case 2:
                    Z = value;
                    break;
                default:
                    throw new IndexOutOfRangeException();
            }
        }
    }

    public bool Equals(Coord other)
    {
        return X == other.X && Y == other.Y && Z == other.Z;
    }

    public override bool Equals([NotNullWhen(true)] object? obj)
    {
        if (obj is not Coord other)
        {
            return false;
        }

        return Equals(other);
    }

    public override int GetHashCode()
    {
        int xHash = (int)(((uint)X & 0xFFFF) << 16) | (int)(((uint)X & 0xFFFF0000) >> 16);
        int yHash = (int)(((uint)Y & 0xFFFFFF) << 8) | (int)(((uint)Y & 0xFF000000) >> 24);
        int zHash = Z;

        return xHash ^ yHash ^ zHash;
    }

    public override string ToString() => $"<{X}, {Y}, {Z}>";

    public static Coord operator +(Coord lhs, Coord rhs) => new Coord
    {
        X = lhs.X + rhs.X,
        Y = lhs.Y + rhs.Y,
        Z = lhs.Z + rhs.Z
    };

    public static Coord operator -(Coord c) => (-c.X, -c.Y, -c.Z);
    public static Coord operator -(Coord lhs, Coord rhs) => lhs + -rhs;

    public static bool operator ==(Coord lhs, Coord rhs) => lhs.Equals(rhs);
    public static bool operator !=(Coord lhs, Coord rhs) => !lhs.Equals(rhs);

    public static implicit operator Coord((int x, int y, int z) t) => new Coord(t.x, t.y, t.z);

    public int X, Y, Z;
}
