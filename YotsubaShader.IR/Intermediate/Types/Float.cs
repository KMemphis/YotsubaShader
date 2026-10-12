using YotsubaShader.IR.Intermediate.Slot;

namespace YotsubaShader.IR.Intermediate.Types;

public record class Float(string HumanName, StorageKind Kind, InterfaceSlot? Slot) : Storage(HumanName, Kind, Slot)
{
    public const uint FloatBits = 32;
    public float? InitialValue;

}