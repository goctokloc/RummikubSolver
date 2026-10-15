using Rummikub.Solver.Mip;
namespace Rummikub.Tests.Mip;

[TestFixture]
public class SimplexSolverTests
{
    const long Inf = long.MaxValue;

    // Wyndor: max 3x + 5y; x <= 4; 2y <= 12; 3x + 2y <= 18  -> 36 at (2, 6)
    static IntegerModel Wyndor()
    {
        var m = new IntegerModel();
        var x = m.AddVariable(0, Inf, false, "x");
        var y = m.AddVariable(0, Inf, false, "y");
        m.AddConstraint([(x, 1)], Sense.LessOrEqual, 4);
        m.AddConstraint([(y, 2)], Sense.LessOrEqual, 12);
        m.AddConstraint([(x, 3), (y, 2)], Sense.LessOrEqual, 18);
        m.SetObjective([(x, 3), (y, 5)]);
        return m;
    }

    [Test] public void Solves_Textbook_Lp()
    {
        var r = SimplexSolver.Solve(Wyndor());
        Assert.That(r.Status, Is.EqualTo(LpStatus.Optimal));
        Assert.That(r.Objective, Is.EqualTo(36).Within(1e-9));
        Assert.That(r.Values, Is.EqualTo(new[] { 2.0, 6.0 }).Within(1e-9));
    }

    [Test] public void Bound_Override_Is_Respected()
    {
        var r = SimplexSolver.Solve(Wyndor(), upper: [1, double.PositiveInfinity]);
        Assert.That(r.Objective, Is.EqualTo(33).Within(1e-9));   // x = 1, y = 6
    }

    [Test] public void Equality_And_GreaterOrEqual_Constraints()
    {
        // max x + 2y; x + y = 4; x >= 1; y <= 2 (variable bound)  -> 6 at (2, 2)
        var m = new IntegerModel();
        var x = m.AddVariable(0, Inf, false, "x");
        var y = m.AddVariable(0, 2, false, "y");
        m.AddConstraint([(x, 1), (y, 1)], Sense.Equal, 4);
        m.AddConstraint([(x, 1)], Sense.GreaterOrEqual, 1);
        m.SetObjective([(x, 1), (y, 2)]);
        var r = SimplexSolver.Solve(m);
        Assert.That(r.Objective, Is.EqualTo(6).Within(1e-9));
        Assert.That(r.Values, Is.EqualTo(new[] { 2.0, 2.0 }).Within(1e-9));
    }

    [Test] public void Detects_Infeasible()
    {
        var m = new IntegerModel();
        var x = m.AddVariable(0, Inf, false, "x");
        var y = m.AddVariable(0, Inf, false, "y");
        m.AddConstraint([(x, 1), (y, 1)], Sense.LessOrEqual, 1);
        m.AddConstraint([(x, 1), (y, 1)], Sense.GreaterOrEqual, 3);
        m.SetObjective([(x, 1)]);
        Assert.That(SimplexSolver.Solve(m).Status, Is.EqualTo(LpStatus.Infeasible));
    }

    [Test] public void Detects_Unbounded()
    {
        var m = new IntegerModel();
        var x = m.AddVariable(0, Inf, false, "x");
        var y = m.AddVariable(0, Inf, false, "y");
        m.AddConstraint([(x, 1), (y, -1)], Sense.LessOrEqual, 1);
        m.SetObjective([(x, 1)]);
        Assert.That(SimplexSolver.Solve(m).Status, Is.EqualTo(LpStatus.Unbounded));
    }

    [Test] public void Beale_Cycling_Example_Terminates_At_Optimum()
    {
        // Beale's example scaled to integers: max 3a - 80b + 2c - 24d
        // a - 32b - 4c + 36d <= 0;  a - 24b - c + 6d <= 0;  c <= 1   -> 5 at a = 1, c = 1
        var m = new IntegerModel();
        var a = m.AddVariable(0, Inf, false, "a");
        var b = m.AddVariable(0, Inf, false, "b");
        var c = m.AddVariable(0, Inf, false, "c");
        var d = m.AddVariable(0, Inf, false, "d");
        m.AddConstraint([(a, 1), (b, -32), (c, -4), (d, 36)], Sense.LessOrEqual, 0);
        m.AddConstraint([(a, 1), (b, -24), (c, -1), (d, 6)], Sense.LessOrEqual, 0);
        m.AddConstraint([(c, 1)], Sense.LessOrEqual, 1);
        m.SetObjective([(a, 3), (b, -80), (c, 2), (d, -24)]);
        var r = SimplexSolver.Solve(m);
        Assert.That(r.Status, Is.EqualTo(LpStatus.Optimal));
        Assert.That(r.Objective, Is.EqualTo(5).Within(1e-9));
    }

    [Test] public void Lower_Bound_Override_Is_Respected()
    {
        // max 3x + 5y with y forced >= 7 is infeasible (2y <= 12); with x >= 3 the optimum is 9 + 5 * 4.5 = 31.5
        Assert.That(SimplexSolver.Solve(Wyndor(), lower: [0, 7]).Status, Is.EqualTo(LpStatus.Infeasible));
        var r = SimplexSolver.Solve(Wyndor(), lower: [3, 0]);
        Assert.That(r.Status, Is.EqualTo(LpStatus.Optimal));
        Assert.That(r.Values, Is.EqualTo(new[] { 3.0, 4.5 }).Within(1e-9));
        Assert.That(r.Objective, Is.EqualTo(31.5).Within(1e-9));
    }

    [Test] public void Crossed_Bound_Overrides_Are_Infeasible()
    {
        var r = SimplexSolver.Solve(Wyndor(), lower: [2, 0], upper: [1, double.PositiveInfinity]);
        Assert.That(r.Status, Is.EqualTo(LpStatus.Infeasible));
        Assert.That(r.Values, Is.Empty);
    }

    [Test] public void Variable_Fixed_By_Equal_Bounds_Stays_Fixed()
    {
        var r = SimplexSolver.Solve(Wyndor(), lower: [2, 0], upper: [2, double.PositiveInfinity]);
        Assert.That(r.Values, Is.EqualTo(new[] { 2.0, 6.0 }).Within(1e-9));

        r = SimplexSolver.Solve(Wyndor(), lower: [4, 0], upper: [4, double.PositiveInfinity]);
        Assert.That(r.Values, Is.EqualTo(new[] { 4.0, 3.0 }).Within(1e-9));
        Assert.That(r.Objective, Is.EqualTo(27).Within(1e-9));
    }

    [Test] public void Model_Without_Constraints_Uses_Bounds()
    {
        var m = new IntegerModel();
        var x = m.AddVariable(1, 3, false, "x");
        var y = m.AddVariable(-2, 5, false, "y");
        m.SetObjective([(x, 2), (y, -1)]);
        var r = SimplexSolver.Solve(m);
        Assert.That(r.Values, Is.EqualTo(new[] { 3.0, -2.0 }).Within(1e-9));
        Assert.That(r.Objective, Is.EqualTo(8).Within(1e-9));
    }

    [Test] public void Redundant_Equality_Rows_Are_Handled()
    {
        // x + y = 2 stated twice and its double: the extra artificials cannot be pivoted out.
        var m = new IntegerModel();
        var x = m.AddVariable(0, Inf, false, "x");
        var y = m.AddVariable(0, 1, false, "y");
        m.AddConstraint([(x, 1), (y, 1)], Sense.Equal, 2);
        m.AddConstraint([(x, 1), (y, 1)], Sense.Equal, 2);
        m.AddConstraint([(x, 2), (y, 2)], Sense.Equal, 4);
        m.SetObjective([(x, 1), (y, 3)]);
        var r = SimplexSolver.Solve(m);
        Assert.That(r.Values, Is.EqualTo(new[] { 1.0, 1.0 }).Within(1e-9));
        Assert.That(r.Objective, Is.EqualTo(4).Within(1e-9));
    }

    [TestCase(1)]
    [TestCase(2)]
    [TestCase(3)]
    public void Degenerate_Assignment_Lp_Matches_Brute_Force(int seed)
    {
        // Assignment LPs are totally unimodular and highly degenerate: the LP optimum equals the best permutation.
        const int size = 7;
        var random = new Random(seed);
        var cost = new long[size, size];
        for (var i = 0; i < size; i++)
            for (var j = 0; j < size; j++)
                cost[i, j] = random.Next(0, 4); // many ties on purpose

        var m = new IntegerModel();
        var v = new int[size, size];
        var objective = new List<(int, long)>();
        for (var i = 0; i < size; i++)
            for (var j = 0; j < size; j++)
            {
                v[i, j] = m.AddVariable(0, Inf, false, $"x{i}{j}");
                objective.Add((v[i, j], cost[i, j]));
            }

        for (var i = 0; i < size; i++)
        {
            m.AddConstraint(Enumerable.Range(0, size).Select(j => (v[i, j], 1L)).ToList(), Sense.Equal, 1);
            m.AddConstraint(Enumerable.Range(0, size).Select(j => (v[j, i], 1L)).ToList(), Sense.LessOrEqual, 1);
        }

        m.SetObjective(objective);

        var r = SimplexSolver.Solve(m);
        Assert.That(r.Status, Is.EqualTo(LpStatus.Optimal));
        Assert.That(r.Objective, Is.EqualTo(BestPermutation(cost, size)).Within(1e-9));
    }

    static long BestPermutation(long[,] cost, int size)
    {
        long best = long.MinValue;
        var perm = Enumerable.Range(0, size).ToArray();
        void Recurse(int k, long acc)
        {
            if (k == size)
            {
                best = Math.Max(best, acc);
                return;
            }

            for (var i = k; i < size; i++)
            {
                (perm[k], perm[i]) = (perm[i], perm[k]);
                Recurse(k + 1, acc + cost[k, perm[k]]);
                (perm[k], perm[i]) = (perm[i], perm[k]);
            }
        }

        Recurse(0, 0);
        return best;
    }
}
