namespace Rummikub.Core;

/// <summary>A position in a group: the physical tile there and the tile it represents.</summary>
public readonly record struct GroupSlot(Tile Tile, Color Color, int Value);
