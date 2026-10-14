using Rummikub.Core;

namespace Rummikub.Tests.Tiles;

[TestFixture]
public class TileTests
{
    [Test]
    public void Of_Round_Trips_Color_Value_And_Id()
    {
        var t = Tile.Of(Color.Yellow, 7);
        Assert.Multiple(() =>
        {
            Assert.That(t.Color, Is.EqualTo(Color.Yellow));
            Assert.That(t.Value, Is.EqualTo(7));
            Assert.That(t.IsJoker, Is.False);
            Assert.That(t.Id, Is.EqualTo(2 * 13 + 7 - 1));
        });
    }

    [TestCase(0)]
    [TestCase(14)]
    [TestCase(-1)]
    public void Of_Rejects_Value_Out_Of_Range(int value) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => Tile.Of(Color.Red, value));

    [Test]
    public void Of_Rejects_Undefined_Color() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => Tile.Of((Color)9, 5));

    [Test]
    public void Joker_Has_No_Color_Or_Value()
    {
        Assert.That(Tile.Joker.IsJoker, Is.True);
        Assert.That(Tile.Joker.Id, Is.EqualTo(52));
        Assert.Throws<InvalidOperationException>(() => _ = Tile.Joker.Color);
        Assert.Throws<InvalidOperationException>(() => _ = Tile.Joker.Value);
    }

    [Test]
    public void Default_Tile_Is_Unusable()
    {
        Tile d = default;
        Assert.Throws<InvalidOperationException>(() => _ = d.IsJoker);
        Assert.Throws<InvalidOperationException>(() => _ = d.Id);
        Assert.That(d.ToString(), Is.EqualTo("?"));
    }

    [Test]
    public void Equal_Tiles_Are_Equal()
    {
        var a = Tile.Of(Color.Blue, 10);
        var b = Tile.Of(Color.Blue, 10);
        Assert.That(a, Is.EqualTo(b));
    }

    [Test]
    public void Ordering_Follows_Code()
    {
        Assert.That(Tile.Of(Color.Red, 13).CompareTo(Tile.Of(Color.Blue, 1)), Is.LessThan(0));
        Assert.That(Tile.Of(Color.Black, 13).CompareTo(Tile.Joker), Is.LessThan(0));
    }

    [Test]
    public void AllKinds_Has_53_Distinct_Ordered_Tiles()
    {
        Assert.That(Tile.AllKinds, Has.Count.EqualTo(53));
        Assert.That(Tile.AllKinds, Is.Unique);
        Assert.That(Tile.AllKinds, Is.Ordered);
        Assert.That(Tile.AllKinds[^1], Is.EqualTo(Tile.Joker));
    }

    [TestCase(Color.Red, 5, "R5")]
    [TestCase(Color.Blue, 13, "B13")]
    [TestCase(Color.Yellow, 1, "Y1")]
    [TestCase(Color.Black, 7, "K7")]
    public void ToString_Uses_Short_Code(Color c, int v, string expected) =>
        Assert.That(Tile.Of(c, v).ToString(), Is.EqualTo(expected));

    [Test]
    public void Joker_ToString_Is_J() => Assert.That(Tile.Joker.ToString(), Is.EqualTo("J"));
}
