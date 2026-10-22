namespace YotsubaShader.HlslSyntax;

[AttributeUsage( AttributeUsagesTable.InputOutput)]
[UserSemantic]
public class POSITION([ConceptKey(UserSemanticAttribute.INDEX)] int index = 0) : YotsubaShaderConcept
{
    public int Index = index;
}