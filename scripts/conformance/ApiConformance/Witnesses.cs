using YotsubaShader.HlslSyntax;
using YotsubaShader.HlslSyntax.Methods;
using YotsubaShader.HlslSyntax.Resources;
using YotsubaShader.HlslSyntax.Resources.Types;
using YotsubaShader.HlslSyntax.Types;
using YotsubaShader.Language;

namespace ApiConformance;

public struct Vertex
{
    [SV_Position] public float4 Position;
}

public struct Row
{
    public float Value;
}

/// <summary>Lo que cualquier API sabe hacer: un vertex y un fragment.</summary>
public class Baseline
{
    [ShaderStage(Stage.Vertex)]
    public Vertex VS([POSITION] float4 position)
    {
        Vertex output;
        output.Position = position;
        return output;
    }

    [ShaderStage(Stage.Fragment)]
    [return: SV_Target]
    public float4 FS(Vertex input) => new float4(1f);
}

/// <summary>double: llega en OpenGL 4.0, no existe en OpenGL ES, WGSL ni MSL.</summary>
public class Float64
{
    [ShaderStage(Stage.Fragment)]
    [return: SV_Target]
    public float4 FS()
    {
        double value = 1.0;
        return new float4((float)value);
    }
}

/// <summary>Compute shader: OpenGL 4.3, OpenGL ES 3.1, Direct3D 11.</summary>
public class Compute
{
    [ShaderStage(Stage.Compute)]
    [numthreads(1, 1, 1)]
    public void CS()
    {
    }
}

/// <summary>Storage buffer: OpenGL 4.3, OpenGL ES 3.1, Direct3D 11.</summary>
public class StorageBuffer
{
    [register("u0")] public RWStructuredBuffer<Row> Items;

    [ShaderStage(Stage.Fragment)]
    [return: SV_Target]
    public float4 FS() => new float4(Items[0].Value);
}

/// <summary>uint: GLSL 1.30 / GLSL ES 3.00; Shader Model 2/3 lo emula con floats.</summary>
public class UnsignedInt
{
    [ShaderStage(Stage.Fragment)]
    [return: SV_Target]
    public float4 FS()
    {
        uint a = 3u;
        uint b = a * 2u;
        return new float4((float)b);
    }
}

/// <summary>Operadores de bits: GLSL 1.30 / GLSL ES 3.00 / Shader Model 4.</summary>
public class IntegerBitOps
{
    [ShaderStage(Stage.Fragment)]
    [return: SV_Target]
    public float4 FS()
    {
        int a = 3;
        int b = a << 2;
        return new float4((float)b);
    }
}

/// <summary>Leer un texel por índice (texelFetch / Load): GLSL 1.30, GLSL ES 3.00, Shader Model 4.</summary>
public class TexelFetch
{
    [register("t0")] public Texture2D Tex;

    [ShaderStage(Stage.Fragment)]
    [return: SV_Target]
    public float4 FS() => Tex.Load(new int3(0, 0, 0));
}

/// <summary>Gather: GLSL 4.0, GLSL ES 3.10, Shader Model 4.1.</summary>
public class TextureGather
{
    [register("t0")] public Texture2D Tex;
    [register("s0")] public SamplerState Smp;

    [ShaderStage(Stage.Fragment)]
    [return: SV_Target]
    public float4 FS() => Tex.Gather(Smp, new float2(0.5f, 0.5f));
}

/// <summary>Muestreo con offset constante: GLSL 1.30, GLSL ES 3.00, Shader Model 4.</summary>
public class TextureOffset
{
    [register("t0")] public Texture2D Tex;
    [register("s0")] public SamplerState Smp;

    [ShaderStage(Stage.Fragment)]
    [return: SV_Target]
    public float4 FS() => Tex.Sample(Smp, new float2(0.5f, 0.5f), new int2(1, 1));
}

/// <summary>Tamaño de una textura: GLSL 1.30, GLSL ES 3.00, Shader Model 4.</summary>
public class TextureQuerySize
{
    [register("t0")] public Texture2D Tex;

    [ShaderStage(Stage.Fragment)]
    [return: SV_Target]
    public float4 FS()
    {
        uint width, height;
        Tex.GetDimensions(out width, out height);
        return new float4((float)width, (float)height, 0f, 1f);
    }
}

/// <summary>Derivadas de pantalla: no existen en ps_1_x; OpenGL ES 2.0 las pide por extensión.</summary>
public class Derivatives
{
    [ShaderStage(Stage.Fragment)]
    [return: SV_Target]
    public float4 FS([TEXCOORD(0)] float x) => new float4(Intrinsic.ddx(x));
}
