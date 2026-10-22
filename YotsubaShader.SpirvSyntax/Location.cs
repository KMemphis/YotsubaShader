namespace YotsubaShader.SpirvSyntax;

[AttributeUsage( AttributeUsagesTable.InputOutput)]
[Concepts.Location]
public class Location([ConceptKey(Concepts.LocationAttribute.INDEX)] int index = 0) : YotsubaShaderConcept
{
    public int Index = index;
}