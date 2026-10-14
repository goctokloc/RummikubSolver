using Rummikub.Core;
using Rummikub.Solver.Solver;

namespace Rummikub.Tests.Solver;

[TestFixture]
public class SolverResultTests
{
    [Test]
    public void Played_Tile_Order_Does_Not_Affect_Equality()
    {
        var a = SolverResult.Create(BoardState.Empty, [Tile.Of(Color.Red, 1), Tile.Joker]);
        var b = SolverResult.Create(BoardState.Empty, [Tile.Joker, Tile.Of(Color.Red, 1)]);
        Assert.That(a, Is.EqualTo(b));
        Assert.That(a.GetHashCode(), Is.EqualTo(b.GetHashCode()));
    }
}
