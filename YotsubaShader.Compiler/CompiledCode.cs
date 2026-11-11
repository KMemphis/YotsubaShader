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

namespace YotsubaShader.Compiler;

public record CompiledCode(
    ShaderReflection Reflection,
    byte[] SpirV,
    string SpirVAs,
    string CSharpFullClassName)
{
    private CppVector<uint> ByteArrToCppVector()
    {
        ReadOnlySpan<uint> words =
            MemoryMarshal.Cast<byte, uint>(SpirV);

        return new CppVector<uint>(words.ToArray());
    }

    public string ToMsl(Stage stage)
    {
        (string entryPoint, ExecutionModel executionModel) =
            GetEntryPoint(stage);

        var compiler = new CompilerMSL(ByteArrToCppVector());

        compiler.set_entry_point(entryPoint, executionModel);

        return compiler.compile().str();
    }

    public string ToHlsl(Stage stage)
    {
        (string entryPoint, ExecutionModel executionModel) =
            GetEntryPoint(stage);

        var compiler = new CompilerHLSL(ByteArrToCppVector());

        compiler.set_entry_point(entryPoint, executionModel);
        
        CompilerHLSL.Options options = compiler.get_hlsl_options();
        options.use_entry_point_name = true;
        options.user_semantic = true;
        options.shader_model = 50;
        compiler.set_hlsl_options(options);

        return compiler.compile().str();
    }

    public string ToGlsl(Stage stage, uint version = 450)
    {
        (string entryPoint, YotsubaShaderCross.Spv.ExecutionModel executionModel) =
            GetEntryPoint(stage);

        var compiler = new CompilerGLSL(ByteArrToCppVector());

        compiler.set_entry_point(entryPoint, executionModel);

        CompilerGLSL.Options options = compiler.get_common_options();
        options.version = version;
        options.es = false;
        options.vulkan_semantics = true;

        // GLSL ejecutable requiere void main().
        options.use_entry_point_name = false;

        compiler.set_common_options(options);

        return compiler.compile().str();
    }

    public string ToGlslEs(Stage stage, uint version = 310)
    {
        (string entryPoint, YotsubaShaderCross.Spv.ExecutionModel executionModel) =
            GetEntryPoint(stage);

        var compiler = new CompilerGLSL(ByteArrToCppVector());

        compiler.set_entry_point(entryPoint, executionModel);

        CompilerGLSL.Options options = compiler.get_common_options();
        options.version = version;
        options.es = true;
        options.vulkan_semantics = true;

        // GLSL ES ejecutable requiere void main().
        options.use_entry_point_name = false;

        compiler.set_common_options(options);

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