using Rummikub.Core.Common;

namespace Rummikub.Core;

/// <summary>The groups on the table, kept in canonical (sorted) order so equal boards compare equal.</summary>
public sealed record BoardState
{
    private BoardState(EquatableArray<Group> groups)
    {
        Groups = groups;
    }

    public static BoardState Empty { get; } = new(EquatableArray<Group>.Empty);

    public EquatableArray<Group> Groups { get; }

    public IEnumerable<Tile> Tiles => Groups.SelectMany(g => g.Tiles);

    public static BoardState Of(IEnumerable<Group> groups) => new(groups.Order().ToEquatableArray());

    public BoardState Add(Group group) => Of(Groups.Append(group));
}
