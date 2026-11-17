namespace YotsubaShader.Extensions.FNA;

public sealed class FNATargetBackend : IShaderTargetBackend
{
    public string Name => "fna";

    public byte[] Compile(CompiledCode shader, uint options)
    {
        return shader.ToFNA_FX();
    }
}
