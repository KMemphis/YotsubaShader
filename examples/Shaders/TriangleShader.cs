using YotsubaShader.HlslSyntax;
using YotsubaShader.Language;
using YotsubaShader.Language.Native.Types.Vector;

namespace Examples.Shaders;

/// <summary>Dibuja cada vértice con su color: lo mínimo para comprobar que un efecto carga y renderiza.</summary>
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
        [TEXCOORD]
        public Vector2 Pos;
        [COLOR] 
        public Vector4 COLOR;
    }

    [ShaderStage(Stage.Vertex)]
    public VSOutput Vs(VSInput vsInput)
    {
        VSOutput output;
        output.COLOR = new Vector4(0, 3, 3, 3);
        output.Pos = new Vector2
        {
            X = 3,
            Y = 4
        };

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
}

[Vector]
public struct Vector3
{
    [VectorComponent(0)] public float X;
    [VectorComponent(1)] public float Y;
    [VectorComponent(2)] public float Z;

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