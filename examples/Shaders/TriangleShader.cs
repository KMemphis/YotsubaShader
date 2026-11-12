using YotsubaShader.HlslSyntax;
using YotsubaShader.Language;
using YotsubaShader.Language.Native.Types.Vector;

namespace Examples.Shaders;

/// <summary>Dibuja cada vértice con su color: lo mínimo para comprobar que un efecto carga y renderiza.</summary>
public class TriangleShader
{
    public struct VS_Input
    {
        [POSITION]
        public Vector2 Position;

        [COLOR]
        public Vector4 Color;
    }

    public struct VS_Output
    {
        [COLOR]
        public Vector4 Color;
    }

    [ShaderStage(Stage.Vertex)]
    public VS_Output VS(VS_Input vertex, [SV_Position] out Vector4 Position)
    {
        VS_Output vertexOutput = new VS_Output();

        Position = new Vector4(vertex.Position.X, vertex.Position.Y, 0);
        vertexOutput.Color = vertex.Color;

        return vertexOutput;
    }

    [ShaderStage(Stage.Fragment)]
    [return: SV_Target(0)]
    public Vector4 FS(VS_Output fragment)
    {
        return fragment.Color;
    }
}

[Vector]
public struct Vector2
{
    [VectorComponent(0)] public float X;
    [VectorComponent(1)] public float Y;

    public Vector2(float x, float y) { X = x; Y = y; }
}

[Vector]
public struct Vector3
{
    [VectorComponent(0)] public float X;
    [VectorComponent(1)] public float Y;
    [VectorComponent(2)] public float Z;

    public Vector3(float x, float y, float z) { X = x; Y = y; Z = z; }
}

[Vector]
public struct Vector4
{
    [VectorComponent(0)] public float X;
    [VectorComponent(1)] public float Y;
    [VectorComponent(2)] public float Z;
    [VectorComponent(3)] public float W;

    public Vector4(float x, float y, float z) { X = x; Y = y; Z = z; W = 1; }
}
