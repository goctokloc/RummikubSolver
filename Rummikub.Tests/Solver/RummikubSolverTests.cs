using Rummikub.Core.Board;
using Rummikub.Core.Groups;
using Rummikub.Core.Hand;
using Rummikub.Core.Tiles;
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
            new Tile(Color.Red, 3, false),
            new Tile(Color.Red, 4, false),
            new Tile(Color.Red, 5, false)
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
            new Tile(Color.Red, 7, false),
            new Tile(Color.Blue, 7, false),
            new Tile(Color.Black, 7, false)
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
            new Tile(Color.Red, 7, false),
            new Tile(Color.Blue, 7, false),
            new Tile(Color.Black, 7, false),
            new Tile(Color.Red, 3, false),
            new Tile(Color.Red, 4, false),
            new Tile(Color.Red, 5, false)
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
            new Tile(Color.Red, 7, false),
            new Tile(Color.Blue, 7, false),
            new Tile(Color.Black, 7, false),
            new Tile(Color.Red, 3, false),
            new Tile(Color.Red, 4, false),
            new Tile(Color.Red, 5, false),
            new Tile(Color.Yellow, 9, false) //junk
        ]);
        BoardState board = BoardState.Empty;

        var result = _solver.SolveMaxTiles(hand, board);
        Assert.Multiple(() =>
        {
            Assert.That(result.PlayedTiles, Has.Count.EqualTo(6));
            Assert.That(result.Board.Groups, Has.Count.EqualTo(2));
            Assert.That(result.PlayedTiles, Does.Not.Contain(new Tile(Color.Yellow, 9, false)));
        });
    }

    [Test]
    public void Solver_Hand_With_Joker_Should_Complete_Group()
    {
        Hand hand = new(
        [
            new Tile(Color.Red, 7, false),
            new Tile(Color.Blue, 7, false),
            new Tile(null, null, true)
        ]);
        BoardState board = BoardState.Empty;

        var result = _solver.SolveMaxTiles(hand, board);
        Assert.Multiple(() =>
        {
            Assert.That(result.PlayedTiles, Has.Count.EqualTo(3));
            Assert.That(result.Board.Groups, Has.Count.EqualTo(1));
            Assert.That(result.Board.Groups[0].Type, Is.EqualTo(GroupType.Set));
            Assert.That(result.Board.Groups[0].Tiles, Does.Contain(new Tile(null, null, true)));
        });
    }

    [Test]
    public void Solver_Hand_With_Joker_Should_Complete_Run()
    {
        Hand hand = new(
        [
            new Tile(Color.Red, 3, false),
            new Tile(Color.Red, 4, false),
            new Tile(null, null, true)
        ]);
        BoardState board = BoardState.Empty;

        var result = _solver.SolveMaxTiles(hand, board);
        Assert.Multiple(() =>
        {
            Assert.That(result.PlayedTiles, Has.Count.EqualTo(3));
            Assert.That(result.Board.Groups, Has.Count.EqualTo(1));
            Assert.That(result.Board.Groups[0].Type, Is.EqualTo(GroupType.Run));
            Assert.That(result.Board.Groups[0].Tiles, Does.Contain(new Tile(null, null, true)));
        });
    }

    [Test]
    public void Solver_Hand_With_Only_Two_Tiles_Should_Return_Empty()
    {
        Hand hand = new([new Tile(Color.Red, 3, false),
            new Tile(Color.Red, 4, false)
        ]);
        BoardState board = BoardState.Empty;

        var result = _solver.SolveMaxTiles(hand, board);

        Assert.That(result.PlayedTiles, Has.Count.EqualTo(0));
    }

    [Test]
    public void Solver_Should_Prefer_Larger_Groups()
    {
        Hand hand = new([
            new Tile(Color.Red, 3, false),
            new Tile(Color.Red, 4, false),
            new Tile(Color.Red, 5, false),
            new Tile(Color.Red, 6, false)
        ]);
        BoardState board = BoardState.Empty;

        var result = _solver.SolveMaxTiles(hand, board);

        Assert.That(result.PlayedTiles, Has.Count.EqualTo(4));
    }

    [Test]
    public void Solver_Repeated_Calls_Are_Idempotent()
    {
        Hand hand = new([
            new Tile(Color.Red, 3, false),
            new Tile(Color.Red, 4, false),
            new Tile(Color.Red, 5, false)
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
            new Group(GroupType.Run, [
                new Tile(Color.Red, 3, false),
                new Tile(Color.Red, 4, false),
                new Tile(Color.Red, 5, false),
                new Tile(Color.Red, 6, false),
                new Tile(Color.Red, 7, false)
            ])
        ]);

        Hand hand = new([
            new Tile(Color.Red, 5, false)
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
            new Group(GroupType.Set, [
                new Tile(Color.Red, 5, false),
                new Tile(Color.Blue, 5, false),
                new Tile(Color.Black, 5, false),
                new Tile(Color.Yellow, 5, false)
            ])
        ]);

        Hand hand = new([
            new Tile(Color.Red, 3, false),
            new Tile(Color.Red, 4, false)
        ]);

        var result = _solver.SolveMaxTiles(hand, board);
        Assert.Multiple(() =>
        {
            Assert.That(result.PlayedTiles, Has.Count.EqualTo(2));
            Assert.That(result.Board.Groups, Has.Count.EqualTo(2));
        });
    }

    [Test]
    public void Solver_Invalid_Board_Yields_Empty_If_Cannot_Fix()
    {
        BoardState board = new BoardState([
            new Group(GroupType.Set, [
                new Tile(Color.Red, 1, false),
                new Tile(Color.Blue, 2, false),
                new Tile(Color.Black, 3, false)
            ])
        ]);

        Hand hand = new([new Tile(Color.Yellow, 2, false)]);

        var result = _solver.SolveMaxTiles(hand, board);
        Assert.That(result, Is.EqualTo(SolverResult.Empty));
    }
}