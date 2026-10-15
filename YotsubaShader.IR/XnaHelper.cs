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

using System.Globalization;
using YotsubaShader.IR.Intermediate;
using YotsubaShader.IR.Intermediate.Slot;
using YotsubaShader.IR.Intermediate.Types;

namespace YotsubaShader.IR;

public static class XnaHelper
{
    #region XNA Vectors

    public static Vector XNA_CreateVector2(string humanName, StorageKind kind, InterfaceSlot slot, float x, float y)
    {
        return new Vector(humanName, kind, slot)
        {
            ComponentsType = VectorComponentsType.Float,
            ComponentsLength = 2,
            ComponentsDefaultValues = new List<string>(2)
            {
                x.ToString(CultureInfo.InvariantCulture), 
                y.ToString(CultureInfo.InvariantCulture)
            },
        };
    }
    public static Vector XNA_CreateVector3(string humanName, StorageKind kind, InterfaceSlot slot, float x, float y, float z)
    {
        return new Vector(humanName, kind, slot)
        {
            ComponentsType = VectorComponentsType.Float,
            ComponentsLength = 3,
            ComponentsDefaultValues = new List<string>(3)
            {
                x.ToString(CultureInfo.InvariantCulture), 
                y.ToString(CultureInfo.InvariantCulture),
                z.ToString(CultureInfo.InvariantCulture)
            },
        };
    }
    #endregion

    #region XNA Textures

    public static Texture XNA_CreateTexture2D(string humanName, InterfaceSlot slot)
    {
        return new Texture(humanName, StorageKind.Texture, slot)
        {
            Dimensions = TextureDimensions.Texture_2D,
            TextureComponentsType = TextureComponentTypes.Float,
            Depth = TextureDepth.No,
            Arrayed = false,
            Multisampled = false,
            Usage = TextureUsage.Sampled,
            Format = TextureFormat.Unknown
        };
    }

    #endregion
}