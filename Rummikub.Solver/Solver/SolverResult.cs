using Rummikub.Core.Board;
using Rummikub.Core.Tiles;

namespace Rummikub.Solver.Solver;

public sealed record SolverResult(BoardState Board, IReadOnlyList<Tile> PlayedTiles)
{
    public static SolverResult Empty { get; } = new SolverResult(BoardState.Empty, []);
}