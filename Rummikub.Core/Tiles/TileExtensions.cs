namespace Rummikub.Core.Tiles;

public static class TileExtensions
{
    public static bool IsSameAs(this Tile tile, Tile other)
    {
        return tile.Color == other.Color && tile.Value == other.Value && tile.IsJoker == other.IsJoker;
    }
}