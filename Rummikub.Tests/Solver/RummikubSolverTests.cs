using Rummikub.Core;
using Rummikub.Solver.Solver;

namespace Rummikub.Tests.Solver;

[TestFixture]
public class RummikubSolverTests
{
    private RummikubSolver _solver = null!;

    [SetUp]
    public void SetUp() => _solver = new RummikubSolver();

    [Test]
    public void Solver_Should_Return_Empty_Result_When_Hand_Is_Empty()
    {
        Hand hand = new([]);
        BoardState board = BoardState.Empty;

        var result = _solver.SolveMaxTiles(hand, board);

        Assert.That(result, Is.EqualTo(SolverResult.Empty));
    }

    [Test]
    public void Solver_Single_Valid_Run_Should_Play_All_Tiles()
    {
        Hand hand = new(
        [
            Tile.Of(Color.Red, 3),
            Tile.Of(Color.Red, 4),
            Tile.Of(Color.Red, 5)
        ]);
        BoardState board = BoardState.Empty;

        var result = _solver.SolveMaxTiles(hand, board);
        Assert.Multiple(() =>
        {
            Assert.That(result.PlayedTiles, Has.Count.EqualTo(3));
            Assert.That(result.Board.Groups, Has.Count.EqualTo(1));
        });
        Assert.That(result.Board.Groups[0].Type, Is.EqualTo(GroupType.Run));
    }

    [Test]
    public void Solver_Single_Valid_Set_Should_Play_All_Tiles()
    {
        Hand hand = new(
        [
            Tile.Of(Color.Red, 7),
            Tile.Of(Color.Blue, 7),
            Tile.Of(Color.Black, 7)
        ]);
        BoardState board = BoardState.Empty;

        var result = _solver.SolveMaxTiles(hand, board);
        Assert.Multiple(() =>
        {
            Assert.That(result.PlayedTiles, Has.Count.EqualTo(3));
            Assert.That(result.Board.Groups, Has.Count.EqualTo(1));
        });
        Assert.That(result.Board.Groups[0].Type, Is.EqualTo(GroupType.Set));
    }

    [Test]
    public void Solver_Hand_With_Set_And_Run_Should_Play_All_Tiles()
    {
        Hand hand = new(
        [
            Tile.Of(Color.Red, 7),
            Tile.Of(Color.Blue, 7),
            Tile.Of(Color.Black, 7),
            Tile.Of(Color.Red, 3),
            Tile.Of(Color.Red, 4),
            Tile.Of(Color.Red, 5)
        ]);
        BoardState board = BoardState.Empty;

        var result = _solver.SolveMaxTiles(hand, board);
        Assert.Multiple(() =>
        {
            Assert.That(result.PlayedTiles, Has.Count.EqualTo(6));
            Assert.That(result.Board.Groups, Has.Count.EqualTo(2));
        });
    }

    [Test]
    public void Solver_Hand_With_Set_And_Run_Should_Play_All_Available_Tiles()
    {
        Hand hand = new(
        [
            Tile.Of(Color.Red, 7),
            Tile.Of(Color.Blue, 7),
            Tile.Of(Color.Black, 7),
            Tile.Of(Color.Red, 3),
            Tile.Of(Color.Red, 4),
            Tile.Of(Color.Red, 5),
            Tile.Of(Color.Yellow, 9) //junk
        ]);
        BoardState board = BoardState.Empty;

        var result = _solver.SolveMaxTiles(hand, board);
        Assert.Multiple(() =>
        {
            Assert.That(result.PlayedTiles, Has.Count.EqualTo(6));
            Assert.That(result.Board.Groups, Has.Count.EqualTo(2));
            Assert.That(result.PlayedTiles, Does.Not.Contain(Tile.Of(Color.Yellow, 9)));
        });
    }

    [Test]
    public void Solver_Hand_With_Joker_Should_Complete_Group()
    {
        Hand hand = new(
        [
            Tile.Of(Color.Red, 7),
            Tile.Of(Color.Blue, 7),
            Tile.Joker
        ]);
        BoardState board = BoardState.Empty;

        var result = _solver.SolveMaxTiles(hand, board);
        Assert.Multiple(() =>
        {
            Assert.That(result.PlayedTiles, Has.Count.EqualTo(3));
            Assert.That(result.Board.Groups, Has.Count.EqualTo(1));
            Assert.That(result.Board.Groups[0].Type, Is.EqualTo(GroupType.Set));
            Assert.That(result.Board.Groups[0].Tiles, Does.Contain(Tile.Joker));
        });
    }

    [Test]
    public void Solver_Hand_With_Joker_Should_Complete_Run()
    {
        Hand hand = new(
        [
            Tile.Of(Color.Red, 3),
            Tile.Of(Color.Red, 4),
            Tile.Joker
        ]);
        BoardState board = BoardState.Empty;

        var result = _solver.SolveMaxTiles(hand, board);
        Assert.Multiple(() =>
        {
            Assert.That(result.PlayedTiles, Has.Count.EqualTo(3));
            Assert.That(result.Board.Groups, Has.Count.EqualTo(1));
            Assert.That(result.Board.Groups[0].Type, Is.EqualTo(GroupType.Run));
            Assert.That(result.Board.Groups[0].Tiles, Does.Contain(Tile.Joker));
        });
    }

    [Test]
    public void Solver_Hand_With_Only_Two_Tiles_Should_Return_Empty()
    {
        Hand hand = new([Tile.Of(Color.Red, 3),
            Tile.Of(Color.Red, 4)
        ]);
        BoardState board = BoardState.Empty;

        var result = _solver.SolveMaxTiles(hand, board);

        Assert.That(result.PlayedTiles, Has.Count.EqualTo(0));
    }

    [Test]
    public void Solver_Should_Prefer_Larger_Groups()
    {
        Hand hand = new([
            Tile.Of(Color.Red, 3),
            Tile.Of(Color.Red, 4),
            Tile.Of(Color.Red, 5),
            Tile.Of(Color.Red, 6)
        ]);
        BoardState board = BoardState.Empty;

        var result = _solver.SolveMaxTiles(hand, board);

        Assert.That(result.PlayedTiles, Has.Count.EqualTo(4));
    }

    [Test]
    public void Solver_Repeated_Calls_Are_Idempotent()
    {
        Hand hand = new([
            Tile.Of(Color.Red, 3),
            Tile.Of(Color.Red, 4),
            Tile.Of(Color.Red, 5)
        ]);
        BoardState board = BoardState.Empty;

        var first = _solver.SolveMaxTiles(hand, board);
        var second = _solver.SolveMaxTiles(hand, board);

        Assert.That(first, Is.EqualTo(second));
    }

    [Test]
    public void Solver_Splits_Existing_Run()
    {
        BoardState board = new BoardState([
            Run.Create(Color.Red, 3, 5, 0)
        ]);

        Hand hand = new([
            Tile.Of(Color.Red, 5)
        ]);

        var result = _solver.SolveMaxTiles(hand, board);
        Assert.Multiple(() =>
        {
            Assert.That(result.PlayedTiles, Has.Count.EqualTo(1));
            Assert.That(result.Board.Groups, Has.Count.EqualTo(2));
        });
    }

    [Test]
    public void Solver_Steals_Tile_From_Set()
    {
        BoardState board = new BoardState([
            Set.Create(5, 0b1111, 0)
        ]);

        Hand hand = new([
            Tile.Of(Color.Red, 3),
            Tile.Of(Color.Red, 4)
        ]);

        var result = _solver.SolveMaxTiles(hand, board);
        Assert.Multiple(() =>
        {
            Assert.That(result.PlayedTiles, Has.Count.EqualTo(2));
            Assert.That(result.Board.Groups, Has.Count.EqualTo(2));
        });
    }

    [Test]
    public void Solver_Does_Not_Throw_With_Three_Jokers_In_Pool()
    {
        var hand = new Hand([Tile.Joker, Tile.Joker, Tile.Joker, Tile.Of(Color.Red, 5)]);
        var result = _solver.SolveMaxTiles(hand, BoardState.Empty);
        Assert.That(result.PlayedTiles, Has.Count.EqualTo(3));
    }
}
