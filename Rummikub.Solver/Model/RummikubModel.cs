using Rummikub.Core;
using Rummikub.Solver.Mip;

namespace Rummikub.Solver.Model;

/// <summary>A Rummikub position as an <see cref="IntegerModel"/>, with the maps needed to read a solution back.</summary>
internal sealed class RummikubModel
{
    public RummikubModel(
        IntegerModel model,
        IReadOnlyList<(int Var, Group Group)> groupVars,
        IReadOnlyList<(int Var, Group Group, int Copies)> keptVars,
        IReadOnlyList<int> playedVars,
        int? meldVar)
    {
        if (playedVars.Count != TileCounts.Kinds)
            throw new ArgumentException($"Expected {TileCounts.Kinds} played variables, got {playedVars.Count}.", nameof(playedVars));

        Model = model;
        GroupVars = groupVars;
        KeptVars = keptVars;
        PlayedVars = playedVars;
        MeldVar = meldVar;
    }

    public IntegerModel Model { get; }

    /// <summary>Catalogue columns <c>x_g</c>.</summary>
    public IReadOnlyList<(int Var, Group Group)> GroupVars { get; }

    /// <summary>Distinct board groups <c>k_b</c>, with their multiplicity on the board.</summary>
    public IReadOnlyList<(int Var, Group Group, int Copies)> KeptVars { get; }

    /// <summary><c>y_t</c>, indexed by <see cref="Tile.Id"/>.</summary>
    public IReadOnlyList<int> PlayedVars { get; }

    /// <summary>Initial-meld indicator <c>m</c>; null when the initial meld is already done.</summary>
    public int? MeldVar { get; }

    /// <summary>Objective levels, highest priority first. Callers may append levels before <see cref="PackObjective"/>.</summary>
    public List<ObjectiveLevel> Levels { get; } = [];

    /// <summary>Packs <see cref="Levels"/> lexicographically into the model's single objective.</summary>
    public void PackObjective()
    {
        var coefs = new Dictionary<int, long>();
        long multiplier = 1;
        for (var i = Levels.Count - 1; i >= 0; i--)
        {
            var level = Levels[i];
            foreach (var (index, coef) in level.Terms)
                coefs[index] = checked(coefs.GetValueOrDefault(index) + coef * multiplier);
            multiplier = checked(multiplier * (level.Max - level.Min + 1));
        }

        Model.SetObjective(coefs.Where(c => c.Value != 0).OrderBy(c => c.Key).Select(c => (c.Key, c.Value)).ToArray());
    }

    /// <summary>The always-feasible solution: keep every board group, play nothing.</summary>
    public long[] KeepEverything()
    {
        var values = new long[Model.Variables.Count];
        foreach (var (index, _, copies) in KeptVars)
            values[index] = copies;
        return values;
    }

    public DecodedPlay Decode(IReadOnlyList<long> values)
    {
        if (values.Count != Model.Variables.Count)
            throw new ArgumentException($"Expected {Model.Variables.Count} values, got {values.Count}.", nameof(values));

        var kept = KeptVars.SelectMany(k => Enumerable.Repeat(k.Group, checked((int)values[k.Var]))).ToList();
        var added = GroupVars.SelectMany(g => Enumerable.Repeat(g.Group, checked((int)values[g.Var]))).ToList();
        var played = Tile.AllKinds.SelectMany(t => Enumerable.Repeat(t, checked((int)values[PlayedVars[t.Id]]))).ToList();
        return new DecodedPlay(kept, added, played);
    }
}
