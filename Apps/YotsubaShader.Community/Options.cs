namespace YotsubaShader.Community.Server;

/// <summary>Configuration (environment variables as Community__Mode, Community__Discord__BotToken, ...).</summary>
public sealed class CommunityOptions
{
    /// <summary>"discord" talks to Discord. "dev" serves made-up data and a fake sign-in, with no Discord at all.</summary>
    public string Mode { get; set; } = "dev";

    /// <summary>The address users' browsers reach this server at (https). Discord redirects there after sign-in.</summary>
    public string PublicUrl { get; set; } = "http://127.0.0.1:5080";

    /// <summary>Signs session tokens. At least 32 characters, random, and secret. In dev mode one is made up at start.</summary>
    public string SessionSecret { get; set; } = "";

    /// <summary>Where the backend keeps its small state (who wrote each message). Needs to survive restarts.</summary>
    public string DataDir { get; set; } = "data";

    public DiscordOptions Discord { get; set; } = new();

    public bool IsDev => Mode.Equals("dev", StringComparison.OrdinalIgnoreCase);

    /// <summary>Returns what is wrong with the configuration, or null when it can run.</summary>
    public string? Validate()
    {
        if (!IsDev && !Mode.Equals("discord", StringComparison.OrdinalIgnoreCase)) return $"Community:Mode must be 'dev' or 'discord', not '{Mode}'.";
        if (!Uri.TryCreate(PublicUrl, UriKind.Absolute, out var url)) return "Community:PublicUrl must be an absolute URL.";
        if (IsDev) return null;
        if (url.Scheme != Uri.UriSchemeHttps && !url.IsLoopback) return "Community:PublicUrl must be https outside development.";
        if (SessionSecret.Length < 32) return "Community:SessionSecret must be at least 32 characters.";
        return Discord.Validate();
    }
}

public sealed class DiscordOptions
{
    public string BotToken { get; set; } = "";
    public ulong GuildId { get; set; }
    public ulong ForumChannelId { get; set; }
    public string ClientId { get; set; } = "";
    public string ClientSecret { get; set; } = "";
    /// <summary>Shown to people who sign in without being in the server.</summary>
    public string InviteUrl { get; set; } = "";
    public ulong[] StaffRoleIds { get; set; } = [];
    public ulong[] HelperRoleIds { get; set; } = [];

    public string? Validate()
    {
        if (BotToken.Length == 0) return "Community:Discord:BotToken is missing.";
        if (GuildId == 0) return "Community:Discord:GuildId is missing.";
        if (ForumChannelId == 0) return "Community:Discord:ForumChannelId is missing.";
        if (ClientId.Length == 0 || ClientSecret.Length == 0) return "Community:Discord:ClientId and ClientSecret are missing.";
        return null;
    }
}
