using YotsubaShader.IR.Intermediate.Slot.Types;

namespace YotsubaShader.IR.Intermediate.Slot;

public sealed record VertexSlot(VertexSemantic Semantic) : InterfaceSlot;        // el backend la resuelve a una Location