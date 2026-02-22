using Rummikub.Core.Board;
using Rummikub.Core.Groups;
using Rummikub.Core.Hand;
using Rummikub.Core.Tiles;
using Rummikub.Solver.Solver;

var hand = new Hand(
[
    new Tile(Color.Yellow, 5, false),
    new Tile(Color.Black, 5, false),
    new Tile( null, null, true) // Joker,
]);

var board = new BoardState([new Group(GroupType.Run, [new Tile(Color.Blue, 5, false), new Tile(Color.Blue, 6, false), new Tile(Color.Blue, 7, false), new Tile(Color.Blue, 8, false), new Tile(Color.Blue, 9, false)]),
    new Group(GroupType.Run, [new Tile(Color.Red, 5, false), new Tile(Color.Red, 6, false), new Tile(Color.Red, 7, false), new Tile(Color.Red, 8, false)]),
    new Group(GroupType.Set, [new Tile(Color.Yellow, 9, false), new Tile(Color.Black, 9, false), new Tile(null, null, true)])]);

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

