using Rummikub.Core;
using Rummikub.Solver.Model;
namespace Rummikub.Tests.Model;

[TestFixture]
public class RunMergerTests
{
    [Test] public void Merges_Touching_Runs() =>
        Assert.That(RunMerger.Merge([Run.Create(Color.Red, 1, 3, 0), Run.Create(Color.Red, 4, 4, 0)]),
            Is.EqualTo(new Group[] { Run.Create(Color.Red, 1, 7, 0) }));

    [Test] public void Keeps_Gapped_Or_Other_Colour_Runs() =>
        Assert.That(RunMerger.Merge([Run.Create(Color.Red, 1, 3, 0), Run.Create(Color.Red, 5, 3, 0), Run.Create(Color.Blue, 4, 3, 0)]),
            Has.Count.EqualTo(3));

    [Test] public void Does_Not_Exceed_Two_Jokers() =>
        Assert.That(RunMerger.Merge([Run.Create(Color.Red, 1, 3, 0b011), Run.Create(Color.Red, 4, 3, 0b001)]), Has.Count.EqualTo(2));
}
