using Rummikub.Core;
namespace Rummikub.Tests.Solver;

/// <summary>
/// Parses positions in the solver's own <c>ToString</c> notation, e.g. <c>"R1 K13 J"</c> for tiles and
/// <c>"Set[B9 J K9] | Run[R5..R9] | Run[B8 B9 B10 B11 J]"</c> for a board (K = Black). Jokers keep the slot
/// they are printed in, so a printed board round-trips exactly.
/// </summary>
static class PositionText
{
    public static Tile[] Tiles(string text) =>
        text.Split(' ', StringSplitOptions.RemoveEmptyEntries).SelectMany(Expand).ToArray();

    public static Hand Hand(string text) => Rummikub.Core.Hand.Of(Tiles(text));

    public static BoardState Board(string text) =>
        BoardState.Of(text.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(Group));

    public static Group Group(string text)
    {
        var open = text.IndexOf('[');
        var type = text[..open];
        var tiles = Tiles(text[(open + 1)..text.LastIndexOf(']')]);
        var regular = tiles.Select((t, i) => (Tile: t, Slot: i)).Where(p => !p.Tile.IsJoker).ToArray();
        if (type == "Run")
        {
            var color = regular[0].Tile.Color;
            var start = regular[0].Tile.Value - regular[0].Slot;
            ushort mask = 0;
            for (var i = 0; i < tiles.Length; i++)
                if (tiles[i].IsJoker) mask |= (ushort)(1 << i);
            return Run.Create(color, start, tiles.Length, mask);
        }

        if (type != "Set")
            throw new FormatException($"Unknown group type in '{text}'.");

        // Slots are ordered by colour: find joker colours that put the jokers in the printed positions.
        var value = regular[0].Tile.Value;
        byte real = 0;
        foreach (var (tile, _) in regular) real |= (byte)(1 << (int)tile.Color);
        for (var jokers = 0; jokers < 16; jokers++)
        {
            if ((jokers & real) != 0 || System.Numerics.BitOperations.PopCount((uint)jokers) != tiles.Length - regular.Length)
                continue;
            var set = Set.Create(value, (byte)(real | jokers), (byte)jokers);
            if (set.Tiles.SequenceEqual(tiles))
                return set;
        }

        throw new FormatException($"Cannot read set '{text}'.");
    }

    static IEnumerable<Tile> Expand(string token)
    {
        var range = token.IndexOf("..", StringComparison.Ordinal);
        if (range < 0)
        {
            yield return Single(token);
            yield break;
        }

        var from = Single(token[..range]);
        var to = Single(token[(range + 2)..]);
        for (var v = from.Value; v <= to.Value; v++)
            yield return Tile.Of(from.Color, v);
    }

    static Tile Single(string token)
    {
        if (token == "J")
            return Tile.Joker;
        var color = token[0] switch
        {
            'R' => Color.Red,
            'B' => Color.Blue,
            'Y' => Color.Yellow,
            'K' => Color.Black,
            _ => throw new FormatException($"Unknown colour in tile '{token}'."),
        };
        return Tile.Of(color, int.Parse(token[1..]));
    }
}
