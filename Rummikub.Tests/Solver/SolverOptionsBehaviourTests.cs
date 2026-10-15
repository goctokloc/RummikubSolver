using Rummikub.Core;
using Rummikub.Solver.Solver;
namespace Rummikub.Tests.Solver;

[TestFixture]
public class SolverOptionsBehaviourTests
{
    static Tile R(int v) => Tile.Of(Color.Red, v);
    static readonly SolverOptions Meld = new() { InitialMeldDone = false };
    readonly RummikubSolver _solver = new();

    [Test] public void Meld_At_Threshold_Plays()
    {
        var board = BoardState.Of([Run.Create(Color.Blue, 1, 3, 0)]);
        var r = _solver.Solve(Hand.Of([R(9), R(10), R(11)]), board, Meld);
        Assert.That(r.PlayedTiles, Has.Count.EqualTo(3));
        Assert.That(r.Board.Groups, Does.Contain(Run.Create(Color.Blue, 1, 3, 0)));
    }

    [Test] public void Meld_Below_Threshold_Plays_Nothing()
    {
        var board = BoardState.Of([Run.Create(Color.Blue, 1, 3, 0)]);
        var r = _solver.Solve(Hand.Of([R(1), R(2), R(3)]), board, Meld);
        Assert.That(r.PlayedTiles, Is.Empty);
        Assert.That(r.Board, Is.EqualTo(board));
    }

    [Test] public void Meld_Joker_Counts_Best_Reading()
    {
        var r = _solver.Solve(Hand.Of([R(9), R(10), Tile.Joker]), BoardState.Empty, Meld);
        Assert.That(r.Board.Groups, Is.EqualTo(new Group[] { Run.Create(Color.Red, 9, 3, 0b100) }));
    }

    [Test] public void Meld_Cannot_Use_Board()
    {
        var board = BoardState.Of([Run.Create(Color.Red, 4, 3, 0)]);
        Assert.That(_solver.Solve(Hand.Of([R(7)]), board, Meld).PlayedTiles, Is.Empty);
        Assert.That(_solver.Solve(Hand.Of([R(7)]), board).PlayedTiles, Has.Count.EqualTo(1));
    }

    [Test] public void Points_And_TileCount_Choose_Differently()
    {
        var hand = Hand.Of([R(1), R(2), R(4), Tile.Joker, Tile.Of(Color.Blue, 13), Tile.Of(Color.Black, 13)]);
        var count = _solver.Solve(hand, BoardState.Empty);
        var points = _solver.Solve(hand, BoardState.Empty, new SolverOptions { Objective = SolverObjective.Points });
        Assert.That(count.PlayedTiles, Has.Count.EqualTo(4).And.Contains(R(4)));
        Assert.That(points.PlayedTiles, Is.EquivalentTo(new[] { Tile.Of(Color.Blue, 13), Tile.Of(Color.Black, 13), Tile.Joker }));
    }

    [Test] public void KeepJokers_Keeps_Unneeded_Joker()
    {
        var hand = Hand.Of([R(5), R(6), R(7), Tile.Joker]);
        Assert.That(_solver.Solve(hand, BoardState.Empty).PlayedTiles, Has.Count.EqualTo(4));
        var kept = _solver.Solve(hand, BoardState.Empty, new SolverOptions { KeepJokers = true });
        Assert.That(kept.PlayedTiles, Has.Count.EqualTo(3).And.Not.Contains(Tile.Joker));
    }

    [Test] public void KeepJokers_With_Three_Jokers_Does_Not_Overflow()
    {
        var r = _solver.Solve(Hand.Of([Tile.Joker, Tile.Joker, Tile.Joker, R(5)]), BoardState.Empty, new SolverOptions { KeepJokers = true });
        Assert.That(r.PlayedTiles, Has.Count.EqualTo(3));
        Assert.That(r.IsOptimal, Is.True);
    }

    [Test] public void Least_Rearrangement_Keeps_Board_Group()
    {
        var set = Set.Create(5, 0b1111, 0);
        var board = BoardState.Of([set, Run.Create(Color.Red, 6, 3, 0)]);
        var r = _solver.Solve(Hand.Of([R(9)]), board);
        Assert.That(r.PlayedTiles, Has.Count.EqualTo(1));
        Assert.That(r.Board.Groups, Does.Contain(set));
    }

    [Test] public void Long_Board_Run_Stays_Intact()
    {
        var board = BoardState.Of([Run.Create(Color.Red, 1, 8, 0)]);
        var r = _solver.Solve(Hand.Of([Tile.Of(Color.Blue, 13)]), board);
        Assert.That(r.Board, Is.EqualTo(board));
    }

    [Test] public void Cancelled_Token_Returns_Keep_Everything()
    {
        var board = BoardState.Of([Run.Create(Color.Red, 1, 3, 0)]);
        var r = _solver.Solve(Hand.Of([R(4)]), board, cancellationToken: new CancellationToken(true));
        Assert.That(r.PlayedTiles, Is.Empty);
        Assert.That(r.Board, Is.EqualTo(board));
        Assert.That(r.IsOptimal, Is.False);
    }
}
