using Microsoft.Xna.Framework;
using YotsubaShader.Language;
using YotsubaShader.Language.StylesAttributes;
using YotsubaShader.Language.StylesAttributes.HlslStyle;

namespace YotsubaShader.Frontend.Tests;

public class BasicColorShader
{
    public struct VS_Input
    {
        [Location(0)]
        public Vector2 Position;

        [Location(1)]
        public Vector4 Color;
    }

    public struct VS_Output
    {
        [SV_Position]
        public Vector4 Position;

        [Location(0)]
        public Vector4 Color;
    }

    public struct FS_Input
    {
        [Location(0)]
        public Vector4 Color;
    }

    [ShaderStage(Stage.Vertex)]
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

    [ShaderStage(Stage.Fragment)]
    public Vector4 FS(FS_Input input)
    {
        return input.Color;
    }
}