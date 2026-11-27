using System.Net;
using YotsubaShader.Community.Server.Auth;

namespace YotsubaShader.Community.Server;

/// <summary>
/// Sign-in for the Hub, which runs on the user's computer and cannot receive a Discord redirect itself:
///   1. The Hub opens /auth/start?login=ID in the browser and waits, polling /auth/poll.
///   2. The page shows a code (derived from ID) that must match the one shown in the Hub, then goes on to Discord.
///   3. Discord redirects to /auth/callback; the session is stored under ID and handed to the poll once.
/// </summary>
public static class AuthEndpoints
{
    public static void Map(WebApplication app)
    {
        var auth = app.MapGroup("/auth");

        auth.MapGet("/start", (HttpContext http, Limiter limit, LoginStore logins, string? login) =>
        {
            if (!limit.Allow($"auth:{http.Connection.RemoteIpAddress}", 30, TimeSpan.FromMinutes(10))) return Page("Too many attempts", "Wait a few minutes and try again.", 429);
            if (!LoginStore.IsValidId(login)) return Page("Link not valid", "Start the sign-in again from the Yotsuba Shader app.", 400);
            // Refused when the id was already used or finished (never restartable), or when too many logins are pending.
            if (!logins.Start(login!)) return Page("Link not valid", "This link was already used. Start the sign-in again from the Yotsuba Shader app.", 400);
            var code = LoginCode.From(login!);
            return Page("Sign in to Yotsuba Shader",
                $"Check that this code is the one shown in the Yotsuba Shader app. If it is not, close this page.<div class=\"code\">{code}</div>" +
                $"<a class=\"btn\" href=\"/auth/continue?login={WebUtility.HtmlEncode(login)}\">Continue with Discord</a>");
        });

        auth.MapGet("/continue", (IIdentityProvider idp, LoginStore logins, string? login) =>
            LoginStore.IsValidId(login) && logins.IsStarted(login!)
                ? Results.Redirect(idp.AuthorizeUrl(login!).ToString())
                : Page("Link not valid", "Start the sign-in again from the Yotsuba Shader app.", 400));

        auth.MapGet("/callback", async (HttpContext http, IIdentityProvider idp, SessionTokens tokens, LoginStore logins, string? code, string? state, string? error, CancellationToken ct) =>
        {
            if (!LoginStore.IsValidId(state) || !logins.IsStarted(state!)) return Page("Link not valid", "Start the sign-in again from the Yotsuba Shader app.", 400);
            if (error is not null || string.IsNullOrEmpty(code)) return Page("Sign-in cancelled", "You can close this page and try again from the app.", 400);
            try
            {
                var user = await idp.CompleteAsync(code, ct);
                logins.Complete(state!, tokens.Issue(user));
                return Page("You are signed in", $"Welcome, {WebUtility.HtmlEncode(user.Name)}. You can close this page and go back to the Yotsuba Shader app.");
            }
            catch (SignInException e) { return Page("Could not sign you in", WebUtility.HtmlEncode(e.Message), 403); }
        });

        // Polled by the Hub. 202 = not finished, 200 = here is the token (once), 404 = taken or expired.
        auth.MapGet("/poll", (LoginStore logins, string? login) =>
        {
            if (!LoginStore.IsValidId(login)) return Results.NotFound();
            var (pending, token) = logins.Take(login!);
            return token is not null ? Results.Ok(new { token }) : pending ? Results.StatusCode(202) : Results.NotFound();
        });
    }

    /// <param name="body">Trusted markup: every caller encodes what it interpolates.</param>
    private static IResult Page(string title, string body, int status = 200) => Results.Content($$"""
        <!doctype html><html lang="en"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
        <title>{{WebUtility.HtmlEncode(title)}}</title><style>
        :root{color-scheme:light dark;font-family:-apple-system,BlinkMacSystemFont,'Segoe UI',system-ui,sans-serif}
        body{margin:0;min-height:100vh;display:grid;place-items:center;background:Canvas;color:CanvasText}
        main{max-width:420px;padding:32px;text-align:center}h1{font-size:26px;letter-spacing:-.015em;margin:0 0 12px}
        p,main>div{color:GrayText;line-height:1.5}.code{font:600 34px ui-monospace,Menlo,monospace;letter-spacing:.08em;color:CanvasText;margin:22px 0}
        .btn{display:inline-block;padding:11px 26px;border-radius:99px;background:#0071e3;color:#fff;text-decoration:none}
        </style></head><body><main><h1>{{WebUtility.HtmlEncode(title)}}</h1><div>{{body}}</div></main></body></html>
        """, "text/html; charset=utf-8", statusCode: status);
}
