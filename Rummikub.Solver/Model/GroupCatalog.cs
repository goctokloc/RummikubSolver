using System.Numerics;
using Rummikub.Core;
using Rummikub.Core.Common;

namespace Rummikub.Solver.Model;

/// <summary>Every legal group, one per distinct tile multiset: the columns of the placement model.</summary>
internal static class GroupCatalog
{
    static readonly Lazy<IReadOnlyList<Group>> _all = new(Build);

    /// <summary>Sets first, then runs.</summary>
    public static IReadOnlyList<Group> All => _all.Value;

    public static int Points(Group g) => g.Slots.Sum(s => s.Value);

    static IReadOnlyList<Group> Build()
    {
        var order = new List<EquatableArray<Tile>>();
        var best = new Dictionary<EquatableArray<Tile>, Group>();
        foreach (var group in Enumerate())
        {
            var key = group.Tiles.Order().ToEquatableArray();
            if (!best.TryGetValue(key, out var current))
            {
                order.Add(key);
                best[key] = group;
            }
            else if (Points(group) > Points(current))
                best[key] = group;
        }
        // A multiset may be first seen as a set yet be best read as a run; keep sets-first ordering (stable).
        return order.Select(k => best[k]).OrderBy(g => g is Set ? 0 : 1).ToList();
    }

    static IEnumerable<Group> Enumerate()
    {
        for (var value = 1; value <= Run.MaxValue; value++)
            for (var colors = 0; colors < 16; colors++)
            {
                if (BitOperations.PopCount((uint)colors) < 3)
                    continue;
                for (var jokers = colors; ; jokers = (jokers - 1) & colors)
                {
                    if (BitOperations.PopCount((uint)jokers) <= 2)
                        yield return Set.Create(value, (byte)colors, (byte)jokers);
                    if (jokers == 0)
                        break;
                }
            }

        foreach (var color in Enum.GetValues<Color>())
            for (var length = 3; length <= 5; length++)
                for (var start = 1; start <= Run.MaxValue + 1 - length; start++)
                    for (var mask = 0; mask < 1 << length; mask++)
                        if (BitOperations.PopCount((uint)mask) <= 2)
                            yield return Run.Create(color, start, length, (ushort)mask);
    }
}
