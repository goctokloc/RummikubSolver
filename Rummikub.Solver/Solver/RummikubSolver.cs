using Rummikub.Core.Board;
using Rummikub.Core.Common;
using Rummikub.Core.Groups;
using Rummikub.Core.Hand;
using Rummikub.Core.Tiles;
using Rummikub.Solver.Memoization;

namespace Rummikub.Solver.Solver
{
    public sealed class RummikubSolver
    {
        private readonly Dictionary<HandStateKey, SolverResult> _memo = [];
        public SolverResult SolveMaxTiles(Hand hand, BoardState board, SolverOptions? options = null)
        {
            options ??= new SolverOptions();


            var initialState = new SolverState(new MultiSet<Tile>(hand.Tiles), board);

            {

                return Dfs(initialState, options);
            }
        }

        private SolverResult Dfs(SolverState state, SolverOptions options)
        {
            var key = StateHasher.HandOnly(state.RemainingHand);

            if (_memo.TryGetValue(key, out var cachedResult))
                return cachedResult;

            var bestResult = new SolverResult(state.Board, Array.Empty<Tile>());
            foreach (var group in GenerateCandidateGroups(state.Board, state.RemainingHand))
            {
                if (!GroupValidator.IsValid(group))
                    continue;

                var nextBoard = state.Board.AddGroup(group);
                var nextHand = state.RemainingHand.Except(group.Tiles);

                var nextState = new SolverState(nextHand, nextBoard);

                var subResult = Dfs(nextState, options);

                var played = group.Tiles.Concat(subResult.PlayedTiles).ToList();

                if (played.Count > bestResult.PlayedTiles.Count)
                    bestResult = new SolverResult(subResult.Board, played);
            }

            _memo[key] = bestResult;
            return bestResult;
        }

        private static IEnumerable<Group> GenerateCandidateGroups(BoardState board, MultiSet<Tile> hand)
        {
            var tiles = hand.Elements.ToList();

            for (int size = 3; size <= 4; size++)
            {
                foreach (var subset in Combinatorics.Combinations(tiles, size))
                {
                    yield return new Group(GroupType.Set, subset);
                }
            }

            for (int size = 3; size <= tiles.Count; size++)
            {
                foreach (var subset in Combinatorics.Combinations(tiles, size))
                {
                    yield return new Group(GroupType.Run, subset);
                }
            }
        }
    }
}