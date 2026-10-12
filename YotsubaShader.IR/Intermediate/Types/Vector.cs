using YotsubaShader.IR.Intermediate.Slot;

namespace YotsubaShader.IR.Intermediate.Types;

public record Vector(string HumanName, StorageKind Kind, InterfaceSlot? Slot) : Storage(HumanName, Kind, Slot)
{
    public required VectorComponentsType ComponentsType;
    public required uint ComponentsLength;
    public IReadOnlyList<string>? ComponentsDefaultValues;
}

public enum VectorComponentsType
{
    Float,
    Int,
    Uint
}