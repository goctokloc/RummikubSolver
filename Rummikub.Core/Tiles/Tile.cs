namespace Rummikub.Core.Tiles;

public readonly record struct Tile(Color? Color, int? Value, bool IsJoker);
