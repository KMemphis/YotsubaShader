namespace YotsubaShader.Extensions.KNI;

public sealed class KNITargetBackend : IShaderTargetBackend
{
    public string Name => "kni";

    public byte[] Compile(CompiledCode shader, uint options)
    {
        return shader.ToKNIWebGL_FX();
    }
}
