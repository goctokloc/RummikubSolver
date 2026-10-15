using Rummikub.Core;
using Rummikub.Solver.Mip;
using Rummikub.Solver.Model;
using Rummikub.Solver.Solver;
namespace Rummikub.Tests.Solver;

[TestFixture]
public class SolverRegressionTests
{
    /// <summary>Full-game initial-meld position that made the simplex hit its pivot limit (final review F1).</summary>
    internal static readonly (Hand Hand, BoardState Board) PivotLimitPosition = (
        PositionText.Hand("R1 R3 R5 R6 B6 Y4 Y9 Y11 K1 K1 K2 K3 K4 K11"),
        PositionText.Board(
            "Set[R1 B1 Y1] | Set[R2 B2 Y2 K2] | Set[B3 Y3 K3] | Set[R4 B4 Y4] | Set[B5 Y5 K5] | Set[R7 B7 K7] | " +
            "Set[R8 B8 Y8 K8] | Set[B9 J K9] | Set[R10 B10 Y10 K10] | Set[R11 B11 Y11] | Set[R12 B12 Y12] | " +
            "Set[B12 Y12 K12] | Set[B13 Y13 K13] | Set[R13 B13 Y13 K13] | Run[R2 R3 R4] | Run[R5..R9] | Run[R9..R13] | " +
            "Run[B1 B2 B3] | Run[B4..B7] | Run[B8 B9 B10 B11 J] | Run[Y1 Y2 Y3] | Run[Y5 Y6 Y7] | Run[Y6..Y10] | " +
            "Run[K4 K5 K6] | Run[K6 K7 K8] | Run[K9..K12]"));

    [Test] public void Position_Text_Keeps_Joker_Slots()
    {
        Assert.That(PositionText.Group("Set[B9 J K9]"), Is.EqualTo(Set.Create(9, 0b1110, 0b0100)));
        Assert.That(PositionText.Group("Run[B8 B9 B10 B11 J]"), Is.EqualTo(Run.Create(Color.Blue, 8, 5, 0b10000)));
        Assert.That(PositionText.Group("Run[R5..R9]"), Is.EqualTo(Run.Create(Color.Red, 5, 5, 0)));
    }

    [Test] public void Initial_Meld_Points_Position_Solves_Optimally(
        [Values] bool keepJokers)
    {
        var (hand, board) = PivotLimitPosition;
        var options = new SolverOptions { InitialMeldDone = false, Objective = SolverObjective.Points, KeepJokers = keepJokers };
        var r = new RummikubSolver().Solve(hand, board, options);
        Assert.That(r.IsOptimal, Is.True);
        Assert.That(r.Board.Groups, Is.SupersetOf(board.Groups));
    }
}

[TestFixture]
public class SimplexDegeneracyRegressionTests
{
    /// <summary>
    /// The F1 position's model before the meld columns were narrowed: board groups fixed, yet every catalogue
    /// column bounded by hand + board counts, which leaves hundreds of degenerate columns. Its LPs used to blow
    /// up numerically (pivots on ~1e-9 entries) and hit the pivot limit.
    /// </summary>
    static IntegerModel DegenerateFixedBoardModel()
    {
        var (hand, board) = SolverRegressionTests.PivotLimitPosition;
        var built = RummikubModelBuilder.Build(hand, board, new SolverOptions { Objective = SolverObjective.Points });
        built.PackObjective();
        var source = built.Model;
        var kept = built.KeptVars.ToDictionary(k => k.Var, k => k.Copies);
        var model = new IntegerModel();
        for (var j = 0; j < source.Variables.Count; j++)
        {
            var v = source.Variables[j];
            model.AddVariable(kept.TryGetValue(j, out var copies) ? copies : v.Lower, v.Upper, v.IsInteger, v.Name);
        }
        foreach (var c in source.Constraints)
            model.AddConstraint(c.Terms, c.Sense, c.Rhs);
        // The initial-meld rows, as the builder adds them.
        var m = model.AddVariable(0, 1, true, "m");
        model.AddConstraint(built.GroupVars.Select(g => (g.Var, (long)GroupCatalog.Points(g.Group))).Append((m, -30L)).ToArray(), Sense.GreaterOrEqual, 0);
        model.AddConstraint(built.GroupVars.Select(g => (g.Var, 1L)).Append((m, -36L)).ToArray(), Sense.LessOrEqual, 0);
        model.SetObjective(source.Objective);
        return model;
    }

    [Test] public void Degenerate_Fixed_Board_Model_Solves_To_Optimality()
    {
        var model = DegenerateFixedBoardModel();
        var incumbent = new long[model.Variables.Count];
        for (var j = 0; j < incumbent.Length; j++)
            incumbent[j] = model.Variables[j].Lower;
        var s = BranchAndBound.Solve(model, incumbent, 200_000, CancellationToken.None);
        Assert.That(s.IsOptimal, Is.True);
        Assert.That(model.IsFeasible(s.Values), Is.True);
    }
}
