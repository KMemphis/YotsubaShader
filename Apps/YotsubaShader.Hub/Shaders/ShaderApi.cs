using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace YotsubaShader.Hub.Shaders;

internal sealed record CompileRequest(string Code);

internal static class ShaderApi
{
    public static void Map(RouteGroupBuilder api)
    {
        var compiler = new ShaderCompiler();
        api.MapPost("/shader/compile", async (CompileRequest r, CancellationToken ct) =>
            Results.Json(await compiler.CompileAsync(r.Code, ct), new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web)));
    }
}
