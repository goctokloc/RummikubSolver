using Rummikub.Core;

namespace Rummikub.Tests.Hands;

[TestFixture]
public class HandTests
{
    [Test]
    public void Of_Sorts_So_Order_Does_Not_Matter() =>
        Assert.That(Hand.Of([Tile.Joker, Tile.Of(Color.Red, 2)]),
            Is.EqualTo(Hand.Of([Tile.Of(Color.Red, 2), Tile.Joker])));

    [Test]
    public void Of_Rejects_Default_Tile() =>
        Assert.Throws<ArgumentException>(() => Hand.Of([default(Tile)]));
}
