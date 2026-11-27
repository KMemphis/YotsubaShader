using System.Diagnostics;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace YotsubaShader.Hub.Updates;

internal static class UpdatesApi
{
    private static readonly string Allowed = $"https://github.com/{UpdateService.Repo}/";

    public static void Map(RouteGroupBuilder api, UpdateService updates, AppUpdater app)
    {
        api.MapGet("/updates", async () => await updates.GetStatusAsync(app.CanSelfUpdate));

        api.MapPost("/updates/check", async () =>
        {
            await updates.CheckAsync();
            return await updates.GetStatusAsync(app.CanSelfUpdate);
        });

        // "Download": the installer of this system in the newest release, saved to Downloads and opened.
        api.MapPost("/updates/download", async (CancellationToken ct) =>
        {
            var (path, error) = await InstallerDownload.RunAsync(updates.LatestRelease(), updates.Http, ct);
            return error is null ? Results.Ok(new { path }) : Results.Conflict(error);
        });

        api.MapPost("/updates/apply", async () =>
            await app.ApplyAsync() is { } error ? Results.Conflict(error) : Results.Accepted());

        // The UI runs in a webview, where target=_blank does nothing useful. Only the project's own pages may be opened.
        api.MapPost("/open", (OpenRequest r) =>
        {
            if (!r.Url.StartsWith(Allowed, StringComparison.Ordinal) || !Uri.TryCreate(r.Url, UriKind.Absolute, out _))
                return Results.BadRequest("Only pages of the project repository can be opened.");
            Process.Start(new ProcessStartInfo(r.Url) { UseShellExecute = true });
            return Results.NoContent();
        });
    }

    internal sealed record OpenRequest(string Url);
}
