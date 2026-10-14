using Rummikub.Core;
using Rummikub.Core.Common;
using Rummikub.Solver.Solver;

namespace Rummikub.Tests.Solver;

[TestFixture]
public class GroupGeneratorTests
{
    [Test]
    public void Groups_With_Same_Tiles_Are_Generated_Once()
    {
        // R2 R3 J can be read as R1-R3 or R2-R4: same physical tiles, one candidate group.
        var pool = new MultiSet<Tile>([Tile.Of(Color.Red, 2), Tile.Of(Color.Red, 3), Tile.Joker]);

        var groups = GroupGenerator.GenerateValidGroups(pool).ToList();

        Assert.That(groups, Is.EqualTo(new Group[] { Run.Create(Color.Red, 1, 3, 0b001) }));
    }

    [Test]
    public void Generated_Groups_Have_Unique_Tile_Multisets()
    {
        var pool = new MultiSet<Tile>([
            Tile.Of(Color.Red, 4), Tile.Of(Color.Red, 5), Tile.Of(Color.Blue, 5),
            Tile.Of(Color.Black, 5), Tile.Joker, Tile.Joker]);

        var keys = GroupGenerator.GenerateValidGroups(pool)
            .Select(g => g.Tiles.Order().ToEquatableArray())
            .ToList();

        Assert.That(keys, Is.Unique);
    }
}
