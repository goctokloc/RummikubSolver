using Rummikub.Core;

namespace Rummikub.Solver.Solver;

public sealed record SolverResult(BoardState Board, IReadOnlyList<Tile> PlayedTiles) : IEquatable<SolverResult>
{
    public static SolverResult Empty { get; } = new SolverResult(BoardState.Empty, []);

    public bool Equals(SolverResult? other)
    {
        if (other is null)
            return false;
        if (!Board.Equals(other.Board))
            return false;
        return PlayedTiles.SequenceEqual(other.PlayedTiles);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            int hash = 17;
            hash = hash * 31 + Board.GetHashCode();
            foreach (var tile in PlayedTiles)
                hash = hash * 31 + tile.GetHashCode();
            return hash;
        }
    }
}
