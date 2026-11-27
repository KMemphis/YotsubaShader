using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace YotsubaShader.Community.Server.Auth;

/// <summary>Signed session tokens the Hub sends back as a bearer token. They say who the user is; nothing else is trusted from the client.</summary>
public sealed class SessionTokens(string secret)
{
    private const string Issuer = "yotsuba-community";
    private static readonly TimeSpan Lifetime = TimeSpan.FromDays(7);

    private readonly SymmetricSecurityKey _key = new(Encoding.UTF8.GetBytes(secret));
    private readonly JsonWebTokenHandler _handler = new();

    public string Issue(Author user) => _handler.CreateToken(new SecurityTokenDescriptor
    {
        Issuer = Issuer,
        Audience = Issuer,
        Expires = DateTime.UtcNow + Lifetime,
        SigningCredentials = new SigningCredentials(_key, SecurityAlgorithms.HmacSha256),
        Claims = new Dictionary<string, object>
        {
            ["sub"] = user.Id, ["name"] = user.Name, ["role"] = user.Role, ["color"] = user.Color, ["avatar"] = user.AvatarUrl ?? "",
        },
    });

    public async Task<Author?> ReadAsync(string token)
    {
        var result = await _handler.ValidateTokenAsync(token, new TokenValidationParameters
        {
            ValidIssuer = Issuer, ValidAudience = Issuer, IssuerSigningKey = _key,
            ValidAlgorithms = [SecurityAlgorithms.HmacSha256], // never trust the algorithm the token names
            ClockSkew = TimeSpan.FromMinutes(1),
        });
        if (!result.IsValid) return null;
        string? C(string t) => result.ClaimsIdentity.FindFirst(t)?.Value;
        return C("sub") is { Length: > 0 } id && C("name") is { Length: > 0 } name
            ? new Author(id, name, C("role") ?? "Member", C("color") ?? "#0071e3", C("avatar") is { Length: > 0 } a ? a : null)
            : null;
    }
}
