namespace YotsubaShader.Community;

/// <summary>
/// A forum the Hub can show. Implemented in memory (preview data), by the Hub against the community backend,
/// and by the backend against Discord. Writes carry the user they are made as.
/// </summary>
public interface IForumSource
{
    Task<ForumInfo> InfoAsync(CancellationToken ct = default);
    Task<IReadOnlyList<ThreadSummary>> ListAsync(string? tag, string? query, CancellationToken ct = default);
    Task<ThreadDetail?> GetAsync(string id, CancellationToken ct = default);
    Task<ThreadDetail> CreateAsync(Author user, string title, string tag, string body, CancellationToken ct = default);
    Task<Message?> ReplyAsync(Author user, string threadId, string body, CancellationToken ct = default);
    /// <summary>Toggles the reaction for the user. Returns null when the message does not exist; throws <see cref="NotSupportedException"/> when reactions are read-only.</summary>
    Task<Message?> ReactAsync(Author user, string threadId, string messageId, string emoji, CancellationToken ct = default);

    /// <summary>Edits a message. Only its author may. Null when it does not exist; throws <see cref="UnauthorizedAccessException"/> for anyone else.</summary>
    Task<Message?> EditAsync(Author user, string threadId, string messageId, string body, CancellationToken ct = default);
    /// <summary>Deletes a message (deleting a post's first message deletes the post). Its author or a Maintainer may. False when it does not exist.</summary>
    Task<bool> DeleteAsync(Author user, string threadId, string messageId, CancellationToken ct = default);
    /// <summary>Pins or unpins a message. Maintainers only.</summary>
    Task<Message?> PinAsync(Author user, string threadId, string messageId, bool pinned, CancellationToken ct = default);
    /// <summary>Marks the post solved or not. The post's author or a Maintainer may.</summary>
    Task<ThreadDetail?> SolveAsync(Author user, string threadId, bool solved, CancellationToken ct = default);
}
