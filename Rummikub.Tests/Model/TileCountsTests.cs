using Rummikub.Core;
using Rummikub.Core.Common;
using Rummikub.Solver.Model;
namespace Rummikub.Tests.Model;

[TestFixture]
public class TileCountsTests
{
    [Test] public void Counts_By_Tile_Id()
    {
        var c = TileCounts.From([Tile.Of(Color.Red, 5), Tile.Of(Color.Red, 5), Tile.Joker]);
        Assert.That(c[Tile.Of(Color.Red, 5).Id], Is.EqualTo(2));
        Assert.That(c[Tile.Joker.Id], Is.EqualTo(1));
        Assert.That(c.Total, Is.EqualTo(3));
    }
}

[TestFixture]
public class GroupCatalogTests
{
    [Test] public void Has_Expected_Counts()
    {
        Assert.That(GroupCatalog.All.OfType<Set>().Count(), Is.EqualTo(281));
        Assert.That(GroupCatalog.All.OfType<Run>().Count(), Is.EqualTo(892));
        Assert.That(GroupCatalog.All, Has.Count.EqualTo(1173));
    }

    [Test] public void Tile_Multisets_Are_Unique() =>
        Assert.That(GroupCatalog.All.Select(g => g.Tiles.Order().ToEquatableArray()), Is.Unique);

    [Test] public void Runs_Are_Three_To_Five_Long() =>
        Assert.That(GroupCatalog.All.OfType<Run>().Select(r => r.Length), Is.All.InRange(3, 5));

    [Test] public void Sets_Come_Before_Runs() =>
        Assert.That(GroupCatalog.All.Select(g => g is Set ? 0 : 1), Is.Ordered);

    [Test] public void Collapsed_Reading_Keeps_Highest_Points()
    {
        var key = new[] { Tile.Of(Color.Red, 9), Tile.Of(Color.Red, 10), Tile.Joker }.Order().ToEquatableArray();
        var g = GroupCatalog.All.Single(x => x.Tiles.Order().ToEquatableArray() == key);
        Assert.That(g, Is.EqualTo(Run.Create(Color.Red, 9, 3, 0b100)));
        Assert.That(GroupCatalog.Points(g), Is.EqualTo(30));
    }

    static Group Reading(params Tile[] tiles) =>
        GroupCatalog.All.Single(x => x.Tiles.Order().ToEquatableArray() == tiles.Order().ToEquatableArray());

    [Test] public void One_Tile_Two_Jokers_Uses_Highest_Points_Reading()
    {
        // B4 J J: run B4-B6 (15) beats set of 4s (12); K13 J J: set of 13s (39) beats run K11-K13 (36)
        Assert.That(Reading(Tile.Of(Color.Blue, 4), Tile.Joker, Tile.Joker), Is.EqualTo(Run.Create(Color.Blue, 4, 3, 0b110)));
        Assert.That(Reading(Tile.Of(Color.Black, 13), Tile.Joker, Tile.Joker), Is.InstanceOf<Set>());
    }
}
