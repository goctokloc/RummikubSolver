using System.Numerics;
using System.Runtime.InteropServices;

namespace Rummikub.Solver.Mip;

/// <summary>
/// Two-phase, bounded-variable primal simplex on a dense tableau. Solves the LP relaxation of an
/// <see cref="IntegerModel"/> (integrality is ignored). Pricing is Dantzig, falling back to Bland's rule
/// after a run of degenerate pivots so that degenerate models cannot cycle.
/// </summary>
internal static class SimplexSolver
{
    public static LpResult Solve(IntegerModel model, double[]? lower = null, double[]? upper = null)
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

        return new Tableau(model, lo, up).Run();
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
        const double PivotTolerance = 1e-9;
        const double FeasibilityTolerance = 1e-9;
        const double StepTolerance = 1e-12;
        const int DegenerateRunBeforeBland = 50;
        const int PivotLimit = 50_000;

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
        int _pivots;

        public Tableau(IntegerModel model, double[] structuralLower, double[] structuralUpper)
        {
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
            // Phase 1: maximise -Σ artificials using the phase-1 costs; keep the model costs aside.
            var objective = _c[.._n];
            Array.Clear(_c);
            for (var i = 0; i < _m; i++)
                _c[_n + _m + i] = -1;
            ComputeReducedCosts();
            if (!Iterate(phaseOne: true))
                throw new InvalidOperationException("Phase 1 of the simplex reported an unbounded objective.");

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
            var bland = false;
            var reducedCostsFresh = true;

            while (true)
            {
                var entering = ChooseEntering(bland, out var direction);
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
                if (++_pivots > PivotLimit)
                    throw new InvalidOperationException("simplex pivot limit exceeded");

                var step = RatioTest(entering, direction, bland, out var leavingRow, out var leavingToUpper);
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
                        bland = true;
                }
                else
                {
                    degenerateRun = 0;
                    bland = false;
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
        /// Bounded ratio test. Returns the step length (positive infinity if unlimited) and the blocking row,
        /// or -1 when the entering variable's own bound blocks first (a bound flip).
        /// </summary>
        double RatioTest(int entering, int direction, bool bland, out int leavingRow, out bool leavingToUpper)
        {
            var best = direction > 0 ? _up[entering] - _x[entering] : _x[entering] - _lo[entering];
            var bestPivot = 0.0;
            leavingRow = -1;
            leavingToUpper = false;

            for (var i = 0; i < _m; i++)
            {
                var a = _t[i * _width + entering];
                if (Math.Abs(a) <= PivotTolerance)
                    continue;

                var basic = _basis[i];
                var rate = -direction * a; // change of the basic variable per unit step
                double limit;
                bool toUpper;
                if (rate < 0)
                {
                    if (double.IsNegativeInfinity(_lo[basic]))
                        continue;
                    limit = (_x[basic] - _lo[basic]) / -rate;
                    toUpper = false;
                }
                else
                {
                    if (double.IsPositiveInfinity(_up[basic]))
                        continue;
                    limit = (_up[basic] - _x[basic]) / rate;
                    toUpper = true;
                }

                if (limit < 0)
                    limit = 0;

                bool take;
                if (limit < best - StepTolerance)
                    take = true;
                else if (leavingRow >= 0 && limit <= best + StepTolerance)
                    take = bland ? basic < _basis[leavingRow] : Math.Abs(a) > bestPivot;
                else
                    take = false;

                if (take)
                {
                    best = Math.Min(best, limit);
                    bestPivot = Math.Abs(a);
                    leavingRow = i;
                    leavingToUpper = toUpper;
                }
            }

            return best;
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

                if (++_pivots > PivotLimit)
                    throw new InvalidOperationException("simplex pivot limit exceeded");

                // Degenerate pivot: the artificial is (numerically) zero, so the entering column keeps its value.
                _x[artificial] = 0;
                Pivot(i, best);
            }
        }
    }
}
