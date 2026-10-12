namespace YotsubaShader.Compiler.Stages;

[AttributeUsage(AttributeTargets.Method)]
public class ShaderStageAttribute(ShaderStage stage) : Attribute
{
    public ShaderStage Stage = stage;
}