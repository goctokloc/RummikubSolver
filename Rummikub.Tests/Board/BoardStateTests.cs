using Rummikub.Core;

namespace Rummikub.Tests.Board;

[TestFixture]
public class BoardStateTests
{
    static readonly Group A = Set.Create(5, 0b0111, 0);
    static readonly Group B = Run.Create(Color.Red, 1, 3, 0);
    static readonly Group C = Run.Create(Color.Blue, 4, 4, 0b0010);

    [Test]
    public void Same_Groups_In_Any_Order_Are_Equal_With_Same_Hash()
    {
        var x = BoardState.Of([C, A, B]);
        var y = BoardState.Of([B, C, A]);
        Assert.That(x, Is.EqualTo(y));
        Assert.That(x.GetHashCode(), Is.EqualTo(y.GetHashCode()));
    }

    [Test]
    public void Add_Keeps_Sorted_Order() =>
        Assert.That(BoardState.Of([C, B]).Add(A).Groups, Is.EqualTo(new[] { A, B, C }));

    [Test]
    public void Tiles_Flattens_Groups() =>
        Assert.That(BoardState.Of([A, B]).Tiles.Count(), Is.EqualTo(6));

    [Test]
    public void Empty_Has_No_Groups() => Assert.That(BoardState.Empty.Groups, Is.Empty);
}
