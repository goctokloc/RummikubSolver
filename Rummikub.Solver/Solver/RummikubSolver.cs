using Rummikub.Core;
using Rummikub.Core.Common;
using Rummikub.Solver.Memoization;

namespace Rummikub.Solver.Solver
{
    internal class GroupInfo
    {
        public Group Group { get; }
        public Dictionary<Tile, int> TileCounts { get; }
        public GroupInfo(Group group)
        {
            Group = group;
            TileCounts = group.Tiles.GroupBy(t => t).ToDictionary(x => x.Key, x => x.Count());
        }
    }

    public sealed class RummikubSolver
    {
        private readonly Dictionary<SolverStateKey, SolverResult?> _memo = [];

        public SolverResult SolveMaxTiles(Hand hand, BoardState board, SolverOptions? options = null)
        {
            options ??= new SolverOptions();
            _memo.Clear();

            var boardTiles = board.Groups.SelectMany(g => g.Tiles).ToList();
            var pool = new MultiSet<Tile>(hand.Tiles.Concat(boardTiles));
            var mandatory = new MultiSet<Tile>(boardTiles);

            var allGroups = GroupGenerator.GenerateValidGroups(pool)
                                          .Distinct()
                                          .Select(g => new GroupInfo(g))
                                          .ToArray();

            var result = Dfs(pool, mandatory, allGroups);

            return result ?? SolverResult.Empty;
        }

        private SolverResult? Dfs(MultiSet<Tile> pool, MultiSet<Tile> mandatory, GroupInfo[] allGroups)
        {
            var key = StateHasher.PoolAndMandatory(pool, mandatory);
            if (_memo.TryGetValue(key, out var cachedResult))
                return cachedResult;

            SolverResult? bestResult = null;

            if (mandatory.Count > 0)
            {
                Tile bestT = default;
                int minGroups = int.MaxValue;

                foreach (var t in mandatory.DistinctItems)
                {
                    int groupsForT = 0;
                    foreach (var g in allGroups)
                    {
                        if (g.TileCounts.ContainsKey(t) && CanForm(g, pool))
                        {
                            groupsForT++;
                        }
                    }
                    if (groupsForT < minGroups)
                    {
                        minGroups = groupsForT;
                        bestT = t;
                    }
                }

                if (minGroups == 0)
                {
                    _memo[key] = null;
                    return null;
                }

                foreach (var g in allGroups)
                {
                    if (g.TileCounts.ContainsKey(bestT) && CanForm(g, pool))
                    {
                        var nextPool = pool.Clone();
                        var nextMandatory = mandatory.Clone();
                        var handTilesUsed = new List<Tile>();

                        foreach (var kv in g.TileCounts)
                        {
                            for (int k = 0; k < kv.Value; k++)
                            {
                                nextPool.Remove(kv.Key);
                                if (nextMandatory.CountOf(kv.Key) > 0)
                                    nextMandatory.Remove(kv.Key);
                                else
                                    handTilesUsed.Add(kv.Key);
                            }
                        }

                        var subResult = Dfs(nextPool, nextMandatory, allGroups);
                        if (subResult != null)
                        {
                            var played = subResult.PlayedTiles.Concat(handTilesUsed).ToList();
                            if (bestResult == null || played.Count > bestResult.PlayedTiles.Count)
                            {
                                bestResult = SolverResult.Create(subResult.Board.Add(g.Group), played);
                            }
                        }
                    }
                }
            }
            else
            {
                if (pool.Count == 0)
                {
                    bestResult = SolverResult.Empty;
                }
                else
                {
                    bestResult = SolverResult.Empty;
                    var t = pool.DistinctItems.First();

                    // Option 1: Skip t entirely
                    var nextPoolSkip = pool.Clone();
                    nextPoolSkip.Remove(t);
                    var subResultSkip = Dfs(nextPoolSkip, mandatory, allGroups);
                    if (subResultSkip != null && subResultSkip.PlayedTiles.Count > bestResult.PlayedTiles.Count)
                    {
                        bestResult = subResultSkip;
                    }

                    // Option 2: Try forming groups containing t
                    foreach (var g in allGroups)
                    {
                        if (g.TileCounts.ContainsKey(t) && CanForm(g, pool))
                        {
                            var nextPool = pool.Clone();
                            var handTilesUsed = new List<Tile>();

                            foreach (var kv in g.TileCounts)
                            {
                                for (int k = 0; k < kv.Value; k++)
                                {
                                    nextPool.Remove(kv.Key);
                                    // since mandatory is empty, all tiles are hand tiles
                                    handTilesUsed.Add(kv.Key);
                                }
                            }

                            var subResult = Dfs(nextPool, mandatory, allGroups);
                            if (subResult != null)
                            {
                                var played = subResult.PlayedTiles.Concat(handTilesUsed).ToList();
                                if (played.Count > bestResult.PlayedTiles.Count)
                                {
                                    bestResult = SolverResult.Create(subResult.Board.Add(g.Group), played);
                                }
                            }
                        }
                    }
                }
            }

            _memo[key] = bestResult;
            return bestResult;
        }

        private static bool CanForm(GroupInfo g, MultiSet<Tile> pool)
        {
            foreach (var kv in g.TileCounts)
            {
                if (pool.CountOf(kv.Key) < kv.Value)
                    return false;
            }
            return true;
        }
    }
}
