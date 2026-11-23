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

using System.Diagnostics;
using System.Reflection;
using YotsubaShader.Compiler.SpirV;
using YotsubaShader.Extensions;
using YotsubaShader.IR.Intermediate;
using YotsubaShader.Roslyn;

namespace YotsubaShader.Example;

/// <summary>
/// Lo que la compilación de un shader válido le promete a cada destino: que el SPIR-V lleve lo que GLSL ES, HLSL y WGSL
/// necesitan (Flat en los varyings enteros, NonWritable en los buffers de solo lectura), que SPIR-V 1.3 sirva aunque el
/// shader copie structs de buffers, y que los plugins publicados sigan encontrando las firmas de <see cref="CompiledCode"/>.
/// </summary>
[Collection("ShaderProcessor")]
public class TargetStabilityTests
{
    private static CompiledCode Compile(string code)
    {
        var compilation = TestCompilation.Create(SpirvPipeline.Header + code);
        var (name, ir) = Assert.Single(new ShaderProcessor().Process(compilation, CancellationToken.None));
        var module = new IRToSpirvModule().Compile(ir);
        return new CompiledCode(new ShaderReflection(ir), new SpirVCompiler().Compile(module), module.ToSpirVAs, name);
    }

    private const string IntegerVarying = """
        public struct VsOut { [SV_Position] public float4 Pos; [TEXCOORD(0)] public uint Id; [TEXCOORD(1)] public float2 Uv; }
        public class S {
            [ShaderStage(Stage.Vertex)] public VsOut VS([POSITION] float4 p, [SV_VertexID] uint v) { VsOut o = default; o.Pos = p; o.Id = v; return o; }
            [ShaderStage(Stage.Fragment)] [return: SV_Target] public float4 FS(VsOut i) { float4 c = default; c.X = i.Id + i.Uv.X; return c; }
        }
        """;

    [Fact]
    public void Integer_varyings_are_flat_on_both_sides()
    {
        CompiledCode code = Compile(IntegerVarying);

        // GLSL ES rechaza un `out uint` sin `flat`; WGSL exige @interpolate(flat) en la salida de vertex.
        string vertex = code.ToGlslEs(Stage.Vertex, 300);
        Assert.Matches(@"flat out (highp )?uint", vertex);
        Assert.Matches(@"flat in (highp )?uint", code.ToGlslEs(Stage.Fragment, 300));
        // El float2 se sigue interpolando.
        Assert.DoesNotMatch(@"flat out (highp )?vec2", vertex);
    }

    private const string Buffers = """
        public struct Item { public float4 A; public uint B; }
        public class S {
            [DescriptorSet(0)] [Binding(0)] public StructuredBuffer<Item> Input;
            [DescriptorSet(0)] [Binding(1)] public RWStructuredBuffer<Item> Output;
            [DescriptorSet(0)] [Binding(2)] public System.Collections.Immutable.ImmutableArray<float4> Colors;
            [ShaderStage(Stage.Compute)] [numthreads(64, 1, 1)]
            public void CS([SV_DispatchThreadID] uint3 id)
            {
                int i = (int)id.X;
                if (i >= Input.Length) return;
                Item item = Input[i];
                item.A += Colors[0];
                Output[i] = item;
            }
        }
        """;

    [Fact]
    public void Read_only_buffers_are_NonWritable_and_become_SRVs()
    {
        CompiledCode code = Compile(Buffers);

        string hlsl = code.ToHlsl(Stage.Compute);
        Assert.Contains("ByteAddressBuffer Input", hlsl);
        Assert.DoesNotContain("RWByteAddressBuffer Input", hlsl);
        Assert.Contains("RWByteAddressBuffer Output", hlsl);
        Assert.Contains("readonly buffer", code.ToGlsl(Stage.Compute));
    }

    [Fact]
    public void Copies_of_buffer_structs_work_in_SPIRV_1_3()
    {
        CompiledCode code = Compile(Buffers);

        byte[] lowered = SpirvVersion.LowerCopyLogical(code.SpirV);
        Assert.True(lowered.Length > code.SpirV.Length, "el módulo debía tener OpCopyLogical para que el test tenga sentido");

        byte[] v13 = code.ToSpirV(1, 3);
        Assert.Equal((1, 3), SpirvVersion.Read(v13));
        Validate(v13, "vulkan1.1");
        Validate(lowered, "vulkan1.3");
    }

    [Fact]
    public void Cpp_target_accepts_textures_with_separate_samplers()
    {
        CompiledCode code = Compile("""
            public struct VsOut { [SV_Position] public float4 Pos; [TEXCOORD(0)] public float2 Uv; }
            public class S {
                [DescriptorSet(0)] [Binding(0)] public Texture2D Tex;
                [DescriptorSet(0)] [Binding(1)] public SamplerState Smp;
                [ShaderStage(Stage.Fragment)] [return: SV_Target] public float4 FS(VsOut i) { return Tex.Sample(Smp, i.Uv); }
            }
            """);

        Assert.Contains("Tex", code.ToCpp(Stage.Fragment));
    }

    [Fact]
    public void PrimitiveId_in_GLSL_ES_3_1_requests_its_extension()
    {
        CompiledCode code = Compile("""
            public class S {
                [ShaderStage(Stage.Fragment)] [return: SV_Target]
                public float4 FS([SV_PrimitiveID] uint p) { float4 r = default; r.X = p; return r; }
            }
            """);

        Assert.Contains("#extension GL_EXT_geometry_shader : require", code.ToGlslEs(Stage.Fragment, 310));
        Assert.DoesNotContain("GL_EXT_geometry_shader", code.ToGlslEs(Stage.Fragment, 320));
    }

    public static TheoryData<string, Type[]> PublishedSignatures => new()
    {
        // Las firmas de CompiledCode en 0.9.x, contra las que se compilaron los plugins MonoGame/KNI/FNA/Godot publicados.
        { nameof(CompiledCode.ToHlsl), [typeof(Stage)] },
        { nameof(CompiledCode.ToMsl), [typeof(Stage)] },
        { nameof(CompiledCode.ToGlsl), [typeof(Stage), typeof(uint)] },
        { nameof(CompiledCode.ToGlslEs), [typeof(Stage), typeof(uint)] },
        { nameof(CompiledCode.ToCpp), [typeof(Stage)] },
    };

    [Theory]
    [MemberData(nameof(PublishedSignatures))]
    public void Published_plugins_still_find_their_signatures(string method, Type[] parameters)
    {
        MethodInfo? found = typeof(CompiledCode).GetMethod(method, BindingFlags.Public | BindingFlags.Instance, parameters);
        Assert.True(found is { ReturnType: var type } && type == typeof(string),
            $"Falta CompiledCode.{method}({string.Join(", ", parameters.Select(p => p.Name))}): los plugins publicados dejarían de cargar.");
    }

    private static void Validate(byte[] spirv, string environment)
    {
        string? validator = (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator)
            .Append("/opt/local/bin").Append("/opt/homebrew/bin").Append("/usr/local/bin")
            .Select(dir => Path.Combine(dir, OperatingSystem.IsWindows() ? "spirv-val.exe" : "spirv-val"))
            .FirstOrDefault(File.Exists);
        if (validator is null) return;

        string file = Path.Combine(Path.GetTempPath(), $"ys_{Guid.NewGuid():N}.spv");
        File.WriteAllBytes(file, spirv);
        try
        {
            var start = new ProcessStartInfo(validator, $"--target-env {environment} \"{file}\"")
            {
                RedirectStandardError = true,
                RedirectStandardOutput = true
            };
            using Process process = Process.Start(start)!;
            string output = process.StandardError.ReadToEnd() + process.StandardOutput.ReadToEnd();
            process.WaitForExit();
            Assert.True(process.ExitCode == 0, $"spirv-val ({environment}) rechaza el módulo:\n{output}");
        }
        finally
        {
            File.Delete(file);
        }
    }
}
