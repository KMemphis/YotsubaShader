namespace YotsubaShader.MslSyntax;

[AttributeUsage(AttributeUsagesTable.Output)]
[Location]
public class color([ConceptKey(LocationAttribute.INDEX)] int n = 0) : YotsubaShaderConcept
{
    public int N = n;
}