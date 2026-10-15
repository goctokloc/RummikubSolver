using Rummikub.Core;
namespace Rummikub.Tests.Solver;

static class RandomGames
{
    public static (Hand Hand, BoardState Board) Deal(int seed, int boardTiles, int handTiles)
    {
        var rng = new Random(seed);
        var colors = Enum.GetValues<Color>();
        var deck = new List<Tile>();
        foreach (var color in colors)
            for (int v = 1; v <= 13; v++)
            {
                deck.Add(Tile.Of(color, v));
                deck.Add(Tile.Of(color, v));
            }
        deck.Add(Tile.Joker);
        deck.Add(Tile.Joker);
        for (int i = deck.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (deck[i], deck[j]) = (deck[j], deck[i]);
        }

        var groups = new List<Group>();
        int tiles = 0, attempts = 0;
        while (tiles < boardTiles && attempts++ < 2000)
        {
            Group candidate;
            List<Tile> needed;
            if (rng.Next(2) == 0)
            {
                int len = rng.Next(3, 6);
                int start = rng.Next(1, 13 - len + 2);
                var color = colors[rng.Next(colors.Length)];
                needed = Enumerable.Range(start, len).Select(v => Tile.Of(color, v)).ToList();
                candidate = Run.Create(color, start, len, 0);
            }
            else
            {
                int value = rng.Next(1, 14);
                int n = rng.Next(3, 5);
                var chosen = colors.OrderBy(_ => rng.Next()).Take(n).ToArray();
                byte mask = 0;
                foreach (var c in chosen) mask |= (byte)(1 << (int)c);
                needed = chosen.Select(c => Tile.Of(c, value)).ToList();
                candidate = Set.Create(value, mask, 0);
            }

            var rest = new List<Tile>(deck);
            if (!needed.All(rest.Remove)) continue;
            deck = rest;
            groups.Add(candidate);
            tiles += needed.Count;
        }

        return (Hand.Of(deck.Take(handTiles)), BoardState.Of(groups));
    }
}
