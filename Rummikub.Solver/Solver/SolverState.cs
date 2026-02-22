using Rummikub.Core.Board;
using Rummikub.Core.Common;
using Rummikub.Core.Tiles;

namespace Rummikub.Solver.Solver;

internal sealed record SolverState(MultiSet<Tile> RemainingHand, BoardState Board);