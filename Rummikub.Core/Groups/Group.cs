using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace Rummikub.Core;

/// <summary>
/// A legal meld: either a <see cref="Run"/> or a <see cref="Set"/>.
/// Instances are valid by construction; jokers are positions in the structure.
/// </summary>
public abstract record Group : IComparable<Group>
{
    public const int MinSize = 3;
    public const int MaxJokers = 2;

    private protected Group()
    {
    }

    public abstract GroupType Type { get; }

    /// <summary>Ordered positions: runs by value, sets by color.</summary>
    public abstract IReadOnlyList<GroupSlot> Slots { get; }

    public IEnumerable<Tile> Tiles => Slots.Select(s => s.Tile);

    public abstract int Count { get; }

    public abstract int JokerCount { get; }

    public int CompareTo(Group? other) => (this, other) switch
    {
        (_, null) => 1,
        (Set a, Set b) => (a.Value, a.Colors, a.JokerColors).CompareTo((b.Value, b.Colors, b.JokerColors)),
        (Run a, Run b) => (a.Color, a.Start, a.Length, a.JokerMask).CompareTo((b.Color, b.Start, b.Length, b.JokerMask)),
        (Set, Run) => -1,
        (Run, Set) => 1,
        _ => throw new UnreachableException()
    };

    public sealed override string ToString() => $"{Type}[{string.Join(" ", Tiles)}]";

    /// <summary>
    /// Builds a group from loose tiles, inferring what each joker represents
    /// (runs: lowest valid start; sets: lowest missing colors). Never throws.
    /// </summary>
    public static bool TryFromTiles(
        GroupType type,
        IEnumerable<Tile> tiles,
        [NotNullWhen(true)] out Group? group,
        [NotNullWhen(false)] out string? error)
    {
        var all = tiles.ToList();
        var regular = all.Where(t => t != default && !t.IsJoker).ToList();
        var jokers = all.Count(t => t == Tile.Joker);
        Group? built = null;

        error =
            all.Contains(default) ? "group contains an uninitialized tile" :
            all.Count < MinSize ? "group needs at least 3 tiles" :
            jokers > MaxJokers ? "group has more than 2 jokers" :
            regular.Count == 0 ? "group needs at least one regular tile" :
            type switch
            {
                GroupType.Run => TryRun(regular, jokers, out built),
                GroupType.Set => TrySet(regular, jokers, out built),
                _ => $"unknown group type {type}"
            };

        if (error is not null)
        {
            group = null;
            return false;
        }

        group = built!;
        return true;
    }

    private static string? TryRun(List<Tile> regular, int jokers, out Group? group)
    {
        group = null;
        var color = regular[0].Color;
        if (regular.Any(t => t.Color != color))
            return "run tiles must share one color";

        var values = regular.Select(t => t.Value).Order().ToList();
        for (var i = 1; i < values.Count; i++)
        {
            if (values[i] == values[i - 1])
                return $"run contains duplicate value {values[i]}";
        }

        int min = values[0], max = values[^1];
        var gaps = max - min + 1 - values.Count;
        if (gaps > jokers)
            return "not enough jokers to fill gaps";

        var extra = jokers - gaps;
        var start = Math.Max(1, min - extra);
        var length = values.Count + jokers;
        if (start + length - 1 > Run.MaxValue)
            return "run exceeds 13";

        ushort jokerMask = 0;
        for (var i = 0; i < length; i++)
        {
            if (!values.Contains(start + i))
                jokerMask |= (ushort)(1 << i);
        }

        group = Run.Create(color, start, length, jokerMask);
        return null;
    }

    private static string? TrySet(List<Tile> regular, int jokers, out Group? group)
    {
        group = null;
        var value = regular[0].Value;
        if (regular.Any(t => t.Value != value))
            return "set tiles must share one value";

        byte realColors = 0;
        foreach (var tile in regular)
        {
            var bit = (byte)(1 << (int)tile.Color);
            if ((realColors & bit) != 0)
                return $"set contains duplicate color {tile.Color}";
            realColors |= bit;
        }

        if (regular.Count + jokers > 4)
            return "set has more than 4 tiles";

        group = Set.CreateFillingJokers(value, realColors, jokers);
        return null;
    }
}
