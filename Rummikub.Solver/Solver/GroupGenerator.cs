using Rummikub.Core.Common;
using Rummikub.Core;

namespace Rummikub.Solver.Solver;

internal static class GroupGenerator
{
    /// <summary>
    /// Every distinct group formable from <paramref name="pool"/>. Readings of the same tiles that differ
    /// only in joker placement (R2 R3 J as R1–R3 or R2–R4) are emitted once, lowest start first.
    /// </summary>
    public static IEnumerable<Group> GenerateValidGroups(MultiSet<Tile> pool) =>
        Candidates(pool).DistinctBy(g => g.Tiles.Order().ToEquatableArray());

    private static IEnumerable<Group> Candidates(MultiSet<Tile> pool)
    {
        int jokerCount = Math.Min(pool.CountOf(Tile.Joker), Group.MaxJokers);

        var regularTiles = pool.DistinctItems.Where(t => !t.IsJoker).ToList();

        // 1. Generate Valid Sets
        var byValue = regularTiles.GroupBy(t => t.Value);
        foreach (var valueGroup in byValue)
        {
            var availableTiles = valueGroup.ToList();
            int maxColors = availableTiles.Count;

            for (int size = 3; size <= 4; size++)
            {
                int minRegular = Math.Max(1, size - jokerCount);
                int maxRegular = Math.Min(size, maxColors);

                for (int numReg = minRegular; numReg <= maxRegular; numReg++)
                {
                    int neededJokers = size - numReg;
                    foreach (var subset in Combinatorics.Combinations(availableTiles, numReg))
                    {
                        byte realColors = 0;
                        foreach (var tile in subset)
                            realColors |= (byte)(1 << (int)tile.Color);

                        yield return Set.CreateFillingJokers(valueGroup.Key, realColors, neededJokers);
                    }
                }
            }
        }

        // 2. Generate Valid Runs
        var byColor = regularTiles.GroupBy(t => t.Color);
        foreach (var colorGroup in byColor)
        {
            var availableValues = colorGroup.Select(t => t.Value).ToHashSet();
            var color = colorGroup.Key;

            for (int startVal = 1; startVal <= 13; startVal++)
            {
                for (int length = 3; length <= 13 - startVal + 1; length++)
                {
                    ushort jokerMask = 0;
                    int usedJokers = 0;
                    bool possible = true;

                    for (int step = 0; step < length; step++)
                    {
                        if (availableValues.Contains(startVal + step))
                            continue;

                        if (usedJokers < jokerCount)
                        {
                            usedJokers++;
                            jokerMask |= (ushort)(1 << step);
                        }
                        else
                        {
                            possible = false;
                            break;
                        }
                    }

                    if (possible)
                        yield return Run.Create(color, startVal, length, jokerMask);
                }
            }
        }
    }
}
