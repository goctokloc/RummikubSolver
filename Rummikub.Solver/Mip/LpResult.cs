namespace Rummikub.Solver.Mip;

internal enum LpStatus
{
    Optimal,
    Infeasible,
    Unbounded,

    /// <summary>The simplex gave up (pivot limit or numerical trouble); nothing is known about the LP.</summary>
    Failed,
}

/// <summary>
/// Outcome of an LP relaxation solve. <see cref="Values"/> holds one entry per model variable when
/// <see cref="Status"/> is <see cref="LpStatus.Optimal"/> and is empty otherwise. <see cref="Objective"/> is
/// negative infinity when infeasible, positive infinity when unbounded and NaN when the solve failed.
/// </summary>
internal sealed record LpResult(LpStatus Status, double Objective, double[] Values);
