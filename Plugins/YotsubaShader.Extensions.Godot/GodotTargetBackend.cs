using System.Text;

namespace YotsubaShader.Extensions.Godot;

public sealed class GodotTargetBackend : IShaderTargetBackend
{
    public string Name => "godot";

    public byte[] Compile(CompiledCode shader, uint options)
    {
        if (!Enum.IsDefined(typeof(GodotShaderType), (int)options))
        {
            throw new ArgumentOutOfRangeException(nameof(options), options, "Tipo de shader de Godot no soportado.");
        }

        return Encoding.UTF8.GetBytes(GodotShaderTranslator.Translate(shader, (GodotShaderType)options));
    }
}
