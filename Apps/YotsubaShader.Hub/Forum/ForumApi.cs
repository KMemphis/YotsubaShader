using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using YotsubaShader.Community;

namespace YotsubaShader.Hub.Forum;

internal static class ForumApi
{
    public static void Map(RouteGroupBuilder api, ForumSession forum)
    {
        var g = api.MapGroup("/forum");

        // Turn failures into short plain-text answers the UI shows as they are.
        g.AddEndpointFilter(async (ctx, next) =>
        {
            try { return await next(ctx); }
            catch (ForumException e) { return Results.Text(e.Message, statusCode: e.Status); }
            catch (UnauthorizedAccessException e) { return Results.Text(e.Message, statusCode: 403); }
            catch (NotSupportedException e) { return Results.Text(e.Message, statusCode: 405); }
        });

        g.MapGet("/info", async () =>
        {
            var info = await forum.Source.InfoAsync();
            return forum.ConfigProblem is { } problem ? info with { Notice = problem } : info;
        });
        g.MapGet("/session", async () => await forum.GetAsync());
        g.MapPost("/signin", () => { forum.BeginSignIn(); return Results.Accepted(); });
        g.MapPost("/signout", () => { forum.SignOut(); return Results.NoContent(); });

        g.MapGet("/threads", async (string? tag, string? q) => await forum.Source.ListAsync(tag, q));
        g.MapGet("/threads/{id}", async (string id) => await forum.Source.GetAsync(id) is { } t ? Results.Ok(t) : Results.NotFound());

        g.MapPost("/threads", async (NewThreadRequest r) =>
        {
            if ((ForumLimits.ValidateTitle(r.Title) ?? ForumLimits.Validate(r.Body)) is { } error) return Results.BadRequest(error);
            if (await forum.UserAsync() is not { } user) return SignInNeeded();
            return Results.Ok(await forum.Source.CreateAsync(user, r.Title, string.IsNullOrWhiteSpace(r.Tag) ? "Help" : r.Tag, r.Body));
        });

        g.MapPost("/threads/{id}/messages", async (string id, NewMessageRequest r) =>
            ForumLimits.Validate(r.Body) is { } e ? Results.BadRequest(e)
            : await forum.UserAsync() is not { } user ? SignInNeeded()
            : await forum.Source.ReplyAsync(user, id, r.Body) is { } m ? Results.Ok(m) : Results.NotFound());

        g.MapPost("/threads/{id}/messages/{mid}/react", async (string id, string mid, ReactRequest r) =>
            r.Emoji is not { Length: > 0 and <= 8 } ? Results.BadRequest("Invalid emoji.")
            : await forum.UserAsync() is not { } user ? SignInNeeded()
            : await forum.Source.ReactAsync(user, id, mid, r.Emoji) is { } m ? Results.Ok(m) : Results.NotFound());

        g.MapMethods("/threads/{id}/messages/{mid}", ["PATCH"], async (string id, string mid, EditMessageRequest r) =>
            ForumLimits.Validate(r.Body) is { } e ? Results.BadRequest(e)
            : await forum.UserAsync() is not { } user ? SignInNeeded()
            : await forum.Source.EditAsync(user, id, mid, r.Body) is { } m ? Results.Ok(m) : Results.NotFound());

        g.MapDelete("/threads/{id}/messages/{mid}", async (string id, string mid) =>
            await forum.UserAsync() is not { } user ? SignInNeeded()
            : await forum.Source.DeleteAsync(user, id, mid) ? Results.NoContent() : Results.NotFound());

        g.MapPost("/threads/{id}/messages/{mid}/pin", async (string id, string mid, PinRequest r) =>
            await forum.UserAsync() is not { } user ? SignInNeeded()
            : await forum.Source.PinAsync(user, id, mid, r.Pinned) is { } m ? Results.Ok(m) : Results.NotFound());

        g.MapPost("/threads/{id}/solve", async (string id, SolveRequest r) =>
            await forum.UserAsync() is not { } user ? SignInNeeded()
            : await forum.Source.SolveAsync(user, id, r.Solved) is { } t ? Results.Ok(t) : Results.NotFound());
    }

    private static IResult SignInNeeded() => Results.Text("Sign in with Discord to post.", statusCode: 401);
}
