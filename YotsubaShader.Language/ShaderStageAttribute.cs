namespace YotsubaShader.Language;

[AttributeUsage(AttributeTargets.Method)]
public class ShaderStageAttribute(Stage stage) : Attribute
{
    public Stage Stage = stage;
}