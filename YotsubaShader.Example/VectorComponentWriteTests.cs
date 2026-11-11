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

/// <summary>#17: leer y escribir un componente de un vector (v.X = ...) con el índice declarado del componente.</summary>
[Collection("ShaderProcessor")]
public class VectorComponentWriteTests
{
    private const string Vectors = """
        [Vector]
        public struct V4
        {
            [VectorComponent(0)] public float X;
            [VectorComponent(1)] public float Y;
            [VectorComponent(2)] public float Z;
            [VectorComponent(3)] public float W;
            public V4(float x, float y, float z) { X = x; Y = y; Z = z; W = 1f; }
        }
        """;

    private static string Spirv(string members, string body, string stage = "Fragment", string returnType = "float",
        string parameters = "") =>
        new IRToSpirvModule().Compile(ShaderIr.Process($$"""
            using YotsubaShader.Language;
            using YotsubaShader.HlslSyntax;
            using YotsubaShader.Language.Native.Types.Vector;

            namespace Game;

            {{Vectors}}

            {{members}}

            public class S
            {
                [ShaderStage(Stage.{{stage}})]
                public {{returnType}} FS({{parameters}})
                {
                    {{body}}
                }
            }
            """)).ToText();

    /// <summary>El índice de los OpAccessChain que apuntan a un componente del vector local <paramref name="local"/>.</summary>
    private static string[] ComponentIndices(string text, string local) =>
        Regex.Matches(text, $@"OpAccessChain %ptr_function_float %local_{local}_\w+ %uint_(\d)")
            .Select(match => match.Groups[1].Value).ToArray();

    [Theory]
    [InlineData("X", "0")]
    [InlineData("Y", "1")]
    [InlineData("Z", "2")]
    [InlineData("W", "3")]
    public void Writing_a_component_stores_through_an_access_chain_with_its_index(string component, string index)
    {
        string text = Spirv("", $"V4 v = new V4(0f, 0f, 0f); v.{component} = 3f; return v.X;");

        Assert.Equal([index], ComponentIndices(text, "v"));
        Assert.Contains("OpStore %address_FS", text);
    }

    [Theory]
    [InlineData("X", "0")]
    [InlineData("Y", "1")]
    [InlineData("Z", "2")]
    [InlineData("W", "3")]
    public void Reading_a_component_extracts_the_component_at_its_index(string component, string index)
    {
        string text = Spirv("", $"V4 v = new V4(0f, 0f, 0f); return v.{component};");

        Assert.Matches($@"OpCompositeExtract %float %value_FS_\d+_\d+ {index}\b", text);
    }

    [Fact]
    public void Compound_assignment_reads_and_writes_the_same_component()
    {
        string text = Spirv("", "V4 v = new V4(0f, 0f, 0f); v.Y += 2f; return v.Y;");

        Assert.Equal(["1"], ComponentIndices(text, "v"));
        Assert.Contains("OpFAdd", text);
    }

    [Fact]
    public void Increment_updates_the_component_in_place()
    {
        string text = Spirv("", "V4 v = new V4(0f, 0f, 0f); v.X++; return v.X;");

        Assert.Equal(["0"], ComponentIndices(text, "v"));
    }

    [Fact]
    public void Component_of_a_vector_inside_a_struct_can_be_written()
    {
        string text = Spirv(
            "public struct Light { public V4 Position; }",
            "Light l = new Light(); l.Position.Z = 1f; return l.Position.Z;");

        // l.Position (miembro 0) y después el componente 2 sobre esa dirección.
        Assert.Matches(
            @"(%address_\w+) = OpAccessChain %\w+ %local_l_\w+ %uint_0\s+%address_\w+ = OpAccessChain %ptr_function_float \1 %uint_2",
            text.Replace("\r", ""));
    }

    [Fact]
    public void Component_of_a_vector_in_an_array_element_can_be_written()
    {
        string text = Spirv("", "V4[] a = new V4[2]; a[1].Y = 2f; return a[1].Y;");

        Assert.Matches(
            @"(%address_\w+) = OpAccessChain %\w+ %local_a_\w+ %\w+\s+%address_\w+ = OpAccessChain %ptr_function_float \1 %uint_1",
            text.Replace("\r", ""));
    }

    [Fact]
    public void Component_of_a_stage_output_struct_can_be_written()
    {
        string text = Spirv(
            """
            public struct VS_Output { [SV_Position] public V4 Position; }
            """,
            "VS_Output o = new VS_Output(); o.Position.X = 1f; return o;",
            stage: "Vertex",
            returnType: "VS_Output");

        Assert.Contains("OpAccessChain", text);
        Assert.Contains("OpStore", text);
    }

    [Fact]
    public void The_declared_index_is_used_not_the_component_name()
    {
        // X se declara como componente 1: escribir s.X escribe el componente 1, no el 0.
        string text = Spirv(
            """
            [Vector]
            public struct Swap
            {
                [VectorComponent(0)] public float Y;
                [VectorComponent(1)] public float X;
                public Swap(float y, float x) { Y = y; X = x; }
            }
            """,
            "Swap s = new Swap(0f, 0f); s.X = 7f; return s.X;");

        Assert.Equal(["1"], ComponentIndices(text, "s"));
        Assert.Matches(@"OpCompositeExtract %float %value_FS_\d+_\d+ 1\b", text);
    }

    [Fact]
    public void Components_named_like_the_last_ones_of_a_smaller_vector_stay_in_range()
    {
        // Dos componentes llamados Z y W: sus índices son 0 y 1; el 3 saldría del vector.
        string text = Spirv(
            """
            [Vector]
            public struct Odd
            {
                [VectorComponent(0)] public float Z;
                [VectorComponent(1)] public float W;
                public Odd(float z, float w) { Z = z; W = w; }
            }
            """,
            "Odd o = new Odd(0f, 0f); o.W = 1f; return o.W;");

        Assert.Equal(["1"], ComponentIndices(text, "o"));
        Assert.DoesNotMatch(@"OpCompositeExtract %float %value_FS_\d+_\d+ [23]\b", text);
    }

    [Fact]
    public void A_vector_member_that_is_not_a_component_is_rejected_with_a_clear_message()
    {
        NotSupportedException error = Assert.Throws<NotSupportedException>(() => Spirv(
            """
            [Vector]
            public struct Tagged
            {
                [VectorComponent(0)] public float X;
                [VectorComponent(1)] public float Y;
                public float Extra;
                public Tagged(float x, float y) { X = x; Y = y; Extra = 0f; }
            }
            """,
            "Tagged t = new Tagged(0f, 0f); t.Extra = 1f; return t.X;"));

        Assert.Contains("is not a component", error.Message);
        Assert.Contains("X, Y", error.Message);
    }
}
