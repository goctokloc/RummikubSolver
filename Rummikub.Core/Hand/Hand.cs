using Rummikub.Core.Tiles;

namespace Rummikub.Core.Hand
{
    public sealed record Hand(IReadOnlyList<Tile> Tiles);
}