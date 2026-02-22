using Rummikub.Core.Common;
using Rummikub.Core.Groups;
using Rummikub.Core.Tiles;

namespace Rummikub.Solver.Solver;

internal static class GroupGenerator
{
    public static IEnumerable<Group> GenerateValidGroups(MultiSet<Tile> pool)
    {
        var distinctJokers = pool.DistinctItems.Where(t => t.IsJoker).ToList();
        int jokerCount = distinctJokers.Sum(j => pool.CountOf(j));

        var regularTiles = pool.DistinctItems.Where(t => !t.IsJoker).ToList();

        // 1. Generate Valid Sets
        var byValue = regularTiles.GroupBy(t => t.Value!.Value);
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
                        var groupTiles = new List<Tile>(subset);
                        int jokersAdded = 0;
                        foreach (var j in distinctJokers)
                        {
                            int count = pool.CountOf(j);
                            for (int i = 0; i < count && jokersAdded < neededJokers; i++)
                            {
                                groupTiles.Add(j);
                                jokersAdded++;
                            }
                        }
                        
                        yield return new Group(GroupType.Set, groupTiles);
                    }
                }
            }
        }

        // 2. Generate Valid Runs
        var byColor = regularTiles.GroupBy(t => t.Color!.Value);
        foreach (var colorGroup in byColor)
        {
            var avColor = colorGroup.OrderBy(t => t.Value!.Value).ToList();
            var availableValues = avColor.Select(t => t.Value!.Value).ToHashSet();
            var color = colorGroup.Key;

            for (int startVal = 1; startVal <= 13; startVal++)
            {
                for (int length = 3; length <= 13 - startVal + 1; length++)
                {
                    var runTiles = new List<Tile>();
                    int usedJokers = 0;
                    bool possible = true;

                    for (int step = 0; step < length; step++)
                    {
                        int currentVal = startVal + step;
                        if (availableValues.Contains(currentVal))
                        {
                            runTiles.Add(new Tile(color, currentVal, false));
                        }
                        else
                        {
                            if (usedJokers < jokerCount)
                            {
                                usedJokers++;
                            }
                            else
                            {
                                possible = false;
                                break;
                            }
                        }
                    }

                    if (possible)
                    {
                        int jokersAdded = 0;
                        foreach (var j in distinctJokers)
                        {
                            int count = pool.CountOf(j);
                            for (int i = 0; i < count && jokersAdded < usedJokers; i++)
                            {
                                runTiles.Add(j);
                                jokersAdded++;
                            }
                        }
                        yield return new Group(GroupType.Run, runTiles);
                    }
                }
            }
        }
    }
}
