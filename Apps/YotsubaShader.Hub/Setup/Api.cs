using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace YotsubaShader.Hub.Setup;

internal static class Api
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() },
    };

    /// <summary>The API lives on the Hub's loopback origin; a per-launch token keeps other local pages from calling it.</summary>
    public static void Map(IEndpointRouteBuilder e, string token, string? licensePath, Updates.UpdateService updates, Updates.AppUpdater app, Forum.ForumSession forum)
    {
        var job = new InstallJob();
        var api = e.MapGroup("/api").AddEndpointFilter(async (ctx, next) =>
            ctx.HttpContext.Request.Query["t"] == token ? await next(ctx) : Results.Unauthorized());

        Forum.ForumApi.Map(api, forum);
        Updates.UpdatesApi.Map(api, updates, app);
        Shaders.ShaderApi.Map(api);

        api.MapGet("/state", async () => Results.Json(new { components = Catalog.All, env = await Probe.RunAsync() }, Json));

        // Engine plugins the CLI has downloaded, with the version each plugin.json records. The CLI only trusts a plugin whose
        // version equals its own, so the UI compares against the compiler version to flag the ones that are out of date.
        api.MapGet("/plugins", () =>
        {
            var root = Path.Combine(Environment.GetEnvironmentVariable("YOTSUBA_HOME") is { Length: > 0 } h ? h : Probe.DefaultHome(), "plugins");
            var found = new List<object>();
            foreach (var c in Catalog.All.Where(c => c.Kind == ComponentKind.Plugin))
            {
                var manifest = Path.Combine(root, c.Id, "plugin.json");
                if (!File.Exists(manifest)) continue;
                try
                {
                    using var doc = JsonDocument.Parse(File.ReadAllText(manifest));
                    var version = doc.RootElement.EnumerateObject().FirstOrDefault(p => p.NameEquals("version") || p.Name.Equals("Version", StringComparison.OrdinalIgnoreCase));
                    found.Add(new { id = c.Id, version = version.Value.ValueKind == JsonValueKind.String ? version.Value.GetString() : null });
                }
                catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException) { /* damaged manifest = not installed, as in the CLI */ }
            }
            return Results.Json(found);
        });

        api.MapGet("/license", () => Results.Text(
            licensePath is not null && File.Exists(licensePath) ? File.ReadAllText(licensePath) : "See https://github.com/KMemphis/YotsubaShader/blob/main/LICENSE"));

        api.MapPost("/install", (InstallRequest r) =>
            string.IsNullOrWhiteSpace(r.Home) || !Path.IsPathRooted(r.Home)
                ? Results.BadRequest("home must be an absolute path")
                : job.TryStart(r.Ids, r.Home) ? Results.Accepted() : Results.Conflict("An installation is already running."));

        api.MapGet("/install/events", async (HttpContext ctx) =>
        {
            ctx.Response.ContentType = "text/event-stream";
            ctx.Response.Headers.CacheControl = "no-cache";
            for (var sent = 0; !ctx.RequestAborted.IsCancellationRequested;)
            {
                var batch = job.Snapshot(sent);
                foreach (var ev in batch)
                    await ctx.Response.WriteAsync($"data: {JsonSerializer.Serialize(ev, Json)}\n\n");
                await ctx.Response.Body.FlushAsync();
                sent += batch.Count;
                if (batch.Any(b => b.Type == "done")) break;
                await Task.Delay(120, ctx.RequestAborted).ContinueWith(_ => { });
            }
        });
    }

    internal sealed record InstallRequest(string[] Ids, string Home);
}
