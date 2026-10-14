namespace Rummikub.Core;

public sealed record Group : IEquatable<Group>
{
    public GroupType Type { get; }
    public IReadOnlyList<Tile> Tiles { get; }

    public Group(GroupType groupType, IEnumerable<Tile> tiles)
    {
        Type = groupType;

        Tiles = [.. tiles
            .OrderBy(t => t)];
    }

    public bool Equals(Group? other)
    {
        if (other is null)
            return false;
        if (Type != other.Type)
            return false;
        return Tiles.SequenceEqual(other.Tiles);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            int hash = 17;
            hash = hash * 31 + Type.GetHashCode();
            foreach (var tile in Tiles)
                hash = hash * 31 + tile.GetHashCode();
            return hash;
        }
    }
}
