using System.Numerics;

namespace Rummikub.Core;

/// <summary>Consecutive values of one color. Bit i of <see cref="JokerMask"/> marks value Start + i as a joker.</summary>
public sealed record Run : Group
{
    public const int MaxValue = 13;

    private Run(Color color, int start, int length, ushort jokerMask)
    {
        Color = color;
        Start = start;
        Length = length;
        JokerMask = jokerMask;
    }

    public Color Color { get; }

    public int Start { get; }

    public int Length { get; }

    public ushort JokerMask { get; }

    public override GroupType Type => GroupType.Run;

    public override int Count => Length;

    public override int JokerCount => BitOperations.PopCount(JokerMask);

    public override IReadOnlyList<GroupSlot> Slots
    {
        get
        {
            var slots = new GroupSlot[Length];
            for (var i = 0; i < Length; i++)
            {
                var value = Start + i;
                var tile = (JokerMask >> i & 1) != 0 ? Tile.Joker : Tile.Of(Color, value);
                slots[i] = new GroupSlot(tile, Color, value);
            }
            return slots;
        }
    }

    public static Run Create(Color color, int start, int length, ushort jokerMask)
    {
        if (!Enum.IsDefined(color))
            throw new ArgumentException("run color is undefined", nameof(color));
        if (length is < MinSize or > MaxValue)
            throw new ArgumentException("run length must be between 3 and 13", nameof(length));
        if (start < 1 || start + length - 1 > MaxValue)
            throw new ArgumentException("run must stay within values 1 to 13", nameof(start));
        if (jokerMask >> length != 0)
            throw new ArgumentException("joker mask has bits beyond the run length", nameof(jokerMask));
        if (BitOperations.PopCount(jokerMask) > MaxJokers)
            throw new ArgumentException("group has more than 2 jokers", nameof(jokerMask));

        return new Run(color, start, length, jokerMask);
    }
}
