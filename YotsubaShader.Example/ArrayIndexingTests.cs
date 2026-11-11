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

using YotsubaShader.IR.Intermediate;
using YotsubaShader.IR.Intermediate.FunctionBody.Expressions;
using YotsubaShader.IR.Intermediate.FunctionBody.Expressions.AssignmentTargets;
using YotsubaShader.IR.Intermediate.FunctionBody.Expressions.Literals;
using YotsubaShader.IR.Intermediate.FunctionBody.Statements;
using YotsubaShader.IR.Intermediate.Types;
using IRArray = YotsubaShader.IR.Intermediate.Types.Array;

namespace YotsubaShader.Example;

/// <summary>#9: lectura, escritura e índices de arrays locales, y rechazo de índices inválidos.</summary>
[Collection("ShaderProcessor")]
public class ArrayIndexingTests
{
    private static Function Compile(string body, string parameters = "")
    {
        Shader shader = ShaderIr.Process($$"""
            using YotsubaShader.Language;

            namespace Game;

            public class S
            {
                [ShaderStage(Stage.Fragment)]
                public int FS({{parameters}})
                {
                    {{body}}
                }
            }
            """);

        return shader.Single("FS");
    }

    private static IRArray LocalArray(Function function, string name) =>
        Assert.IsType<IRArray>(ShaderIr.Flatten(function.Body).OfType<VariableDeclaration>()
            .Single(d => d.Storage.HumanName == name).Storage);

    private static Assignment[] Assignments(Function function) =>
        ShaderIr.Flatten(function.Body).OfType<Assignment>().ToArray();

    private static NotSupportedException Rejects(string body, string parameters = "") =>
        Assert.Throws<NotSupportedException>(() => Compile(body, parameters));

    [Fact]
    public void Array_with_initializer_gets_its_length_and_one_assignment_per_element()
    {
        Function fs = Compile("int[] a = new int[] { 4, 5, 6 }; return 0;");

        Assert.Equal(3u, LocalArray(fs, "a").Length);

        Assignment[] elements = Assignments(fs);
        Assert.Equal(3, elements.Length);

        for (int i = 0; i < elements.Length; i++)
        {
            var target = Assert.IsType<StorageArrayElementReference>(elements[i].Target);
            Assert.Equal(i.ToString(), Assert.IsType<IntLiteral>(target.Index).Value);
        }
    }

    [Fact]
    public void Array_created_with_a_length_is_declared_without_initial_assignments()
    {
        Function fs = Compile("int[] a = new int[3]; return 0;");

        Assert.Equal(3u, LocalArray(fs, "a").Length);
        Assert.Empty(Assignments(fs));
    }

    [Fact]
    public void Reading_an_element_produces_an_array_element_reference()
    {
        Function fs = Compile("int[] a = new int[3]; int x = a[1]; return x;");

        var read = Assert.IsType<StorageArrayElementReference>(fs.InitializerOf("x"));
        Assert.Equal("a", Assert.IsType<StorageReference>(read.ArrayReference).Storage.HumanName);
        Assert.Equal("1", Assert.IsType<IntLiteral>(read.Index).Value);
    }

    [Fact]
    public void Writing_an_element_targets_the_array_element()
    {
        Function fs = Compile("int[] a = new int[3]; a[2] = 7; return a[2];");

        Assignment write = Assert.Single(Assignments(fs));
        var target = Assert.IsType<StorageArrayElementReference>(write.Target);
        Assert.Equal("2", Assert.IsType<IntLiteral>(target.Index).Value);
        Assert.Equal("7", Assert.IsType<IntLiteral>(write.Value).Value);
    }

    [Fact]
    public void Compound_assignment_uses_the_array_element_as_its_target()
    {
        Function fs = Compile("int[] a = new int[3]; a[1] += 5; return a[1];");

        Assignment write = Assert.Single(Assignments(fs));
        Assert.IsType<StorageArrayElementReference>(write.Target);
    }

    [Fact]
    public void Index_may_be_a_variable_or_an_unsigned_value()
    {
        Function fs = Compile(
            "int[] a = new int[4]; uint u = 2u; int x = a[i]; int y = a[u]; return x + y;",
            "int i");

        Assert.IsType<StorageReference>(Assert.IsType<StorageArrayElementReference>(fs.InitializerOf("x")).Index);
        Assert.IsType<StorageReference>(Assert.IsType<StorageArrayElementReference>(fs.InitializerOf("y")).Index);
    }

    [Fact]
    public void Index_with_side_effects_is_evaluated_once_before_the_write()
    {
        Function fs = Compile("int[] a = new int[4]; int i = 0; a[i++] += 1; return i;");

        // El índice con efectos laterales se guarda en un temporal antes de leer y escribir.
        Assert.Contains(ShaderIr.Flatten(fs.Body).OfType<VariableDeclaration>(),
            d => d.Storage.HumanName.StartsWith("__index"));
    }

    [Fact]
    public void Length_of_a_local_array_is_its_declared_length()
    {
        Function fs = Compile("int[] a = new int[3]; int n = a.Length; return n;");

        Assert.Equal("3", Assert.IsType<IntLiteral>(fs.InitializerOf("n")).Value);
    }

    [Fact]
    public void Length_of_an_array_with_an_initializer_counts_its_elements()
    {
        Function fs = Compile("int[] a = new int[] { 4, 5, 6, 7 }; int n = a.Length; return n;");

        Assert.Equal("4", Assert.IsType<IntLiteral>(fs.InitializerOf("n")).Value);
    }

    [Fact]
    public void Constant_index_past_the_end_is_rejected()
    {
        Assert.Contains("out of range", Rejects("int[] a = new int[3]; return a[3];").Message);
    }

    [Fact]
    public void Constant_index_past_the_end_is_rejected_when_writing()
    {
        Assert.Contains("out of range", Rejects("int[] a = new int[3]; a[5] = 1; return 0;").Message);
    }

    [Fact]
    public void Negative_constant_index_is_rejected()
    {
        Assert.Contains("negative", Rejects("int[] a = new int[3]; return a[-1];").Message);
    }

    [Fact]
    public void Last_valid_index_is_accepted()
    {
        Compile("int[] a = new int[3]; return a[2];");
    }

    [Fact]
    public void Index_of_a_type_the_shader_does_not_support_is_rejected()
    {
        Rejects("int[] a = new int[3]; long i = 1; return a[i];");
    }

    [Fact]
    public void Array_length_must_be_a_compile_time_constant()
    {
        Assert.Contains("compile-time constant", Rejects("int[] a = new int[n]; return 0;", "int n").Message);
    }

    [Fact]
    public void Multidimensional_indexing_is_rejected()
    {
        Rejects("int[,] a = new int[2, 2]; return a[0, 1];");
    }
}
