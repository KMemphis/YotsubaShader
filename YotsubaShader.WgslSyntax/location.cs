namespace YotsubaShader.WgslSyntax;

[AttributeUsage(AttributeUsagesTable.InputOutput)]
[Location]
public class @location([ConceptKey(LocationAttribute.INDEX)] int n) : YotsubaShaderConcept
{
    public int N = n;
}