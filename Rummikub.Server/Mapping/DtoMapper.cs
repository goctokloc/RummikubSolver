using System.Diagnostics.CodeAnalysis;
using Rummikub.Core;
using Rummikub.Server.Models;
using Rummikub.Solver.Solver;

namespace Rummikub.Server.Mapping;

/// <summary>Pure mapping between API DTOs and the domain, collecting validation errors by path.</summary>
public static class DtoMapper
{
    public static bool TryMapRequest(
        SolveRequest request,
        [NotNullWhen(true)] out Hand? hand,
        [NotNullWhen(true)] out BoardState? board,
        [NotNullWhen(true)] out SolverOptions? options,
        out IReadOnlyDictionary<string, string> errors)
    {
        var found = new Dictionary<string, string>();

        var handTiles = MapTiles(request.Hand, "hand", found);

        var groups = new List<Group>();
        var boardDtos = request.Board ?? [];
        for (var i = 0; i < boardDtos.Count; i++)
        {
            var path = $"board[{i}]";
            var dto = boardDtos[i];
            if (dto is null)
            {
                found[path] = "group is required";
                continue;
            }

            var tileErrorsBefore = found.Count;
            var tiles = MapTiles(dto.Tiles, $"{path}.tiles", found);
            if (found.Count > tileErrorsBefore)
                continue;

            if (!TryParseEnum<GroupType>(dto.Type, out var type))
            {
                found[path] = $"unknown group type '{dto.Type}'";
                continue;
            }

            if (Group.TryFromTiles(type, tiles, out var group, out var error))
                groups.Add(group);
            else
                found[path] = error;
        }

        var mapped = MapOptions(request.Options, found);

        errors = found;
        if (found.Count > 0)
        {
            hand = null;
            board = null;
            options = null;
            return false;
        }

        options = mapped;

        hand = Hand.Of(handTiles);
        board = BoardState.Of(groups);
        return true;
    }

    public static SolveResponse ToResponse(SolverResult result) => new()
    {
        PlayedTiles = result.PlayedTiles.Select(ToDto).ToList(),
        Board = result.Board.Groups.Select(ToDto).ToList(),
        TilesPlayed = result.PlayedTiles.Count,
        Optimal = result.IsOptimal
    };

    public static TileDto ToDto(Tile tile) => tile.IsJoker
        ? new TileDto { Joker = true }
        : new TileDto { Color = tile.Color.ToString(), Value = tile.Value, Joker = false };

    public static GroupDto ToDto(Group group) => new()
    {
        Type = group.Type.ToString(),
        Slots = group.Slots
            .Select(s => new SlotDto { Color = s.Color.ToString(), Value = s.Value, Joker = s.Tile.IsJoker })
            .ToList()
    };

    private static SolverOptions MapOptions(SolveOptionsDto? dto, Dictionary<string, string> errors)
    {
        var defaults = new SolverOptions();
        if (dto is null)
            return defaults;

        var objective = defaults.Objective;
        if (dto.Objective is not null && !TryParseEnum(dto.Objective, out objective))
            errors["options.objective"] = $"unknown objective '{dto.Objective}'";

        return defaults with
        {
            InitialMeldDone = dto.InitialMeldDone ?? defaults.InitialMeldDone,
            Objective = objective,
            KeepJokers = dto.KeepJokers ?? defaults.KeepJokers,
        };
    }

    private static List<Tile> MapTiles(List<TileDto>? dtos, string path, Dictionary<string, string> errors)
    {
        var tiles = new List<Tile>();
        dtos ??= [];
        for (var i = 0; i < dtos.Count; i++)
        {
            if (TryMapTile(dtos[i], out var tile, out var error))
                tiles.Add(tile);
            else
                errors[$"{path}[{i}]"] = error;
        }
        return tiles;
    }

    private static bool TryMapTile(TileDto? dto, out Tile tile, [NotNullWhen(false)] out string? error)
    {
        tile = default;
        error = null;

        if (dto is null)
            error = "tile is required";
        else if (dto.Joker == true)
        {
            if (dto.Color is not null || dto.Value is not null)
                error = "joker must not have color or value";
            else
                tile = Tile.Joker;
        }
        else if (dto.Color is null || dto.Value is null)
            error = "color and value are required";
        else if (!TryParseEnum<Color>(dto.Color, out var color))
            error = $"unknown color '{dto.Color}'";
        else if (dto.Value is < 1 or > 13)
            error = "value must be between 1 and 13";
        else
            tile = Tile.Of(color, dto.Value.Value);

        return error is null;
    }

    /// <summary>
    /// Case-insensitive match against a single enum member name; rejects numeric and
    /// comma-combined text that <see cref="Enum.TryParse{TEnum}(string?, bool, out TEnum)"/> would accept.
    /// </summary>
    private static bool TryParseEnum<TEnum>(string? text, out TEnum value) where TEnum : struct, Enum
    {
        var name = Enum.GetNames<TEnum>().FirstOrDefault(n => string.Equals(n, text, StringComparison.OrdinalIgnoreCase));
        value = name is null ? default : Enum.Parse<TEnum>(name);
        return name is not null;
    }
}
