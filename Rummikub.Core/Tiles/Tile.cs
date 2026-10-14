namespace Rummikub.Core;

/// <summary>
/// One of the 53 tile kinds, stored as a single byte code:
/// 0 = uninitialised (<c>default</c>), 1..52 = <c>(int)color * 13 + value</c>, 53 = joker.
/// </summary>
public readonly record struct Tile : IComparable<Tile>
{
    private const byte JokerCode = 53;
    private const int ValuesPerColor = 13;

    private readonly byte _code;

    private Tile(byte code)
    {
        _code = code;
    }

    public static Tile Joker { get; } = new(JokerCode);

    public static IReadOnlyList<Tile> AllKinds { get; } =
        Enumerable.Range(1, JokerCode).Select(code => new Tile((byte)code)).ToArray();

    public static Tile Of(Color color, int value)
    {
        if (!Enum.IsDefined(color))
            throw new ArgumentOutOfRangeException(nameof(color), color, "Undefined color.");
        if (value is < 1 or > ValuesPerColor)
            throw new ArgumentOutOfRangeException(nameof(value), value, "Value must be between 1 and 13.");

        return new Tile((byte)((int)color * ValuesPerColor + value));
    }

    public bool IsJoker => Code == JokerCode;

    /// <summary>Dense index 0..52 (joker = 52).</summary>
    public int Id => Code - 1;

    public Color Color => (Color)((RegularCode - 1) / ValuesPerColor);

    public int Value => (RegularCode - 1) % ValuesPerColor + 1;

    private byte Code => _code != 0
        ? _code
        : throw new InvalidOperationException("Tile is uninitialized (default).");

    private byte RegularCode => Code != JokerCode
        ? _code
        : throw new InvalidOperationException("A joker has no color or value.");

    public int CompareTo(Tile other) => Code.CompareTo(other.Code);

    public override string ToString() => _code switch
    {
        0 => "?",
        JokerCode => "J",
        _ => $"{ColorLetter(Color)}{Value}"
    };

    private static char ColorLetter(Color color) => color switch
    {
        Color.Red => 'R',
        Color.Blue => 'B',
        Color.Yellow => 'Y',
        Color.Black => 'K',
        _ => '?'
    };
}
