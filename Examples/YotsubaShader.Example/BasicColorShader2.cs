namespace YotsubaShader.Example;
using YotsubaShader.Language;
using YotsubaShader.HlslSyntax;
using YotsubaShader.HlslSyntax.Resources;

public class BasicColorShader2
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

        Vector4 vec = new Vector4(0,0,3);
        var arr = new int[] { 1, 3, 4, 5, 6 };
        foreach (var VARIABLE in arr)
        {
            vec = new Vector4(
                vertex.Position.X,
                1,
                3);
        }
        
        foreach (var VARIABLE in arr)
        {
            vec = new Vector4(
                vertex.Position.X,
                1,
                3);
        }

        Position = vec;

        vertexOutput.Color = vertex.Color;

        return vertexOutput;
    }

    float SystemCall(){
        return 3f;
    }

    float SystemCall(float pFloat){
        return pFloat;
    }
    
    [ShaderStage(Stage.Fragment)]
    [return: SV_Target(0)]
    public Vector4 FS(VS_Output fragment)
    {
        return fragment.Color;
    }
}