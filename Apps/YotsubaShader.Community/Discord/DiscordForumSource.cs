using Discord;
using Discord.Rest;
using Discord.Webhook;

namespace YotsubaShader.Community.Server.Discord;

/// <summary>
/// The forum read from, and posted to, a Discord forum channel. Reading uses the bot's REST access (no gateway, so no
/// connection to keep alive). Posts are made through a webhook so they appear under the author's own name and avatar,
/// with mentions disabled. Needs the Message Content intent enabled for the bot, or message text comes back empty.
/// </summary>
public sealed class DiscordForumSource(CommunityOptions options, OwnerStore owners, ILogger<DiscordForumSource> log) : IForumSource, IHostedService
{
    private const string WebhookName = "Yotsuba Hub";
    private static readonly TimeSpan ListTtl = TimeSpan.FromSeconds(20);
    private static readonly TimeSpan MemberTtl = TimeSpan.FromMinutes(10);
    private static readonly string[] Palette = ["#ff375f", "#30b0c7", "#bf5af2", "#ff9f0a", "#32d74b", "#5e5ce6", "#ff6482", "#64d2ff"];
    private static readonly string[] SolvedTags = ["solved", "resolved", "answered"];

    private readonly DiscordRestClient _rest = new();
    private readonly SemaphoreSlim _refresh = new(1, 1);
    private readonly Dictionary<ulong, CachedThread> _threads = [];
    private readonly Dictionary<ulong, (Author? Author, DateTime At)> _members = [];
    private DateTime _listedAt = DateTime.MinValue;
    private (int Members, int Online, DateTime At) _counts;
    private RestGuild _guild = null!;
    private RestForumChannel _forum = null!;
    private DiscordWebhookClient _hook = null!;

    private sealed record CachedThread(int MessageCount, ThreadSummary Summary);

    public async Task StartAsync(CancellationToken ct)
    {
        var d = options.Discord;
        await _rest.LoginAsync(TokenType.Bot, d.BotToken);
        _guild = await _rest.GetGuildAsync(d.GuildId, withCounts: true)
            ?? throw new InvalidOperationException("The bot is not in the configured guild.");
        _forum = await _guild.GetChannelAsync(d.ForumChannelId) as RestForumChannel
            ?? throw new InvalidOperationException("The configured channel is not a forum channel the bot can see.");

        var mine = (await _forum.GetWebhooksAsync()).FirstOrDefault(w => w.Name == WebhookName && w.Creator?.Id == _rest.CurrentUser.Id && w.Token is not null)
            ?? await _forum.CreateWebhookAsync(WebhookName);
        _hook = new DiscordWebhookClient(mine.Id, mine.Token);
        log.LogInformation("Connected to Discord guild {Guild}, forum {Forum}.", _guild.Name, _forum.Name);
    }

    public async Task StopAsync(CancellationToken ct)
    {
        _hook?.Dispose();
        await _rest.LogoutAsync();
        _rest.Dispose();
    }

    // ---- reading ----

    public async Task<ForumInfo> InfoAsync(CancellationToken ct = default)
    {
        if (DateTime.UtcNow - _counts.At > TimeSpan.FromMinutes(5))
        {
            var g = await _rest.GetGuildAsync(options.Discord.GuildId, withCounts: true);
            _counts = (g.ApproximateMemberCount ?? 0, g.ApproximatePresenceCount ?? 0, DateTime.UtcNow);
        }
        return new ForumInfo($"#{_forum.Name}", _guild.Name, _counts.Members, _counts.Online, Live: true, CanReact: false, "",
            _forum.Tags.Select(t => t.Name).Where(n => !SolvedTags.Contains(n.ToLowerInvariant())).ToList());
    }

    public async Task<IReadOnlyList<ThreadSummary>> ListAsync(string? tag, string? query, CancellationToken ct = default)
    {
        await RefreshAsync(ct);
        IEnumerable<ThreadSummary> all;
        lock (_threads) all = _threads.Values.Select(t => t.Summary).ToList();
        return all
            .Where(s => string.IsNullOrEmpty(tag) || s.Tag.Equals(tag, StringComparison.OrdinalIgnoreCase))
            // Only titles and first-message previews are known without fetching every thread, so that is what search covers.
            .Where(s => string.IsNullOrWhiteSpace(query) || s.Title.Contains(query, StringComparison.OrdinalIgnoreCase) || s.Preview.Contains(query, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(s => s.Pinned).ThenByDescending(s => s.LastActivity)
            .ToList();
    }

    public async Task<ThreadDetail?> GetAsync(string id, CancellationToken ct = default)
    {
        if (!ulong.TryParse(id, out var tid)) return null;
        await RefreshAsync(ct);
        CachedThread? cached;
        lock (_threads) _threads.TryGetValue(tid, out cached);
        if (cached is null) return null; // only threads of the forum channel are ever served

        var thread = await _rest.GetChannelAsync(tid) as RestThreadChannel;
        if (thread is null) return null;
        var raw = (await thread.GetMessagesAsync(100).FlattenAsync()).OrderBy(m => m.Timestamp).ToList();
        var messages = new List<Message>();
        foreach (var m in raw)
            if (await MapAsync(m) is { } mapped) messages.Add(mapped);
        var s = cached.Summary;
        return new ThreadDetail(s.Id, s.Title, s.Tag, s.Pinned, s.Solved, messages);
    }

    private async Task RefreshAsync(CancellationToken ct)
    {
        if (DateTime.UtcNow - _listedAt < ListTtl) return;
        await _refresh.WaitAsync(ct);
        try
        {
            if (DateTime.UtcNow - _listedAt < ListTtl) return;
            // Active threads of this forum channel first, then the most recent archived ones.
            var channels = new List<IThreadChannel>();
            channels.AddRange(await ((IThreadContainerChannel)_forum).GetActiveThreadsAsync());
            channels.AddRange((await _forum.GetPublicArchivedThreadsAsync(limit: 50)).Where(t => channels.All(c => c.Id != t.Id)));

            var next = new Dictionary<ulong, CachedThread>();
            foreach (var t in channels.Take(100))
            {
                CachedThread? old;
                lock (_threads) _threads.TryGetValue(t.Id, out old);
                if (old is not null && old.MessageCount == t.MessageCount) { next[t.Id] = old; continue; }
                if (await SummarizeAsync(t) is { } summary) next[t.Id] = new CachedThread(t.MessageCount, summary);
            }
            lock (_threads) { _threads.Clear(); foreach (var (k, v) in next) _threads[k] = v; }
            _listedAt = DateTime.UtcNow;
        }
        finally { _refresh.Release(); }
    }

    private async Task<ThreadSummary?> SummarizeAsync(IThreadChannel t)
    {
        // In a forum post the first message has the same id as the thread.
        var first = await t.GetMessageAsync(t.Id);
        if (first is null) return null;
        var last = (await t.GetMessagesAsync(1).FlattenAsync()).FirstOrDefault() ?? first;
        var names = t.AppliedTags.SelectMany(id => _forum.Tags.Where(x => x.Id == id).Select(x => x.Name)).ToList();
        var solved = names.Any(n => SolvedTags.Contains(n.ToLowerInvariant()));
        var tag = names.FirstOrDefault(n => !SolvedTags.Contains(n.ToLowerInvariant())) ?? "General";
        var author = await MapAsync(first);
        if (author is null) return null;
        return new ThreadSummary(t.Id.ToString(), t.Name, tag, t.Flags.HasFlag(ChannelFlags.Pinned), solved,
            author.Author, t.MessageCount, last.Timestamp, Preview.Of(author.Body));
    }

    private async Task<Message?> MapAsync(IMessage m)
    {
        if (m.Type is not (MessageType.Default or MessageType.Reply)) return null; // joins, pins and other system notices
        var body = m.CleanContent;
        foreach (var a in m.Attachments) body += (body.Length > 0 ? "\n" : "") + $"[attachment: {a.Filename}]";
        if (string.IsNullOrWhiteSpace(body)) return null;

        // Webhook posts (from the Hub) carry the author's name; real members get their role from the server.
        var author = m.Author.IsWebhook
            ? (owners.OwnerOf(m.Id) is { } ownerId && ulong.TryParse(ownerId, out var oid)
                ? await MemberAsync(oid, m.Author.Username, m.Author.GetAvatarUrl(size: 64))
                : null) ?? new Author($"hook:{m.Author.Username}", m.Author.Username, "Member", ColorFor(m.Author.Id), m.Author.GetAvatarUrl(size: 64))
            : await MemberAsync(m.Author.Id, m.Author.GlobalName ?? m.Author.Username, m.Author.GetAvatarUrl(size: 64)) ?? new Author(m.Author.Id.ToString(), m.Author.GlobalName ?? m.Author.Username, "Member", ColorFor(m.Author.Id), m.Author.GetAvatarUrl(size: 64));

        // Custom emoji have no text form to show, and who reacted is not known: counts only.
        var reactions = m.Reactions.Where(r => r.Key is Emoji).Select(r => new Reaction(r.Key.Name, r.Value.ReactionCount, false)).ToList();
        var threadId = m.Channel.Id;
        return new Message(m.Id.ToString(), author, body, m.Timestamp, reactions, m.EditedTimestamp is not null, m.IsPinned,
            $"https://discord.com/channels/{options.Discord.GuildId}/{threadId}/{m.Id}");
    }

    /// <summary>The member's forum identity (name, role, color), or null when they are not in the server.</summary>
    public async Task<Author?> MemberAsync(ulong userId, string name, string? avatarUrl)
    {
        lock (_members)
            if (_members.TryGetValue(userId, out var hit) && DateTime.UtcNow - hit.At < MemberTtl) return hit.Author is null ? null : hit.Author with { Name = name, AvatarUrl = avatarUrl };

        var member = await _guild.GetUserAsync(userId);
        Author? author = null;
        if (member is not null)
        {
            var role = member.RoleIds.Any(r => options.Discord.StaffRoleIds.Contains(r)) ? "Maintainer"
                : member.RoleIds.Any(r => options.Discord.HelperRoleIds.Contains(r)) ? "Helper" : "Member";
            author = new Author(userId.ToString(), name, role, ColorFor(userId), avatarUrl);
        }
        lock (_members) _members[userId] = (author, DateTime.UtcNow);
        return author;
    }

    // ---- writing ----

    public async Task<ThreadDetail> CreateAsync(Author user, string title, string tag, string body, CancellationToken ct = default)
    {
        ulong[]? tags = _forum.Tags.FirstOrDefault(t => t.Name.Equals(tag, StringComparison.OrdinalIgnoreCase)) is { } t ? [t.Id] : null;
        try
        {
            var id = await _hook.SendMessageAsync(body, username: SafeName(user.Name), avatarUrl: user.AvatarUrl,
                allowedMentions: AllowedMentions.None, threadName: title.Trim(), appliedTags: tags);
            owners.Set(id, user.Id);
            _listedAt = DateTime.MinValue; // list again so the new post shows up
            return new ThreadDetail(id.ToString(), title.Trim(), tag, false, false,
                [new Message(id.ToString(), user, body.Trim(), DateTimeOffset.UtcNow, [])]);
        }
        catch (Exception e) when (e is global::Discord.Net.HttpException or InvalidOperationException) { throw Wrap(e); }
    }

    public async Task<Message?> ReplyAsync(Author user, string threadId, string body, CancellationToken ct = default)
    {
        if (!ulong.TryParse(threadId, out var tid)) return null;
        lock (_threads) if (!_threads.ContainsKey(tid)) return null; // never post anywhere but this forum's threads
        try
        {
            var id = await _hook.SendMessageAsync(body, username: SafeName(user.Name), avatarUrl: user.AvatarUrl,
                allowedMentions: AllowedMentions.None, threadId: tid);
            owners.Set(id, user.Id);
            lock (_threads) _threads.Remove(tid); // makes the next list refetch this thread
            return new Message(id.ToString(), user, body.Trim(), DateTimeOffset.UtcNow, []);
        }
        catch (Exception e) when (e is global::Discord.Net.HttpException) { throw Wrap(e); }
    }

    public Task<Message?> ReactAsync(Author user, string threadId, string messageId, string emoji, CancellationToken ct = default) =>
        throw new NotSupportedException("Reactions can only be added in Discord itself.");

    // ---- editing ----

    private static bool IsStaff(Author u) => u.Role == "Maintainer";

    private async Task<(RestThreadChannel Thread, IMessage Message)?> FindAsync(string threadId, string messageId)
    {
        if (!ulong.TryParse(threadId, out var tid) || !ulong.TryParse(messageId, out var mid)) return null;
        lock (_threads) if (!_threads.ContainsKey(tid)) return null; // only this forum's threads
        if (await _rest.GetChannelAsync(tid) is not RestThreadChannel thread) return null;
        return await thread.GetMessageAsync(mid) is { } m ? (thread, m) : null;
    }

    private void Stale(ulong threadId) { lock (_threads) _threads.Remove(threadId); _listedAt = DateTime.MinValue; }

    public async Task<Message?> EditAsync(Author user, string threadId, string messageId, string body, CancellationToken ct = default)
    {
        if (await FindAsync(threadId, messageId) is not var (thread, msg)) return null;
        if (owners.OwnerOf(msg.Id) != user.Id) throw new UnauthorizedAccessException("You can only edit your own messages.");
        try { await _hook.ModifyMessageAsync(msg.Id, p => { p.Content = body.Trim(); p.AllowedMentions = AllowedMentions.None; }, threadId: thread.Id); }
        catch (global::Discord.Net.HttpException e) { throw Wrap(e); }
        Stale(thread.Id);
        return await MapAsync(await thread.GetMessageAsync(msg.Id));
    }

    public async Task<bool> DeleteAsync(Author user, string threadId, string messageId, CancellationToken ct = default)
    {
        if (await FindAsync(threadId, messageId) is not var (thread, msg)) return false;
        var mine = owners.OwnerOf(msg.Id) == user.Id;
        if (!mine && !IsStaff(user)) throw new UnauthorizedAccessException("You can only delete your own messages.");
        try
        {
            if (msg.Id == thread.Id) await thread.DeleteAsync();            // the first message is the post itself
            else if (msg.Author.IsWebhook && owners.OwnerOf(msg.Id) is not null) await _hook.DeleteMessageAsync(msg.Id, threadId: thread.Id);
            else await msg.DeleteAsync();
        }
        catch (global::Discord.Net.HttpException e) { throw Wrap(e); }
        owners.Remove(msg.Id);
        Stale(thread.Id);
        return true;
    }

    public async Task<Message?> PinAsync(Author user, string threadId, string messageId, bool pinned, CancellationToken ct = default)
    {
        if (!IsStaff(user)) throw new UnauthorizedAccessException("Only maintainers can pin messages.");
        if (await FindAsync(threadId, messageId) is not var (thread, msg)) return null;
        try
        {
            if (msg is not IUserMessage um) return null;
            if (pinned) await um.PinAsync(); else await um.UnpinAsync();
        }
        catch (global::Discord.Net.HttpException e) { throw Wrap(e); }
        return await MapAsync(await thread.GetMessageAsync(msg.Id));
    }

    public async Task<ThreadDetail?> SolveAsync(Author user, string threadId, bool solved, CancellationToken ct = default)
    {
        if (!ulong.TryParse(threadId, out var tid)) return null;
        lock (_threads) if (!_threads.ContainsKey(tid)) return null;
        if (await _rest.GetChannelAsync(tid) is not RestThreadChannel thread) return null;
        if (owners.OwnerOf(thread.Id) != user.Id && !IsStaff(user)) throw new UnauthorizedAccessException("Only the author or a maintainer can mark a post solved.");
        var solvedIds = _forum.Tags.Where(t => SolvedTags.Contains(t.Name.ToLowerInvariant())).Select(t => t.Id).ToList();
        if (solvedIds.Count == 0) throw new InvalidOperationException("Create a tag named Solved in the forum channel first.");
        var next = thread.AppliedTags.Where(id => !solvedIds.Contains(id)).ToList();
        if (solved) next.Add(solvedIds[0]);
        try { await thread.ModifyAsync(p => p.AppliedTags = next); }
        catch (global::Discord.Net.HttpException e) { throw Wrap(e); }
        Stale(thread.Id);
        return await GetAsync(threadId, ct);
    }

    // ---- helpers ----

    /// <summary>Discord rejects webhook names containing "discord" or "clyde", and names over 80 characters.</summary>
    private static string SafeName(string name)
    {
        var clean = System.Text.RegularExpressions.Regex.Replace(name, "discord|clyde", m => string.Concat(m.Value.Select((c, i) => i == 1 ? '_' : c)), System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        return clean.Length is >= 2 and <= 80 ? clean : "Yotsuba user";
    }

    private static string ColorFor(ulong id) => Palette[(int)(id % (ulong)Palette.Length)];

    private static Exception Wrap(Exception e) => e is global::Discord.Net.HttpException h && h.DiscordCode is DiscordErrorCode.CannotExecuteOnArchivedThread or DiscordErrorCode.ThreadIsLocked
        ? new InvalidOperationException("This thread is archived or locked.")
        : e is global::Discord.Net.HttpException { DiscordCode: DiscordErrorCode.MissingPermissions or DiscordErrorCode.InsufficientPermissions }
        ? new InvalidOperationException("The bot is missing a permission for that. Re-invite it with the updated link.")
        : new InvalidOperationException("Discord refused the post. Try again in a moment.");
}
