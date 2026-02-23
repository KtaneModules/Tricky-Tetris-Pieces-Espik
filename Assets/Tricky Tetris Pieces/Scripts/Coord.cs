/// Written by Timwi - repurposed from The Blue Button

using System;
using System.Collections.Generic;
using System.Linq;

public struct Coord : IEquatable<Coord>
{
    public int Index { get; private set; }
    public int Width { get; private set; }
    public int Height { get; private set; }
    public int X => Index % Width;
    public int Y => Index / Width;

    public Coord(int width, int height, int index)
    {
        Index = index;
        Width = width;
        Height = height;
    }

    public Coord(int width, int height, int x, int y) : this(width, height, x + width * y) { }
    public override string ToString() => $"({X}, {Y})/({Width}×{Height})";

    public Coord AddXWrap(int dx) => new Coord(Width, Height, ((X + dx) % Width + Width) % Width, Y);
    public Coord AddYWrap(int dy) => new Coord(Width, Height, X, ((Y + dy) % Height + Height) % Height);
    public Coord AddWrap(int dx, int dy) => new Coord(Width, Height, ((X + dx) % Width + Width) % Width, ((Y + dy) % Height + Height) % Height);
    public Coord AddWrap(Coord c) => AddWrap(c.X, c.Y);

    public bool Equals(Coord other) => other.Index == Index && other.Width == Width && other.Height == Height;
    public override bool Equals(object obj) => obj is Coord && Equals((Coord) obj);
    public override int GetHashCode() => unchecked(Index * 1048583 + Width * 1031 + Height);

    public static bool operator ==(Coord one, Coord two) => one.Equals(two);
    public static bool operator !=(Coord one, Coord two) => !one.Equals(two);

    public static IEnumerable<Coord> Cells(int w, int h) => Enumerable.Range(0, w * h).Select(ix => new Coord(w, h, ix));
    public bool AdjacentToWrap(Coord other) => other == AddXWrap(1) || other == AddXWrap(-1) || other == AddYWrap(1) || other == AddYWrap(-1);
    public bool AdjacentTo(Coord other) => (CanMoveBy(-1, 0) && AddXWrap(-1) == other) ||
        (CanMoveBy(1, 0) && AddXWrap(1) == other) ||
        (CanMoveBy(0, -1) && AddYWrap(-1) == other) ||
        (CanMoveBy(0, 1) && AddYWrap(1) == other);

    public bool CanMoveBy(int x, int y) => (X + x) >= 0 && (X + x) < Width && (Y + y) >= 0 && (Y + y) < Height;
}
