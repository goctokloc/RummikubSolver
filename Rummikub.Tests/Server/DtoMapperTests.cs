using Rummikub.Core;
using Rummikub.Server.Mapping;
using Rummikub.Server.Models;
using Rummikub.Solver.Solver;

namespace Rummikub.Tests.Server;

[TestFixture]
public class DtoMapperTests
{
    static TileDto T(string c, int v) => new() { Color = c, Value = v };
    static TileDto J() => new() { Joker = true };

    static IReadOnlyDictionary<string, string> Errors(SolveRequest req)
    {
        Assert.That(DtoMapper.TryMapRequest(req, out _, out _, out var errors), Is.False);
        return errors;
    }

    [Test]
    public void Maps_Valid_Request()
    {
        var req = new SolveRequest
        {
            Hand = [T("red", 5), J()],
            Board = [new GroupInputDto { Type = "run", Tiles = [T("Blue", 3), J(), T("Blue", 5)] }],
        };
        Assert.That(DtoMapper.TryMapRequest(req, out var hand, out var board, out var errors), Is.True);
        Assert.That(errors, Is.Empty);
        Assert.That(hand, Is.EqualTo(Hand.Of([Tile.Of(Color.Red, 5), Tile.Joker])));
        Assert.That(board, Is.EqualTo(BoardState.Of([Run.Create(Color.Blue, 3, 3, 0b010)])));
    }

    [Test]
    public void Null_Lists_Are_Empty()
    {
        Assert.That(DtoMapper.TryMapRequest(new SolveRequest(), out var hand, out var board, out _), Is.True);
        Assert.That(hand, Is.EqualTo(Hand.Empty));
        Assert.That(board, Is.EqualTo(BoardState.Empty));
    }

    [Test]
    public void Bad_Tiles_Report_Exact_Paths()
    {
        var errors = Errors(new SolveRequest
        {
            Hand = [new TileDto { Joker = true, Color = "Red" }, new TileDto { Color = "Red" },
                    T("Green", 3), T("Red", 14)],
        });
        Assert.That(errors, Is.EquivalentTo(new Dictionary<string, string>
        {
            ["hand[0]"] = "joker must not have color or value",
            ["hand[1]"] = "color and value are required",
            ["hand[2]"] = "unknown color 'Green'",
            ["hand[3]"] = "value must be between 1 and 13",
        }));
    }

    [Test]
    public void Unknown_Group_Type_Is_Reported() =>
        Assert.That(Errors(new SolveRequest { Board = [new GroupInputDto { Type = "pair", Tiles = [T("Red", 1), T("Red", 2), T("Red", 3)] }] }),
            Is.EquivalentTo(new Dictionary<string, string> { ["board[0]"] = "unknown group type 'pair'" }));

    [Test]
    public void Invalid_Group_Uses_Domain_Message() =>
        Assert.That(Errors(new SolveRequest { Board = [new GroupInputDto { Type = "set", Tiles = [T("Red", 1), T("Blue", 2), T("Black", 3)] }] }),
            Is.EquivalentTo(new Dictionary<string, string> { ["board[0]"] = "set tiles must share one value" }));

    [Test]
    public void Tile_Error_Inside_Group_Has_Nested_Path() =>
        Assert.That(Errors(new SolveRequest { Board = [new GroupInputDto { Type = "run", Tiles = [T("Red", 1), T("Red", 2), T("Pink", 3)] }] }),
            Is.EquivalentTo(new Dictionary<string, string> { ["board[0].tiles[2]"] = "unknown color 'Pink'" }));

    [Test]
    public void Null_List_Elements_Are_Reported()
    {
        var errors = Errors(new SolveRequest
        {
            Hand = [null!],
            Board = [null!, new GroupInputDto { Type = "run", Tiles = [T("Red", 1), T("Red", 2), null!] }],
        });
        Assert.That(errors, Is.EquivalentTo(new Dictionary<string, string>
        {
            ["hand[0]"] = "tile is required",
            ["board[0]"] = "group is required",
            ["board[1].tiles[2]"] = "tile is required",
        }));
    }

    [Test]
    public void Comma_Separated_Enum_Text_Is_Rejected()
    {
        var errors = Errors(new SolveRequest
        {
            Hand = [T("Red,Blue", 3)],
            Board = [new GroupInputDto { Type = "Run,Set", Tiles = [T("Red", 1), T("Red", 2), T("Red", 3)] }],
        });
        Assert.That(errors, Is.EquivalentTo(new Dictionary<string, string>
        {
            ["hand[0]"] = "unknown color 'Red,Blue'",
            ["board[0]"] = "unknown group type 'Run,Set'",
        }));
    }

    [Test]
    public void Response_Slots_Carry_Joker_Meaning()
    {
        var result = SolverResult.Create(BoardState.Of([Run.Create(Color.Red, 3, 3, 0b010)]), [Tile.Joker], isOptimal: true);
        var dto = DtoMapper.ToResponse(result);
        Assert.That(dto.TilesPlayed, Is.EqualTo(1));
        Assert.That(dto.PlayedTiles[0].Joker, Is.True);
        Assert.That(dto.Board[0].Type, Is.EqualTo("Run"));
        var slot = dto.Board[0].Slots[1];
        Assert.That((slot.Color, slot.Value, slot.Joker), Is.EqualTo(("Red", 4, true)));
    }
}
