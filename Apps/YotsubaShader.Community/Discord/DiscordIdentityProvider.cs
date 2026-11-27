using System.Net.Http.Headers;
using System.Text.Json;

namespace YotsubaShader.Community.Server.Auth;

/// <summary>Discord OAuth2 (authorization code, scope "identify"). Only people who are in the server may post.</summary>
public sealed class DiscordIdentityProvider(CommunityOptions options, Discord.DiscordForumSource forum, IHttpClientFactory http) : IIdentityProvider
{
    private string RedirectUri => $"{options.PublicUrl.TrimEnd('/')}/auth/callback";

    public Uri AuthorizeUrl(string state) => new(
        "https://discord.com/oauth2/authorize?response_type=code&scope=identify&prompt=none" +
        $"&client_id={Uri.EscapeDataString(options.Discord.ClientId)}" +
        $"&redirect_uri={Uri.EscapeDataString(RedirectUri)}&state={Uri.EscapeDataString(state)}");

    public async Task<Author> CompleteAsync(string code, CancellationToken ct)
    {
        var client = http.CreateClient("discord");
        using var tokenRes = await client.PostAsync("https://discord.com/api/oauth2/token", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_id"] = options.Discord.ClientId,
            ["client_secret"] = options.Discord.ClientSecret,
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["redirect_uri"] = RedirectUri,
        }), ct);
        if (!tokenRes.IsSuccessStatusCode) throw new SignInException("Discord did not accept the sign-in. Start again from the app.");
        using var tokenDoc = JsonDocument.Parse(await tokenRes.Content.ReadAsStringAsync(ct));
        var access = tokenDoc.RootElement.GetProperty("access_token").GetString()!;

        using var req = new HttpRequestMessage(HttpMethod.Get, "https://discord.com/api/users/@me");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", access);
        using var meRes = await client.SendAsync(req, ct);
        if (!meRes.IsSuccessStatusCode) throw new SignInException("Could not read your Discord profile.");
        using var me = JsonDocument.Parse(await meRes.Content.ReadAsStringAsync(ct));
        var root = me.RootElement;
        var id = ulong.Parse(root.GetProperty("id").GetString()!);
        var name = root.TryGetProperty("global_name", out var g) && g.GetString() is { Length: > 0 } gn ? gn : root.GetProperty("username").GetString()!;
        var avatar = root.TryGetProperty("avatar", out var a) && a.GetString() is { Length: > 0 } hash
            ? $"https://cdn.discordapp.com/avatars/{id}/{hash}.png?size=64"
            : $"https://cdn.discordapp.com/embed/avatars/{(id >> 22) % 6}.png";

        return await forum.MemberAsync(id, name, avatar)
            ?? throw new SignInException(options.Discord.InviteUrl.Length > 0
                ? $"Join the Discord server first, then sign in again: {options.Discord.InviteUrl}"
                : "Join the Discord server first, then sign in again.");
    }
}
