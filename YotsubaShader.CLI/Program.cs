using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;
using YotsubaShader.CLI;
using YotsubaShader.Compiler;
using YotsubaShader.IR.Intermediate;

Args parameters = new Args(args);

if (!File.Exists(parameters.ProjectPath))
{
    throw new FileNotFoundException(
        "El proyecto no existe.",
        parameters.ProjectPath);
}

if (!MSBuildLocator.IsRegistered)
{
    MSBuildLocator.RegisterDefaults();
}

CancellationToken token = CancellationToken.None;

using var workspace = MSBuildWorkspace.Create();

workspace.RegisterWorkspaceFailedHandler(eventArgs =>
    Console.Error.WriteLine(
        $"MSBuild: {eventArgs.Diagnostic.Message}"));

Project project = await workspace.OpenProjectAsync(
    parameters.ProjectPath,
    cancellationToken: token);

Compilation compilation = await project.GetCompilationAsync(token)
    ?? throw new InvalidOperationException(
        $"No se pudo crear la compilación de '{parameters.ProjectPath}'.");

Diagnostic[] errors = compilation
    .GetDiagnostics(token)
    .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
    .ToArray();

if (errors.Length != 0)
{
    foreach (Diagnostic error in errors)
    {
        Console.Error.WriteLine(error);
    }

    Environment.ExitCode = 1;
    return;
}

List<CompiledCode> shaders = CSharpToSpirvCompiler.Compile(
    compilation,
    token);

switch (parameters.Command)
{
    case Command.Compile:
        Directory.CreateDirectory(parameters.OutputDirectory);

        foreach (CompiledCode compiledCode in shaders)
        {
            foreach (ShaderTarget target in parameters.Targets)
            {
                if (target == ShaderTarget.SpirV)
                {
                    string spirvPath = GetShaderPath(
                        compiledCode,
                        target,
                        parameters,
                        stage: null);

                    CreateParentDirectory(spirvPath);

                    await File.WriteAllBytesAsync(
                        spirvPath,
                        compiledCode.SpirV,
                        token);

                    continue;
                }

                foreach (Stage stage in GetStages(compiledCode))
                {
                    string shaderPath = GetShaderPath(
                        compiledCode,
                        target,
                        parameters,
                        stage);

                    CreateParentDirectory(shaderPath);

                    string transpilation = GetTranspilation(
                        target,
                        compiledCode,
                        stage);

                    await File.WriteAllTextAsync(
                        shaderPath,
                        transpilation,
                        token);
                }
            }
        }

        break;

    default:
        throw new ArgumentOutOfRangeException(
            nameof(parameters.Command),
            parameters.Command,
            "Comando no soportado.");
}

static IEnumerable<Stage> GetStages(CompiledCode compiledCode)
{
    return compiledCode.Reflection.IntermediateLanguage.Functions
        .Select(function => function.Stage)
        .Where(stage => stage != Stage.None)
        .Distinct();
}

static string GetShaderPath(
    CompiledCode compiledCode,
    ShaderTarget target,
    Args parameters,
    Stage? stage)
{
    string relativePath = compiledCode.CSharpFullClassName.Replace(
        '.',
        Path.DirectorySeparatorChar);

    string stageSuffix = stage is null
        ? string.Empty
        : $".{stage.Value}";

    return Path.Combine(
        parameters.OutputDirectory,
        relativePath + stageSuffix + GetExtension(target));
}

static void CreateParentDirectory(string filePath)
{
    string? directory = Path.GetDirectoryName(filePath);

    if (directory is not null)
    {
        Directory.CreateDirectory(directory);
    }
}

static string GetTranspilation(
    ShaderTarget target,
    CompiledCode compiledCode,
    Stage stage)
{
    return target switch
    {
        ShaderTarget.Cpp => compiledCode.ToCpp(stage),
        ShaderTarget.Glsl => compiledCode.ToGlsl(stage),
        ShaderTarget.GlslEs => compiledCode.ToGlslEs(stage),
        ShaderTarget.Hlsl => compiledCode.ToHlsl(stage),
        ShaderTarget.Msl => compiledCode.ToMsl(stage),

        ShaderTarget.SpirV => throw new InvalidOperationException(
            "SPIR-V binario se escribe directamente como bytes."),

        _ => throw new ArgumentOutOfRangeException(
            nameof(target),
            target,
            "Target no soportado.")
    };
}

static string GetExtension(ShaderTarget target)
{
    return target switch
    {
        ShaderTarget.SpirV => ".spv",
        ShaderTarget.Cpp => ".cpp",
        ShaderTarget.Glsl => ".glsl",
        ShaderTarget.GlslEs => ".gles",
        ShaderTarget.Hlsl => ".hlsl",
        ShaderTarget.Msl => ".msl",

        _ => throw new ArgumentOutOfRangeException(
            nameof(target),
            target,
            "Target no soportado.")
    };
}