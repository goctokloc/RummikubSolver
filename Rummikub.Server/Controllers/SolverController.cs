using Microsoft.AspNetCore.Mvc;
using Rummikub.Core.Board;
using Rummikub.Core.Groups;
using Rummikub.Core.Hand;
using Rummikub.Core.Tiles;
using Rummikub.Server.Models;
using Rummikub.Solver.Solver;

namespace Rummikub.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SolverController : ControllerBase
{
    private readonly RummikubSolver _solver;

    public SolverController()
    {
        _solver = new RummikubSolver();
    }

    [HttpPost("solve")]
    public ActionResult<SolveResponseDto> Solve([FromBody] SolveRequestDto request)
    {
        try
        {
            var hand = MapHand(request.Hand);
            var board = MapBoard(request.Board);

            var result = _solver.SolveMaxTiles(hand, board);

            return Ok(new SolveResponseDto
            {
                PlayedTiles = result.PlayedTiles.Select(MapTileDto).ToList(),
                NewBoard = result.Board.Groups.Select(MapGroupDto).ToList(),
                TilesPlayedCount = result.PlayedTiles.Count
            });
        }
        catch (Exception ex)
        {
            return BadRequest(new { Error = ex.Message });
        }
    }

    private static Hand MapHand(List<TileDto> dtos)
    {
        return new Hand(dtos.Select(MapTile).ToList());
    }

    private static BoardState MapBoard(List<GroupDto> dtos)
    {
        var groups = new List<Group>();
        foreach (var dto in dtos)
        {
            var type = Enum.Parse<GroupType>(dto.Type, ignoreCase: true);
            var tiles = dto.Tiles.Select(MapTile).ToList();
            groups.Add(new Group(type, tiles));
        }
        return new BoardState(groups);
    }

    private static Tile MapTile(TileDto dto)
    {
        Color? color = null;
        if (!string.IsNullOrEmpty(dto.Color) && Enum.TryParse<Color>(dto.Color, true, out var c))
        {
            color = c;
        }

        return new Tile(color, dto.Value, dto.IsJoker);
    }

    private static TileDto MapTileDto(Tile tile)
    {
        return new TileDto
        {
            Color = tile.Color?.ToString(),
            Value = tile.Value,
            IsJoker = tile.IsJoker
        };
    }

    private static GroupDto MapGroupDto(Group group)
    {
        return new GroupDto
        {
            Type = group.Type.ToString(),
            Tiles = group.Tiles.Select(MapTileDto).ToList()
        };
    }
}
