using Rummikub.Solver.Mip;
namespace Rummikub.Tests.Mip;

[TestFixture]
public class BranchAndBoundTests
{
    static IntegerModel Knapsack()
    {
        // max 5a + 4b + 3c; 2a + 3b + c <= 5; binary  -> 9 at (1, 1, 0)
        var m = new IntegerModel();
        var a = m.AddVariable(0, 1, true, "a");
        var b = m.AddVariable(0, 1, true, "b");
        var c = m.AddVariable(0, 1, true, "c");
        m.AddConstraint([(a, 2), (b, 3), (c, 1)], Sense.LessOrEqual, 5);
        m.SetObjective([(a, 5), (b, 4), (c, 3)]);
        return m;
    }

    static IntegerModel OddCycle()
    {
        // max x1 + x2 + x3; pairwise sums <= 1; binary. LP = 1.5, IP = 1
        var m = new IntegerModel();
        var x = Enumerable.Range(0, 3).Select(i => m.AddVariable(0, 1, true, $"x{i}")).ToArray();
        m.AddConstraint([(x[0], 1), (x[1], 1)], Sense.LessOrEqual, 1);
        m.AddConstraint([(x[1], 1), (x[2], 1)], Sense.LessOrEqual, 1);
        m.AddConstraint([(x[0], 1), (x[2], 1)], Sense.LessOrEqual, 1);
        m.SetObjective([(x[0], 1), (x[1], 1), (x[2], 1)]);
        return m;
    }

    [Test] public void Solves_Knapsack()
    {
        var s = BranchAndBound.Solve(Knapsack(), [0, 0, 0], 1000, CancellationToken.None);
        Assert.That(s.Objective, Is.EqualTo(9));
        Assert.That(s.Values, Is.EqualTo(new long[] { 1, 1, 0 }));
        Assert.That(s.IsOptimal, Is.True);
    }

    [Test] public void Resolves_Fractional_Root()
    {
        var s = BranchAndBound.Solve(OddCycle(), [0, 0, 0], 1000, CancellationToken.None);
        Assert.That(s.Objective, Is.EqualTo(1));
        Assert.That(s.IsOptimal, Is.True);
    }

    [Test] public void General_Integer_Bounds()
    {
        // max x + y; 2x + 2y <= 7; x, y in [0, 10] integer -> 3
        var m = new IntegerModel();
        var x = m.AddVariable(0, 10, true, "x");
        var y = m.AddVariable(0, 10, true, "y");
        m.AddConstraint([(x, 2), (y, 2)], Sense.LessOrEqual, 7);
        m.SetObjective([(x, 1), (y, 1)]);
        Assert.That(BranchAndBound.Solve(m, [0, 0], 1000, CancellationToken.None).Objective, Is.EqualTo(3));
    }

    [Test] public void Node_Limit_Returns_Feasible_Non_Optimal()
    {
        var m = OddCycle();
        var s = BranchAndBound.Solve(m, [0, 0, 0], 1, CancellationToken.None);
        Assert.That(s.IsOptimal, Is.False);
        Assert.That(m.IsFeasible(s.Values), Is.True);
    }

    [Test] public void Cancelled_Token_Returns_Incumbent()
    {
        var s = BranchAndBound.Solve(Knapsack(), [0, 0, 1], 1000, new CancellationToken(true));
        Assert.That(s.Values, Is.EqualTo(new long[] { 0, 0, 1 }));
        Assert.That(s.Objective, Is.EqualTo(3));
        Assert.That(s.IsOptimal, Is.False);
    }

    [Test] public void Infeasible_Incumbent_Throws() =>
        Assert.Throws<ArgumentException>(() => BranchAndBound.Solve(Knapsack(), [1, 1, 1], 1000, CancellationToken.None));

    [Test] public void Optimal_Incumbent_Is_Kept_And_Proven()
    {
        var s = BranchAndBound.Solve(Knapsack(), [1, 1, 0], 1000, CancellationToken.None);
        Assert.That(s.Values, Is.EqualTo(new long[] { 1, 1, 0 }));
        Assert.That(s.Objective, Is.EqualTo(9));
        Assert.That(s.IsOptimal, Is.True);
    }

    [Test] public void Wrong_Length_Incumbent_Throws() =>
        Assert.Throws<ArgumentException>(() => BranchAndBound.Solve(Knapsack(), [0, 0], 1000, CancellationToken.None));

    [Test] public void Failed_Lp_Returns_Incumbent_Not_Optimal()
    {
        // A pivot limit of zero makes the root LP fail: the search must stop with the incumbent instead of throwing.
        var s = BranchAndBound.Solve(Knapsack(), [0, 0, 1], 1000, CancellationToken.None, pivotLimit: 0);
        Assert.That(s.Values, Is.EqualTo(new long[] { 0, 0, 1 }));
        Assert.That(s.IsOptimal, Is.False);
    }
}
