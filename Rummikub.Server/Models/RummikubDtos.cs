namespace Rummikub.Server.Models;

/// <summary>A tile: <c>{ "joker": true }</c> or <c>{ "color": "Red", "value": 5 }</c>.</summary>
public class TileDto
{
    public string? Color { get; set; }
    public int? Value { get; set; }
    public bool? Joker { get; set; }
}

/// <summary>A board group as entered by the user; joker positions are inferred.</summary>
public class GroupInputDto
{
    public string Type { get; set; } = string.Empty;
    public List<TileDto>? Tiles { get; set; }
}

/// <summary>Optional solver settings; omitted fields use the solver defaults.</summary>
public class SolveOptionsDto
{
    public bool? InitialMeldDone { get; set; }
    public string? Objective { get; set; }
    public bool? KeepJokers { get; set; }
}

public class SolveRequest
{
    public List<TileDto>? Hand { get; set; }
    public List<GroupInputDto>? Board { get; set; }
    public SolveOptionsDto? Options { get; set; }
}

/// <summary>One position in a group: the tile it represents, and whether a joker fills it.</summary>
public class SlotDto
{
    public string Color { get; set; } = string.Empty;
    public int Value { get; set; }
    public bool Joker { get; set; }
}

public class GroupDto
{
    public string Type { get; set; } = string.Empty;
    public List<SlotDto> Slots { get; set; } = [];
}

public class SolveResponse
{
    public List<TileDto> PlayedTiles { get; set; } = [];
    public List<GroupDto> Board { get; set; } = [];
    public int TilesPlayed { get; set; }
    public bool Optimal { get; set; }
}
