using Rummikub.Core;

namespace Rummikub.Tests.Groups;

[TestFixture]
public class SetTests
{
    const byte R = 1, B = 2, Y = 4, K = 8;

    [Test]
    public void Slots_Ordered_By_Color_With_Joker_Meaning()
    {
        var set = Set.Create(7, R | B | K, K);
        Assert.That(set.Slots, Is.EqualTo(new[]
        {
            new GroupSlot(Tile.Of(Color.Red, 7), Color.Red, 7),
            new GroupSlot(Tile.Of(Color.Blue, 7), Color.Blue, 7),
            new GroupSlot(Tile.Joker, Color.Black, 7),
        }));
        Assert.That(set.Type, Is.EqualTo(GroupType.Set));
    }

    [TestCase(0, R | B | Y, 0)]             // value below 1
    [TestCase(14, R | B | Y, 0)]            // value above 13
    [TestCase(5, R | B, 0)]                 // 2 colours
    [TestCase(5, 0b1_0111, 0)]              // bit outside low 4
    [TestCase(5, R | B | Y, K)]             // joker colour not in Colors
    [TestCase(5, R | B | Y | K, R | B | Y)] // 3 jokers
    public void Create_Rejects_Invalid(int value, int colors, int jokers) =>
        Assert.Throws<ArgumentException>(() => Set.Create(value, (byte)colors, (byte)jokers));

    [Test]
    public void CreateFillingJokers_Uses_Lowest_Missing_Colors()
    {
        Assert.That(Set.CreateFillingJokers(4, B | K, 1), Is.EqualTo(Set.Create(4, R | B | K, R)));
        Assert.That(Set.CreateFillingJokers(4, K, 2), Is.EqualTo(Set.Create(4, R | B | K, R | B)));
    }

    [Test]
    public void CreateFillingJokers_Rejects_More_Than_Four_Tiles() =>
        Assert.Throws<ArgumentException>(() => Set.CreateFillingJokers(4, R | B | Y | K, 1));

    [Test]
    public void Equal_Structure_Is_Equal_With_Same_Hash()
    {
        var a = Set.Create(9, R | Y | K, 0);
        var b = Set.Create(9, R | Y | K, 0);
        Assert.That(a, Is.EqualTo(b));
        Assert.That(a.GetHashCode(), Is.EqualTo(b.GetHashCode()));
    }
}
