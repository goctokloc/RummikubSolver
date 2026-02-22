using Rummikub.Core.Tiles;

namespace Rummikub.Core.Groups;

public class GroupValidator
{
    public static bool IsValid(Group group)
    {
        return IsValid(group.Type, [.. group.Tiles]);
    }

    private static bool IsValid(GroupType groupType, List<Tile> tiles)
    {
        switch (groupType)
        {
            case GroupType.Set:
                {
                    var nonJokerTiles = tiles.Where(t => !t.IsJoker).ToList();
                    var firstValue = nonJokerTiles.First().Value;
                    var colors = new HashSet<Color>();

                    foreach (var tile in nonJokerTiles)
                    {
                        if (tile.Value != firstValue || tile.Color == null || !colors.Add(tile.Color.Value))
                        {
                            return false;
                        }
                    }

                    return true;
                }

            case GroupType.Run:
                {
                    var nonJokerTiles = tiles.Where(t => !t.IsJoker).OrderBy(t => t.Value).ToList();
                    var firstColor = nonJokerTiles.First().Color;

                    for (int i = 0; i < nonJokerTiles.Count - 1; i++)
                    {
                        if (nonJokerTiles[i].Color != firstColor || nonJokerTiles[i + 1].Value != nonJokerTiles[i].Value + 1)
                        {
                            return false;
                        }
                    }

                    return true;
                }
        }

        return false;
    }
}