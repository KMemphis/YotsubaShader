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
        (string entryPoint, YotsubaShaderCross.Spv.ExecutionModel executionModel) =
            GetEntryPoint(stage);

        var compiler = new CompilerMSL(ByteArrToCppVector());

        compiler.set_entry_point(entryPoint, executionModel);

        return compiler.compile().str();
    }

    public string ToHlsl(Stage stage)
    {
        (string entryPoint, YotsubaShaderCross.Spv.ExecutionModel executionModel) =
            GetEntryPoint(stage);

        var compiler = new CompilerHLSL(ByteArrToCppVector());

        compiler.set_entry_point(entryPoint, executionModel);

        CompilerHLSL.Options options = compiler.get_hlsl_options();
        options.use_entry_point_name = true;
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