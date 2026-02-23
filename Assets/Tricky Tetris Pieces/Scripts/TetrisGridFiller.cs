using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

static class TetrisGridFiller
{
    const int _gridWidth = 10;
    const int _gridHeight = 20;

    public static TetrisFillResult GenerateTetrisFill()
    {
        return solvePolyominoPuzzle(new int?[_gridWidth * _gridHeight], 0, getAllTetrominoPlacements().Shuffle(), new List<PolyominoPlacement>()).First();
    }

    private static Polyomino[] getAllTetrominos()
    {
        var tetrominos = new[] { "####", "##,##", "###,#", "##,.##", "###,.#" };
        return tetrominos
            .Select(p => new Polyomino(p))
            .SelectMany(p => new[] { p, p.RotateClockwise(), p.RotateClockwise().RotateClockwise(), p.RotateClockwise().RotateClockwise().RotateClockwise() })
            .SelectMany(p => new[] { p, p.Reflect() })
            .Distinct()
            .ToArray();
    }

    private static List<PolyominoPlacement> getAllTetrominoPlacements()
    {
        return (from poly in getAllTetrominos()
                from place in Coord.Cells(_gridWidth, _gridHeight)
                select new PolyominoPlacement(poly, place)).Where(p => p.IsInRange).ToList();
    }

    private static IEnumerable<TetrisFillResult> solvePolyominoPuzzle(
        int?[] sofar,
        int pieceIx,
        List<PolyominoPlacement> possiblePlacements,
        List<PolyominoPlacement> polysSofar)
    {
        Coord? bestCell = null;
        int[] bestPlacementIxs = null;

        foreach (var tCell in Coord.Cells(_gridWidth, _gridHeight))
        {
            if (sofar[tCell.Index] != null)
                continue;
            var tPossiblePlacementIxs = possiblePlacements.SelectIndexWhere(pl => pl.Polyomino.Has((tCell.X - pl.Place.X + _gridWidth) % _gridWidth, (tCell.Y - pl.Place.Y + _gridHeight) % _gridHeight)).ToArray();
            if (tPossiblePlacementIxs.Length == 0)
                yield break;
            if (bestPlacementIxs == null || tPossiblePlacementIxs.Length < bestPlacementIxs.Length)
            {
                bestCell = tCell;
                bestPlacementIxs = tPossiblePlacementIxs;
            }
            if (tPossiblePlacementIxs.Length == 1)
                goto shortcut;
        }

        if (bestPlacementIxs == null)
        {
            yield return new TetrisFillResult(sofar.Select(i => i.Value).ToArray(), polysSofar.ToArray());
            yield break;
        }

        shortcut:
        var cell = bestCell.Value;

        for (var i = bestPlacementIxs.Length - 1; i >= 0; i--)
        {
            var poly = possiblePlacements[bestPlacementIxs[i]].Polyomino;
            var place = possiblePlacements[bestPlacementIxs[i]].Place;
            possiblePlacements.RemoveAt(bestPlacementIxs[i]);

            foreach (var c in poly.Cells)
                sofar[place.AddWrap(c).Index] = pieceIx;
            polysSofar.Add(new PolyominoPlacement(poly, place));

            var newPlacements = possiblePlacements
                .Where(p => p.Polyomino.Cells.All(c => sofar[p.Place.AddWrap(c).Index] == null))
                .ToList();

            foreach (var solution in solvePolyominoPuzzle(sofar, pieceIx + 1, newPlacements, polysSofar))
                yield return solution;

            polysSofar.RemoveAt(polysSofar.Count - 1);
            foreach (var c in poly.Cells)
                sofar[place.AddWrap(c).Index] = null;
        }
    }
}

internal struct TetrisFillResult
{
    public int[] Solution { get; private set; }
    public PolyominoPlacement[] Tetrominos { get; private set; }

    public TetrisFillResult(int[] solution, PolyominoPlacement[] polys)
    {
        Solution = solution;
        Tetrominos = polys;
    }

    public override bool Equals(object obj)
    {
        if (!(obj is TetrisFillResult))
            return false;

        var other = (TetrisFillResult) obj;
        return EqualityComparer<int[]>.Default.Equals(Solution, other.Solution) &&
               EqualityComparer<PolyominoPlacement[]>.Default.Equals(Tetrominos, other.Tetrominos);
    }

    public override int GetHashCode()
    {
        var hashCode = 1728871946;
        hashCode = hashCode * -1521134295 + EqualityComparer<int[]>.Default.GetHashCode(Solution);
        hashCode = hashCode * -1521134295 + EqualityComparer<PolyominoPlacement[]>.Default.GetHashCode(Tetrominos);
        return hashCode;
    }

    public void Deconstruct(out int[] solution, out PolyominoPlacement[] polys)
    {
        solution = this.Solution;
        polys = this.Tetrominos;
    }
}