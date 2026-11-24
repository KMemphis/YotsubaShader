using YotsubaShader.HlslSyntax;
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
