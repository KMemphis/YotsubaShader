namespace YotsubaShader.Community.Server.Auth;

public sealed class SignInException(string message) : Exception(message);

/// <summary>Turns a finished browser sign-in into a forum identity. Discord in production, a stand-in in development.</summary>
public interface IIdentityProvider
{
    /// <summary>Where to send the browser to sign in. <paramref name="state"/> comes back unchanged on the callback.</summary>
    Uri AuthorizeUrl(string state);

    /// <exception cref="SignInException">With a message that is safe to show to the user.</exception>
    Task<Author> CompleteAsync(string code, CancellationToken ct);
}

public sealed class DevIdentityProvider(CommunityOptions options) : IIdentityProvider
{
    public Uri AuthorizeUrl(string state) => new($"{options.PublicUrl.TrimEnd('/')}/auth/callback?code=dev&state={Uri.EscapeDataString(state)}");

    public Task<Author> CompleteAsync(string code, CancellationToken ct) =>
        Task.FromResult(new Author("dev-1", "Dev User", "Member", "#0071e3"));
}
