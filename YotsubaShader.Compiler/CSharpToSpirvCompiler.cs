using Microsoft.CodeAnalysis;
using YotsubaShader.Compiler.SpirV;
using YotsubaShader.Compiler.SpirV.Builder;
using YotsubaShader.IR.Intermediate;
using YotsubaShader.Roslyn;

namespace YotsubaShader.Compiler;

public sealed class CSharpToSpirvCompiler
{
    
    public static List<CompiledCode> Compile(Compilation compilation, CancellationToken cancellationToken = default)
    {
        var shaderProcessor = new ShaderProcessor();
        var shaders = shaderProcessor.Process(compilation, cancellationToken);
        var byteCodes = new List<CompiledCode>(shaders.Length);

        var compiler = new SpirVCompiler();

        var span = shaders.AsSpan();
        if (span.IsEmpty) return byteCodes;
        foreach (var shader in span)
        {        
            var spirVModule = new IRToSpirvModule().Compile(shader.Item2);
            ShaderReflection reflection = new ShaderReflection(shader.Item2);
            CompiledCode compiledCode = new CompiledCode(reflection, compiler.Compile(spirVModule), spirVModule.ToSpirVAs,
                shader.Item1);
            byteCodes.Add(compiledCode);
        }

        return byteCodes;
    }
}