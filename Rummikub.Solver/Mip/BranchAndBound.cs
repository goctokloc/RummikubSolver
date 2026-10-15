namespace Rummikub.Solver.Mip;

/// <summary>Depth-first branch and bound over the LP relaxation solved by <see cref="SimplexSolver"/>.</summary>
internal static class BranchAndBound
{
    const double Tolerance = 1e-6;

    /// <summary>
    /// Maximises <paramref name="model"/> starting from the feasible <paramref name="incumbentValues"/>.
    /// Returns the best solution found; <see cref="MipSolution.IsOptimal"/> is false when the node limit is hit
    /// or the token is cancelled before the search tree is exhausted, or when an LP relaxation fails (the search
    /// then stops and keeps the incumbent rather than throwing).
    /// </summary>
    public static MipSolution Solve(IntegerModel model, long[] incumbentValues, int maxNodes, CancellationToken cancellationToken,
        int pivotLimit = SimplexSolver.DefaultPivotLimit)
    {
        var variables = model.Variables;
        var n = variables.Count;
        if (incumbentValues.Length != n)
            throw new ArgumentException($"Incumbent has {incumbentValues.Length} values but the model has {n} variables.", nameof(incumbentValues));
        if (!model.IsFeasible(incumbentValues))
            throw new ArgumentException("Incumbent is not feasible for the model.", nameof(incumbentValues));

        var bestValues = (long[])incumbentValues.Clone();
        var bestObjective = model.Evaluate(bestValues);
        if (cancellationToken.IsCancellationRequested)
            return new MipSolution(bestValues, bestObjective, false);

        // The floor of the LP bound is a valid bound only when the objective takes integer values at every
        // integer-feasible point, i.e. every objective variable is integer (coefficients are always integers).
        var integralObjective = model.Objective.All(t => t.Coef == 0 || variables[t.Var].IsInteger);

        var rootLower = new double[n];
        var rootUpper = new double[n];
        for (var j = 0; j < n; j++)
        {
            rootLower[j] = variables[j].Lower;
            rootUpper[j] = variables[j].Upper == long.MaxValue ? double.PositiveInfinity : variables[j].Upper;
        }

        var stack = new Stack<(double[] Lower, double[] Upper)>();
        stack.Push((rootLower, rootUpper));
        var nodes = 0;
        var exhaustive = true;

        while (stack.Count > 0)
        {
            if (nodes >= maxNodes || cancellationToken.IsCancellationRequested)
                return new MipSolution(bestValues, bestObjective, false);
            nodes++;

            var (lower, upper) = stack.Pop();
            var lp = SimplexSolver.Solve(model, lower, upper, pivotLimit);
            if (lp.Status == LpStatus.Failed)
                return new MipSolution(bestValues, bestObjective, false);
            if (lp.Status == LpStatus.Infeasible)
                continue;
            if (lp.Status == LpStatus.Unbounded)
                throw new InvalidOperationException("The LP relaxation is unbounded; branch and bound requires a bounded model.");

            var cannotImprove = integralObjective
                ? Math.Floor(lp.Objective + Tolerance) <= bestObjective
                : lp.Objective <= bestObjective + Tolerance;
            if (cannotImprove)
                continue;

            var branchVar = ChooseBranch(model, lp.Values, lower, upper, out var integral);
            if (branchVar < 0)
            {
                if (integral is null)
                    exhaustive = false; // the node could not be split, so it was not fully explored
                else if (model.Evaluate(integral) > bestObjective)
                {
                    bestValues = integral;
                    bestObjective = model.Evaluate(integral);
                }

                continue;
            }

            var value = lp.Values[branchVar];
            var downUpper = (double[])upper.Clone();
            downUpper[branchVar] = Math.Floor(value);
            var upLower = (double[])lower.Clone();
            upLower[branchVar] = Math.Ceiling(value);

            // Pushed last, so the "x >= ceil(v)" child is explored first.
            stack.Push((lower, downUpper));
            stack.Push((upLower, upper));
        }

        return new MipSolution(bestValues, bestObjective, exhaustive);
    }

    /// <summary>
    /// Decides what to do with an LP point. Returns the variable to branch on, or -1 with
    /// <paramref name="integral"/> set when the rounded point is feasible. An integral-looking point that rounds
    /// infeasibly is branched on the variable furthest from an integer, however close; when no variable can split
    /// the node (every value exactly integral, or drifted outside its bounds), returns -1 with
    /// <paramref name="integral"/> null and the node must be dropped without claiming optimality.
    /// </summary>
    internal static int ChooseBranch(IntegerModel model, double[] values, double[] lower, double[] upper, out long[]? integral)
    {
        integral = null;
        var branchVar = MostFractional(model.Variables, values, lower, upper, Tolerance);
        if (branchVar >= 0)
            return branchVar;

        var candidate = new long[values.Length];
        for (var j = 0; j < values.Length; j++)
            candidate[j] = (long)Math.Round(values[j]);
        if (model.IsFeasible(candidate))
        {
            integral = candidate;
            return -1;
        }

        return MostFractional(model.Variables, values, lower, upper, 0);
    }

    /// <summary>Index of the integer variable furthest from an integer (ties: lowest index), or -1 if none is further than
    /// <paramref name="threshold"/>. Only variables whose branches both shrink the node are considered: a value that
    /// drifted outside its bounds (by the LP tolerance) cannot be split on.</summary>
    static int MostFractional(IReadOnlyList<Variable> variables, double[] values, double[] lower, double[] upper, double threshold)
    {
        var best = -1;
        var bestDistance = threshold;
        for (var j = 0; j < variables.Count; j++)
        {
            if (!variables[j].IsInteger)
                continue;
            var distance = Math.Abs(values[j] - Math.Round(values[j]));
            if (distance > bestDistance && Math.Floor(values[j]) >= lower[j] && Math.Ceiling(values[j]) <= upper[j])
            {
                best = j;
                bestDistance = distance;
            }
        }

        return best;
    }
}
