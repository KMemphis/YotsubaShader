#region MS-PL License - Copyright (c) 2026 Memphis Yomael Núñez Cruz - Dominican Republic
/*
╔════════════════════════════════════════════════════════════════════════════════════╗
║                                                                                    ║
║  ███╗   ███╗███████╗      ██████╗ ██╗                                              ║
║  ████╗ ████║██╔════╝      ██╔══██╗██║                                              ║
║  ██╔████╔██║███████╗█████╗██████╔╝██║                                              ║
║  ██║╚██╔╝██║╚════██║╚════╝██╔═══╝ ██║                                              ║
║  ██║ ╚═╝ ██║███████║      ██║     ███████╗                                         ║
║  ╚═╝     ╚═╝╚══════╝      ╚═╝     ╚══════╝                                         ║
║                                                                                    ║
╠════════════════════════════════════════════════════════════════════════════════════╣
║  Copyright (c) 2026 Memphis Yomael Núñez Cruz - Dominican Republic                 ║
╠════════════════════════════════════════════════════════════════════════════════════╣
║                                                                                    ║
║  Microsoft Public License (MS-PL)                                                  ║
║                                                                                    ║
║  This license governs use of the accompanying software. If you use the software,   ║
║  you accept this license. If you do not accept the license, do not use the         ║
║  software.                                                                         ║
║                                                                                    ║
║  1. Definitions                                                                    ║
║  The terms "reproduce," "reproduction," "derivative works," and "distribution"     ║
║  have the same meaning here as under U.S. copyright law.                           ║
║  A "contribution" is the original software, to which copyright notices are         ║
║  attached, and any additions or changes to the software.                           ║
║  A "contributor" is any person that distributes its contribution under this        ║
║  license.                                                                          ║
║  "Licensed patents" are a contributor's patent claims that read directly on its    ║
║  contribution.                                                                     ║
║                                                                                    ║
║  2. Grant of Rights                                                                ║
║  (A) Copyright Grant- Subject to the terms of this license, including the license  ║
║  conditions and limitations in section 3, each contributor grants you a non-       ║
║  exclusive, worldwide, royalty-free copyright license to reproduce its             ║
║  contribution, prepare derivative works of its contribution, and distribute its    ║
║  contribution or any derivative works that you create.                             ║
║  (B) Patent Grant- Subject to the terms of this license, including the license     ║
║  conditions and limitations in section 3, each contributor grants you a non-       ║
║  exclusive, worldwide, royalty-free license under its licensed patents to make,    ║
║  have made, use, sell, offer for sale, import, and/or otherwise dispose of its     ║
║  contribution in the software or derivative works of the contribution in the       ║
║  software.                                                                         ║
║                                                                                    ║
║  3. Conditions and Limitations                                                     ║
║  (A) No Trademark License- This license does not grant you rights to use any       ║
║  contributors' name, logo, or trademarks.                                          ║
║  (B) If you bring a patent claim against any contributor over patents that you     ║
║  claim are infringed by the software, your patent license from such contributor    ║
║  to the software ends automatically.                                               ║
║  (C) If you distribute any portion of the software, you must retain all copyright, ║
║  patent, trademark, and attribution notices that are present in the software.      ║
║  (D) If you distribute any portion of the software in source code form, you may do ║
║  so only under this license by including a complete copy of this license with      ║
║  your distribution. If you distribute any portion of the software in compiled or   ║
║  object code form, you may only do so under a license that complies with this      ║
║  license.                                                                          ║
║  (E) The software is licensed "as-is." You bear the risk of using it. The          ║
║  contributors give no express warranties, guarantees or conditions. You may have   ║
║  additional consumer rights under your local laws which this license cannot        ║
║  change. To the extent permitted under your local laws, the contributors exclude   ║
║  the implied warranties of merchantability, fitness for a particular purpose and   ║
║  non-infringement.                                                                 ║
║                                                                                    ║
╚════════════════════════════════════════════════════════════════════════════════════╝
*/
#endregion

using YotsubaShader.Concepts.BindingSlotsAndResources;

namespace YotsubaShader.HlslSyntax.Resources;

[AttributeUsage(AttributeUsagesTable.Resource)]
[BindingSlot, TextureSlot, SamplerSlot]
public class register : YotsubaShaderConcept
{
    public register([ConceptKey(BindingSlot.SET)] RegisterPrefix prefix, [ConceptKey(BindingSlot.BINDING)] int n)
    {

        if (!Enum.IsDefined(prefix))
            throw new ArgumentOutOfRangeException(nameof(prefix), prefix, "Unknown HLSL register prefix.");

        if (n < 0)
            throw new ArgumentOutOfRangeException(nameof(n), n, "An HLSL register index cannot be negative.");
        

        Prefix = prefix;
        N = n;
        Space = (int)prefix;
    }
    
    public register(RegisterPrefix prefix, [ConceptKey(BindingSlot.BINDING)] int n, [ConceptKey(BindingSlot.SET)] int space)
    {

        if (!Enum.IsDefined(prefix))
            throw new ArgumentOutOfRangeException(nameof(prefix), prefix, "Unknown HLSL register prefix.");

        if (n < 0)
            throw new ArgumentOutOfRangeException(nameof(n), n, "An HLSL register index cannot be negative.");

        Prefix = prefix;
        N = n;
        Space = space;
    }

    public register([ConceptKey(BindingSlot.RegisterString)] string resource)
    {
        ArgumentNullException.ThrowIfNull(resource);

        if (resource.Length < 2)
            throw new ArgumentException($"Invalid HLSL register: '{resource}'.", nameof(resource));

        var prefix = Parse(resource, out var binding);

        Prefix = prefix;
        N = binding;
        Space = (int)prefix;
    }

    public static RegisterPrefix Parse(string resource, out int binding)
    {
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
                out binding))
            throw new ArgumentException($"Invalid HLSL register index: '{numberText.ToString()}'.", nameof(resource));
        return prefix;
    }

    public RegisterPrefix Prefix;
    public int N;
    public int Space;
}

public enum RegisterPrefix : int
{
    b = 0,
    t = 1,
    s = 2,
    u = 3,
    c = 4
}
