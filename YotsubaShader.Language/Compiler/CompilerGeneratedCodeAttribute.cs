namespace YotsubaShader.Language.Compiler;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
public class CompilerGeneratedCodeAttribute(Binaries binariesFormats) : YotsubaShaderAttribute
{
    public Binaries BinaryFormat = binariesFormats;

    public CompilerGeneratedCodeAttribute() : this(Binaries.SpirV)
    {
        
    }
}

[Flags]
public enum Binaries : uint
{
    SpirV,
    Msl,
    Dxil,
    Dxbc,
    Wgsl,
    Glsl,
    GlslEs,
    Hlsl
}