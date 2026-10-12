using YotsubaShader.IR.Intermediate.Slot;

namespace YotsubaShader.IR.Intermediate.Types;

public record Bool(string HumanName, StorageKind Kind, InterfaceSlot? Slot) : Storage(HumanName, Kind, Slot)
{
    public bool? InitialValue;
}