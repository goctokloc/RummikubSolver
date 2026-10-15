using Rummikub.Core;
using Rummikub.Solver.Mip;
using Rummikub.Solver.Model;

namespace Rummikub.Solver.Solver;

public sealed class RummikubSolver
{
    public SolverResult Solve(Hand hand, BoardState board, SolverOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new SolverOptions();
        var model = RummikubModelBuilder.Build(hand, board, options);
        model.PackObjective();
        var solution = BranchAndBound.Solve(model.Model, model.KeepEverything(), options.MaxNodes, cancellationToken);
        var decoded = model.Decode(solution.Values);

        var newGroups = RunMerger.Merge(decoded.NewGroups);
        var play = decoded with { NewGroups = newGroups };
        SolutionVerifier.Verify(hand, board, options, play);

        var finalBoard = BoardState.Of(decoded.KeptGroups.Concat(newGroups));
        return SolverResult.Create(finalBoard, decoded.PlayedTiles, solution.IsOptimal);
    }
}
