using Rummikub.Solver.Reference;
using Rummikub.Solver.Solver;
namespace Rummikub.Tests.Solver;

[TestFixture]
public class SolverOracleTests
{
    [Test] public void Matches_Dfs_On_Small_Random_Games([Range(0, 199)] int seed)
    {
        var (hand, board) = RandomGames.Deal(seed, boardTiles: 3 + seed % 18, handTiles: 1 + seed % 8);
        var expected = new DfsReferenceSolver().SolveMaxTiles(hand, board).PlayedTiles.Count;
        var actual = new RummikubSolver().Solve(hand, board);
        Assert.That(actual.PlayedTiles, Has.Count.EqualTo(expected), $"seed {seed}\nhand: {hand}\nboard: {board}");
        Assert.That(actual.IsOptimal, Is.True);
    }
}
