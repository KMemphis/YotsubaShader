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


using YotsubaShader.Roslyn;

namespace YotsubaShader.Example;

/// <summary>
/// El invariante de YotsubaShader: si el shader no tiene errores YS (los mismos que muestra el IDE), compila a
/// SPIR-V válido para Vulkan. Cada caso del corpus es un shader raro, mal hecho o al límite; el test falla si el
/// compilador lo acepta y produce algo inválido, o si lo rechaza con un error que no es YS.
/// </summary>
[Collection("ShaderProcessor")]
public class InvariantTests
{
    private const string Vs = "[ShaderStage(Stage.Vertex)]";
    private const string Fs = "[ShaderStage(Stage.Fragment)]";
    private const string Cs = "[ShaderStage(Stage.Compute)] [numthreads(8, 8, 1)]";

    public static TheoryData<string> Corpus => new()
    {
        // ── Slots y etiquetas en sitios raros ──
        $"public class S {{ {Cs} public void CS([SV_Position] float4 p) {{ }} }}",
        $"public class S {{ {Fs} public float4 FS([SV_DispatchThreadID] uint3 id) {{ return default; }} }}",
        $"public class S {{ {Vs} public void VS([SV_Position] out float4 a, [SV_Position] out float4 b) {{ a = default; b = default; }} }}",
        $"public class S {{ {Vs} [return: SV_Position] public float4 VS([SV_Position] float4 p) {{ return p; }} }}",
        $"public class S {{ {Fs} [return: SV_Position] public float4 FS() {{ return default; }} }}",
        $"public class S {{ {Fs} [return: SV_Target(9)] public float4 FS() {{ return default; }} }}",
        $"public class S {{ {Fs} [return: SV_Target(-2)] public float4 FS() {{ return default; }} }}",
        $"public class S {{ {Fs} public void FS([SV_Target(0)] out float4 a, [SV_Target(0)] out float4 b) {{ a = default; b = default; }} }}",
        $"public class S {{ {Fs} public float4 FS([TEXCOORD(0)] [SV_Position] float4 p) {{ return p; }} }}",
        $"public class S {{ {Fs} public float4 FS(float4 p) {{ return p; }} }}",
        $"public class S {{ {Vs} [return: SV_Position] public float4 VS([TEXCOORD(0)] bool b) {{ return default; }} }}",
        $"public class S {{ {Vs} [return: SV_Position] public float3 VS() {{ return default; }} }}",
        $"public class S {{ {Fs} public float4 FS([SV_IsFrontFace] int f) {{ return default; }} }}",
        $"public class S {{ {Cs} public void CS([SV_DispatchThreadID] uint id) {{ }} }}",
        $"public class S {{ {Cs} public void CS([SV_DispatchThreadID] int3 id) {{ }} }}",
        $"public class S {{ {Cs} public void CS([TEXCOORD(0)] float x) {{ }} }}",
        $"public class S {{ {Fs} [return: TEXCOORD(0)] public float4 FS() {{ return default; }} }}",
        $"public class S {{ {Vs} public float4 VS([POSITION] float4 p) {{ return p; }} }}",
        $"public class S {{ {Vs} [return: SV_Position] public float4 VS([TEXCOORD(0)] float4 a, [TEXCOORD(0)] float4 b) {{ return a; }} }}",
        $"public class S {{ {Fs} public float4 FS([SV_SampleIndex] uint s, [SV_PrimitiveID] uint p, [SV_IsFrontFace] bool f) {{ float4 r = default; r.X = s + p; return r; }} }}",
        $"public class S {{ {Fs} public void FS([SV_Target] out float4 c, [SV_Depth] out float d, [SV_DepthLessEqual] out float e) {{ c = default; d = 0f; e = 0f; }} }}",
        $"public class S {{ {Fs} public float4 FS([SV_Barycentrics] float3 b) {{ return default; }} }}",
        $"public class S {{ {Fs} public float4 FS([TEXCOORD(0)] Texture2D t) {{ return default; }} }}",
        "public struct Io { [SV_Position] public float4 P; public float2 Uv; } public class S { [ShaderStage(Stage.Vertex)] public Io VS([POSITION] float4 p) { Io o = default; o.P = p; return o; } }",
        "public struct In2 { public float X; } public struct Io { [SV_Position] public float4 P; public In2 I; } public class S { [ShaderStage(Stage.Vertex)] public Io VS([POSITION] float4 p) { Io o = default; o.P = p; return o; } }",
        """
        public struct VsOut { [SV_Position] public float4 Pos; [TEXCOORD(0)] public float2 Uv; [TEXCOORD(1)] public float3 N; }
        public class S {
            [ShaderStage(Stage.Vertex)] public VsOut VS([POSITION] float4 p) { VsOut o = default; o.Pos = p; return o; }
            [ShaderStage(Stage.Fragment)] public float4 FS(VsOut i) { float4 r = default; r.X = i.Uv.X + i.N.Z; return r; }
        }
        """,
        """
        public struct VsOut { [SV_Position] public float4 Pos; [TEXCOORD(0)] public float2 Uv; }
        public struct PsIn { [SV_Position] public float4 Pos; [TEXCOORD(3)] public float2 Uv; }
        public class S {
            [ShaderStage(Stage.Vertex)] public VsOut VS([POSITION] float4 p) { VsOut o = default; o.Pos = p; return o; }
            [ShaderStage(Stage.Fragment)] public float4 FS(PsIn i) { return i.Pos; }
        }
        """,

        // ── Entry points y compute ──
        "public class S { [ShaderStage(Stage.Compute)] public void CS() { } }",
        "public class S { [ShaderStage(Stage.Compute)] [numthreads(0, 1, 1)] public void CS() { } }",
        "public class S { [ShaderStage(Stage.Compute)] [numthreads(2048, 1, 1)] public void CS() { } }",
        "public class S { [ShaderStage(Stage.Compute)] [numthreads(32, 32, 2)] public void CS() { } }",
        "public class S { [ShaderStage(Stage.Compute)] [numthreads(1024, 1, 1)] public void CS() { } }",
        $"public class S {{ {Fs} [numthreads(8, 8, 1)] public float4 FS() {{ return default; }} }}",
        $"public class S {{ {Fs} public float4 FS() {{ return default; }} {Fs} public float4 FS([TEXCOORD(0)] float x) {{ return default; }} }}",
        $"public class S {{ {Fs} public float4 FS([TEXCOORD(0)] ref float x) {{ return default; }} }}",
        $"public class S {{ {Fs} public float4 FS() {{ return Other(); }} {Vs} [return: SV_Position] public float4 Other() {{ return default; }} }}",

        // ── Recursos ──
        $"public class S {{ public Texture2D T; {Fs} public float4 FS() {{ return default; }} }}",
        $"public class S {{ [DescriptorSet(0)] [Binding(0)] public Texture2D A; [DescriptorSet(0)] [Binding(0)] public SamplerState B; {Fs} public float4 FS() {{ return default; }} }}",
        $"public class S {{ [register(\"s0\")] public Texture2D T; {Fs} public float4 FS() {{ return default; }} }}",
        $"public class S {{ [Uniform] [DescriptorSet(0)] [Binding(0)] public float Time; {Fs} public float4 FS() {{ return default; }} }}",
        $"public struct P {{ public bool Flag; }} public class S {{ [Uniform] [DescriptorSet(0)] [Binding(0)] public P G; {Fs} public float4 FS() {{ return default; }} }}",
        $"public struct P {{ public float[] Items; }} public class S {{ [Uniform] [DescriptorSet(0)] [Binding(0)] public P G; {Fs} public float4 FS() {{ return default; }} }}",
        $"public struct P {{ public float[] Items; public float After; }} public class S {{ [StorageBuffer] [DescriptorSet(0)] [Binding(0)] public P G; {Fs} public float4 FS() {{ return default; }} }}",
        $"public class S {{ public float Plain; {Fs} public float4 FS() {{ return default; }} }}",
        $"public class S {{ static float counter; {Fs} public float4 FS() {{ counter += 1f; return default; }} }}",
        $"public struct P {{ public float4x4 M; [ArrayLength(3)] public float3x3[] N; public float3 V; public float S; }} public class S {{ [Uniform] [DescriptorSet(0)] [Binding(0)] public P G; {Fs} public float4 FS() {{ float4 r = default; r.X = G.V.X + G.S; return r; }} }}",
        "public struct Item { public float4 A; public uint B; } public struct Buf { public uint N; public Item[] Items; } public class S { [StorageBuffer] [DescriptorSet(0)] [Binding(0)] public Buf Data; [ShaderStage(Stage.Compute)] [numthreads(64,1,1)] public void CS([SV_DispatchThreadID] uint3 id) { Item it = Data.Items[(int)id.X]; it.B += 1u; Data.Items[(int)id.X] = it; } }",

        // ── Buffers envoltorio ──
        $"public class S {{ public StructuredBuffer<float4> Items; {Fs} public float4 FS() {{ return Items[0]; }} }}",
        $"public class S {{ [register(\"t0\")] public StructuredBuffer<bool> Flags; {Fs} public float4 FS() {{ return default; }} }}",
        $"public class S {{ [register(\"b0\")] public ConstantBuffer<float> Time; {Fs} public float4 FS() {{ return default; }} }}",
        $"public class S {{ [register(\"t0\")] public StructuredBuffer<float4> A; [register(\"t0\")] public StructuredBuffer<float4> B; {Fs} public float4 FS() {{ return A[0]; }} }}",
        "public struct P { public float4x4 M; public uint N; } public class S { [register(\"u0\")] public RWStructuredBuffer<P> Items; [register(\"b0\")] public ConstantBuffer<P> Cb; [ShaderStage(Stage.Compute)] [numthreads(32,1,1)] public void CS([SV_DispatchThreadID] uint3 id) { int i = (int)id.X; if (i < Items.Length) { P p = Items[i]; p.N += Cb.Value.N; Items[i] = p; Intrinsic.InterlockedAdd(ref Items[i].N, 1u); } } }",
        $"public class S {{ [register(\"t0\")] public StructuredBuffer<float4> Items; {Fs} public float4 FS([TEXCOORD(0)] int i) {{ float4 sum = default; for (int k = 0; k < Items.Length; k++) sum.X += Items[k].X; return sum; }} }}",

        // ── Colecciones de .NET como buffers ──
        $"public class S {{ public float[] Data; {Fs} public float4 FS() {{ float4 r = default; r.X = Data[0]; return r; }} }}",
        $"public class S {{ [DescriptorSet(0)] [Binding(0)] public System.Collections.Immutable.ImmutableArray<bool> F; {Fs} public float4 FS() {{ return default; }} }}",
        $"public class S {{ [DescriptorSet(0)] [Binding(0)] public System.Collections.Generic.List<float> L; {Cs} public void CS() {{ L.Add(1f); }} }}",
        $"public class S {{ [DescriptorSet(0)] [Binding(0)] public float[] A; [DescriptorSet(0)] [Binding(1)] public System.Collections.Immutable.ImmutableList<float4> B; {Cs} public void CS([SV_DispatchThreadID] uint3 id) {{ int i = (int)id.X; if (i < A.Length && i < B.Count) A[i] = B[i].X; }} }}",
        $"public class S {{ [DescriptorSet(0)] [Binding(0)] public System.Collections.Generic.List<float> L; {Cs} public void CS() {{ float s = 0f; foreach (float v in L) s += v; L[0] = s; }} }}",

        // ── C# raro ──
        $"public class S {{ {Fs} public float4 FS() {{ try {{ return default; }} catch {{ return default; }} }} }}",
        $"public class S {{ {Fs} public float4 FS() {{ string s = \"x\"; return default; }} }}",
        $"public class S {{ {Fs} public float4 FS([TEXCOORD(0)] uint a, [TEXCOORD(1)] int b) {{ float4 r = default; r.X = a + b; return r; }} }}",
        $"public class S {{ {Fs} public float4 FS() {{ return F(3); }} float4 F(int n) {{ return n <= 0 ? default : F(n - 1); }} }}",
        $"public class S {{ {Fs} public float4 FS() {{ float[] a = new float[2]; return default; }} }}",
        $"public class S {{ {Fs} public float4 FS([TEXCOORD(0)] int n) {{ float[] a = new float[n]; return default; }} }}",
        $"public class S {{ {Fs} public float4 FS([TEXCOORD(0)] int n) {{ float4 r = default; r.X = n switch {{ 0 => 1f, < 0 => -1f, _ => 2f }}; switch (n) {{ case 1: r.Y = 1f; break; default: break; }} return r; }} }}",
        $"public class S {{ {Fs} public float4 FS([TEXCOORD(0)] float x) {{ float t = 0f; void Add(float v) {{ t += v * x; }} Add(1f); var (a, b) = (t, x); float4 r = default; r.X = a + b; return r; }} }}",
        $"public class S {{ {Fs} public float4 FS([TEXCOORD(0)] float x) {{ x = 3f; return default; }} }}",
        $"public class S {{ {Fs} public float4 FS([TEXCOORD(0)] float x) {{ float4 r = default; r.X = (float)System.Math.Round((double)x, 3) + System.MathF.Cbrt(x); return r; }} }}",
        $"public class S {{ {Fs} public float4 FS() {{ float4 r = default; r.X = (float)System.Math.BigMul(2, 3); return r; }} }}",

        // ── Operaciones de una etapa en otra ──
        $"public class S {{ {Vs} [return: SV_Position] public float4 VS([POSITION] float4 p) {{ float d = Intrinsic.ddx(p.X); return p; }} }}",
        $"public class S {{ {Cs} public void CS() {{ Intrinsic.clip(1f); }} }}",
        $"public class S {{ {Vs} [return: SV_Position] public float4 VS([POSITION] float4 p) {{ H(p.X); return p; }} void H(float v) {{ Intrinsic.clip(v); }} }}",
        $"public class S {{ {Fs} public float4 FS([TEXCOORD(0)] float x) {{ float4 r = default; r.X = Intrinsic.ddx(x) + Intrinsic.fwidth(x); Intrinsic.clip(x); return r; }} }}",
    };

    [Theory]
    [MemberData(nameof(Corpus))]
    public void Either_a_YS_error_or_valid_SPIRV(string code)
    {
        try
        {
            SpirvPipeline.Compile(code);
        }
        catch (ShaderValidationException validation)
        {
            Assert.All(validation.Diagnostics, d => Assert.StartsWith("YS", d.Id));
        }
    }
}
