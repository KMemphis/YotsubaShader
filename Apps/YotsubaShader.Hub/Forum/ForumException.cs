namespace YotsubaShader.Hub.Forum;

/// <summary>A failure the user can understand and act on; <see cref="Status"/> is what the Hub API answers with.</summary>
internal sealed class ForumException(string message, int status = 502) : Exception(message)
{
    public int Status { get; } = status;
}
