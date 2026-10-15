namespace YotsubaShader.Compiler.SpirV.Stages;

[AttributeUsage(AttributeTargets.Method)]
internal class ShaderStageAttribute(ShaderStage stage) : Attribute
{
    public ShaderStage Stage = stage;
}