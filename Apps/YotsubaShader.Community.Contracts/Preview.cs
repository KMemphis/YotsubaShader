namespace YotsubaShader.Community;

/// <summary>The one-line text shown under a thread title: plain text, no code fences, no inline-code ticks.</summary>
public static class Preview
{
    public static string Of(string body)
    {
        var inFence = false;
        var lines = body.Split('\n').Where(l =>
        {
            if (l.TrimStart().StartsWith("```")) { inFence = !inFence; return false; }
            return !inFence;
        });
        var text = string.Join(' ', lines.Select(l => l.Trim().TrimStart('-', ' '))).Replace("`", "").Trim();
        return text.Length > 140 ? text[..140] + "…" : text;
    }
}
