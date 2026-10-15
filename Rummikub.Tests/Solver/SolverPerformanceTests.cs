using System.Diagnostics;
using Rummikub.Core;
using Rummikub.Solver.Solver;
namespace Rummikub.Tests.Solver;

[TestFixture, Category("Performance")]
public class SolverPerformanceTests
{
    [Test] public void Full_Game_Positions_Solve_Under_A_Second([Range(0, 4)] int seed)
    {
        var (hand, board) = RandomGames.Deal(1000 + seed, boardTiles: 60 + seed * 7, handTiles: 14);
        new RummikubSolver().Solve(hand, BoardState.Empty); // warm-up (JIT, catalogue)
        var sw = Stopwatch.StartNew();
        var r = new RummikubSolver().Solve(hand, board);
        sw.Stop();
        TestContext.Progress.WriteLine($"perf seed {seed}: {sw.ElapsedMilliseconds} ms, played {r.PlayedTiles.Count}, optimal {r.IsOptimal}");
        Assert.That(r.IsOptimal, Is.True);
        Assert.That(sw.ElapsedMilliseconds, Is.LessThan(1000));
    }
}
