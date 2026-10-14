using System.Diagnostics;

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
}
