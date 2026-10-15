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

namespace YotsubaShader.Compiler.SpirV.YS_Types;

public enum SpirVStorageClass : uint
{
    // ============================================================
    // Core SPIR-V
    // ============================================================

    UniformConstant = 0,
    Input = 1,
    Uniform = 2,
    Output = 3,
    Workgroup = 4,
    CrossWorkgroup = 5,
    Private = 6,
    Function = 7,
    Generic = 8,
    PushConstant = 9,
    AtomicCounter = 10,
    Image = 11,
    StorageBuffer = 12,

    // ============================================================
    // EXT / QCOM / AMD extensions
    // ============================================================

    TileImageEXT = 4172,

    TileAttachmentQCOM = 4491,

    NodePayloadAMDX = 5068,

    // ============================================================
    // Ray Tracing
    // KHR values with NV aliases
    // ============================================================

    CallableDataKHR = 5328,
    CallableDataNV = CallableDataKHR,

    IncomingCallableDataKHR = 5329,
    IncomingCallableDataNV = IncomingCallableDataKHR,

    RayPayloadKHR = 5338,
    RayPayloadNV = RayPayloadKHR,

    HitAttributeKHR = 5339,
    HitAttributeNV = HitAttributeKHR,

    IncomingRayPayloadKHR = 5342,
    IncomingRayPayloadNV = IncomingRayPayloadKHR,

    ShaderRecordBufferKHR = 5343,
    ShaderRecordBufferNV = ShaderRecordBufferKHR,

    // ============================================================
    // Physical storage buffers
    // ============================================================

    PhysicalStorageBuffer = 5349,

    // Historical / extension alias
    PhysicalStorageBufferEXT = PhysicalStorageBuffer,

    // ============================================================
    // Shader Invocation Reorder
    // ============================================================

    HitObjectAttributeNV = 5385,

    // ============================================================
    // Mesh shaders
    // ============================================================

    TaskPayloadWorkgroupEXT = 5402,

    // ============================================================
    // Shader Invocation Reorder EXT
    // ============================================================

    HitObjectAttributeEXT = 5411,

    // ============================================================
    // Intel
    // ============================================================

    CodeSectionINTEL = 5605,

    // ============================================================
    // USM Storage Classes
    // ALTERA names are the current names.
    // INTEL names are aliases.
    // ============================================================

    DeviceOnlyALTERA = 5936,
    DeviceOnlyINTEL = DeviceOnlyALTERA,

    HostOnlyALTERA = 5937,
    HostOnlyINTEL = HostOnlyALTERA,
}