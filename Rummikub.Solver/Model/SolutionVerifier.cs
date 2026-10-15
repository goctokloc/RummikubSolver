using Rummikub.Core;
using Rummikub.Solver.Solver;

namespace Rummikub.Solver.Model;

/// <summary>Exact, model-independent check that a decoded play is legal for the position.</summary>
internal static class SolutionVerifier
{
    /// <exception cref="InvalidOperationException">The play breaks a rule; the message names it.</exception>
    public static void Verify(Hand hand, BoardState board, SolverOptions options, DecodedPlay play)
    {
        var handCounts = TileCounts.From(hand.Tiles);
        var playedCounts = TileCounts.From(play.PlayedTiles);
        var boardCounts = TileCounts.From(board.Tiles);
        var tableCounts = TileCounts.From(play.KeptGroups.Concat(play.NewGroups).SelectMany(g => g.Tiles));

        foreach (var tile in Tile.AllKinds)
        {
            var id = tile.Id;
            if (playedCounts[id] > handCounts[id])
                throw new InvalidOperationException(
                    $"Played tiles must come from the hand: {tile} played {playedCounts[id]} time(s), hand has {handCounts[id]}.");
            if (tableCounts[id] != boardCounts[id] + playedCounts[id])
                throw new InvalidOperationException(
                    $"Tile balance violated for {tile}: table has {tableCounts[id]}, board {boardCounts[id]} + played {playedCounts[id]}.");
        }

        var boardGroups = board.Groups.GroupBy(g => g).ToDictionary(g => g.Key, g => g.Count());
        foreach (var kept in play.KeptGroups.GroupBy(g => g))
        {
            if (kept.Count() > boardGroups.GetValueOrDefault(kept.Key))
                throw new InvalidOperationException($"Kept group {kept.Key} is kept {kept.Count()} time(s) but is on the board fewer times.");
        }

        if (options.InitialMeldDone)
            return;

        // Kept groups are a sub-multiset of the board groups, so equal counts mean equal multisets.
        if (play.KeptGroups.Count != board.Groups.Count)
            throw new InvalidOperationException("Initial meld must leave every board group unchanged.");
        if (play.NewGroups.Count > 0)
        {
            var points = play.NewGroups.Sum(GroupCatalog.Points);
            if (points < options.InitialMeldPoints)
                throw new InvalidOperationException(
                    $"Initial meld is worth {points} points, below the required {options.InitialMeldPoints}.");
        }
    }
}
