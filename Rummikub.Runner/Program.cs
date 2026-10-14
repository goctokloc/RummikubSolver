using Rummikub.Core;
using Rummikub.Solver.Solver;

var hand = new Hand(
[
    Tile.Of(Color.Yellow, 5),
    Tile.Of(Color.Black, 5),
    Tile.Joker // Joker,
]);

var board = new BoardState([
    Run.Create(Color.Blue, 5, 5, 0),
    Run.Create(Color.Red, 5, 4, 0),
    Set.Create(9, 0b1101, 0b0001)]); // Yellow 9, Black 9, joker as Red 9

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

