using Rummikub.Core;

namespace Rummikub.Tests.Groups;

[TestFixture]
public class GroupOrderingTests
{
    [Test]
    public void Sets_Sort_Before_Runs_Then_By_Fields()
    {
        Group[] groups =
        [
            Run.Create(Color.Blue, 1, 3, 0),
            Set.Create(5, 0b0111, 0),
            Run.Create(Color.Red, 4, 3, 0),
            Set.Create(3, 0b0111, 0),
            Run.Create(Color.Red, 2, 3, 0),
        ];
        Assert.That(groups.Order().ToArray(), Is.EqualTo(new Group[]
        {
            Set.Create(3, 0b0111, 0), Set.Create(5, 0b0111, 0),
            Run.Create(Color.Red, 2, 3, 0), Run.Create(Color.Red, 4, 3, 0),
            Run.Create(Color.Blue, 1, 3, 0),
        }));
    }

    [Test]
    public void Run_Never_Equals_Set() =>
        Assert.That(Run.Create(Color.Red, 1, 3, 0), Is.Not.EqualTo((Group)Set.Create(1, 0b0111, 0)));
}
