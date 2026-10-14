using Rummikub.Core.Common;

namespace Rummikub.Core;

/// <summary>The tiles on a player's rack, kept sorted so equal hands compare equal.</summary>
public sealed record Hand
{
    private Hand(EquatableArray<Tile> tiles)
    {
        Tiles = tiles;
    }

    public static Hand Empty { get; } = new(EquatableArray<Tile>.Empty);

    public EquatableArray<Tile> Tiles { get; }

    public static Hand Of(IEnumerable<Tile> tiles)
    {
        var sorted = tiles.Order().ToEquatableArray();
        if (sorted.Contains(default))
            throw new ArgumentException("Hand contains an uninitialized tile.", nameof(tiles));
        return new Hand(sorted);
    }
}
