namespace Rummikub.Solver.Mip;

internal enum LpStatus
{
    Optimal,
    Infeasible,
    Unbounded,
}

/// <summary>
/// Outcome of an LP relaxation solve. <see cref="Values"/> holds one entry per model variable when
/// <see cref="Status"/> is <see cref="LpStatus.Optimal"/> and is empty otherwise. <see cref="Objective"/> is
/// negative infinity when infeasible and positive infinity when unbounded.
/// </summary>
internal sealed record LpResult(LpStatus Status, double Objective, double[] Values);
