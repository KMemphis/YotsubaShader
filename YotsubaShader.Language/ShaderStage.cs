namespace YotsubaShader.Language;

[AttributeUsage(AttributeTargets.Method)]
public class ShaderStageAttribute(Stage Stage) : Attribute
{
}