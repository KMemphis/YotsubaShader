using YotsubaShader.IR.Intermediate.Slot;

namespace YotsubaShader.IR.Intermediate;

public abstract record class Storage(
    string HumanName,
    StorageKind Kind,
    InterfaceSlot? Slot
    );
