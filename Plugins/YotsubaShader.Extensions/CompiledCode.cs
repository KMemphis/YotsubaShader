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

using System.Runtime.InteropServices;
using YotsubaShader.IR.Intermediate;
using YotsubaShaderCross;
using YotsubaShaderCross.Spv;

namespace YotsubaShader.Extensions;

public record CompiledCode(
    ShaderReflection Reflection,
    byte[] FullSpirV,
    string SpirVAs,
    string CSharpFullClassName)
{
    private byte[]? _cleanSpirV;

    /// <summary>
    /// El módulo tal como lo emite el compilador, con las extensiones <c>SPV_GOOGLE_*</c> y las semánticas HLSL
    /// (<c>POSITION</c>, <c>COLOR</c>...). Es la entrada de las traducciones a HLSL/GLSL/MSL/C++ y no sale de la librería.
    /// </summary>
    internal byte[] FullSpirV { get; init; } = FullSpirV;

    /// <summary>
    /// El SPIR-V para las APIs gráficas: el módulo del compilador (1.6) sin <c>SPV_GOOGLE_*</c> ni sus decoraciones,
    /// que Vulkan sin <c>VK_GOOGLE_*</c> rechaza y que sólo sirven para generar HLSL con sus semánticas.
    /// </summary>
    public byte[] SpirV => _cleanSpirV ??= SpirvVersion.StripGoogleExtensions(FullSpirV);

    private CppVector<uint> ByteArrToCppVector()
    {
        ReadOnlySpan<uint> words =
            MemoryMarshal.Cast<byte, uint>(FullSpirV);

        return new CppVector<uint>(words.ToArray());
    }

    /// <summary>
    /// El SPIR-V del shader con la versión pedida (1.3 a 1.6). El compilador emite 1.6; <see cref="SpirV"/> es ese módulo ya limpio.
    /// </summary>
    public byte[] ToSpirV(int major, int minor) => SpirvVersion.Retarget(SpirV, major, minor);

    /// <summary>
    /// Metal Shading Language. Sin versión usa la que fija SPIRV-Cross por defecto, MSL 1.2; para usar funciones más
    /// nuevas (p. ej. 2.1 para texturas de lectura/escritura) pide una versión mayor.
    /// </summary>
    public string ToMsl(Stage stage, uint? majorVersion = null, uint minorVersion = 0, uint patchVersion = 0)
    {
        (string entryPoint, ExecutionModel executionModel) =
            GetEntryPoint(stage);

        var compiler = new CompilerMSL(ByteArrToCppVector());

        compiler.set_entry_point(entryPoint, executionModel);

        if (majorVersion is { } major)
        {
            CompilerMSL.Options options = compiler.get_msl_options();
            options.msl_version = CompilerMSL.Options.make_msl_version(major, minorVersion, patchVersion);
            compiler.set_msl_options(options);
        }

        return compiler.compile().str();
    }

    /// <summary>HLSL. <paramref name="shaderModel"/> en la notación de SPIRV-Cross: 50 = SM 5.0, 51 = SM 5.1, 60 = SM 6.0...</summary>
    public string ToHlsl(Stage stage, uint shaderModel = 50)
    {
        (string entryPoint, ExecutionModel executionModel) =
            GetEntryPoint(stage);

        var compiler = new CompilerHLSL(ByteArrToCppVector());

        compiler.set_entry_point(entryPoint, executionModel);
        
        CompilerHLSL.Options options = compiler.get_hlsl_options();
        options.use_entry_point_name = true;
        options.user_semantic = true;
        options.shader_model = shaderModel;
        compiler.set_hlsl_options(options);

        return compiler.compile().str();
    }

    /// <summary>
    /// GLSL for OpenGL (not Vulkan): no descriptor sets, and every texture paired with its sampler in one
    /// <c>sampler2D</c>-style uniform. <paramref name="version"/> is the <c>#version</c> line (330, 410, 450...).
    /// </summary>
    public string ToGlsl(Stage stage, uint version = 450) => ToOpenGlGlsl(stage, version, es: false);

    /// <summary>GLSL ES for OpenGL ES and WebGL, with the same rules as <see cref="ToGlsl"/>.</summary>
    public string ToGlslEs(Stage stage, uint version = 310) => ToOpenGlGlsl(stage, version, es: true);

    private string ToOpenGlGlsl(Stage stage, uint version, bool es)
    {
        (string entryPoint, YotsubaShaderCross.Spv.ExecutionModel executionModel) =
            GetEntryPoint(stage);

        var compiler = new CompilerGLSL(ByteArrToCppVector());

        compiler.set_entry_point(entryPoint, executionModel);

        CompilerGLSL.Options options = compiler.get_common_options();
        options.version = version;
        options.es = es;
        // OpenGL, not Vulkan: SPIR-V already covers Vulkan.
        options.vulkan_semantics = false;
        // Below 420 the binding qualifier does not exist; do not ask for the extension that adds it (macOS lacks it).
        options.enable_420pack_extension = false;
        // gl_InstanceID as OpenGL gives it, without an extra SPIRV_Cross_BaseInstance uniform the program would have to fill.
        options.vertex.support_nonzero_base_instance = false;

        // GLSL ejecutable requiere void main().
        options.use_entry_point_name = false;

        compiler.set_common_options(options);

        // OpenGL has no separate textures and samplers: join each pair into one combined sampler,
        // named after the texture and with the texture's binding.
        compiler.build_combined_image_samplers();
        foreach (CombinedImageSampler pair in compiler.get_combined_image_samplers())
        {
            compiler.set_name(
                new TypedID_TypeNone(pair.combined_id),
                compiler.get_name(new TypedID_TypeNone(pair.image_id)));
        }
        YotsubaShaderCross.Globals.inherit_combined_sampler_bindings(compiler);

        // Before GLSL 410 (and GLSL ES 310) the values that travel from the vertex to the fragment stage have no
        // location: OpenGL joins them by name. Then give each one the same name in both stages, from its location.
        bool varyingsHaveLocation = es ? version >= 310 : version >= 410;
        ShaderResources resources = compiler.get_shader_resources();
        CppVector<Resource>? varyings = varyingsHaveLocation ? null : stage switch
        {
            Stage.Vertex => resources.stage_outputs,
            Stage.Fragment => resources.stage_inputs,
            _ => null,
        };
        if (varyings is not null)
        {
            foreach (Resource varying in varyings)
            {
                if (compiler.has_decoration(varying.id, Decoration.DecorationLocation))
                {
                    uint location = compiler.get_decoration(varying.id, Decoration.DecorationLocation);
                    YotsubaShaderCross.Globals.rename_interface_variable(compiler, varyings, location, $"ys_varying{location}");
                }
            }
        }

        return compiler.compile().str();
    }

    public string ToCpp(Stage stage)
    {
        (string entryPoint, YotsubaShaderCross.Spv.ExecutionModel executionModel) =
            GetEntryPoint(stage);

        var compiler = new CompilerCPP(ByteArrToCppVector());

        compiler.set_entry_point(entryPoint, executionModel);

        return compiler.compile().str();
    }

    private (string EntryPoint, YotsubaShaderCross.Spv.ExecutionModel ExecutionModel)
        GetEntryPoint(Stage stage)
    {
        if (stage == Stage.None)
        {
            throw new ArgumentOutOfRangeException(
                nameof(stage),
                stage,
                "Stage.None no representa un entry point.");
        }

        Function[] functions = Reflection.IntermediateLanguage.Functions
            .Where(function => function.Stage == stage)
            .ToArray();

        return functions.Length switch
        {
            1 => (
                functions[0].HumanName,
                ToExecutionModel(functions[0].Stage)),

            0 => throw new InvalidOperationException(
                $"El shader '{CSharpFullClassName}' no tiene un entry point de stage '{stage}'."),

            _ => throw new InvalidOperationException(
                $"El shader '{CSharpFullClassName}' tiene {functions.Length} entry points " +
                $"para el stage '{stage}'. Seleccionar solo Stage no es suficiente.")
        };
    }

    private static YotsubaShaderCross.Spv.ExecutionModel ToExecutionModel(Stage stage)
    {
        return stage switch
        {
            Stage.Vertex => YotsubaShaderCross.Spv.ExecutionModel.ExecutionModelVertex,
            Stage.Fragment => YotsubaShaderCross.Spv.ExecutionModel.ExecutionModelFragment,
            Stage.Compute => YotsubaShaderCross.Spv.ExecutionModel.ExecutionModelGLCompute,
            _ => throw new ArgumentOutOfRangeException(nameof(stage), stage, null)
        };
    }
}