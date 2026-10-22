namespace YotsubaShader.SpirvSyntax;

[AttributeUsage( AttributeUsagesTable.InputOutput)]
[YotsubaShader.Concepts.LocationAttribute]
public class Location([ConceptKey(YotsubaShader.Concepts.LocationAttribute.INDEX)] int index = 0) : YotsubaShaderConcept
{
    public int Index = index;
}