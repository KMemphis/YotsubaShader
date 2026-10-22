namespace YotsubaShader.MslSyntax;

[AttributeUsage(AttributeUsagesTable.Input)]
[Location]
public class attribute([ConceptKey(LocationAttribute.INDEX)] int n) : YotsubaShaderConcept
{
    public int N = n;
}