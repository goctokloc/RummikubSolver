using Microsoft.AspNetCore.Mvc;
using Rummikub.Server.Mapping;
using Rummikub.Server.Models;
using Rummikub.Solver.Solver;

namespace Rummikub.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SolverController : ControllerBase
{
    private readonly RummikubSolver _solver = new();

    [HttpPost("solve")]
    public ActionResult<SolveResponse> Solve([FromBody] SolveRequest request)
    {
        if (!DtoMapper.TryMapRequest(request, out var hand, out var board, out var errors))
        {
            foreach (var (path, message) in errors)
                ModelState.AddModelError(path, message);
            return ValidationProblem(ModelState);
        }

        var result = _solver.SolveMaxTiles(hand, board);
        return Ok(DtoMapper.ToResponse(result));
    }
}
