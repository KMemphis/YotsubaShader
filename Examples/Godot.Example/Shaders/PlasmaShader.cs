using YotsubaShader.Language;
using YotsubaShader.HlslSyntax;
using YotsubaShader.HlslSyntax.Types;
using YotsubaShader.SpirvSyntax.Resources;

namespace Examples.GodotShaders;

public struct PlasmaVertexOutput
{
    [SV_Position] public float4 Position;
    [TEXCOORD(0)] public float2 Uv;
}

public struct PlasmaParams
{
    public float4 Tint;
    public float Time;
}

/// <summary>
/// Un efecto a pantalla completa: el vértice pasa la posición tal cual (espacio de recorte) y el fragmento pinta
/// un plasma animado. Se aplica a un QuadMesh de 2x2 en Godot. La hora y el tinte llegan como uniforms.
/// </summary>
public class PlasmaShader
{
    [Uniform] [DescriptorSet(0)] [Binding(0)] public PlasmaParams P;

    [ShaderStage(Stage.Vertex)]
    public PlasmaVertexOutput Vs([POSITION] float4 position, [TEXCOORD(0)] float2 uv)
    {
        PlasmaVertexOutput output;
        output.Position = position;
        output.Uv = uv;
        return output;
    }

    [ShaderStage(Stage.Fragment)]
    [return: SV_Target]
    public float4 Fs(PlasmaVertexOutput input)
    {
        float t = P.Time;
        float v = System.MathF.Sin(input.Uv.X * 12f + t)
                + System.MathF.Sin(input.Uv.Y * 9f - t * 1.3f)
                + System.MathF.Sin((input.Uv.X + input.Uv.Y) * 7f + t * 0.7f);

        float4 color = new float4();
    
        color.X = (0.5f + 0.5f * System.MathF.Sin(v)) * P.Tint.X;
        color.Y = (0.5f + 0.5f * System.MathF.Sin(v + 2.1f)) * P.Tint.Y;
        color.Z = (0.5f + 0.5f * System.MathF.Sin(v + 4.2f)) * P.Tint.Z;
        color.W = 1f;
        return color;
    }
}
