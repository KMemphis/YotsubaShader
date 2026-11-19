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

/// <summary>Cada built-in que el analizador acepta compila y pasa spirv-val en su etapa.</summary>
[Collection("ShaderProcessor")]
public class BuiltInTests
{
    public static TheoryData<string, string> Shaders => new()
    {
        { "Vertex inputs", """
            public class S {
                [ShaderStage(Stage.Vertex)] [return: SV_Position]
                public float4 VS([POSITION] float4 p, [SV_VertexID] uint v, [SV_InstanceID] uint i,
                                 [SV_StartVertexLocation] int bv, [SV_StartInstanceLocation] int bi)
                { float4 r = p; r.X += v + i + (uint)bv + (uint)bi; return r; }
            }
            """ },
        { "Fragment inputs", """
            public class S {
                [ShaderStage(Stage.Fragment)] [return: SV_Target]
                public float4 FS([SV_Position] float4 c, [SV_IsFrontFace] bool f, [SV_SampleIndex] uint s,
                                 [SV_PrimitiveID] uint p)
                { float4 r = c; r.X += s + p; return r; }
            }
            """ },
        { "Fragment helper and sample position", """
            public class S {
                [ShaderStage(Stage.Fragment)] [return: SV_Target]
                public float4 FS([YotsubaShader.SpirvSyntax.BuiltIn_HelperInvocation] bool h, [YotsubaShader.SpirvSyntax.BuiltIn_SamplePosition] float2 sp)
                { float4 r = default; r.X = sp.X; return r; }
            }
            """ },
        { "Depth output", """
            public class S {
                [ShaderStage(Stage.Fragment)]
                public void FS([SV_Target] out float4 c, [SV_Depth] out float d) { c = default; d = 0.5f; }
            }
            """ },
        { "Conservative depth", """
            public class S {
                [ShaderStage(Stage.Fragment)]
                public void FS([SV_Target] out float4 c, [SV_DepthGreaterEqual] out float d) { c = default; d = 0.5f; }
            }
            """ },
        { "Compute inputs", """
            public struct Data { public uint[] Values; }
            public class S {
                [StorageBuffer] [DescriptorSet(0)] [Binding(0)] public Data Buffer;
                [ShaderStage(Stage.Compute)] [numthreads(8, 8, 1)]
                public void CS([SV_DispatchThreadID] uint3 g, [SV_GroupThreadID] uint3 l, [SV_GroupIndex] uint i, [SV_GroupID] uint3 w)
                { Buffer.Values[(int)i] = g.X + l.Y + w.Z; }
            }
            """ },
    };

    [Theory]
    [MemberData(nameof(Shaders))]
    public void Built_in_compiles_and_validates(string name, string code)
    {
        Assert.NotNull(name);
        SpirvPipeline.Compile(code);
    }
}
