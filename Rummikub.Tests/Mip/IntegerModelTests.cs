using Rummikub.Solver.Mip;
namespace Rummikub.Tests.Mip;

[TestFixture]
public class IntegerModelTests
{
    static IntegerModel TwoVarModel()
    {
        var m = new IntegerModel();
        var x = m.AddVariable(0, 10, true, "x");
        var y = m.AddVariable(0, 10, true, "y");
        m.AddConstraint([(x, 1), (y, 1)], Sense.LessOrEqual, 4);
        m.AddConstraint([(x, 1)], Sense.GreaterOrEqual, 1);
        m.SetObjective([(x, 3), (y, 2)]);
        return m;
    }

    [Test] public void AddVariable_Returns_Sequential_Indices()
    {
        var m = new IntegerModel();
        Assert.That(m.AddVariable(0, 1, true, "a"), Is.EqualTo(0));
        Assert.That(m.AddVariable(0, 1, true, "b"), Is.EqualTo(1));
    }

    [Test] public void Evaluate_Computes_Objective() =>
        Assert.That(TwoVarModel().Evaluate([2, 1]), Is.EqualTo(8));

    [TestCase(new long[] { 1, 3 }, true)]
    [TestCase(new long[] { 0, 3 }, false)]   // x >= 1 violated
    [TestCase(new long[] { 3, 2 }, false)]   // x + y <= 4 violated
    [TestCase(new long[] { 11, 0 }, false)]  // bound violated
    public void IsFeasible_Checks_Bounds_And_Constraints(long[] values, bool expected) =>
        Assert.That(TwoVarModel().IsFeasible(values), Is.EqualTo(expected));

    [Test] public void Lower_Above_Upper_Throws() =>
        Assert.Throws<ArgumentException>(() => new IntegerModel().AddVariable(2, 1, true, "z"));

    [Test] public void Unknown_Variable_In_Term_Throws() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new IntegerModel().AddConstraint([(0, 1)], Sense.Equal, 0));
}
