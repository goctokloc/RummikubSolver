namespace Rummikub.Solver.Solver;

/// <summary>What the solver maximises before any tie-break.</summary>
public enum SolverObjective
{
    /// <summary>Number of tiles played from hand.</summary>
    TileCount,

    /// <summary>Face value of tiles played from hand; a joker counts 30.</summary>
    Points,
}
