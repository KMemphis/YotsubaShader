namespace YotsubaShader.HlslSyntax;

[AttributeUsage(AttributeUsagesTable.Output)]
[Location]
public class SV_Target([ConceptKey(LocationAttribute.INDEX)] int n = 0) : YotsubaShaderConcept
{
    public int N = n;
}