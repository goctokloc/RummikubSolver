namespace Rummikub.Solver.Model;

/// <summary>One lexicographic objective level; <paramref name="Min"/>..<paramref name="Max"/> bound its value over every feasible solution.</summary>
internal sealed record ObjectiveLevel(string Name, IReadOnlyList<(int Var, long Coef)> Terms, long Min, long Max);
