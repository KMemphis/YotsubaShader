using Microsoft.Xna.Framework;
using YotsubaShader.Stages;

namespace YotsubaShader.Frontend.Tests;

public class BasicColorShader
{
    public struct VS_Input
    {
        public Vector2 Position;
        public Vector4 Color;
    }

    public struct VS_Output
    {
        public Vector4 Position;
        public Vector4 Color;
    }

    [ShaderStage(ShaderStage.Vertex)]
    public VS_Output VS(VS_Input input)
    {
        VS_Output output;

        output.Position = new Vector4(
            input.Position.X,
            input.Position.Y,
            0.0f,
            1.0f
        );

        output.Color = input.Color;

        return output;
    }

    [ShaderStage(ShaderStage.Fragment)]
    public Vector4 FS(VS_Output input)
    {
        return input.Color;
    }
}