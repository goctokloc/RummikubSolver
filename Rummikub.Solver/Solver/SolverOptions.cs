namespace Rummikub.Solver.Solver;

/// <summary>Rules and preferences for one solve.</summary>
public sealed record SolverOptions
{
    /// <summary>When false, the play must be an initial meld: board untouched, new groups from hand worth at least <see cref="InitialMeldPoints"/>.</summary>
    public bool InitialMeldDone { get; init; } = true;

    public int InitialMeldPoints { get; init; } = 30;

    public SolverObjective Objective { get; init; } = SolverObjective.TileCount;

    /// <summary>Prefer keeping jokers in hand when that does not lower the primary score.</summary>
    public bool KeepJokers { get; init; }

    /// <summary>Branch-and-bound node limit; beyond it the best play found so far is returned as not proven optimal.</summary>
    public int MaxNodes { get; init; } = 200_000;
}
