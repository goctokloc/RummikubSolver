using Rummikub.Core;

namespace Rummikub.Tests.Groups;

[TestFixture]
public class GroupFromTilesTests
{
    static Tile R(int v) => Tile.Of(Color.Red, v);
    static readonly Tile J = Tile.Joker;

    static Group Ok(GroupType type, params Tile[] tiles)
    {
        Assert.That(Group.TryFromTiles(type, tiles, out var g, out var err), Is.True, err);
        return g!;
    }

    static string Fail(GroupType type, params Tile[] tiles)
    {
        Assert.That(Group.TryFromTiles(type, tiles, out _, out var err), Is.False);
        return err!;
    }

    [Test]
    public void Run_With_Inner_Joker() =>
        Assert.That(Ok(GroupType.Run, R(3), J, R(5)), Is.EqualTo(Run.Create(Color.Red, 3, 3, 0b010)));

    [Test]
    public void Run_Shuffled_Input_Equals_Create() =>
        Assert.That(Ok(GroupType.Run, J, R(5), R(3)), Is.EqualTo(Run.Create(Color.Red, 3, 3, 0b010)));

    [Test]
    public void Run_Extra_Joker_Goes_Below() =>
        Assert.That(Ok(GroupType.Run, R(3), R(4), J), Is.EqualTo(Run.Create(Color.Red, 2, 3, 0b001)));

    [Test]
    public void Run_Low_Edge_Joker_Goes_Above() =>
        Assert.That(Ok(GroupType.Run, R(1), R(2), J), Is.EqualTo(Run.Create(Color.Red, 1, 3, 0b100)));

    [Test]
    public void Run_High_Edge() =>
        Assert.That(Ok(GroupType.Run, R(12), R(13), J), Is.EqualTo(Run.Create(Color.Red, 11, 3, 0b001)));

    [Test]
    public void Run_Two_Extra_Jokers() =>
        Assert.That(Ok(GroupType.Run, R(5), J, J), Is.EqualTo(Run.Create(Color.Red, 3, 3, 0b011)));

    [Test]
    public void Set_Jokers_Take_Lowest_Missing_Colors() =>
        Assert.That(Ok(GroupType.Set, Tile.Of(Color.Red, 7), Tile.Of(Color.Blue, 7), J),
            Is.EqualTo(Set.Create(7, 0b0111, 0b0100)));

    [Test]
    public void Set_Three_Reals_Plus_Joker() =>
        Assert.That(Ok(GroupType.Set, Tile.Of(Color.Red, 2), Tile.Of(Color.Yellow, 2), Tile.Of(Color.Black, 2), J),
            Is.EqualTo(Set.Create(2, 0b1111, 0b0010)));

    [Test]
    public void Slots_Of_Inferred_Run_Report_Joker_Meaning() =>
        Assert.That(Ok(GroupType.Run, R(3), R(4), J).Slots[0], Is.EqualTo(new GroupSlot(J, Color.Red, 2)));

    [Test]
    public void Rejects_Default_Tile() =>
        Assert.That(Fail(GroupType.Run, R(1), R(2), default), Is.EqualTo("group contains an uninitialized tile"));

    [Test]
    public void Rejects_Two_Tiles() =>
        Assert.That(Fail(GroupType.Run, R(1), R(2)), Is.EqualTo("group needs at least 3 tiles"));

    [Test]
    public void Rejects_Three_Jokers() =>
        Assert.That(Fail(GroupType.Run, R(1), J, J, J), Is.EqualTo("group has more than 2 jokers"));

    [Test]
    public void Rejects_All_Jokers() =>
        Assert.That(Fail(GroupType.Set, J, J, J), Is.EqualTo("group has more than 2 jokers"));

    [Test]
    public void Rejects_Mixed_Run_Colors() =>
        Assert.That(Fail(GroupType.Run, R(1), Tile.Of(Color.Blue, 2), R(3)), Is.EqualTo("run tiles must share one color"));

    [Test]
    public void Rejects_Duplicate_Run_Value() =>
        Assert.That(Fail(GroupType.Run, R(4), R(4), R(5)), Is.EqualTo("run contains duplicate value 4"));

    [Test]
    public void Rejects_Unfillable_Gap() =>
        Assert.That(Fail(GroupType.Run, R(1), R(5), J), Is.EqualTo("not enough jokers to fill gaps"));

    [Test]
    public void Rejects_Mixed_Set_Values() =>
        Assert.That(Fail(GroupType.Set, R(1), Tile.Of(Color.Blue, 2), Tile.Of(Color.Black, 1)), Is.EqualTo("set tiles must share one value"));

    [Test]
    public void Rejects_Duplicate_Set_Color() =>
        Assert.That(Fail(GroupType.Set, R(5), R(5), Tile.Of(Color.Blue, 5)), Is.EqualTo("set contains duplicate color Red"));

    [Test]
    public void Rejects_Five_Tile_Set() =>
        Assert.That(Fail(GroupType.Set, R(5), Tile.Of(Color.Blue, 5), Tile.Of(Color.Yellow, 5), Tile.Of(Color.Black, 5), J),
            Is.EqualTo("set has more than 4 tiles"));
}
