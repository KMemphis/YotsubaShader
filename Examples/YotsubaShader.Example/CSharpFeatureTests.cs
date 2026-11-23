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

/// <summary>Construcciones de C# traducidas a la GPU: cada una compila y pasa spirv-val.</summary>
[Collection("ShaderProcessor")]
public class CSharpFeatureTests
{
    private static string Fragment(string body, string members = "") =>
        SpirvPipeline.Compile($$"""
            public struct Pair
            {
                public float A; public int B;
                public float Sum() { return A + B; }
                public void Scale(float k) { A = A * k; }
                public void Deconstruct(out float a, out int b) { a = A; b = B; }
            }
            public class S
            {
                {{members}}
                [ShaderStage(Stage.Fragment)]
                public float4 FS([TEXCOORD(0)] float x, [TEXCOORD(1)] int n)
                {
                    {{body}}
                }
            }
            """);

    [Fact]
    public void Default_of_a_scalar_vector_and_struct_is_a_null_constant()
    {
        string text = Fragment("float f = default; float4 v = default(float4); Pair p = default; return new float4 { X = f + p.A + v.X, W = 1f };");
        Assert.Contains("OpConstantNull", text);
    }

    [Fact]
    public void Constant_fields_are_folded_into_literals()
    {
        Fragment("return new float4 { X = Scale * x, W = 1f };", "const float Scale = 2f;");
    }

    [Fact]
    public void Switch_on_an_int_becomes_an_OpSwitch()
    {
        string text = Fragment("""
            float r = 0f;
            switch (n)
            {
                case 0: r = 1f; break;
                case 1:
                case 2: r = 2f; break;
                case -3: return new float4 { X = 3f };
                default: r = x; break;
            }
            return new float4 { X = r, W = 1f };
            """);
        Assert.Contains("OpSwitch", text);
    }

    [Fact]
    public void Break_inside_a_switch_inside_a_loop_leaves_only_the_switch()
    {
        Fragment("""
            float r = 0f;
            for (int i = 0; i < 4; i++)
            {
                switch (i)
                {
                    case 1: continue;
                    case 2: r += 2f; break;
                    default: if (r > 3f) break; r += 1f; break;
                }
                r += 0.5f;
            }
            return new float4 { X = r, W = 1f };
            """);
    }

    [Fact]
    public void Switch_with_patterns_and_guards()
    {
        Fragment("""
            float r;
            switch (x)
            {
                case < 0f: r = -1f; break;
                case >= 0f and < 1f when n > 2: r = 0.5f; break;
                case 1f: r = 1f; break;
                default: r = 2f; break;
            }
            return new float4 { X = r, W = 1f };
            """);
    }

    [Fact]
    public void Switch_expression_with_constants_patterns_and_discard()
    {
        string text = Fragment("""
            float r = n switch
            {
                0 => 1f,
                1 or 2 => 2f,
                > 10 when x > 0f => x,
                _ => 0f
            };
            float s = x switch { < 0.5f => 0f, _ => 1f };
            return new float4 { X = r + s, W = 1f };
            """);
        Assert.Contains("OpSwitch", text);
    }

    [Fact]
    public void Ref_out_and_in_parameters_of_helpers()
    {
        string text = Fragment("""
            float a = x;
            Twice(ref a);
            Split(a, out float whole, out var frac);
            Pair p = default;
            Fill(ref p, in a);
            return new float4 { X = a + whole + frac + p.A, W = 1f };
            """, """
            void Twice(ref float v) { v = v * 2f; }
            void Split(float v, out float w, out float f) { w = (float)(int)v; f = v - w; }
            void Fill(ref Pair p, in float value) { p.A = value; p.B = 3; }
            """);
        Assert.Contains("OpFunctionCall", text);
    }

    [Fact]
    public void Ref_argument_can_be_a_struct_field()
    {
        Fragment("""
            Pair p = default;
            Twice(ref p.A);
            return new float4 { X = p.A, W = 1f };
            """, "void Twice(ref float v) { v = v * 2f + 1f; }");
    }

    [Fact]
    public void Local_functions_with_captures_by_reference()
    {
        string text = Fragment("""
            float total = 0f;
            float scale = x * 2f;
            void Add(float v) { total += v * scale; }
            float Square(float v) => v * v;
            static float Half(float v) { return v * 0.5f; }
            void AddTwice(float v) { Add(v); Add(v); }
            Add(1f);
            AddTwice(Square(x));
            return new float4 { X = total + Half(scale), W = 1f };
            """);
        Assert.Contains("OpFunctionCall", text);
    }

    [Fact]
    public void Local_function_reads_a_stage_input()
    {
        Fragment("""
            float Twice() => x * 2f + n;
            return new float4 { X = Twice(), W = 1f };
            """);
    }

    [Fact]
    public void Tuples_and_deconstruction()
    {
        Fragment("""
            var (a, b) = (x, 2f);
            (a, b) = (b, a);
            (float s, float c) sc = SinCos(x);
            var (s2, _) = SinCos(a);
            Pair p = default;
            p.A = 1f; p.B = 2;
            var (pa, pb) = p;
            return new float4 { X = a + b + sc.s + sc.c + s2 + pa + pb, W = 1f };
            """, "(float, float) SinCos(float v) { return (System.MathF.Sin(v), System.MathF.Cos(v)); }");
    }

    [Fact]
    public void Instance_methods_of_structs_receive_this_by_reference()
    {
        Fragment("""
            Pair p = default;
            p.A = x;
            p.Scale(2f);
            return new float4 { X = p.Sum(), W = 1f };
            """);
    }

    [Fact]
    public void Array_parameters_take_the_length_of_each_argument()
    {
        Fragment("""
            float[] three = new float[3];
            float[] five = new float[] { 1f, 2f, 3f, 4f, x };
            return new float4 { X = Sum(three) + Sum(five), W = 1f };
            """, """
            float Sum(float[] values)
            {
                float total = values.Length;
                foreach (float v in values) total += v;
                return total + Inner(values);
            }
            float Inner(float[] values) => values[values.Length - 1];
            """);
    }

    [Fact]
    public void Matrices_and_fixed_arrays_in_a_uniform_buffer()
    {
        SpirvPipeline.Compile("""
            public struct Camera
            {
                public float4x4 ViewProjection;
                public float3x3 Normal;
                [ArrayLength(4)] public float4[] Lights;
                [ArrayLength(2)] public float2x2[] Pairs;
                public float Time;
            }
            public class S
            {
                [Uniform] [DescriptorSet(0)] [Binding(0)] public Camera Cam;

                [ShaderStage(Stage.Fragment)]
                public float4 FS([TEXCOORD(0)] float x)
                {
                    float sum = 0f;
                    foreach (float4 light in Cam.Lights) sum += light.X;
                    return new float4 { X = sum + Cam.Time + Cam.Lights.Length, W = 1f };
                }
            }
            """);
    }

    [Fact]
    public void Unsized_array_at_the_end_of_a_storage_buffer()
    {
        string text = SpirvPipeline.Compile("""
            public struct Particle { public float4 Position; public float4x4 Transform; }
            public struct Particles { public uint Count; public Particle[] Items; }
            public class S
            {
                [StorageBuffer] [DescriptorSet(0)] [Binding(0)] public Particles Data;

                [ShaderStage(Stage.Compute)]
                [numthreads(64, 1, 1)]
                public void CS([SV_DispatchThreadID] uint3 id)
                {
                    int n = Data.Items.Length;
                    float total = 0f;
                    foreach (Particle p in Data.Items) total += p.Position.X;
                    if ((int)id.X < n)
                    {
                        Data.Items[(int)id.X].Position.Y = total;
                    }
                }
            }
            """);
        Assert.Contains("OpArrayLength", text);
        Assert.Contains("OpTypeRuntimeArray", text);
    }

    [Fact]
    public void Math_methods_without_a_gpu_instruction_are_compiled_as_functions()
    {
        Fragment("""
            double d = x;
            float r = 0f;
            r += (float)System.Math.Round(d, 2);
            r += System.MathF.Round(x, System.MidpointRounding.AwayFromZero);
            r += System.MathF.Round(x, 1, System.MidpointRounding.ToZero);
            r += System.MathF.Cbrt(x) + (float)System.Math.Cbrt(d);
            r += System.MathF.CopySign(x, -1f) + System.MathF.IEEERemainder(x, 3f);
            r += System.MathF.MaxMagnitude(x, -2f) + System.MathF.MinMagnitude(x, -2f);
            r += System.MathF.ILogB(x) + System.MathF.BitIncrement(x) + System.MathF.BitDecrement(x);
            int q = System.Math.DivRem(n, 3, out int rem);
            var (q2, rem2) = System.Math.DivRem(n, 4);
            r += q + rem + q2 + rem2;
            r += System.BitConverter.Int32BitsToSingle(System.BitConverter.SingleToInt32Bits(x));
            return new float4 { X = r, W = 1f };
            """);
    }

    [Fact]
    public void StructuredBuffer_RWStructuredBuffer_and_ConstantBuffer()
    {
        string text = SpirvPipeline.Compile("""
            public struct Particle { public float4 Position; public uint Count; }
            public struct Camera { public float4x4 ViewProjection; public float Time; }
            public class S
            {
                [register("t0")] public StructuredBuffer<Particle> Input;
                [register("u0")] public RWStructuredBuffer<Particle> Output;
                [register("b0")] public ConstantBuffer<Camera> Cam;
                [register("t1")] public StructuredBuffer<float4> Colors;

                [ShaderStage(Stage.Compute)]
                [numthreads(64, 1, 1)]
                public void CS([SV_DispatchThreadID] uint3 id)
                {
                    int i = (int)id.X;
                    if (i >= Input.Length) return;
                    Particle p = Input[i];
                    p.Position.X += Cam.Value.Time + Colors[0u].Y;
                    Output[i] = p;
                    Output[i].Position.Y = 2f;
                    Intrinsic.InterlockedAdd(ref Output[i].Count, 1u);
                }
            }
            """);
        Assert.Contains("OpTypeRuntimeArray", text);
        Assert.Contains("OpArrayLength", text);
    }

    [Fact]
    public void Compound_assignment_and_increment_on_buffer_elements()
    {
        string text = SpirvPipeline.Compile("""
            public struct Item { public uint A; public float B; }
            public struct Items { public Item[] List; }
            public class S
            {
                [DescriptorSet(0)] [Binding(0)] public RWStructuredBuffer<uint> Counters;
                [DescriptorSet(0)] [Binding(1)] public RWStructuredBuffer<Item> Output;
                [StorageBuffer] [DescriptorSet(0)] [Binding(2)] public Items Data;
                [DescriptorSet(0)] [Binding(3)] public uint[] Plain;

                [ShaderStage(Stage.Compute)]
                [numthreads(64, 1, 1)]
                public void CS([SV_DispatchThreadID] uint3 id)
                {
                    uint k = id.X;
                    Counters[k] += 2;
                    Counters[k]++;
                    Output[k].A += 1;
                    Output[k].B *= 2f;
                    Data.List[(int)k].A -= 1;
                    Data.List[(int)k].B++;
                    Plain[(int)k] |= 1u;
                }
            }
            """);
        Assert.Contains("OpIAdd", text);
        Assert.Contains("OpFMul", text);
    }

    [Fact]
    public void Target_typed_new_builds_the_same_vector_as_the_explicit_constructor()
    {
        string text = SpirvPipeline.Compile("""
            public class S
            {
                [ShaderStage(Stage.Fragment)]
                public float4 FS()
                {
                    float4 c = new(1f, 2f, 3f, 4f);
                    return c;
                }
            }
            """);
        Assert.Contains("OpCompositeConstruct", text);
    }

    [Fact]
    public void Arrays_lists_and_immutable_collections_are_storage_buffers()
    {
        string text = SpirvPipeline.Compile("""
            using System.Collections.Generic;
            using System.Collections.Immutable;

            public struct Particle { public float4 Position; public uint Hits; }
            public class S
            {
                [DescriptorSet(0)] [Binding(0)] public ImmutableArray<Particle> Input;
                [DescriptorSet(0)] [Binding(1)] public ImmutableList<float> Weights;
                [DescriptorSet(0)] [Binding(2)] public Particle[] Output;
                [DescriptorSet(0)] [Binding(3)] public List<uint> Counters;

                [ShaderStage(Stage.Compute)]
                [numthreads(64, 1, 1)]
                public void CS([SV_DispatchThreadID] uint3 id)
                {
                    int i = (int)id.X;
                    if (i >= Input.Length || i >= Output.Length || i >= Counters.Count) return;

                    float total = 0f;
                    foreach (float w in Weights) total += w;

                    Particle p = Input[i];
                    p.Position.X += total * Weights[0];
                    Output[i] = p;
                    Output[i].Position.Y = 1f;
                    Counters[i] = Counters[i] + 1u;
                    Intrinsic.InterlockedAdd(ref Output[i].Hits, 1u);
                }
            }
            """);
        Assert.Contains("OpArrayLength", text);
    }
}
