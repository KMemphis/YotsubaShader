namespace YotsubaShader.HlslSyntax;

[AttributeUsage( AttributeUsagesTable.InputOutput)]
[UserSemantic]
public class TANGENT([ConceptKey(UserSemanticAttribute.INDEX)] int index = 0) : YotsubaShaderConcept
{
    public int Index = index;
}