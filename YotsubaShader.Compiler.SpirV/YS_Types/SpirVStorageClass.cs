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