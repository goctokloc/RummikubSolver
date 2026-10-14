using Rummikub.Core;
using Rummikub.Solver.Solver;

var hand = new Hand(
[
    Tile.Of(Color.Yellow, 5),
    Tile.Of(Color.Black, 5),
    Tile.Joker // Joker,
]);

var board = new BoardState([new Group(GroupType.Run, [Tile.Of(Color.Blue, 5), Tile.Of(Color.Blue, 6), Tile.Of(Color.Blue, 7), Tile.Of(Color.Blue, 8), Tile.Of(Color.Blue, 9)]),
    new Group(GroupType.Run, [Tile.Of(Color.Red, 5), Tile.Of(Color.Red, 6), Tile.Of(Color.Red, 7), Tile.Of(Color.Red, 8)]),
    new Group(GroupType.Set, [Tile.Of(Color.Yellow, 9), Tile.Of(Color.Black, 9), Tile.Joker])]);

var solver = new RummikubSolver();

var result = solver.SolveMaxTiles(hand, board);

Console.WriteLine("================= Rummikub Solver ================");
Console.WriteLine($"Tiles played: {result.PlayedTiles.Count}");

foreach (var group in result.Board.Groups)
{
    Console.WriteLine(FormatGroup(group));
}

static string FormatGroup(Group group)
{
    var tiles = string.Join(", ", group.Tiles.Select(t => t.IsJoker ? "Joker" : $"{t.Color} {t.Value}"));
    return $"Group Type: {group.Type}, Tiles: [{tiles}]";
}

