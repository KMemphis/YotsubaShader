using System.Net;
using System.Security.Cryptography;
using YotsubaShader.Community;
using YotsubaShader.Community.Server;
using YotsubaShader.Community.Server.Auth;
using YotsubaShader.Community.Server.Discord;

var builder = WebApplication.CreateBuilder(args);
var options = builder.Configuration.GetSection("Community").Get<CommunityOptions>() ?? new CommunityOptions();
if (options.IsDev && options.SessionSecret.Length < 32) options.SessionSecret = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
if (options.Validate() is { } problem)
{
    Console.Error.WriteLine($"Configuration error: {problem}");
    return 2;
}

builder.Services.AddSingleton(options);
builder.Services.AddSingleton(new SessionTokens(options.SessionSecret));
builder.Services.AddSingleton<LoginStore>();
builder.Services.AddSingleton<Limiter>();
builder.Services.AddHttpClient("discord", c => { c.Timeout = TimeSpan.FromSeconds(15); c.DefaultRequestHeaders.UserAgent.ParseAdd("YotsubaShaderCommunity"); });
if (options.IsDev)
{
    builder.Services.AddSingleton<IForumSource>(new InMemoryForum(preview: false));
    builder.Services.AddSingleton<IIdentityProvider, DevIdentityProvider>();
}
else
{
    builder.Services.AddSingleton(new OwnerStore(options.DataDir));
    builder.Services.AddSingleton<DiscordForumSource>();
    builder.Services.AddHostedService(sp => sp.GetRequiredService<DiscordForumSource>());
    builder.Services.AddSingleton<IForumSource>(sp => sp.GetRequiredService<DiscordForumSource>());
    builder.Services.AddSingleton<IIdentityProvider, DiscordIdentityProvider>();
}

var app = builder.Build();
app.UseForwardedHeaders(new() { ForwardedHeaders = Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedFor });
app.Use(async (ctx, next) =>
{
    ctx.Response.Headers["X-Content-Type-Options"] = "nosniff";
    ctx.Response.Headers["Referrer-Policy"] = "no-referrer";
    ctx.Response.Headers["Cache-Control"] = "no-store";
    await next();
});

ForumEndpoints.Map(app);
AuthEndpoints.Map(app);
app.MapGet("/healthz", () => "ok");

if (options.IsDev) app.Logger.LogWarning("DEV MODE: made-up data and a fake sign-in. Never expose this to the internet.");
await app.RunAsync();
return 0;

namespace YotsubaShader.Community.Server
{
    /// <summary>A small in-memory fixed-window limiter; one instance serves the whole process.</summary>
    public sealed class Limiter
    {
        private readonly Dictionary<string, (DateTime Start, int Count)> _windows = [];

        public bool Allow(string key, int max, TimeSpan window)
        {
            lock (_windows)
            {
                var now = DateTime.UtcNow;
                if (_windows.Count > 20_000) foreach (var k in _windows.Where(w => now - w.Value.Start > TimeSpan.FromHours(1)).Select(w => w.Key).ToList()) _windows.Remove(k);
                var (start, count) = _windows.TryGetValue(key, out var cur) && now - cur.Start < window ? cur : (now, 0);
                if (count >= max) return false;
                _windows[key] = (start, count + 1);
                return true;
            }
        }
    }
}
