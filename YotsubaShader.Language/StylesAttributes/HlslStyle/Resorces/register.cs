namespace YotsubaShader.Language.StylesAttributes.HlslStyle.Resorces;

[AttributeUsage(AttributeUsagesTable.Resource)]
public class register : YotsubaShaderAttribute
{
    public register(RegisterPrefix prefix, int n)
    {
        if (!Enum.IsDefined(prefix))
            throw new ArgumentOutOfRangeException(nameof(prefix), prefix, "Unknown HLSL register prefix.");

        if (n < 0)
            throw new ArgumentOutOfRangeException(nameof(n), n, "An HLSL register index cannot be negative.");

        Prefix = prefix;
        N = n;
    }
    
    public register(string resource)
    {
        ArgumentNullException.ThrowIfNull(resource);

        if (resource.Length < 2)
            throw new ArgumentException($"Invalid HLSL register: '{resource}'.", nameof(resource));

        ReadOnlySpan<char> text = resource.AsSpan();
        ReadOnlySpan<char> prefixText = text[..1];

        if (!Enum.TryParse<RegisterPrefix>(prefixText, ignoreCase: false, out var prefix)
            || !Enum.IsDefined(prefix))
            throw new ArgumentException($"Unknown HLSL register prefix: '{prefixText.ToString()}'.", nameof(resource));

        ReadOnlySpan<char> numberText = text[1..];
        if (!int.TryParse(
                numberText,
                System.Globalization.NumberStyles.None,
                System.Globalization.CultureInfo.InvariantCulture,
                out var number))
            throw new ArgumentException($"Invalid HLSL register index: '{numberText.ToString()}'.", nameof(resource));

        Prefix = prefix;
        N = number;
    }
    
    public RegisterPrefix Prefix;
    public int N;
}

public enum RegisterPrefix
{
    b,
    t,
    s,
    u,
    c
}
