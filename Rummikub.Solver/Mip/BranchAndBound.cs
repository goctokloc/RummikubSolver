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

            var branchVar = MostFractional(variables, lp.Values);
            if (branchVar < 0)
            {
                var candidate = new long[n];
                for (var j = 0; j < n; j++)
                    candidate[j] = (long)Math.Round(lp.Values[j]);
                if (model.IsFeasible(candidate))
                {
                    var objective = model.Evaluate(candidate);
                    if (objective > bestObjective)
                    {
                        bestValues = candidate;
                        bestObjective = objective;
                    }
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

        return new MipSolution(bestValues, bestObjective, true);
    }

    /// <summary>Index of the integer variable furthest from an integer (ties: lowest index), or -1 if none is fractional.</summary>
    static int MostFractional(IReadOnlyList<Variable> variables, double[] values)
    {
        var best = -1;
        var bestDistance = Tolerance;
        for (var j = 0; j < variables.Count; j++)
        {
            if (!variables[j].IsInteger)
                continue;
            var distance = Math.Abs(values[j] - Math.Round(values[j]));
            if (distance > bestDistance)
            {
                best = j;
                bestDistance = distance;
            }
        }

        return best;
    }
}
