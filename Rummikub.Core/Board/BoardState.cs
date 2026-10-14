using Rummikub.Core.Common;

namespace Rummikub.Core;

public sealed record BoardState : IEquatable<BoardState>
{
    public IReadOnlyList<Group> Groups { get; }

    public static BoardState Empty { get; } =
        new BoardState(Array.Empty<Group>());

    public BoardState(IEnumerable<Group> groups)
    {
        // canonical ordering: size, type, then tile hashes
        Groups = groups
            .OrderBy(g => g.Tiles.Count)
            .ThenBy(g => g.Type)
            .ThenBy(g => string.Join(",", g.Tiles.Select(t => t.GetHashCode())))
            .ToArray();
    }

    public BoardState AddGroup(Group group)
        => new BoardState(Groups.Append(group));

    public bool Equals(BoardState? other)
    {
        if (other is null)
            return false;
        return Groups.SequenceEqual(other.Groups);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            int hash = 17;
            foreach (var group in Groups)
                hash = hash * 31 + group.GetHashCode();
            return hash;
        }
    }
}
