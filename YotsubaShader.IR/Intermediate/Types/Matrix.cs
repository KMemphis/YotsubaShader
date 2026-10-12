using YotsubaShader.IR.Intermediate.Slot;

namespace YotsubaShader.IR.Intermediate.Types;

public record class Matrix(string HumanName, StorageKind Kind, InterfaceSlot? Slot) : Storage(HumanName, Kind, Slot)
{
    public required MatrixComponentType ComponentsType;

    public required uint Rows;

    public required uint Columns;
}

public enum MatrixComponentType
{
    Float,
    Double
}