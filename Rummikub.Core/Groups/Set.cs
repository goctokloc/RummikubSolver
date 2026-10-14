using System.Numerics;

namespace Rummikub.Core;

/// <summary>
/// One value in 3–4 different colors. <see cref="Colors"/> and <see cref="JokerColors"/> are
/// 4-bit masks (bit <c>1 &lt;&lt; (int)color</c>); joker colors are filled by jokers.
/// </summary>
public sealed record Set : Group
{
    private const int ColorCount = 4;
    private const byte AllColors = (1 << ColorCount) - 1;

    private Set(int value, byte colors, byte jokerColors)
    {
        Value = value;
        Colors = colors;
        JokerColors = jokerColors;
    }

    public int Value { get; }

    public byte Colors { get; }

    public byte JokerColors { get; }

    public override GroupType Type => GroupType.Set;

    public override int Count => BitOperations.PopCount(Colors);

    public override int JokerCount => BitOperations.PopCount(JokerColors);

    public override IReadOnlyList<GroupSlot> Slots
    {
        get
        {
            var slots = new List<GroupSlot>(ColorCount);
            for (var c = 0; c < ColorCount; c++)
            {
                if ((Colors >> c & 1) == 0)
                    continue;
                var color = (Color)c;
                var tile = (JokerColors >> c & 1) != 0 ? Tile.Joker : Tile.Of(color, Value);
                slots.Add(new GroupSlot(tile, color, Value));
            }
            return slots;
        }
    }

    public static Set Create(int value, byte colors, byte jokerColors)
    {
        if (value is < 1 or > Run.MaxValue)
            throw new ArgumentException("set value must be between 1 and 13", nameof(value));
        if ((colors & ~AllColors) != 0)
            throw new ArgumentException("set colors mask has bits outside the 4 colors", nameof(colors));
        if (BitOperations.PopCount(colors) is < MinSize or > ColorCount)
            throw new ArgumentException("set must have 3 or 4 colors", nameof(colors));
        if ((jokerColors & ~colors) != 0)
            throw new ArgumentException("joker colors must be part of the set colors", nameof(jokerColors));
        if (BitOperations.PopCount(jokerColors) > MaxJokers)
            throw new ArgumentException("group has more than 2 jokers", nameof(jokerColors));

        return new Set(value, colors, jokerColors);
    }

    /// <summary>
    /// Builds a set from the colors of its real tiles plus <paramref name="jokers"/> jokers,
    /// which take the missing colors, lowest first.
    /// </summary>
    public static Set CreateFillingJokers(int value, byte realColors, int jokers)
    {
        byte jokerColors = 0;
        for (var c = 0; c < ColorCount && BitOperations.PopCount(jokerColors) < jokers; c++)
        {
            if ((realColors >> c & 1) == 0)
                jokerColors |= (byte)(1 << c);
        }
        if (BitOperations.PopCount(jokerColors) < jokers)
            throw new ArgumentException("set has more than 4 tiles", nameof(jokers));

        return Create(value, (byte)(realColors | jokerColors), jokerColors);
    }
}
