using YotsubaShader.Community.Server.Auth;

namespace YotsubaShader.Community.Server;

/// <summary>The /v1 API the Hub talks to. Reading is public; writing needs the session token from the Discord sign-in.</summary>
public static class ForumEndpoints
{
    public static void Map(WebApplication app)
    {
        var v1 = app.MapGroup("/v1");

        // Plain-text answers: the Hub shows them to the user as they are.
        v1.AddEndpointFilter(async (ctx, next) =>
        {
            var http = ctx.HttpContext;
            if (!http.RequestServices.GetRequiredService<Limiter>().Allow($"ip:{http.Connection.RemoteIpAddress}", 240, TimeSpan.FromMinutes(1)))
                return Results.Text("Too many requests. Wait a moment.", statusCode: 429);
            try { return await next(ctx); }
            catch (UnauthorizedAccessException e) { return Results.Text(e.Message, statusCode: 403); }
            catch (NotSupportedException e) { return Results.Text(e.Message, statusCode: 405); }
            catch (InvalidOperationException e) { return Results.Text(e.Message, statusCode: 409); }
        });

        v1.MapGet("/info", (IForumSource f, CancellationToken ct) => f.InfoAsync(ct));
        v1.MapGet("/threads", (IForumSource f, string? tag, string? q, CancellationToken ct) => f.ListAsync(tag, q?.Length > 100 ? q[..100] : q, ct));
        v1.MapGet("/threads/{id}", async (IForumSource f, string id, CancellationToken ct) => await f.GetAsync(id, ct) is { } t ? Results.Ok(t) : Results.NotFound());

        v1.MapGet("/me", async (HttpContext http, SessionTokens tokens) => await User(http, tokens) is { } u ? Results.Ok(u) : Results.Unauthorized());

        v1.MapPost("/threads", async (HttpContext http, SessionTokens tokens, Limiter limit, IForumSource f, NewThreadRequest r, CancellationToken ct) =>
        {
            if (await User(http, tokens) is not { } user) return Results.Text("Sign in with Discord to post.", statusCode: 401);
            if ((ForumLimits.ValidateTitle(r.Title) ?? ForumLimits.Validate(r.Body)) is { } error) return Results.Text(error, statusCode: 400);
            if (!limit.Allow($"thread:{user.Id}", 3, TimeSpan.FromMinutes(10))) return Results.Text("You are posting too fast. Try again in a few minutes.", statusCode: 429);
            return Results.Ok(await f.CreateAsync(user, r.Title, r.Tag ?? "", r.Body, ct));
        });

        v1.MapPost("/threads/{id}/messages", async (HttpContext http, SessionTokens tokens, Limiter limit, IForumSource f, string id, NewMessageRequest r, CancellationToken ct) =>
        {
            if (await User(http, tokens) is not { } user) return Results.Text("Sign in with Discord to post.", statusCode: 401);
            if (ForumLimits.Validate(r.Body) is { } error) return Results.Text(error, statusCode: 400);
            if (!limit.Allow($"msg:{user.Id}", 10, TimeSpan.FromMinutes(1))) return Results.Text("You are posting too fast. Try again in a minute.", statusCode: 429);
            return await f.ReplyAsync(user, id, r.Body, ct) is { } m ? Results.Ok(m) : Results.NotFound();
        });

        v1.MapPost("/threads/{id}/messages/{mid}/react", async (HttpContext http, SessionTokens tokens, IForumSource f, string id, string mid, ReactRequest r, CancellationToken ct) =>
        {
            if (await User(http, tokens) is not { } user) return Results.Text("Sign in with Discord to react.", statusCode: 401);
            if (r.Emoji is not { Length: > 0 and <= 8 }) return Results.Text("Invalid emoji.", statusCode: 400);
            return await f.ReactAsync(user, id, mid, r.Emoji, ct) is { } m ? Results.Ok(m) : Results.NotFound();
        });

        v1.MapMethods("/threads/{id}/messages/{mid}", ["PATCH"], async (HttpContext http, SessionTokens tokens, Limiter limit, IForumSource f, string id, string mid, EditMessageRequest r, CancellationToken ct) =>
        {
            if (await User(http, tokens) is not { } user) return Results.Text("Sign in with Discord to edit.", statusCode: 401);
            if (ForumLimits.Validate(r.Body) is { } error) return Results.Text(error, statusCode: 400);
            if (!limit.Allow($"msg:{user.Id}", 10, TimeSpan.FromMinutes(1))) return Results.Text("You are posting too fast. Try again in a minute.", statusCode: 429);
            return await f.EditAsync(user, id, mid, r.Body, ct) is { } m ? Results.Ok(m) : Results.NotFound();
        });

        v1.MapDelete("/threads/{id}/messages/{mid}", async (HttpContext http, SessionTokens tokens, Limiter limit, IForumSource f, string id, string mid, CancellationToken ct) =>
        {
            if (await User(http, tokens) is not { } user) return Results.Text("Sign in with Discord to delete.", statusCode: 401);
            if (!limit.Allow($"msg:{user.Id}", 10, TimeSpan.FromMinutes(1))) return Results.Text("You are doing that too fast. Try again in a minute.", statusCode: 429);
            return await f.DeleteAsync(user, id, mid, ct) ? Results.NoContent() : Results.NotFound();
        });

        v1.MapPost("/threads/{id}/messages/{mid}/pin", async (HttpContext http, SessionTokens tokens, IForumSource f, string id, string mid, PinRequest r, CancellationToken ct) =>
            await User(http, tokens) is not { } user ? Results.Text("Sign in with Discord to pin.", statusCode: 401)
            : await f.PinAsync(user, id, mid, r.Pinned, ct) is { } m ? Results.Ok(m) : Results.NotFound());

        v1.MapPost("/threads/{id}/solve", async (HttpContext http, SessionTokens tokens, IForumSource f, string id, SolveRequest r, CancellationToken ct) =>
            await User(http, tokens) is not { } user ? Results.Text("Sign in with Discord to do that.", statusCode: 401)
            : await f.SolveAsync(user, id, r.Solved, ct) is { } t ? Results.Ok(t) : Results.NotFound());
    }

    /// <summary>The signed-in user from the bearer token, or null.</summary>
    private static async Task<Author?> User(HttpContext http, SessionTokens tokens) =>
        http.Request.Headers.Authorization.ToString() is { } h && h.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
            ? await tokens.ReadAsync(h[7..].Trim())
            : null;
}
