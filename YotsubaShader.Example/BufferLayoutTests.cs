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

/// <summary>
/// #24: los uniform y storage buffers son un Block y cada miembro lleva su Offset: std140 para los uniform,
/// std430 para los storage buffers. Los desplazamientos esperados son los de la especificación de Vulkan,
/// los mismos que debe usar la aplicación al llenar el buffer.
/// </summary>
[Collection("ShaderProcessor")]
public class BufferLayoutTests
{
    private const string Types = """
        using YotsubaShader.Language;
        using YotsubaShader.SpirvSyntax.Resources;
        using YotsubaShader.Language.Native.Types.Vector;

        namespace Game;

        [Vector] public struct V2 { [VectorComponent(0)] public float X; [VectorComponent(1)] public float Y; }
        [Vector] public struct V3 { [VectorComponent(0)] public float X; [VectorComponent(1)] public float Y; [VectorComponent(2)] public float Z; }
        [Vector] public struct V4 { [VectorComponent(0)] public float X; [VectorComponent(1)] public float Y; [VectorComponent(2)] public float Z; [VectorComponent(3)] public float W; }
        """;

    private static string Spirv(string declarations, string members)
    {
        var shader = ShaderIr.Process($$"""
            {{Types}}

            {{declarations}}

            public class S
            {
                {{members}}

                [ShaderStage(Stage.Fragment)]
                public float FS() { return 1f; }
            }
            """);

        var module = new IRToSpirvModule().Compile(shader);
        Assert.NotEmpty(new SpirVCompiler().Compile(module));
        return module.ToText();
    }

    /// <summary>Los desplazamientos de cada miembro del struct, en orden.</summary>
    private static int[] Offsets(string text, string structId) =>
        [.. Regex.Matches(text, $@"OpMemberDecorate {Regex.Escape(structId)} (\d+) Offset (\d+)")
            .Select(m => (Index: int.Parse(m.Groups[1].Value), Offset: int.Parse(m.Groups[2].Value)))
            .OrderBy(x => x.Index)
            .Select(x => x.Offset)];

    private static void Rejects(string declarations, string members, string expected)
    {
        var error = Assert.ThrowsAny<Exception>(() => Spirv(declarations, members));
        Assert.Contains(expected, error.Message);
    }

    private const string Uniform = "[Uniform] [DescriptorSet(0)] [Binding(0)]";
    private const string Storage = "[StorageBuffer] [DescriptorSet(0)] [Binding(1)]";

    [Fact]
    public void A_uniform_struct_is_a_block()
    {
        string text = Spirv("public struct P { public float A; }", $"{Uniform} public P Data;");

        Assert.Contains("OpDecorate %struct_P_Uniform Block", text);
    }

    [Fact]
    public void A_storage_buffer_struct_is_a_block()
    {
        string text = Spirv("public struct P { public float A; }", $"{Storage} public P Data;");

        Assert.Contains("OpDecorate %struct_P_StorageBuffer Block", text);
    }

    [Fact]
    public void Scalars_are_packed_every_four_bytes()
    {
        string text = Spirv("public struct P { public float A; public int B; public uint C; }", $"{Uniform} public P Data;");

        Assert.Equal([0, 4, 8], Offsets(text, "%struct_P_Uniform"));
    }

    [Fact]
    public void A_double_is_aligned_to_eight_bytes()
    {
        string text = Spirv("public struct P { public float A; public double B; }", $"{Storage} public P Data;");

        Assert.Equal([0, 8], Offsets(text, "%struct_P_StorageBuffer"));
    }

    [Fact]
    public void In_std140_a_vec3_is_aligned_to_16_and_the_next_scalar_fills_its_fourth_component()
    {
        string text = Spirv("public struct P { public float A; public V3 B; public float C; public V4 D; }",
            $"{Uniform} public P Data;");

        Assert.Equal([0, 16, 28, 32], Offsets(text, "%struct_P_Uniform"));
    }

    [Fact]
    public void In_std430_a_vec2_is_aligned_to_8()
    {
        string text = Spirv("public struct P { public float A; public V2 B; public float C; }", $"{Storage} public P Data;");

        Assert.Equal([0, 8, 16], Offsets(text, "%struct_P_StorageBuffer"));
    }

    [Fact]
    public void In_std140_a_nested_struct_is_aligned_and_sized_to_16()
    {
        string text = Spirv("public struct Inner { public float X; } public struct P { public float A; public Inner I; public float B; }",
            $"{Uniform} public P Data;");

        Assert.Equal([0, 16, 32], Offsets(text, "%struct_P_Uniform"));
        Assert.Equal([0], Offsets(text, "%struct_Inner"));
    }

    [Fact]
    public void In_std430_a_nested_struct_keeps_the_alignment_of_its_members()
    {
        string text = Spirv("public struct Inner { public float X; } public struct P { public float A; public Inner I; public float B; }",
            $"{Storage} public P Data;");

        Assert.Equal([0, 4, 8], Offsets(text, "%struct_P_StorageBuffer"));
    }

    [Fact]
    public void Only_the_resource_struct_is_a_block_not_the_nested_ones()
    {
        string text = Spirv("public struct Inner { public float X; } public struct P { public Inner I; }", $"{Uniform} public P Data;");

        Assert.DoesNotContain("OpDecorate %struct_Inner Block", text);
    }

    [Fact]
    public void The_same_struct_as_uniform_and_storage_buffer_gets_each_layout()
    {
        string text = Spirv("public struct P { public float A; public V3 B; }",
            $"{Uniform} public P Ubo; [StorageBuffer] [DescriptorSet(0)] [Binding(2)] public P Ssbo;");

        Assert.Equal([0, 16], Offsets(text, "%struct_P_Uniform"));
        Assert.Equal([0, 16], Offsets(text, "%struct_P_StorageBuffer"));
    }

    [Fact]
    public void Two_resources_of_the_same_type_declare_the_block_once()
    {
        string text = Spirv("public struct P { public float A; }",
            $"{Uniform} public P First; [Uniform] [DescriptorSet(0)] [Binding(3)] public P Second;");

        Assert.Single(Regex.Matches(text, @"OpDecorate %struct_P_Uniform Block"));
        Assert.Single(Regex.Matches(text, @"OpMemberDecorate %struct_P_Uniform 0 Offset"));
    }

    [Fact]
    public void A_nested_struct_with_the_same_offsets_in_both_layouts_can_be_shared()
    {
        string text = Spirv("public struct Inner { public float X; } public struct A { public Inner I; } public struct B { public Inner I; }",
            $"{Uniform} public A First; {Storage} public B Second;");

        Assert.Single(Regex.Matches(text, @"OpMemberDecorate %struct_Inner 0 Offset"));
    }

    [Fact]
    public void A_nested_struct_that_needs_two_different_layouts_is_rejected()
    {
        Rejects("public struct Inner { public float X; } public struct Mid { public float A; public Inner I; } public struct A { public Mid M; } public struct B { public Mid M; }",
            $"{Uniform} public A First; {Storage} public B Second;",
            "two different buffer layouts");
    }

    [Fact]
    public void A_bool_member_is_rejected()
    {
        Rejects("public struct P { public bool Flag; }", $"{Uniform} public P Data;", "bool");
    }

    [Fact]
    public void An_array_member_is_rejected()
    {
        Rejects("public struct P { public float[] Values; }", $"{Uniform} public P Data;", "float[]");
    }

    [Fact]
    public void A_buffer_that_is_not_a_struct_is_rejected_with_advice()
    {
        Rejects("", $"{Uniform} public float Time;", "must be a struct");
    }
}
