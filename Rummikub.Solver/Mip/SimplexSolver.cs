using System.Numerics;
using System.Runtime.InteropServices;

namespace Rummikub.Solver.Mip;

/// <summary>
/// Two-phase, bounded-variable primal simplex on a dense tableau. Solves the LP relaxation of an
/// <see cref="IntegerModel"/> (integrality is ignored). Pricing is Dantzig, falling back to Bland's rule
/// after a run of degenerate pivots and keeping it for the rest of the solve (phase 2 included), so that degenerate
/// models cannot cycle. A solve that still exceeds the pivot limit reports <see cref="LpStatus.Failed"/> instead of throwing.
/// </summary>
internal static class SimplexSolver
{
    public const int DefaultPivotLimit = 50_000;

    public static LpResult Solve(IntegerModel model, double[]? lower = null, double[]? upper = null, int pivotLimit = DefaultPivotLimit)
    {
        var n = model.Variables.Count;
        if (lower is not null && lower.Length != n)
            throw new ArgumentException($"Expected {n} lower bounds, got {lower.Length}.", nameof(lower));
        if (upper is not null && upper.Length != n)
            throw new ArgumentException($"Expected {n} upper bounds, got {upper.Length}.", nameof(upper));

        var lo = new double[n];
        var up = new double[n];
        for (var j = 0; j < n; j++)
        {
            var variable = model.Variables[j];
            lo[j] = lower?[j] ?? variable.Lower;
            up[j] = upper?[j] ?? (variable.Upper == long.MaxValue ? double.PositiveInfinity : variable.Upper);
            if (double.IsNaN(lo[j]) || double.IsNaN(up[j]) || double.IsPositiveInfinity(lo[j]) || double.IsNegativeInfinity(up[j]))
                throw new ArgumentException($"Invalid bounds [{lo[j]}, {up[j]}] for variable '{variable.Name}'.");
            if (lo[j] > up[j])
                return new LpResult(LpStatus.Infeasible, double.NegativeInfinity, []);
        }

        return new Tableau(model, lo, up, pivotLimit).Run();
    }

    /// <summary>
    /// Column layout: structurals [0, n), slacks [n, n + m), artificials [n + m, n + 2m).
    /// Row i reads <c>Σ a_ij x_j + s_i + σ_i r_i = rhs_i</c> where σ_i makes the starting artificial non-negative.
    /// Every nonbasic variable sits exactly on one of its bounds (or at 0 when it has none).
    /// Artificial columns are not stored: each artificial starts basic, is never priced, and once it leaves the
    /// basis it is fixed at zero, so its column (part of the basis inverse) is never needed.
    /// </summary>
    sealed class Tableau
    {
        const double CostTolerance = 1e-9;
        const double PivotTolerance = 1e-7; // pivoting on smaller entries let degenerate LPs blow up numerically
        const double FeasibilityTolerance = 1e-9;
        const double StepTolerance = 1e-12;
        const double ZeroTolerance = 1e-9; // tableau entries this small are treated as zero
        const int DegenerateRunBeforeBland = 50;

        readonly int _n;
        readonly int _m;
        readonly int _width; // tableau row stride: structurals and slacks; artificial columns are never stored
        readonly double[] _t;
        readonly double[] _lo;
        readonly double[] _up;
        readonly double[] _x;
        readonly double[] _c;
        readonly double[] _d;
        readonly int[] _basis;
        readonly bool[] _isBasic;
        readonly int[] _nonzero;
        readonly int _pivotLimit;
        int _pivots;
        bool _bland; // once Bland's rule is triggered it stays on for the rest of the solve, phase 2 included

        public Tableau(IntegerModel model, double[] structuralLower, double[] structuralUpper, int pivotLimit)
        {
            _pivotLimit = pivotLimit;
            _n = model.Variables.Count;
            _m = model.Constraints.Count;
            _width = _n + _m;
            var variables = _n + 2 * _m;
            _t = new double[_m * _width];
            _lo = new double[variables];
            _up = new double[variables];
            _x = new double[variables];
            _c = new double[variables];
            _d = new double[variables];
            _basis = new int[_m];
            _isBasic = new bool[variables];
            _nonzero = new int[_width];

            for (var j = 0; j < _n; j++)
            {
                _lo[j] = structuralLower[j];
                _up[j] = structuralUpper[j];
                _x[j] = !double.IsInfinity(_lo[j]) ? _lo[j] : !double.IsInfinity(_up[j]) ? _up[j] : 0;
            }

            for (var i = 0; i < _m; i++)
            {
                var constraint = model.Constraints[i];
                var row = i * _width;
                var slack = _n + i;
                var artificial = _n + _m + i;

                (_lo[slack], _up[slack]) = constraint.Sense switch
                {
                    Sense.LessOrEqual => (0.0, double.PositiveInfinity),
                    Sense.GreaterOrEqual => (double.NegativeInfinity, 0.0),
                    Sense.Equal => (0.0, 0.0),
                    _ => throw new InvalidOperationException($"Unknown sense {constraint.Sense}."),
                };

                double residual = constraint.Rhs;
                foreach (var (index, coef) in constraint.Terms)
                {
                    _t[row + index] += coef;
                    residual -= coef * _x[index];
                }

                _t[row + slack] = 1;
                if (residual < 0)
                {
                    for (var j = 0; j < _width; j++)
                        _t[row + j] = -_t[row + j];
                }

                _lo[artificial] = 0;
                _up[artificial] = double.PositiveInfinity;
                _x[artificial] = Math.Abs(residual);
                _basis[i] = artificial;
                _isBasic[artificial] = true;
            }

            foreach (var (index, coef) in model.Objective)
                _c[index] += coef;
        }

        public LpResult Run()
        {
            try
            {
                return RunPhases();
            }
            catch (SimplexFailedException)
            {
                return new LpResult(LpStatus.Failed, double.NaN, []);
            }
        }

        LpResult RunPhases()
        {
            // Phase 1: maximise -Σ artificials using the phase-1 costs; keep the model costs aside.
            var objective = _c[.._n];
            Array.Clear(_c);
            for (var i = 0; i < _m; i++)
                _c[_n + _m + i] = -1;
            ComputeReducedCosts();
            if (!Iterate(phaseOne: true))
                throw new SimplexFailedException(); // phase 1 is bounded by construction: only numerical trouble gets here

            double infeasibility = 0;
            for (var i = 0; i < _m; i++)
            {
                if (IsArtificial(_basis[i]))
                    infeasibility += _x[_basis[i]];
            }

            if (infeasibility > FeasibilityTolerance)
                return new LpResult(LpStatus.Infeasible, double.NegativeInfinity, []);

            DriveOutArtificials();
            for (var j = _n + _m; j < _x.Length; j++)
            {
                _lo[j] = _up[j] = 0;
                _x[j] = 0;
            }

            // Phase 2: artificials are fixed at zero (they were never priced, having no stored column).
            Array.Clear(_c);
            objective.CopyTo(_c, 0);
            ComputeReducedCosts();
            if (!Iterate(phaseOne: false))
                return new LpResult(LpStatus.Unbounded, double.PositiveInfinity, []);

            var values = _x[.._n];
            double total = 0;
            for (var j = 0; j < _n; j++)
                total += objective[j] * values[j];
            return new LpResult(LpStatus.Optimal, total, values);
        }

        bool IsArtificial(int column) => column >= _n + _m;

        /// <summary>Runs simplex iterations to optimality. Returns false when the objective is unbounded.</summary>
        bool Iterate(bool phaseOne)
        {
            var degenerateRun = 0;
            var reducedCostsFresh = true;

            while (true)
            {
                var entering = ChooseEntering(_bland, out var direction);
                if (entering < 0)
                {
                    if (reducedCostsFresh)
                        return true;

                    // Confirm optimality against reduced costs recomputed from scratch, not incrementally updated ones.
                    ComputeReducedCosts();
                    reducedCostsFresh = true;
                    continue;
                }

                reducedCostsFresh = false;
                if (++_pivots > _pivotLimit)
                    throw new SimplexFailedException();

                var step = RatioTest(entering, direction, _bland, out var leavingRow, out var leavingToUpper);
                if (double.IsPositiveInfinity(step))
                    return false;

                if (step > 0)
                {
                    _x[entering] += direction * step;
                    for (var i = 0; i < _m; i++)
                    {
                        var a = _t[i * _width + entering];
                        if (a != 0)
                            _x[_basis[i]] -= direction * step * a;
                    }
                }

                if (leavingRow < 0)
                {
                    // Bound flip: the entering variable reached its opposite bound before any basic variable blocked.
                    _x[entering] = direction > 0 ? _up[entering] : _lo[entering];
                }
                else
                {
                    var leaving = _basis[leavingRow];
                    _x[leaving] = leavingToUpper ? _up[leaving] : _lo[leaving];
                    Pivot(leavingRow, entering);
                    if (phaseOne && IsArtificial(leaving))
                        _up[leaving] = 0; // it has no stored column, so it can never re-enter
                }

                if (step <= StepTolerance)
                {
                    if (++degenerateRun >= DegenerateRunBeforeBland)
                        _bland = true;
                }
                else
                {
                    // Bland stays on once triggered: switching back to Dantzig on tolerance-sized steps can cycle.
                    degenerateRun = 0;
                }
            }
        }

        int ChooseEntering(bool bland, out int direction)
        {
            var best = -1;
            var bestScore = 0.0;
            direction = 0;
            for (var j = 0; j < _width; j++)
            {
                if (_isBasic[j])
                    continue;

                var dj = _d[j];
                int dir;
                if (dj > CostTolerance && _x[j] < _up[j])
                    dir = 1;
                else if (dj < -CostTolerance && _x[j] > _lo[j])
                    dir = -1;
                else
                    continue;

                if (bland)
                {
                    direction = dir;
                    return j;
                }

                var score = Math.Abs(dj);
                if (score > bestScore)
                {
                    best = j;
                    bestScore = score;
                    direction = dir;
                }
            }

            return best;
        }

        /// <summary>
        /// Bounded Harris ratio test. Returns the step length (positive infinity if unlimited) and the blocking row,
        /// or -1 when the entering variable's own bound blocks first (a bound flip). Pass 1 finds the longest step
        /// that keeps every basic variable within its bounds relaxed by <see cref="FeasibilityTolerance"/>; pass 2
        /// picks, among the rows blocking within that step, the largest pivot element, so tiny (noise-sized)
        /// entries are pivoted on only when nothing else blocks. Under Bland's rule the lowest basic index wins
        /// among pivots of at least <see cref="PivotTolerance"/>.
        /// </summary>
        double RatioTest(int entering, int direction, bool bland, out int leavingRow, out bool leavingToUpper)
        {
            leavingRow = -1;
            leavingToUpper = false;
            var flip = direction > 0 ? _up[entering] - _x[entering] : _x[entering] - _lo[entering];

            var relaxed = double.PositiveInfinity;
            for (var i = 0; i < _m; i++)
            {
                var a = _t[i * _width + entering];
                if (Math.Abs(a) > ZeroTolerance)
                    relaxed = Math.Min(relaxed, Limit(i, -direction * a, FeasibilityTolerance, out _));
            }

            if (flip <= relaxed)
                return flip;

            var bestPivot = 0.0;
            var bestLimit = 0.0;
            for (var i = 0; i < _m; i++)
            {
                var a = _t[i * _width + entering];
                var pivot = Math.Abs(a);
                if (pivot <= ZeroTolerance)
                    continue;

                var limit = Limit(i, -direction * a, 0, out var toUpper);
                if (limit > relaxed)
                    continue;

                bool take;
                if (leavingRow < 0)
                    take = true;
                else if (bland && pivot >= PivotTolerance)
                    take = bestPivot < PivotTolerance || _basis[i] < _basis[leavingRow];
                else
                    take = (!bland || bestPivot < PivotTolerance) && pivot > bestPivot;

                if (take)
                {
                    bestPivot = pivot;
                    bestLimit = limit;
                    leavingRow = i;
                    leavingToUpper = toUpper;
                }
            }

            return Math.Max(bestLimit, 0);
        }

        /// <summary>
        /// Step at which row <paramref name="row"/>'s basic variable, changing by <paramref name="rate"/> per unit,
        /// reaches its bound relaxed by <paramref name="slack"/>; positive infinity when that bound is infinite.
        /// </summary>
        double Limit(int row, double rate, double slack, out bool toUpper)
        {
            var basic = _basis[row];
            toUpper = rate > 0;
            return toUpper
                ? double.IsPositiveInfinity(_up[basic]) ? double.PositiveInfinity : (_up[basic] - _x[basic] + slack) / rate
                : double.IsNegativeInfinity(_lo[basic]) ? double.PositiveInfinity : (_x[basic] - _lo[basic] + slack) / -rate;
        }

        void Pivot(int row, int column)
        {
            var pivotRow = _t.AsSpan(row * _width, _width);
            var inverse = 1.0 / pivotRow[column];
            var count = 0;
            for (var j = 0; j < pivotRow.Length; j++)
            {
                var value = pivotRow[j];
                if (value == 0)
                    continue;

                pivotRow[j] = value * inverse;
                _nonzero[count++] = j;
            }

            pivotRow[column] = 1;

            // A sparse pivot row is applied through its nonzero indices; a dense one with a vectorised sweep.
            var nonzero = _nonzero.AsSpan(0, count);
            var sparse = count * 8 < _width;
            for (var i = 0; i < _m; i++)
            {
                if (i == row)
                    continue;

                var target = _t.AsSpan(i * _width, _width);
                var factor = target[column];
                if (factor == 0)
                    continue;

                SubtractScaled(target, pivotRow, factor, sparse, nonzero);
                target[column] = 0;
            }

            var costFactor = _d[column];
            if (costFactor != 0)
                SubtractScaled(_d.AsSpan(0, _width), pivotRow, costFactor, sparse, nonzero);

            _d[column] = 0;
            _isBasic[_basis[row]] = false;
            _basis[row] = column;
            _isBasic[column] = true;
        }

        /// <summary><c>target -= factor * source</c>, touching only <paramref name="nonzero"/> indices when sparse.</summary>
        static void SubtractScaled(Span<double> target, ReadOnlySpan<double> source, double factor, bool sparse, ReadOnlySpan<int> nonzero)
        {
            if (sparse)
            {
                foreach (var j in nonzero)
                    target[j] -= factor * source[j];
                return;
            }

            var start = 0;
            if (Vector.IsHardwareAccelerated)
            {
                var targetVectors = MemoryMarshal.Cast<double, Vector<double>>(target);
                var sourceVectors = MemoryMarshal.Cast<double, Vector<double>>(source);
                var scale = new Vector<double>(factor);
                for (var k = 0; k < targetVectors.Length; k++)
                    targetVectors[k] -= scale * sourceVectors[k];
                start = targetVectors.Length * Vector<double>.Count;
            }

            for (var j = start; j < target.Length; j++)
                target[j] -= factor * source[j];
        }

        void ComputeReducedCosts()
        {
            Array.Copy(_c, _d, _width);
            for (var i = 0; i < _m; i++)
            {
                var cost = _c[_basis[i]];
                if (cost == 0)
                    continue;

                var o = i * _width;
                for (var j = 0; j < _width; j++)
                    _d[j] -= cost * _t[o + j];
            }

            for (var i = 0; i < _m; i++)
                _d[_basis[i]] = 0;
        }

        /// <summary>
        /// After a feasible phase 1, swaps each zero-valued basic artificial for a non-artificial column of its row.
        /// Rows where no such column exists are redundant; their artificial stays basic, fixed at zero.
        /// </summary>
        void DriveOutArtificials()
        {
            for (var i = 0; i < _m; i++)
            {
                var artificial = _basis[i];
                if (!IsArtificial(artificial))
                    continue;

                var o = i * _width;
                var best = -1;
                var bestPivot = PivotTolerance;
                for (var j = 0; j < _n + _m; j++)
                {
                    if (!_isBasic[j] && Math.Abs(_t[o + j]) > bestPivot)
                    {
                        best = j;
                        bestPivot = Math.Abs(_t[o + j]);
                    }
                }

                if (best < 0)
                    continue;

                if (++_pivots > _pivotLimit)
                    throw new SimplexFailedException();

                // Degenerate pivot: the artificial is (numerically) zero, so the entering column keeps its value.
                _x[artificial] = 0;
                Pivot(i, best);
            }
        }
    }
}

/// <summary>Internal signal that a simplex solve gave up; surfaced to callers as <see cref="LpStatus.Failed"/>.</summary>
sealed class SimplexFailedException : Exception
{
}
