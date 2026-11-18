#region MS-PL License - Copyright (c) 2026 Memphis Yomael Núñez Cruz - Dominican Republic
/*
╔════════════════════════════════════════════════════════════════════════════════════╗
║                                                                                    ║
║  ███╗   ███╗███████╗      ██████╗ ██╗                                              ║
║  ████╗ ████║██╔════╝      ██╔══██╗██║                                              ║
║  ██╔████╔██║███████╗█████╗██████╔╝██║                                              ║
║  ██║╚██╔╝██║╚════██║╚════╝██╔═══╝ ██║                                              ║
║  ██║ ╚═╝ ██║███████║      ██║     ███████╗                                         ║
║  ╚═╝     ╚═╝╚══════╝      ╚═╝     ╚══════╝                                         ║
║                                                                                    ║
╠════════════════════════════════════════════════════════════════════════════════════╣
║  Copyright (c) 2026 Memphis Yomael Núñez Cruz - Dominican Republic                 ║
╠════════════════════════════════════════════════════════════════════════════════════╣
║                                                                                    ║
║  Microsoft Public License (MS-PL)                                                  ║
║                                                                                    ║
║  This license governs use of the accompanying software. If you use the software,   ║
║  you accept this license. If you do not accept the license, do not use the         ║
║  software.                                                                         ║
║                                                                                    ║
║  1. Definitions                                                                    ║
║  The terms "reproduce," "reproduction," "derivative works," and "distribution"     ║
║  have the same meaning here as under U.S. copyright law.                           ║
║  A "contribution" is the original software, to which copyright notices are         ║
║  attached, and any additions or changes to the software.                           ║
║  A "contributor" is any person that distributes its contribution under this        ║
║  license.                                                                          ║
║  "Licensed patents" are a contributor's patent claims that read directly on its    ║
║  contribution.                                                                     ║
║                                                                                    ║
║  2. Grant of Rights                                                                ║
║  (A) Copyright Grant- Subject to the terms of this license, including the license  ║
║  conditions and limitations in section 3, each contributor grants you a non-       ║
║  exclusive, worldwide, royalty-free copyright license to reproduce its             ║
║  contribution, prepare derivative works of its contribution, and distribute its    ║
║  contribution or any derivative works that you create.                             ║
║  (B) Patent Grant- Subject to the terms of this license, including the license     ║
║  conditions and limitations in section 3, each contributor grants you a non-       ║
║  exclusive, worldwide, royalty-free license under its licensed patents to make,    ║
║  have made, use, sell, offer for sale, import, and/or otherwise dispose of its     ║
║  contribution in the software or derivative works of the contribution in the       ║
║  software.                                                                         ║
║                                                                                    ║
║  3. Conditions and Limitations                                                     ║
║  (A) No Trademark License- This license does not grant you rights to use any       ║
║  contributors' name, logo, or trademarks.                                          ║
║  (B) If you bring a patent claim against any contributor over patents that you     ║
║  claim are infringed by the software, your patent license from such contributor    ║
║  to the software ends automatically.                                               ║
║  (C) If you distribute any portion of the software, you must retain all copyright, ║
║  patent, trademark, and attribution notices that are present in the software.      ║
║  (D) If you distribute any portion of the software in source code form, you may do ║
║  so only under this license by including a complete copy of this license with      ║
║  your distribution. If you distribute any portion of the software in compiled or   ║
║  object code form, you may only do so under a license that complies with this      ║
║  license.                                                                          ║
║  (E) The software is licensed "as-is." You bear the risk of using it. The          ║
║  contributors give no express warranties, guarantees or conditions. You may have   ║
║  additional consumer rights under your local laws which this license cannot        ║
║  change. To the extent permitted under your local laws, the contributors exclude   ║
║  the implied warranties of merchantability, fitness for a particular purpose and   ║
║  non-infringement.                                                                 ║
║                                                                                    ║
╚════════════════════════════════════════════════════════════════════════════════════╝
*/
#endregion

using System.Text.RegularExpressions;
using YotsubaShader.Compiler.SpirV;

namespace YotsubaShader.Example;

/// <summary>#21: la metadata de la interfaz se valida antes de generar SPIR-V y cada variable recibe su decoración.</summary>
[Collection("ShaderProcessor")]
public class InterfaceValidationTests
{
    private const string Header = """
        using YotsubaShader.Language;
        using YotsubaShader.HlslSyntax;
        using YotsubaShader.Language.Native.Types.Vector;

        namespace Game;

        [Vector]
        public struct V4
        {
            [VectorComponent(0)] public float X;
            [VectorComponent(1)] public float Y;
            [VectorComponent(2)] public float Z;
            [VectorComponent(3)] public float W;
        }
        """;

    private static string Spirv(string body) =>
        new IRToSpirvModule().Compile(ShaderIr.Process(Header + "\n" + body)).ToText();

    private static void Rejects(string body, params string[] expected)
    {
        var error = Assert.ThrowsAny<Exception>(() => Spirv(body));
        foreach (string text in expected) Assert.Contains(text, error.Message);
    }

    /// <summary>Cuántas decoraciones Location o BuiltIn tiene cada variable Input/Output.</summary>
    private static Dictionary<string, int> InterfaceDecorations(string text)
    {
        var variables = Regex.Matches(text, @"(%\S+) = OpVariable \S+ (Input|Output)")
            .Select(m => m.Groups[1].Value);

        return variables.ToDictionary(
            id => id,
            id => Regex.Matches(text, $@"OpDecorate {Regex.Escape(id)} (Location|BuiltIn)\b").Count);
    }

    [Fact]
    public void A_negative_location_is_rejected_with_the_member_and_the_value()
    {
        Rejects("""
            public class S
            {
                [ShaderStage(Stage.Fragment)]
                [return: SV_Target(-1)]
                public float FS() { return 1f; }
            }
            """, "negative location -1", "__return", "'FS'");
    }

    [Fact]
    public void A_negative_semantic_index_is_rejected()
    {
        Rejects("""
            public class S
            {
                [ShaderStage(Stage.Vertex)]
                [return: SV_Position]
                public V4 VS([TEXCOORD(-1)] V4 p) { return p; }
            }
            """, "negative semantic index -1", "TextureCoordinate", "'p'");
    }

    [Fact]
    public void Two_outputs_with_the_same_explicit_location_are_rejected()
    {
        Rejects("""
            public class S
            {
                [ShaderStage(Stage.Fragment)]
                public void FS([SV_Target(1)] out float a, [SV_Target(1)] out float b) { a = 1f; b = 2f; }
            }
            """, "duplicada");
    }

    [Fact]
    public void Two_inputs_with_the_same_semantic_are_rejected()
    {
        Rejects("""
            public class S
            {
                [ShaderStage(Stage.Vertex)]
                [return: SV_Position]
                public V4 VS([TEXCOORD(0)] V4 a, [TEXCOORD(0)] V4 b) { return a; }
            }
            """, "repetida");
    }

    [Fact]
    public void An_input_without_location_or_built_in_is_rejected()
    {
        Rejects("""
            public class S
            {
                [ShaderStage(Stage.Fragment)]
                public float FS(float x) { return x; }
            }
            """, "no Location or built-in", "'x'", "'FS'");
    }

    [Fact]
    public void A_vertex_input_without_a_semantic_is_rejected()
    {
        Rejects("""
            public class S
            {
                [ShaderStage(Stage.Vertex)]
                [return: SV_Position]
                public V4 VS(V4 p) { return p; }
            }
            """, "no Location or built-in", "'p'", "'VS'");
    }

    [Fact]
    public void A_vertex_built_in_used_by_a_fragment_shader_is_rejected()
    {
        Rejects("""
            public class S
            {
                [ShaderStage(Stage.Fragment)]
                public float FS([SV_VertexID] int id) { return 1f; }
            }
            """, "VertexIndex", "'id'", "vertex shader input");
    }

    [Fact]
    public void A_fragment_built_in_used_by_a_vertex_shader_is_rejected()
    {
        Rejects("""
            public class S
            {
                [ShaderStage(Stage.Vertex)]
                [return: SV_Position]
                public V4 VS([POSITION] V4 p, [SV_IsFrontFace] bool front) { return p; }
            }
            """, "FrontFacing", "'front'", "fragment shader input");
    }

    [Fact]
    public void A_built_in_used_in_the_wrong_direction_is_rejected()
    {
        Rejects("""
            public class S
            {
                [ShaderStage(Stage.Fragment)]
                [return: SV_Position]
                public V4 FS([SV_Position] V4 p) { return p; }
            }
            """, "FragCoord", "fragment shader input");
    }

    [Fact]
    public void A_target_without_an_index_is_location_zero()
    {
        string text = Spirv("""
            public class S
            {
                [ShaderStage(Stage.Fragment)]
                [return: SV_Target]
                public float FS() { return 1f; }
            }
            """);

        Assert.Contains("OpDecorate %FS_0_Output___return Location 0", text);
    }

    [Fact]
    public void A_fragment_output_without_annotation_still_writes_to_target_zero()
    {
        string text = Spirv("""
            public class S
            {
                [ShaderStage(Stage.Fragment)]
                public float FS() { return 1f; }
            }
            """);

        Assert.Contains("OpDecorate %FS_0_Output___return Location 0", text);
    }

    [Fact]
    public void The_front_facing_flag_is_decorated_as_a_built_in()
    {
        string text = Spirv("""
            public class S
            {
                [ShaderStage(Stage.Fragment)]
                public float FS([SV_IsFrontFace] bool front) { return 1f; }
            }
            """);

        Assert.Contains("OpDecorate %FS_0_Input_front BuiltIn FrontFacing", text);
    }

    [Fact]
    public void Every_interface_variable_gets_exactly_one_location_or_built_in()
    {
        string text = Spirv("""
            public class S
            {
                public struct VsIn { [POSITION] public V4 Position; [COLOR] public V4 Color; }
                public struct VsOut { [SV_Position] public V4 Position; [COLOR] public V4 Color; }
                public struct FsIn { [SV_Position] public V4 Position; [COLOR] public V4 Color; }

                [ShaderStage(Stage.Vertex)]
                public VsOut VS(VsIn input)
                {
                    VsOut output;
                    output.Position = input.Position;
                    output.Color = input.Color;
                    return output;
                }

                [ShaderStage(Stage.Fragment)]
                public V4 FS(FsIn input) { return input.Color; }
            }
            """);

        Dictionary<string, int> decorations = InterfaceDecorations(text);

        Assert.NotEmpty(decorations);
        Assert.All(decorations, pair => Assert.True(pair.Value == 1, $"{pair.Key} has {pair.Value} decorations"));
    }
}
