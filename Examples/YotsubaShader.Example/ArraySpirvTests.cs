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

using YotsubaShader.Compiler.SpirV;

namespace YotsubaShader.Example;

/// <summary>#9: los arrays locales llegan a SPIR-V como OpAccessChain/OpLoad/OpStore sobre un OpTypeArray.</summary>
[Collection("ShaderProcessor")]
public class ArraySpirvTests
{
    private static string Spirv(string members, string body) =>
        new IRToSpirvModule().Compile(ShaderIr.Process($$"""
            using YotsubaShader.Language;
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

            public class S
            {
                {{members}}

                [ShaderStage(Stage.Fragment)]
                public float FS()
                {
                    {{body}}
                }
            }
            """)).ToText();

    [Fact]
    public void Reading_an_element_loads_through_an_access_chain()
    {
        string text = Spirv("", "int[] a = new int[3]; return a[1];");

        Assert.Contains("OpTypeArray", text);
        Assert.Contains("OpAccessChain", text);
        Assert.Contains("OpLoad", text);
    }

    [Fact]
    public void Writing_an_element_stores_through_an_access_chain()
    {
        string text = Spirv("", "int[] a = new int[3]; a[2] = 7; return a[2];");

        Assert.Contains("OpAccessChain", text);
        Assert.Contains("OpStore", text);
    }

    [Fact]
    public void Array_of_vectors_is_declared_with_a_vector_element_type()
    {
        string text = Spirv("", "V4[] a = new V4[2]; return a[1].X;");

        Assert.Contains("OpTypeArray", text);
        Assert.Contains("OpTypeVector", text);
    }

    [Fact]
    public void Member_of_an_array_element_can_be_written()
    {
        string text = Spirv(
            "public struct P { public float V; }",
            "P[] a = new P[2]; a[1].V = 2f; return a[1].V;");

        Assert.Contains("OpTypeArray", text);
        Assert.Contains("OpStore", text);
    }

    [Fact]
    public void Length_is_usable_as_a_loop_bound()
    {
        string text = Spirv("", "int[] a = new int[3]; int s = 0; for (int i = 0; i < a.Length; i++) { s += a[i]; } return s;");

        Assert.Contains("OpLoopMerge", text);
    }
}
