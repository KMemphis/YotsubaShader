#region MS-PL License - Copyright (c) 2026 Memphis Yomael
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
║  Copyright (c) 2026 Memphis Yomael                                                 ║
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