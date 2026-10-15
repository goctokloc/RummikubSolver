using Rummikub.Core;

namespace Rummikub.Solver.Model;

/// <summary>A model solution read back as domain objects: board groups left intact, new groups, and tiles played from hand.</summary>
internal sealed record DecodedPlay(IReadOnlyList<Group> KeptGroups, IReadOnlyList<Group> NewGroups, IReadOnlyList<Tile> PlayedTiles);
