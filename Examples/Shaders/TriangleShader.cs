using YotsubaShader.Compiler.Metadata;
using YotsubaShader.HlslSyntax;
using YotsubaShader.Language;
using YotsubaShader.Language.Native.Types.Vector;

namespace Examples.Shaders;

/// <summary>Dibuja cada vértice con su color: lo mínimo para comprobar que un efecto carga y renderiza.</summary>
// Usa compute: OpenGL desde 4.3, OpenGL ES desde 3.1 y Direct3D desde 11. Target.All incluiría Direct3D 8/9, que ningún backend genera.
[ShaderTargets(Target.Vulkan_11 | Target.Vulkan_12 | Target.Vulkan_13 | Target.Vulkan_14 | Target.OpenGL_43 | Target.OpenGLES_31 | Target.DirectX_11 | Target.DirectX_12 | Target.AllMetal | Target.WebGpu)]
public class TriangleShader
{
    public struct VSInput
    {
        [POSITION] 
        public Vector2 Position;

        [COLOR] 
        public Vector4 Color;
    }

    public struct VSOutput
    {
        [SV_Position]
        public Vector4 Pos;
        [COLOR] 
        public Vector4 COLOR;
    }

    [ShaderStage(Stage.Vertex)]
    public VSOutput Vs(VSInput vsInput)
    {
        VSOutput output;
        output.COLOR = new Vector4(1, 0, 0, 1);
        output.Pos = new Vector4(vsInput.Position.X, vsInput.Position.Y, 0, 1);

        return output;
    }

    [ShaderStage(Stage.Fragment)]
    public Vector4 Fs(VSOutput vsOutput)
    {
        return vsOutput.COLOR;
    }

    [ShaderStage(Stage.Compute)]
    [numthreads(1,2,3)]
    void Compute()
    {
        
    }
}

[Vector]
public struct Vector2
{
    [VectorComponent(0)] public float X;
    [VectorComponent(1)] public float Y;

    public Vector2()
    {
    }
}

[Vector]
public struct Vector3
{
    [VectorComponent(0)] public float X;
    [VectorComponent(1)] public float Y;
    [VectorComponent(2)] public float Z;
    public Vector3()
    {
    }
}

[Vector]
public struct Vector4
{
    [VectorComponent(0)] public float X;
    [VectorComponent(1)] public float Y;
    [VectorComponent(2)] public float Z;
    [VectorComponent(3)] public float W;

    public Vector4(float x, float y, float z, float w)
    {
        X = x;
        Y = y;
        Z = z;
        W = w;
    }
}
