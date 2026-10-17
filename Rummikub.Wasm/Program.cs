using System.Runtime.InteropServices.JavaScript;
using System.Text.Json;
using System.Text.Json.Serialization;
using Rummikub.Server.Mapping;
using Rummikub.Server.Models;
using Rummikub.Solver.Solver;

// The runtime calls Main once on startup; the work happens in the exported Solve.
return;

[System.Runtime.Versioning.SupportedOSPlatform("browser")]
public static partial class SolverExports
{
    private static readonly RummikubSolver Solver = new();

    /// <summary>
    /// Takes a <see cref="SolveRequest"/> as JSON and returns either a <see cref="SolveResponse"/>
    /// or <c>{ "errors": { path: message } }</c>, mirroring the server's 200 and 400 bodies.
    /// </summary>
    [JSExport]
    public static string Solve(string requestJson)
    {
        SolveRequest? request;
        try
        {
            request = JsonSerializer.Deserialize(requestJson, WasmJsonContext.Default.SolveRequest);
        }
        catch (JsonException e)
        {
            return Errors(new Dictionary<string, string> { ["$"] = e.Message });
        }

        if (request is null)
            return Errors(new Dictionary<string, string> { ["$"] = "request is required" });

        if (!DtoMapper.TryMapRequest(request, out var hand, out var board, out var options, out var errors))
            return Errors(errors);

        var result = Solver.Solve(hand, board, options);
        return JsonSerializer.Serialize(DtoMapper.ToResponse(result), WasmJsonContext.Default.SolveResponse);
    }

    private static string Errors(IReadOnlyDictionary<string, string> errors) =>
        JsonSerializer.Serialize(new ErrorResponse { Errors = errors.ToDictionary(e => e.Key, e => e.Value) },
            WasmJsonContext.Default.ErrorResponse);
}

public class ErrorResponse
{
    public Dictionary<string, string> Errors { get; set; } = [];
}

[JsonSourceGenerationOptions(JsonSerializerDefaults.Web)]
[JsonSerializable(typeof(SolveRequest))]
[JsonSerializable(typeof(SolveResponse))]
[JsonSerializable(typeof(ErrorResponse))]
internal partial class WasmJsonContext : JsonSerializerContext;
