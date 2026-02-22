namespace Rummikub.Server.Models;

public class TileDto
{
    public string? Color { get; set; }
    public int? Value { get; set; }
    public bool IsJoker { get; set; }
}

public class GroupDto
{
    public string Type { get; set; } = string.Empty;
    public List<TileDto> Tiles { get; set; } = new();
}

public class SolveRequestDto
{
    public List<TileDto> Hand { get; set; } = new();
    public List<GroupDto> Board { get; set; } = new();
}

public class SolveResponseDto
{
    public List<TileDto> PlayedTiles { get; set; } = new();
    public List<GroupDto> NewBoard { get; set; } = new();
    public int TilesPlayedCount { get; set; }
}
