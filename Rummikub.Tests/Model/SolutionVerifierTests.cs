using Rummikub.Core;
using Rummikub.Solver.Model;
using Rummikub.Solver.Solver;
namespace Rummikub.Tests.Model;

[TestFixture]
public class SolutionVerifierTests
{
    static Tile R(int v) => Tile.Of(Color.Red, v);
    static readonly Group Board = Run.Create(Color.Red, 1, 3, 0);
    static readonly SolverOptions Default = new();

    [Test] public void Accepts_Valid_Play() =>
        Assert.DoesNotThrow(() => SolutionVerifier.Verify(Hand.Of([R(4)]), BoardState.Of([Board]), Default,
            new DecodedPlay([], [Run.Create(Color.Red, 1, 4, 0)], [R(4)])));

    [Test] public void Rejects_Tile_Not_In_Hand() =>
        Assert.Throws<InvalidOperationException>(() => SolutionVerifier.Verify(Hand.Empty, BoardState.Of([Board]), Default,
            new DecodedPlay([], [Run.Create(Color.Red, 1, 4, 0)], [R(4)])));

    [Test] public void Rejects_Dropped_Board_Tile() =>
        Assert.Throws<InvalidOperationException>(() => SolutionVerifier.Verify(Hand.Empty, BoardState.Of([Run.Create(Color.Red, 1, 4, 0)]), Default,
            new DecodedPlay([], [Board], [])));

    [Test] public void Rejects_Meld_Below_Threshold() =>
        Assert.Throws<InvalidOperationException>(() => SolutionVerifier.Verify(Hand.Of([R(4), R(5), R(6)]), BoardState.Empty,
            Default with { InitialMeldDone = false }, new DecodedPlay([], [Run.Create(Color.Red, 4, 3, 0)], [R(4), R(5), R(6)])));

    [Test] public void Rejects_Meld_That_Changes_Board() =>
        Assert.Throws<InvalidOperationException>(() => SolutionVerifier.Verify(Hand.Of([R(4)]), BoardState.Of([Board]),
            Default with { InitialMeldDone = false, InitialMeldPoints = 0 }, new DecodedPlay([], [Run.Create(Color.Red, 1, 4, 0)], [R(4)])));
}
