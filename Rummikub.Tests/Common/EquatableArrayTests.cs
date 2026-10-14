using Rummikub.Core.Common;

namespace Rummikub.Tests.Common;

[TestFixture]
public class EquatableArrayTests
{
    [Test]
    public void Same_Sequence_Is_Equal_With_Same_Hash()
    {
        var a = new[] { 1, 2, 3 }.ToEquatableArray();
        var b = new[] { 1, 2, 3 }.ToEquatableArray();
        Assert.That(a, Is.EqualTo(b));
        Assert.That(a == b, Is.True);
        Assert.That(a.GetHashCode(), Is.EqualTo(b.GetHashCode()));
    }

    [Test]
    public void Different_Order_Is_Not_Equal()
    {
        Assert.That(new[] { 1, 2 }.ToEquatableArray() != new[] { 2, 1 }.ToEquatableArray(), Is.True);
    }

    [Test]
    public void Default_Behaves_As_Empty()
    {
        EquatableArray<int> d = default;
        Assert.That(d.Count, Is.EqualTo(0));
        Assert.That(d.ToList(), Is.Empty);
        Assert.That(d, Is.EqualTo(EquatableArray<int>.Empty));
        Assert.That(d.GetHashCode(), Is.EqualTo(EquatableArray<int>.Empty.GetHashCode()));
    }

    [Test]
    public void Indexer_And_Count_Reflect_Items()
    {
        var a = new[] { "x", "y" }.ToEquatableArray();
        Assert.That(a.Count, Is.EqualTo(2));
        Assert.That(a[1], Is.EqualTo("y"));
    }
}
