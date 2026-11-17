namespace YotsubaShader.Extensions.MonoGame;

public sealed class MonoGameTargetBackend : IShaderTargetBackend
{
    public string Name => "monogame";

    public byte[] Compile(CompiledCode shader, uint options)
    {
        if (!Enum.IsDefined(typeof(MonoGamePlatform), options))
        {
            throw new ArgumentOutOfRangeException(
                nameof(options), options, "Plataforma de MonoGame no soportada.");
        }

        return shader.ToMGFX((MonoGamePlatform)options);
    }
}
