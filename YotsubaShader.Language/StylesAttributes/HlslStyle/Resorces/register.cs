#region MIT License - Copyright (c) 2026 Memphis Yomael
/*
╔════════════════════════════════════════════════════════════════════════════════════╗
║                                                                                    ║
║  ███╗   ███╗██╗████████╗    ██╗     ██╗ ██████╗███████╗███╗   ██╗███████╗███████╗  ║
║  ████╗ ████║██║╚══██╔══╝    ██║     ██║██╔════╝██╔════╝████╗  ██║██╔════╝██╔════╝  ║
║  ██╔████╔██║██║   ██║       ██║     ██║██║     █████╗  ██╔██╗ ██║███████╗█████╗    ║
║  ██║╚██╔╝██║██║   ██║       ██║     ██║██║     ██╔══╝  ██║╚██╗██║╚════██║██╔══╝    ║
║  ██║ ╚═╝ ██║██║   ██║       ███████╗██║╚██████╗███████╗██║ ╚████║███████║███████╗  ║
║  ╚═╝     ╚═╝╚═╝   ╚═╝       ╚══════╝╚═╝ ╚═════╝╚══════╝╚═╝  ╚═══╝╚══════╝╚══════╝  ║
║                                                                                    ║
╠════════════════════════════════════════════════════════════════════════════════════╣
║  Copyright (c) 2026 Memphis Yomael                                                 ║
╠════════════════════════════════════════════════════════════════════════════════════╣
║                                                                                    ║
║  Permission is hereby granted, free of charge, to any person obtaining a copy      ║
║  of this software and associated documentation files (the "Software"), to deal     ║
║  in the Software without restriction, including without limitation the rights      ║
║  to use, copy, modify, merge, publish, distribute, sublicense, and/or sell         ║
║  copies of the Software, and to permit persons to whom the Software is             ║
║  furnished to do so, subject to the following conditions:                          ║
║                                                                                    ║
║  The above copyright notice and this permission notice shall be included           ║
║  in all copies or substantial portions of the Software.                            ║
║                                                                                    ║
║  THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR        ║
║  IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,          ║
║  FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE       ║
║  AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER            ║
║  LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,     ║
║  OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE     ║
║  SOFTWARE.                                                                         ║
║                                                                                    ║
╚════════════════════════════════════════════════════════════════════════════════════╝
*/
#endregion

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
