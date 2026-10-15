using Rummikub.Core;

namespace Rummikub.Solver.Model;

/// <summary>Multiplicity of each tile kind (52 coloured tiles plus the joker) indexed by <see cref="Tile.Id"/>.</summary>
internal sealed class TileCounts
{
    public const int Kinds = 53;

    readonly int[] _counts = new int[Kinds];

    TileCounts()
    {
    }

    public int this[int id] => _counts[id];

    public int Total { get; private set; }

    public static TileCounts From(IEnumerable<Tile> tiles)
    {
        var result = new TileCounts();
        foreach (var tile in tiles)
        {
            result._counts[tile.Id]++;
            result.Total++;
        }
        return result;
    }
}
