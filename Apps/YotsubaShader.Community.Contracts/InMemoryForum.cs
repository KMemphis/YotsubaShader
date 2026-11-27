namespace YotsubaShader.Community;

/// <summary>Made-up community data in memory, so the forum UI can be built and judged before Discord is connected.</summary>
public sealed class InMemoryForum : IForumSource
{
    public static readonly Author Me = new("me", "You", "Member", "#0071e3");
    private static readonly Author Sakura = new("sakura", "Sakura", "Maintainer", "#ff375f");
    private static readonly Author Kenji = new("kenji", "Kenji", "Member", "#30b0c7");
    private static readonly Author Lucia = new("lucia", "Lucía", "Member", "#bf5af2");
    private static readonly Author Mateo = new("mateo", "Mateo", "Member", "#ff9f0a");
    private static readonly Author Priya = new("priya", "Priya", "Helper", "#32d74b");
    private static readonly Author Aiko = new("aiko", "Aiko", "Member", "#5e5ce6");

    private static readonly string[] AllTags = ["Announcements", "Help", "Showcase", "Ideas", "Engines"];

    private sealed class StoredReaction { public required string Emoji { get; init; } public int Count { get; set; } public bool Mine { get; set; } }

    private sealed class StoredMessage
    {
        public required string Id { get; init; }
        public required Author Author { get; init; }
        public required string Body { get; set; }
        public required DateTimeOffset At { get; init; }
        public List<StoredReaction> Reactions { get; init; } = [];
        public bool Edited { get; set; }
        public bool Pinned { get; set; }
    }

    private sealed class StoredThread
    {
        public required string Id { get; init; }
        public required string Title { get; init; }
        public required string Tag { get; init; }
        public bool Pinned { get; init; }
        public bool Solved { get; set; }
        public List<StoredMessage> Messages { get; init; } = [];
    }

    private readonly object _lock = new();
    private readonly List<StoredThread> _threads;
    private readonly bool _preview;
    private int _next = 100;

    /// <param name="preview">True when this is shown as sample data (the Hub's offline mode), false when a backend serves it as its dev stand-in.</param>
    public InMemoryForum(bool preview = true) { _preview = preview; _threads = Seed(); }

    public Task<ForumInfo> InfoAsync(CancellationToken ct = default) => Task.FromResult(new ForumInfo(
        "#help-forum", "Yotsuba Shader", 1284, 312, Live: false, CanReact: true,
        _preview ? "Preview data. This forum is not connected to Discord yet." : "Development data. The backend is not connected to Discord.",
        AllTags));

    public Task<IReadOnlyList<ThreadSummary>> ListAsync(string? tag, string? query, CancellationToken ct = default)
    {
        lock (_lock)
            return Task.FromResult<IReadOnlyList<ThreadSummary>>(_threads
                .Where(t => string.IsNullOrEmpty(tag) || t.Tag.Equals(tag, StringComparison.OrdinalIgnoreCase))
                .Where(t => string.IsNullOrWhiteSpace(query)
                    || t.Title.Contains(query, StringComparison.OrdinalIgnoreCase)
                    || t.Messages.Any(m => m.Body.Contains(query, StringComparison.OrdinalIgnoreCase)))
                .Select(t => new ThreadSummary(t.Id, t.Title, t.Tag, t.Pinned, t.Solved, t.Messages[0].Author,
                    t.Messages.Count - 1, t.Messages[^1].At, Preview.Of(t.Messages[0].Body)))
                .OrderByDescending(s => s.Pinned).ThenByDescending(s => s.LastActivity)
                .ToList());
    }

    public Task<ThreadDetail?> GetAsync(string id, CancellationToken ct = default)
    {
        lock (_lock) return Task.FromResult(_threads.FirstOrDefault(t => t.Id == id) is { } t ? Snapshot(t) : null);
    }

    public Task<ThreadDetail> CreateAsync(Author user, string title, string tag, string body, CancellationToken ct = default)
    {
        lock (_lock)
        {
            var t = new StoredThread { Id = $"t{_next++}", Title = title.Trim(), Tag = tag };
            t.Messages.Add(New(user, body));
            _threads.Add(t);
            return Task.FromResult(Snapshot(t));
        }
    }

    public Task<Message?> ReplyAsync(Author user, string threadId, string body, CancellationToken ct = default)
    {
        lock (_lock)
        {
            var t = _threads.FirstOrDefault(x => x.Id == threadId);
            if (t is null) return Task.FromResult<Message?>(null);
            var m = New(user, body);
            t.Messages.Add(m);
            return Task.FromResult<Message?>(Map(m));
        }
    }

    public Task<Message?> ReactAsync(Author user, string threadId, string messageId, string emoji, CancellationToken ct = default)
    {
        lock (_lock)
        {
            var m = _threads.FirstOrDefault(t => t.Id == threadId)?.Messages.FirstOrDefault(x => x.Id == messageId);
            if (m is null) return Task.FromResult<Message?>(null);
            var r = m.Reactions.FirstOrDefault(x => x.Emoji == emoji);
            if (r is null) m.Reactions.Add(r = new StoredReaction { Emoji = emoji });
            r.Mine = !r.Mine;
            r.Count += r.Mine ? 1 : -1;
            if (r.Count <= 0) m.Reactions.Remove(r);
            return Task.FromResult<Message?>(Map(m));
        }
    }

    private static bool IsStaff(Author u) => u.Role == "Maintainer";

    public Task<Message?> EditAsync(Author user, string threadId, string messageId, string body, CancellationToken ct = default)
    {
        lock (_lock)
        {
            var m = _threads.FirstOrDefault(t => t.Id == threadId)?.Messages.FirstOrDefault(x => x.Id == messageId);
            if (m is null) return Task.FromResult<Message?>(null);
            if (m.Author.Id != user.Id) throw new UnauthorizedAccessException("You can only edit your own messages.");
            m.Body = body.Trim(); m.Edited = true;
            return Task.FromResult<Message?>(Map(m));
        }
    }

    public Task<bool> DeleteAsync(Author user, string threadId, string messageId, CancellationToken ct = default)
    {
        lock (_lock)
        {
            var t = _threads.FirstOrDefault(x => x.Id == threadId);
            var m = t?.Messages.FirstOrDefault(x => x.Id == messageId);
            if (t is null || m is null) return Task.FromResult(false);
            if (m.Author.Id != user.Id && !IsStaff(user)) throw new UnauthorizedAccessException("You can only delete your own messages.");
            if (t.Messages[0] == m) _threads.Remove(t); else t.Messages.Remove(m);
            return Task.FromResult(true);
        }
    }

    public Task<Message?> PinAsync(Author user, string threadId, string messageId, bool pinned, CancellationToken ct = default)
    {
        lock (_lock)
        {
            var m = _threads.FirstOrDefault(t => t.Id == threadId)?.Messages.FirstOrDefault(x => x.Id == messageId);
            if (m is null) return Task.FromResult<Message?>(null);
            if (!IsStaff(user)) throw new UnauthorizedAccessException("Only maintainers can pin messages.");
            m.Pinned = pinned;
            return Task.FromResult<Message?>(Map(m));
        }
    }

    public Task<ThreadDetail?> SolveAsync(Author user, string threadId, bool solved, CancellationToken ct = default)
    {
        lock (_lock)
        {
            var t = _threads.FirstOrDefault(x => x.Id == threadId);
            if (t is null) return Task.FromResult<ThreadDetail?>(null);
            if (t.Messages[0].Author.Id != user.Id && !IsStaff(user)) throw new UnauthorizedAccessException("Only the author or a maintainer can mark a post solved.");
            t.Solved = solved;
            return Task.FromResult<ThreadDetail?>(Snapshot(t));
        }
    }

    private StoredMessage New(Author user, string body) => new() { Id = $"m{_next++}", Author = user, Body = body.Trim(), At = DateTimeOffset.UtcNow };

    private static Message Map(StoredMessage m) =>
        new(m.Id, m.Author, m.Body, m.At, m.Reactions.Select(r => new Reaction(r.Emoji, r.Count, r.Mine)).ToList(), m.Edited, m.Pinned);

    private static ThreadDetail Snapshot(StoredThread t) =>
        new(t.Id, t.Title, t.Tag, t.Pinned, t.Solved, t.Messages.Select(Map).ToList());

    private static StoredMessage M(string id, Author a, int minutesAgo, string body, params (string Emoji, int Count)[] reactions) => new()
    {
        Id = id, Author = a, Body = body, At = DateTimeOffset.UtcNow.AddMinutes(-minutesAgo),
        Reactions = reactions.Select(r => new StoredReaction { Emoji = r.Emoji, Count = r.Count }).ToList(),
    };

    private static List<StoredThread> Seed() =>
    [
        new()
        {
            Id = "t1", Title = "Read this first: how to ask for help", Tag = "Announcements", Pinned = true,
            Messages =
            {
                M("m1", Sakura, 60 * 24 * 40, """
                    Welcome! A good question gets a fast answer. Please include:

                    - the output of `dotnet yotsuba --version`
                    - the engine plugin you target (MonoGame, KNI, FNA or Godot)
                    - the smallest shader that still shows the problem

                    ```csharp
                    [ShaderTargets(Target.Vulkan_13 | Target.DirectX_12)]
                    public class MyShader
                    {
                        [ShaderStage(Stage.Fragment)]
                        public Vector4 Fs(VSOutput input) => input.COLOR;
                    }
                    ```

                    Be kind, and mark the reply that solved your problem.
                    """, ("👍", 48), ("❤️", 21)),
            },
        },
        new()
        {
            Id = "t2", Title = "Early return in a fragment shader breaks on the Godot route", Tag = "Help", Solved = true,
            Messages =
            {
                M("m2", Mateo, 60 * 5, """
                    My fragment shader compiles for MonoGame but the Godot output is rejected. I think it is the early `return`:

                    ```csharp
                    [ShaderStage(Stage.Fragment)]
                    public Vector4 Fs(VSOutput input)
                    {
                        if (input.COLOR.W < 0.01f)
                            return new Vector4(0, 0, 0, 0);

                        return input.COLOR;
                    }
                    ```
                    """, ("👀", 3)),
                M("m3", Priya, 60 * 4 + 40, "Godot's shading language does not allow returning from `fragment()`, so a fragment with several return points cannot map to it. Keep a single return and write into a local."),
                M("m4", Sakura, 60 * 4 + 5, """
                    Right. Rewrite it with one exit:

                    ```csharp
                    [ShaderStage(Stage.Fragment)]
                    public Vector4 Fs(VSOutput input)
                    {
                        Vector4 color = input.COLOR;
                        if (color.W < 0.01f)
                            color = new Vector4(0, 0, 0, 0);

                        return color;
                    }
                    ```
                    """, ("✅", 6), ("🙏", 2)),
            },
        },
        new()
        {
            Id = "t3", Title = "How do I target only Vulkan and DirectX 12?", Tag = "Help", Solved = true,
            Messages =
            {
                M("m5", Lucia, 60 * 26, "The example uses a huge flag list. I only ship on Windows with D3D12 and a Vulkan fallback. Is there a smaller set?"),
                M("m6", Kenji, 60 * 25, """
                    `ShaderTargets` is a flags enum, so combine only what you need:

                    ```csharp
                    [ShaderTargets(Target.Vulkan_13 | Target.DirectX_12)]
                    public class SpriteShader { /* ... */ }
                    ```

                    Fewer targets also means a faster build.
                    """, ("👍", 9)),
            },
        },
        new()
        {
            Id = "t4", Title = "Plasma effect running in KNI WebGL", Tag = "Showcase",
            Messages =
            {
                M("m7", Aiko, 60 * 8, "Ported my plasma background to YotsubaShader and it runs in the browser through the KNI plugin. 60 fps on my laptop, 0 lines of GLSL written by hand. Source in the next message.", ("🔥", 34), ("🎉", 12), ("👀", 7)),
                M("m8", Kenji, 60 * 7, "That looks great. Did you need anything special for the time uniform?", ("👍", 2)),
                M("m9", Aiko, 60 * 6 + 30, "Just a constant buffer field that I update every frame. Nothing special."),
            },
        },
        new()
        {
            Id = "t5", Title = "MGFX output does not load on Android", Tag = "Engines",
            Messages =
            {
                M("m10", Mateo, 60 * 52, "The `.mgfx` file built by the MonoGame plugin loads on desktop but throws on Android. Same file, same project."),
                M("m11", Priya, 60 * 50, "Which GraphicsProfile? Android needs OpenGL ES, and an effect compiled for a DirectX profile will not load there. Check the targets of the shader first."),
            },
        },
        new()
        {
            Id = "t6", Title = "Hot reload for shaders in the Playground", Tag = "Ideas",
            Messages =
            {
                M("m12", Lucia, 60 * 72, "It would be great if the preview recompiled while typing, like a live shader editor. Even a 500 ms debounce would do.", ("👍", 17), ("🚀", 5)),
                M("m13", Sakura, 60 * 70, "We are looking at it. The compiler already runs in the browser, so the debounce part is easy; the expensive part is the preview surface.", ("❤️", 4)),
            },
        },
        new()
        {
            Id = "t7", Title = "Dissolve effect for 2D sprites (FNA)", Tag = "Showcase",
            Messages =
            {
                M("m14", Kenji, 60 * 100, """
                    A small dissolve driven by a noise texture and a threshold:

                    ```csharp
                    [ShaderStage(Stage.Fragment)]
                    public Vector4 Fs(VSOutput input)
                    {
                        Vector4 color = input.COLOR;
                        float noise = input.COLOR.X;
                        if (noise < Threshold)
                            color.W = 0;

                        return color;
                    }
                    ```
                    """, ("🔥", 11), ("👍", 8)),
            },
        },
    ];
}
