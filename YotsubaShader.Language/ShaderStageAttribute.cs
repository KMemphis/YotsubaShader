namespace YotsubaShader.Language;

[AttributeUsage(AttributeTargets.Method)]
public class ShaderStageAttribute(Stage stage) : YotsubaShaderAttribute
{
    public Stage Stage = stage;
}