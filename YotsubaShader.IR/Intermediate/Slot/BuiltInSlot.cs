using YotsubaShader.IR.Intermediate.Slot.Types;

namespace YotsubaShader.IR.Intermediate.Slot;

public sealed record BuiltInSlot(BuiltIn BuiltIn) : InterfaceSlot;               // valor de sistema
