using Rummikub.Core;
using Rummikub.Solver.Model;
using Rummikub.Solver.Solver;
namespace Rummikub.Tests.Model;

[TestFixture]
public class RummikubModelBuilderTests
{
    static Tile R(int v) => Tile.Of(Color.Red, v);
    static readonly SolverOptions Default = new();

    [Test] public void Default_Options()
    {
        var o = new SolverOptions();
        Assert.Multiple(() =>
        {
            Assert.That(o.InitialMeldDone, Is.True);
            Assert.That(o.InitialMeldPoints, Is.EqualTo(30));
            Assert.That(o.Objective, Is.EqualTo(SolverObjective.TileCount));
            Assert.That(o.KeepJokers, Is.False);
            Assert.That(o.MaxNodes, Is.EqualTo(200_000));
        });
    }

    [Test] public void Keep_Everything_Is_Feasible()
    {
        var board = BoardState.Of([Run.Create(Color.Blue, 1, 8, 0), Set.Create(5, 0b0111, 0)]);
        var m = RummikubModelBuilder.Build(Hand.Of([R(3)]), board, Default);
        Assert.That(m.Model.IsFeasible(m.KeepEverything()), Is.True);
        Assert.That(m.Decode(m.KeepEverything()).KeptGroups, Is.EquivalentTo(board.Groups));
    }

    [Test] public void Identical_Board_Groups_Share_One_Kept_Column()
    {
        var run = Run.Create(Color.Red, 1, 3, 0);
        var m = RummikubModelBuilder.Build(Hand.Empty, BoardState.Of([run, run]), Default);
        Assert.That(m.KeptVars, Has.Count.EqualTo(1));
        Assert.That(m.KeptVars[0].Copies, Is.EqualTo(2));
    }

    [Test] public void Unavailable_Columns_Are_Omitted() =>
        Assert.That(RummikubModelBuilder.Build(Hand.Of([R(1), R(2), R(3)]), BoardState.Empty, Default)
            .GroupVars.Select(v => v.Group), Is.EquivalentTo(new Group[] { Run.Create(Color.Red, 1, 3, 0) }));

    [Test] public void Level_Order_Depends_On_KeepJokers()
    {
        var plain = RummikubModelBuilder.Build(Hand.Of([R(1)]), BoardState.Empty, Default);
        var keep = RummikubModelBuilder.Build(Hand.Of([R(1)]), BoardState.Empty, Default with { KeepJokers = true });
        Assert.That(plain.Levels.Select(l => l.Name), Is.EqualTo(new[] { "Primary", "GroupsKept" }));
        Assert.That(keep.Levels.Select(l => l.Name), Is.EqualTo(new[] { "Primary", "JokersKept", "GroupsKept" }));
    }

    [Test] public void Packing_Multiplies_Higher_Levels()
    {
        // hand: 2 tiles (Primary 0..2); board: 3 groups (GroupsKept 0..3) -> played tile coef 4, kept coef 1
        var board = BoardState.Of([Run.Create(Color.Blue, 1, 3, 0), Run.Create(Color.Blue, 5, 3, 0), Run.Create(Color.Blue, 9, 3, 0)]);
        var m = RummikubModelBuilder.Build(Hand.Of([R(1), R(2)]), board, Default);
        m.PackObjective();
        var coef = m.Model.Objective.ToDictionary(t => t.Var, t => t.Coef);
        Assert.That(coef[m.PlayedVars[R(1).Id]], Is.EqualTo(4));
        Assert.That(m.KeptVars.Select(k => coef[k.Var]), Is.All.EqualTo(1));
    }

    [Test] public void Points_Weights_Joker_30_Unless_Kept()
    {
        var hand = Hand.Of([Tile.Of(Color.Red, 12), Tile.Joker]);
        var pts = RummikubModelBuilder.Build(hand, BoardState.Empty, Default with { Objective = SolverObjective.Points });
        var w = pts.Levels[0].Terms.ToDictionary(t => t.Var, t => t.Coef);
        Assert.That(w[pts.PlayedVars[Tile.Joker.Id]], Is.EqualTo(30));
        Assert.That(w[pts.PlayedVars[Tile.Of(Color.Red, 12).Id]], Is.EqualTo(12));

        var keep = RummikubModelBuilder.Build(hand, BoardState.Empty, Default with { Objective = SolverObjective.Points, KeepJokers = true });
        Assert.That(keep.Levels[0].Terms.Where(t => t.Var == keep.PlayedVars[Tile.Joker.Id]).Sum(t => t.Coef), Is.EqualTo(0));
    }

    [Test] public void Initial_Meld_Adds_Meld_Variable() =>
        Assert.That(RummikubModelBuilder.Build(Hand.Of([R(1)]), BoardState.Empty, Default with { InitialMeldDone = false }).MeldVar, Is.Not.Null);
}
