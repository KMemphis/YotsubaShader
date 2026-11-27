using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using YotsubaShader.Community;

namespace YotsubaShader.Hub.Forum;

/// <summary>The forum as served by the community backend (which is the one talking to Discord).</summary>
internal sealed class RemoteForumSource : IForumSource
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _http;
    private readonly Func<string?> _token;

    public RemoteForumSource(Uri baseUri, Func<string?> token)
    {
        _token = token;
        _http = new HttpClient { BaseAddress = baseUri, Timeout = TimeSpan.FromSeconds(15) };
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("YotsubaShaderHub");
    }

    public Task<ForumInfo> InfoAsync(CancellationToken ct = default) => Get<ForumInfo>("v1/info", ct);

    public Task<IReadOnlyList<ThreadSummary>> ListAsync(string? tag, string? query, CancellationToken ct = default) =>
        Get<IReadOnlyList<ThreadSummary>>($"v1/threads?tag={Uri.EscapeDataString(tag ?? "")}&q={Uri.EscapeDataString(query ?? "")}", ct);

    public async Task<ThreadDetail?> GetAsync(string id, CancellationToken ct = default)
    {
        try { return await Get<ThreadDetail>($"v1/threads/{Uri.EscapeDataString(id)}", ct); }
        catch (ForumException e) when (e.Status == 404) { return null; }
    }

    public Task<ThreadDetail> CreateAsync(Author user, string title, string tag, string body, CancellationToken ct = default) =>
        Post<ThreadDetail>("v1/threads", new NewThreadRequest(title, tag, body), ct)!;

    public async Task<Message?> ReplyAsync(Author user, string threadId, string body, CancellationToken ct = default)
    {
        try { return await Post<Message>($"v1/threads/{Uri.EscapeDataString(threadId)}/messages", new NewMessageRequest(body), ct); }
        catch (ForumException e) when (e.Status == 404) { return null; }
    }

    public async Task<Message?> ReactAsync(Author user, string threadId, string messageId, string emoji, CancellationToken ct = default)
    {
        try { return await Post<Message>($"v1/threads/{Uri.EscapeDataString(threadId)}/messages/{Uri.EscapeDataString(messageId)}/react", new ReactRequest(emoji), ct); }
        catch (ForumException e) when (e.Status == 404) { return null; }
    }

    public async Task<Message?> EditAsync(Author user, string threadId, string messageId, string body, CancellationToken ct = default)
    {
        try { return await Send<Message>(new HttpRequestMessage(HttpMethod.Patch, MessagePath(threadId, messageId)) { Content = JsonContent.Create(new EditMessageRequest(body), options: Json) }, ct); }
        catch (ForumException e) when (e.Status == 404) { return null; }
    }

    public async Task<bool> DeleteAsync(Author user, string threadId, string messageId, CancellationToken ct = default)
    {
        try { await Send<System.Text.Json.JsonElement?>(new HttpRequestMessage(HttpMethod.Delete, MessagePath(threadId, messageId)), ct); return true; }
        catch (ForumException e) when (e.Status == 404) { return false; }
    }

    public async Task<Message?> PinAsync(Author user, string threadId, string messageId, bool pinned, CancellationToken ct = default)
    {
        try { return await Post<Message>(MessagePath(threadId, messageId) + "/pin", new PinRequest(pinned), ct); }
        catch (ForumException e) when (e.Status == 404) { return null; }
    }

    public async Task<ThreadDetail?> SolveAsync(Author user, string threadId, bool solved, CancellationToken ct = default)
    {
        try { return await Post<ThreadDetail>($"v1/threads/{Uri.EscapeDataString(threadId)}/solve", new SolveRequest(solved), ct); }
        catch (ForumException e) when (e.Status == 404) { return null; }
    }

    private static string MessagePath(string threadId, string messageId) => $"v1/threads/{Uri.EscapeDataString(threadId)}/messages/{Uri.EscapeDataString(messageId)}";

    public Task<Author> MeAsync(CancellationToken ct = default) => Get<Author>("v1/me", ct);

    private Task<T> Get<T>(string path, CancellationToken ct) => Send<T>(new HttpRequestMessage(HttpMethod.Get, path), ct);

    private Task<T> Post<T>(string path, object body, CancellationToken ct) =>
        Send<T>(new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(body, options: Json) }, ct);

    private async Task<T> Send<T>(HttpRequestMessage req, CancellationToken ct)
    {
        if (_token() is { Length: > 0 } token) req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        HttpResponseMessage res;
        try { res = await _http.SendAsync(req, ct); }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException)
        {
            throw new ForumException("Could not reach the community server. Check your connection and try again.");
        }

        using (res)
        {
            if (res.IsSuccessStatusCode)
            {
                if (res.StatusCode == HttpStatusCode.NoContent) return default!;
                try { return (await res.Content.ReadFromJsonAsync<T>(Json, ct))!; }
                catch (JsonException) { throw new ForumException("The community server sent a response the Hub does not understand."); }
            }

            // The backend answers with a short plain-text reason the user can read. Anything longer is not one of ours.
            var text = await res.Content.ReadAsStringAsync(ct);
            var message = text.Length is > 0 and <= 300 ? text : $"The community server answered {(int)res.StatusCode}.";
            throw new ForumException(message, res.StatusCode == HttpStatusCode.Unauthorized ? 401 : (int)res.StatusCode is >= 400 and < 500 ? (int)res.StatusCode : 502);
        }
    }
}
