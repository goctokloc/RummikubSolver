using Rummikub.Core;
using Rummikub.Solver.Mip;
using Rummikub.Solver.Model;
using Rummikub.Solver.Solver;

namespace Rummikub.Tests.Model;

[TestFixture]
public class RummikubModelRoundTripTests
{
    static Tile R(int v) => Tile.Of(Color.Red, v);

    static DecodedPlay SolveAndVerify(Hand hand, BoardState board, SolverOptions options)
    {
        var model = RummikubModelBuilder.Build(hand, board, options);
        model.PackObjective();
        var solution = BranchAndBound.Solve(model.Model, model.KeepEverything(), options.MaxNodes, CancellationToken.None);
        Assert.That(solution.IsOptimal, Is.True);
        var play = model.Decode(solution.Values);
        SolutionVerifier.Verify(hand, board, options, play);
        return play;
    }

    [Test]
    public void Extends_Board_Run_Without_Breaking_It_Apart()
    {
        var board = BoardState.Of([Run.Create(Color.Red, 1, 3, 0)]);
        var play = SolveAndVerify(Hand.Of([R(4)]), board, new SolverOptions());
        Assert.That(play.PlayedTiles, Is.EqualTo(new[] { R(4) }));
        Assert.That(play.KeptGroups.Concat(play.NewGroups).SelectMany(g => g.Tiles).Order(),
            Is.EqualTo(new[] { R(1), R(2), R(3), R(4) }));
    }

    [Test]
    public void Prefers_Keeping_Board_Groups_When_Primary_Ties()
    {
        var board = BoardState.Of([Run.Create(Color.Blue, 1, 3, 0), Run.Create(Color.Blue, 5, 3, 0)]);
        var play = SolveAndVerify(Hand.Of([R(1), R(2), R(3)]), board, new SolverOptions());
        Assert.That(play.PlayedTiles, Has.Count.EqualTo(3));
        Assert.That(play.KeptGroups, Is.EquivalentTo(board.Groups));
    }

    [Test]
    public void Initial_Meld_Below_Threshold_Plays_Nothing()
    {
        var board = BoardState.Of([Run.Create(Color.Blue, 1, 3, 0)]);
        var play = SolveAndVerify(Hand.Of([R(1), R(2), R(3), Tile.Of(Color.Blue, 4)]), board,
            new SolverOptions { InitialMeldDone = false });
        Assert.That(play.PlayedTiles, Is.Empty);
        Assert.That(play.KeptGroups, Is.EquivalentTo(board.Groups));
    }

    [Test]
    public void Initial_Meld_At_Threshold_Plays_From_Hand_Only()
    {
        var board = BoardState.Of([Run.Create(Color.Blue, 1, 3, 0)]);
        var play = SolveAndVerify(Hand.Of([R(9), R(10), R(11), Tile.Of(Color.Blue, 4)]), board,
            new SolverOptions { InitialMeldDone = false });
        Assert.That(play.PlayedTiles, Is.EqualTo(new[] { R(9), R(10), R(11) }));
        Assert.That(play.KeptGroups, Is.EquivalentTo(board.Groups));
    }
}
