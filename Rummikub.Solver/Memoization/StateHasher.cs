using Rummikub.Core.Common;
using Rummikub.Core.Tiles;

namespace Rummikub.Solver.Memoization;

/// <summary>
/// Produces canonical, immutable keys for solver states.
/// </summary>
public static class StateHasher
{
    public static HandStateKey HandOnly(MultiSet<Tile> hand)
    {
        var entries = hand
            .DistinctItems
            .Select(tile => (tile, hand.CountOf(tile)))
            .OrderBy(e => e.tile)
            .ToArray();

        return new HandStateKey(entries);
    }
}

/// <summary>
/// Immutable snapshot of a hand multiset.
/// </summary>
public readonly record struct HandStateKey(
    IReadOnlyList<(Tile Tile, int Count)> Entries
);
