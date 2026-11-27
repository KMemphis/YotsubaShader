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

namespace YotsubaShader.Example;

/// <summary>Un parámetro por valor que el método reasigna se comporta como una variable local.</summary>
public class ParameterReassignmentTests
{
    private static string Compile(string helper, string call) => SpirvPipeline.Compile(
        $$"""
        public class S
        {
            {{helper}}

            [ShaderStage(Stage.Fragment)]
            [return: SV_Target]
            public float4 FS() { {{call}} }
        }
        """);

    [Fact]
    public void A_parameter_reassigned_outside_a_loop_compiles()
    {
        string spirv = Compile("float F(float q) { q = q * 2f; return q; }", "return new float4(F(1f));");
        Assert.Contains("OpFunction", spirv);
    }

    [Fact]
    public void A_parameter_reassigned_inside_a_loop_compiles()
    {
        string spirv = Compile(
            "float F(float q) { float sum = 0f; for (int i = 0; i < 4; i++) { sum += q; q = q * 2f; } return sum; }",
            "return new float4(F(1f));");
        Assert.Contains("OpFunction", spirv);
    }

    [Fact]
    public void A_vector_parameter_reassigned_inside_a_loop_compiles()
    {
        string spirv = Compile(
            "float F(float2 q) { float sum = 0f; for (int i = 0; i < 4; i++) { sum += q.X; q = q * 2f + new float2(1f, 0f); } return sum; }",
            "return new float4(F(new float2(1f, 2f)));");
        Assert.Contains("OpFunction", spirv);
    }

    [Fact]
    public void Reassigning_the_parameter_does_not_change_the_caller()
    {
        string spirv = Compile(
            "float F(float q) { q = 5f; return q; }",
            "float a = 1f; float b = F(a); return new float4(a + b);");
        Assert.Contains("OpFunction", spirv);
    }
}
