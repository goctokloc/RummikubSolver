using Rummikub.Core.Tiles;
using Rummikub.Core.Common;

namespace Rummikub.Solver.Memoization;

public static class StateHasher
{
    public static SolverStateKey PoolAndMandatory(MultiSet<Tile> pool, MultiSet<Tile> mandatory)
    {
        var poolKey = pool
            .DistinctItems
            .Select(tile => (tile, pool.CountOf(tile)))
            .OrderBy(e => e.tile)
            .ToArray();

        var mandatoryKey = mandatory
            .DistinctItems
            .Select(tile => (tile, mandatory.CountOf(tile)))
            .OrderBy(e => e.tile)
            .ToArray();

        return new SolverStateKey(poolKey, mandatoryKey);
    }
}

public readonly record struct SolverStateKey(
    IReadOnlyList<(Tile Tile, int Count)> PoolEntries,
    IReadOnlyList<(Tile Tile, int Count)> MandatoryEntries
) : IEquatable<SolverStateKey>
{
    public bool Equals(SolverStateKey other)
    {
        if (PoolEntries.Count != other.PoolEntries.Count) return false;
        if (MandatoryEntries.Count != other.MandatoryEntries.Count) return false;
        
        for (int i = 0; i < PoolEntries.Count; i++)
        {
            if (PoolEntries[i] != other.PoolEntries[i]) return false;
        }
        for (int i = 0; i < MandatoryEntries.Count; i++)
        {
            if (MandatoryEntries[i] != other.MandatoryEntries[i]) return false;
        }
        return true;
    }

    public override int GetHashCode()
    {
        unchecked
        {
            int hash = 17;
            foreach (var e in PoolEntries)
            {
                hash = hash * 31 + e.Tile.GetHashCode();
                hash = hash * 31 + e.Count;
            }
            foreach (var e in MandatoryEntries)
            {
                hash = hash * 31 + e.Tile.GetHashCode();
                hash = hash * 31 + e.Count;
            }
            return hash;
        }
    }
}
