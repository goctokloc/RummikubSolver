namespace Rummikub.Core.Tiles;

public readonly record struct Tile(Color? Color, int? Value, bool IsJoker) : IComparable<Tile>
{
    public int CompareTo(Tile other)
    {
        int jokerComparison = IsJoker.CompareTo(other.IsJoker);
        if (jokerComparison != 0)
            return jokerComparison;

        int colorComparison = Nullable.Compare(Color, other.Color);
        if (colorComparison != 0)
            return colorComparison;

        return Nullable.Compare(Value, other.Value);
    }
}
