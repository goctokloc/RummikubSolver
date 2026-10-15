using Rummikub.Core;
using Rummikub.Solver.Mip;
using Rummikub.Solver.Solver;

namespace Rummikub.Solver.Model;

/// <summary>Turns a position and options into a <see cref="RummikubModel"/>: game rules as constraints, preferences as objective levels.</summary>
internal static class RummikubModelBuilder
{
    const int MaxCopies = 2;
    const int JokerPoints = 30;

    /// <summary>Upper bound on the number of new groups in an initial meld (106 tiles / 3, rounded up).</summary>
    const int MaxMeldGroups = 36;

    /// <summary>Catalogue groups with their tile usage <c>a_tg</c> as (tile id, count) pairs.</summary>
    static readonly Lazy<IReadOnlyList<(Group Group, (int Id, int Count)[] Usage)>> _catalog = new(() =>
        GroupCatalog.All.Select(g => (g, Usage(g))).ToList());

    public static RummikubModel Build(Hand hand, BoardState board, SolverOptions options)
    {
        var handCounts = TileCounts.From(hand.Tiles);
        var boardCounts = TileCounts.From(board.Tiles);
        var model = new IntegerModel();
        var balance = Enumerable.Range(0, TileCounts.Kinds).Select(_ => new List<(int Var, long Coef)>()).ToArray();

        // During the initial meld the board is fixed, so new groups can only be built from hand tiles.
        var available = options.InitialMeldDone
            ? Tile.AllKinds.Select(t => handCounts[t.Id] + boardCounts[t.Id]).ToArray()
            : Tile.AllKinds.Select(t => handCounts[t.Id]).ToArray();
        var groupVars = new List<(int Var, Group Group)>();
        foreach (var (group, usage) in _catalog.Value)
        {
            var upper = Math.Min(MaxCopies, usage.Min(u => available[u.Id] / u.Count));
            if (upper == 0)
                continue;
            var index = model.AddVariable(0, upper, true, $"x:{group}");
            groupVars.Add((index, group));
            foreach (var (id, count) in usage)
                balance[id].Add((index, count));
        }

        // Rules: during the initial meld the board is fixed (k_b = mult_b).
        var keptVars = new List<(int Var, Group Group, int Copies)>();
        foreach (var distinct in board.Groups.GroupBy(g => g))
        {
            var copies = distinct.Count();
            var index = model.AddVariable(options.InitialMeldDone ? 0 : copies, copies, true, $"k:{distinct.Key}");
            keptVars.Add((index, distinct.Key, copies));
            foreach (var (id, count) in Usage(distinct.Key))
                balance[id].Add((index, count));
        }

        var playedVars = new int[TileCounts.Kinds];
        foreach (var tile in Tile.AllKinds)
        {
            var index = model.AddVariable(0, handCounts[tile.Id], true, $"y:{tile}");
            playedVars[tile.Id] = index;
            balance[tile.Id].Add((index, -1));
            model.AddConstraint(balance[tile.Id], Sense.Equal, boardCounts[tile.Id]);
        }

        int? meldVar = null;
        if (!options.InitialMeldDone)
        {
            var m = model.AddVariable(0, 1, true, "m");
            meldVar = m;
            var points = groupVars.Select(g => (g.Var, (long)GroupCatalog.Points(g.Group)))
                .Append((m, -options.InitialMeldPoints)).ToArray();
            model.AddConstraint(points, Sense.GreaterOrEqual, 0);
            var count = groupVars.Select(g => (g.Var, 1L)).Append((m, -MaxMeldGroups)).ToArray();
            model.AddConstraint(count, Sense.LessOrEqual, 0);
        }

        var result = new RummikubModel(model, groupVars, keptVars, playedVars, meldVar);
        AddLevels(result, handCounts, options);
        return result;
    }

    /// <summary>Preferences, highest priority first: primary score, jokers kept (optional), board groups kept intact.</summary>
    static void AddLevels(RummikubModel model, TileCounts hand, SolverOptions options)
    {
        var primary = new List<(int Var, long Coef)>();
        long primaryMax = 0;
        foreach (var tile in Tile.AllKinds)
        {
            var weight = Weight(tile, options);
            if (hand[tile.Id] == 0 || weight == 0)
                continue;
            primary.Add((model.PlayedVars[tile.Id], weight));
            primaryMax += weight * hand[tile.Id];
        }
        model.Levels.Add(new ObjectiveLevel("Primary", primary, 0, primaryMax));

        if (options.KeepJokers)
        {
            var joker = Tile.Joker.Id;
            model.Levels.Add(new ObjectiveLevel("JokersKept", [(model.PlayedVars[joker], -1)], -hand[joker], 0));
        }

        model.Levels.Add(new ObjectiveLevel(
            "GroupsKept",
            model.KeptVars.Select(k => (k.Var, 1L)).ToArray(),
            0,
            model.KeptVars.Sum(k => (long)k.Copies)));
    }

    static long Weight(Tile tile, SolverOptions options)
    {
        if (tile.IsJoker && options.KeepJokers)
            return 0;
        if (options.Objective == SolverObjective.TileCount)
            return 1;
        return tile.IsJoker ? JokerPoints : tile.Value;
    }

    static (int Id, int Count)[] Usage(Group group) =>
        group.Tiles.GroupBy(t => t.Id).Select(g => (g.Key, g.Count())).ToArray();
}
