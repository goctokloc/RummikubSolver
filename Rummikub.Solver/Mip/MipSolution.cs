namespace Rummikub.Solver.Mip;

/// <summary>
/// Result of a branch-and-bound search: the best feasible assignment found, its objective value, and whether
/// the search finished (proving optimality) rather than stopping at the node limit or on cancellation.
/// </summary>
internal sealed record MipSolution(long[] Values, long Objective, bool IsOptimal);
