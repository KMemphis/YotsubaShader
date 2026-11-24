namespace YotsubaShader.Compiler.Metadata;

/// <summary>
/// APIs gráficas (y versiones) para las que el shader debe ser válido. El analizador del IDE y el compilador
/// avisan o dan error cuando el shader usa algo que alguna de ellas no tiene. Va en la clase del shader o en el
/// ensamblado (<c>[assembly: ShaderTargets(...)]</c>); la clase manda sobre el ensamblado. Sin atributo se valida
/// contra <see cref="Target.Vulkan_13"/>.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Assembly)]
public sealed class ShaderTargetsAttribute(Target graphicsApi) : Attribute
{
    public Target GraphicsApi { get; } = graphicsApi;
}
