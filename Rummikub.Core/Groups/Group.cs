using Rummikub.Core.Tiles;

namespace Rummikub.Core.Groups;

public sealed record Group(GroupType Type, IReadOnlyList<Tile> Tiles);