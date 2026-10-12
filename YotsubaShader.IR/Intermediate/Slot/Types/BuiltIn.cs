namespace YotsubaShader.IR.Intermediate.Slot.Types;

public enum BuiltIn
{
    // Vertex
    Position, PointSize, VertexIndex, InstanceIndex,
    // Fragment
    FragCoord, FrontFacing, FragDepth,
    // Compute
    GlobalInvocationId, LocalInvocationId, LocalInvocationIndex, WorkgroupId, NumWorkgroups,
}
