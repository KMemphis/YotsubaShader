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

using YotsubaShader.IR.Intermediate.Slot;

namespace YotsubaShader.IR.Intermediate.Types;

public record Texture(string HumanName, StorageKind Kind, InterfaceSlot? Slot) : Storage(HumanName, Kind, Slot)
{
    
    public required TextureDimensions Dimensions;
    /// <summary>
    /// Tipo de componentes
    /// </summary>
    public required TextureComponentTypes TextureComponentsType;

    public required TextureDepth Depth;

    public required bool Arrayed;

    public required bool Multisampled;

    public required TextureUsage Usage;

    public required TextureFormat Format;
}

public enum TextureDimensions
{
    Texture_1D,
    Texture_2D,
    Texture_3D,
    Cube
}

public enum TextureComponentTypes
{
    Float,
    Int,
    Uint
}

public enum TextureUsage
{
    Unknown = 0,
    Sampled = 1,
    Storage = 2
}

public enum TextureFormat
{
    Unknown = 0,
    Rgba32f = 1,
    Rgba16f = 2,
    R32f = 3,
    Rgba8 = 4,
    Rgba8Snorm = 5,
    Rgba32i = 21,
    R32i = 24,
    Rgba32ui = 30,
    R32ui = 33
}

public enum TextureDepth
{
    No = 0,
    Yes = 1,
    Unknown = 2
}