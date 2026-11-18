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

using YotsubaShader.IR.Intermediate.Types;
using YotsubaShader.Language.Exceptions;

namespace YotsubaShader.Example;

/// <summary>#18: los índices de [VectorComponent] deben ser únicos, no negativos y contiguos desde 0.</summary>
[Collection("ShaderProcessor")]
public class VectorComponentIndexTests
{
    /// <summary>Un fragment shader que devuelve el vector: obliga a procesar la declaración del tipo.</summary>
    private static IR.Intermediate.Shader Compile(params string[] fields)
    {
        string members = string.Join("\n    ", fields);
        return ShaderIr.Process($$"""
            using YotsubaShader.Language;
            using YotsubaShader.Language.Native.Types.Vector;

            namespace Game;

            [Vector]
            public struct V
            {
                {{members}}
            }

            public class S
            {
                [ShaderStage(Stage.Fragment)]
                public V FS(V input) { return input; }
            }
            """);
    }

    private static InvalidVectorComponentIndexException Rejects(params string[] fields) =>
        Assert.Throws<InvalidVectorComponentIndexException>(() => Compile(fields));

    [Fact]
    public void Valid_XYZW_mapping_keeps_every_component()
    {
        var shader = Compile(
            "[VectorComponent(0)] public float X;",
            "[VectorComponent(1)] public float Y;",
            "[VectorComponent(2)] public float Z;",
            "[VectorComponent(3)] public float W;");

        var vector = Assert.IsType<Vector>(Assert.Single(shader.Single("FS").Outputs));
        Assert.Equal(4, vector.ComponentsLength);
    }

    [Fact]
    public void Components_may_be_declared_in_any_order_as_long_as_indices_are_contiguous()
    {
        var shader = Compile(
            "[VectorComponent(1)] public float Y;",
            "[VectorComponent(0)] public float X;");

        Assert.Equal(2, Assert.IsType<Vector>(Assert.Single(shader.Single("FS").Outputs)).ComponentsLength);
    }

    [Fact]
    public void Missing_component_is_rejected_instead_of_shifting_positions()
    {
        // X(0) y Z(2): antes se convertía en un vector de 2 con X y Z.
        var error = Rejects(
            "[VectorComponent(0)] public float X;",
            "[VectorComponent(2)] public float Z;");

        Assert.Contains("falta el componente 1", error.Message);
    }

    [Fact]
    public void Duplicate_index_is_rejected()
    {
        var error = Rejects(
            "[VectorComponent(0)] public float X;",
            "[VectorComponent(1)] public float Y;",
            "[VectorComponent(1)] public float Z;");

        Assert.Contains("ya lo usa", error.Message);
        Assert.Contains("'Z'", error.Message);
    }

    [Fact]
    public void Negative_index_is_rejected()
    {
        var error = Rejects(
            "[VectorComponent(-1)] public float X;",
            "[VectorComponent(0)] public float Y;");

        Assert.Contains("negativo", error.Message);
    }

    [Fact]
    public void A_vector_needs_between_two_and_four_components()
    {
        Assert.Contains("entre 2 y 4", Rejects("[VectorComponent(0)] public float X;").Message);

        Assert.Contains("entre 2 y 4", Rejects(
            "[VectorComponent(0)] public float A;",
            "[VectorComponent(1)] public float B;",
            "[VectorComponent(2)] public float C;",
            "[VectorComponent(3)] public float D;",
            "[VectorComponent(4)] public float E;").Message);
    }
}
