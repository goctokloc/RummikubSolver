using Rummikub.Core.Groups;

namespace Rummikub.Core.Board;

public sealed record BoardState(IReadOnlyList<Group> Groups)
{
    public static BoardState Empty { get; } = new BoardState([]);

    public BoardState AddGroup(Group group) => new([.. Groups, group]);
}