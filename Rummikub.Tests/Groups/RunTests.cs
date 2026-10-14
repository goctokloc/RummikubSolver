using Rummikub.Core;

namespace Rummikub.Tests.Groups;

[TestFixture]
public class RunTests
{
    [Test]
    public void Slots_Report_Joker_Meaning()
    {
        var run = Run.Create(Color.Red, 3, 3, 0b010);
        Assert.That(run.Slots, Is.EqualTo(new[]
        {
            new GroupSlot(Tile.Of(Color.Red, 3), Color.Red, 3),
            new GroupSlot(Tile.Joker, Color.Red, 4),
            new GroupSlot(Tile.Of(Color.Red, 5), Color.Red, 5),
        }));
        Assert.That(run.JokerCount, Is.EqualTo(1));
        Assert.That(run.Count, Is.EqualTo(3));
        Assert.That(run.Type, Is.EqualTo(GroupType.Run));
    }

    [Test]
    public void Equal_Structure_Is_Equal_With_Same_Hash()
    {
        var a = Run.Create(Color.Blue, 5, 4, 0);
        var b = Run.Create(Color.Blue, 5, 4, 0);
        Assert.That(a, Is.EqualTo(b));
        Assert.That(a.GetHashCode(), Is.EqualTo(b.GetHashCode()));
    }

    [TestCase(1, 2, (ushort)0)]          // too short
    [TestCase(1, 14, (ushort)0)]         // too long
    [TestCase(0, 3, (ushort)0)]          // start below 1
    [TestCase(12, 3, (ushort)0)]         // runs past 13
    [TestCase(1, 3, (ushort)0b1000)]     // mask bit beyond length
    [TestCase(1, 4, (ushort)0b0111)]     // 3 jokers
    [TestCase(1, 3, (ushort)0b0111)]     // no regular tile (and 3 jokers)
    public void Create_Rejects_Invalid(int start, int length, ushort mask) =>
        Assert.Throws<ArgumentException>(() => Run.Create(Color.Red, start, length, mask));

    [Test]
    public void Max_Length_Run_Is_Valid() =>
        Assert.That(Run.Create(Color.Black, 1, 13, 0b1_0000_0000_0001).Count, Is.EqualTo(13));
}
