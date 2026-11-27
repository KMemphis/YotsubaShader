namespace YotsubaShader.Community;

// The JSON shapes shared by the Hub and the community backend (camelCase on the wire).

public sealed record Author(string Id, string Name, string Role, string Color, string? AvatarUrl = null);

public sealed record Reaction(string Emoji, int Count, bool Mine);

/// <param name="Url">Link to the message in Discord, when there is one.</param>
public sealed record Message(string Id, Author Author, string Body, DateTimeOffset At, IReadOnlyList<Reaction> Reactions,
    bool Edited = false, bool Pinned = false, string? Url = null);

public sealed record ThreadSummary(
    string Id, string Title, string Tag, bool Pinned, bool Solved,
    Author Author, int Replies, DateTimeOffset LastActivity, string Preview);

public sealed record ThreadDetail(string Id, string Title, string Tag, bool Pinned, bool Solved, IReadOnlyList<Message> Messages);

/// <param name="Live">False for made-up preview data.</param>
/// <param name="CanReact">Whether reactions can be added from the app. Discord only lets a user add their own reactions.</param>
public sealed record ForumInfo(
    string Channel, string Server, int Members, int Online, bool Live, bool CanReact, string Notice, IReadOnlyList<string> Tags);

public sealed record NewThreadRequest(string Title, string? Tag, string Body);
public sealed record NewMessageRequest(string Body);
public sealed record ReactRequest(string Emoji);
public sealed record EditMessageRequest(string Body);
public sealed record PinRequest(bool Pinned);
public sealed record SolveRequest(bool Solved);

/// <summary>Who a post is made as. The Hub's sign-in produces it; <see cref="ForumLimits"/> applies to everything they send.</summary>
public static class ForumLimits
{
    public const int MaxBody = 4000, MaxTitle = 100;

    public static string? Validate(string? body) =>
        string.IsNullOrWhiteSpace(body) ? "Message is empty."
        : body.Length > MaxBody ? $"Message is too long (max {MaxBody} characters)." : null;

    public static string? ValidateTitle(string? title) =>
        string.IsNullOrWhiteSpace(title) ? "Title is required."
        : title.Trim().Length > MaxTitle ? $"Title is too long (max {MaxTitle} characters)." : null;
}
