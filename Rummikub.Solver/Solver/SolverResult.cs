using Rummikub.Core;
using Rummikub.Core.Common;

namespace Rummikub.Solver.Solver;

public sealed record SolverResult(BoardState Board, EquatableArray<Tile> PlayedTiles, bool IsOptimal)
{
    public static SolverResult Empty { get; } = new(BoardState.Empty, EquatableArray<Tile>.Empty, true);

    /// <summary>Creates a result with <paramref name="playedTiles"/> sorted, so search order does not affect equality.</summary>
    public static SolverResult Create(BoardState board, IEnumerable<Tile> playedTiles, bool isOptimal) =>
        new(board, playedTiles.Order().ToEquatableArray(), isOptimal);
}
