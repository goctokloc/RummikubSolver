using Rummikub.Core.Tiles;

namespace Rummikub.Tests.Tiles;

[TestFixture]
public class TileTests
{
    [Test]
    public void Tile_Creation_Should_Set_Properties_Correctly()
    {
        var tile = new Tile(Color.Red, 5, false);

        Assert.That(tile.Color, Is.EqualTo(Color.Red));
        Assert.That(tile.Value, Is.EqualTo(5));
        Assert.IsFalse(tile.IsJoker);
    }

    [Test]
    public void Tile_Joker_Creation_Should_Set_IsJoker_True()
    {
        var tile = new Tile(null, null, true);

        Assert.IsTrue(tile.IsJoker);
        Assert.IsNull(tile.Color);
        Assert.IsNull(tile.Value);
    }

    [Test]
    public void Two_Identical_Tiles_Should_Be_Equal()
    {
        var tile1 = new Tile(Color.Blue, 10, false);
        var tile2 = new Tile(Color.Blue, 10, false);

        Assert.That(tile1, Is.EqualTo(tile2));
        Assert.That(tile1, Is.EqualTo(tile2));
    }

    [Test]
    public void JokerTiles_Should_Be_Equal()
    {
        var joker1 = new Tile(null, null, true);
        var joker2 = new Tile(null, null, true);

        Assert.That(joker1, Is.EqualTo(joker2));
    }
}