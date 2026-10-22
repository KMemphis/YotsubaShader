namespace YotsubaShader.GlslSyntax;

[AttributeUsage(AttributeUsagesTable.InputOutput)]
[Location]
public class location([ConceptKey(LocationAttribute.INDEX)] int n) : YotsubaShaderConcept
{
    
}