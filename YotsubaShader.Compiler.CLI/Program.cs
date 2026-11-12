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

using System.Text;
using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;
using YotsubaShader.Compiler.CLI;
using YotsubaShader.Compiler.Library;
using YotsubaShader.Compiler.Library.CompileToGameEngines;
using YotsubaShader.IR.Intermediate;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;


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
                if (IsBinaryTarget(target))
                {
                    foreach ((string extension, Func<byte[]> build) in GetBinaries(target, compiledCode))
                    {
                        string binaryPath = GetShaderPath(
                            compiledCode,
                            parameters,
                            extension,
                            stage: null);

                        CreateParentDirectory(binaryPath);

                        try
                        {
                            await File.WriteAllBytesAsync(binaryPath, build(), token);
                        }
                        catch (Exception exception) when (target != ShaderTarget.SpirV)
                        {
                            Console.Error.WriteLine(
                                $"{compiledCode.CSharpFullClassName}{extension}: {exception.Message}");
                            Environment.ExitCode = 1;
                        }
                    }

                    continue;
                }

                foreach (Stage stage in GetStages(compiledCode))
                {
                    string shaderPath = GetShaderPath(
                        compiledCode,
                        parameters,
                        GetExtension(target),
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

    case Command.Add:
        var stringBuilder = new StringBuilder();

        stringBuilder
            .AppendLine("// <auto-generated />")
            .AppendLine("// Generated by Yotsuba Shader.")
            .AppendLine("#nullable enable")
            .AppendLine()
            .AppendLine("namespace YotsubaShader;")
            .AppendLine()
            .AppendLine("public enum MonoGamePlatform")
            .AppendLine("{")
            .AppendLine(string.Join(
                ",\n",
                Enum.GetNames<MonoGamePlatform>().Select(name => "    " + name)))
            .AppendLine("}")
            .AppendLine()
            .AppendLine("[global::System.Runtime.CompilerServices.CompilerGenerated]")
            .AppendLine("[global::System.ComponentModel.EditorBrowsable(")
            .AppendLine("    global::System.ComponentModel.EditorBrowsableState.Never)]")
            .AppendLine("public static class YotsubaShader_ShadersCompilations")
            .AppendLine("{")
            .AppendLine();

        foreach (CompiledCode compiledCode in shaders)
        {
            string shaderType =
                $"global::{compiledCode.CSharpFullClassName}";

            AppendToSpirVMethod(
                stringBuilder,
                shaderType,
                compiledCode);

            AppendTranspilationMethod(
                stringBuilder,
                shaderType,
                compiledCode,
                "ToHlsl",
                stage => compiledCode.ToHlsl(stage));

            AppendTranspilationMethod(
                stringBuilder,
                shaderType,
                compiledCode,
                "ToGlsl",
                stage => compiledCode.ToGlsl(stage));

            AppendTranspilationMethod(
                stringBuilder,
                shaderType,
                compiledCode,
                "ToGlslEs",
                stage => compiledCode.ToGlslEs(stage));

            AppendTranspilationMethod(
                stringBuilder,
                shaderType,
                compiledCode,
                "ToMSL",
                stage => compiledCode.ToMsl(stage));

            AppendToMgfxMethod(stringBuilder, shaderType, compiledCode);

            AppendEngineMethod(
                stringBuilder,
                shaderType,
                compiledCode,
                "ToKNIWebGL_FX",
                () => compiledCode.ToKNIWebGL_FX());

            AppendEngineMethod(
                stringBuilder,
                shaderType,
                compiledCode,
                "ToFNA_FX",
                () => compiledCode.ToFNA_FX());
        }

        stringBuilder.AppendLine("}");

        string projectDirectory =
            Path.GetDirectoryName(parameters.ProjectPath)
            ?? throw new InvalidOperationException(
                "No se encontró el directorio del proyecto.");

        string generatedFilePath = Path.Combine(
            projectDirectory,
            "YotsubaShader.g.cs");

        await File.WriteAllTextAsync(
            generatedFilePath,
            stringBuilder.ToString(),
            token);

        Console.WriteLine(
            $"Archivo generado: {generatedFilePath}");

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
    Args parameters,
    string extension,
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
        relativePath + stageSuffix + extension);
}

static void CreateParentDirectory(string filePath)
{
    string? directory = Path.GetDirectoryName(filePath);

    if (directory is not null)
    {
        Directory.CreateDirectory(directory);
    }
}

static bool IsBinaryTarget(ShaderTarget target)
{
    return target is ShaderTarget.SpirV
        or ShaderTarget.MonoGame
        or ShaderTarget.Kni
        or ShaderTarget.Fna;
}

// Cada binario se construye de forma diferida para poder reportar el error de un
// solo archivo sin abortar el resto.
static IEnumerable<(string Extension, Func<byte[]> Build)> GetBinaries(
    ShaderTarget target,
    CompiledCode compiledCode)
{
    switch (target)
    {
        case ShaderTarget.SpirV:
            yield return (".spv", () => compiledCode.SpirV);
            break;

        case ShaderTarget.MonoGame:
            foreach (MonoGamePlatform platform in Enum.GetValues<MonoGamePlatform>())
            {
                MonoGamePlatform current = platform;
                yield return (
                    $".{current.ToString().ToLowerInvariant()}.mgfx",
                    () => compiledCode.ToMGFX(current));
            }

            break;

        case ShaderTarget.Kni:
            yield return (".knifx", () => compiledCode.ToKNIWebGL_FX());
            break;

        case ShaderTarget.Fna:
            yield return (".fxb", () => compiledCode.ToFNA_FX());
            break;

        default:
            throw new ArgumentOutOfRangeException(
                nameof(target),
                target,
                "Target binario no soportado.");
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

// Si ShadowDusk no puede compilar un target, el método generado lanza con el motivo
// en lugar de abortar todo el comando "add".
static string BuildEngineBody(string description, Func<byte[]> build, string indentation, string returnPrefix)
{
    try
    {
        string base64 = Convert.ToBase64String(build());
        return $"{indentation}{returnPrefix}global::System.Convert.FromBase64String(\"{base64}\");";
    }
    catch (Exception exception)
    {
        Console.Error.WriteLine($"{description}: {exception.Message}");

        string message = Microsoft.CodeAnalysis.CSharp.SymbolDisplay.FormatLiteral(
            $"{description}: {exception.Message}",
            quote: true);

        return $"{indentation}throw new global::System.NotSupportedException({message});";
    }
}

static void AppendEngineMethod(
    StringBuilder builder,
    string shaderType,
    CompiledCode compiledCode,
    string methodName,
    Func<byte[]> build)
{
    builder
        .AppendLine($"    public static byte[] {methodName}(this {shaderType} _shader)")
        .AppendLine("    {")
        .AppendLine(BuildEngineBody(
            $"{compiledCode.CSharpFullClassName}.{methodName}",
            build,
            "        ",
            "return "))
        .AppendLine("    }")
        .AppendLine();
}

static void AppendToMgfxMethod(
    StringBuilder builder,
    string shaderType,
    CompiledCode compiledCode)
{
    builder
        .AppendLine($"    public static byte[] ToMGFX(")
        .AppendLine($"        this {shaderType} _shader,")
        .AppendLine("        global::YotsubaShader.MonoGamePlatform platform)")
        .AppendLine("    {")
        .AppendLine("        switch (platform)")
        .AppendLine("        {");

    // Las plataformas que producen exactamente los mismos bytes comparten un solo literal.
    var bodies = new List<(string Body, List<MonoGamePlatform> Platforms)>();

    foreach (MonoGamePlatform platform in Enum.GetValues<MonoGamePlatform>())
    {
        string body = BuildEngineBody(
            $"{compiledCode.CSharpFullClassName}.ToMGFX({platform})",
            () => compiledCode.ToMGFX(platform),
            "                ",
            "return ");

        int existing = bodies.FindIndex(entry => entry.Body == body);

        if (existing >= 0)
        {
            bodies[existing].Platforms.Add(platform);
        }
        else
        {
            bodies.Add((body, [platform]));
        }
    }

    foreach ((string body, List<MonoGamePlatform> platforms) in bodies)
    {
        foreach (MonoGamePlatform platform in platforms)
        {
            builder.AppendLine(
                $"            case global::YotsubaShader.MonoGamePlatform.{platform}:");
        }

        builder.AppendLine(body);
    }

    builder
        .AppendLine("            default:")
        .AppendLine("                throw new global::System.ArgumentOutOfRangeException(")
        .AppendLine("                    nameof(platform),")
        .AppendLine("                    platform,")
        .AppendLine("                    \"Plataforma de MonoGame no soportada.\");")
        .AppendLine("        }")
        .AppendLine("    }")
        .AppendLine();
}

static void AppendToSpirVMethod(
    StringBuilder builder,
    string shaderType,
    CompiledCode compiledCode)
{
    string base64 = Convert.ToBase64String(
        compiledCode.SpirV);

    builder
        .AppendLine(
            $"    public static byte[] ToSpirV(this {shaderType} _shader)")
        .AppendLine("    {")
        .AppendLine(
            $"        return global::System.Convert.FromBase64String(\"{base64}\");")
        .AppendLine("    }")
        .AppendLine();
}
static void AppendTranspilationMethod(
    StringBuilder builder,
    string shaderType,
    CompiledCode compiledCode,
    string methodName,
    Func<Stage, string> getTranspilation)
{
    builder
        .AppendLine($"    public static string {methodName}(")
        .AppendLine($"        this {shaderType} _shader,")
        .AppendLine(
            "        global::YotsubaShader.Language.Stage stage)")
        .AppendLine("    {")
        .AppendLine("        return stage switch")
        .AppendLine("        {");

    foreach (Stage irStage in GetStages(compiledCode)
                 .OrderBy(stage => stage))
    {
        string transpilation = getTranspilation(irStage);

        builder.AppendLine(
            $"            global::YotsubaShader.Language.Stage.{irStage} =>");

        AppendRawStringLiteral(
            builder,
            transpilation,
            "                ");

        builder.AppendLine(",");
    }

    builder
        .AppendLine(
            "            _ => throw new global::System.ArgumentOutOfRangeException(")
        .AppendLine("                nameof(stage),")
        .AppendLine("                stage,")
        .AppendLine(
            $"                \"Stage no soportado para {methodName}.\"),")
        .AppendLine("        };")
        .AppendLine("    }")
        .AppendLine();
}

static void AppendRawStringLiteral(
    StringBuilder builder,
    string value,
    string indentation)
{
    if (value.Length == 0)
    {
        builder.AppendLine($"{indentation}\"\"");
        return;
    }

    value = value
        .Replace("\r\n", "\n")
        .Replace('\r', '\n');

    int quoteCount = 3;
    string delimiter = new('"', quoteCount);

    while (value.Contains(delimiter, StringComparison.Ordinal))
    {
        quoteCount++;
        delimiter = new string('"', quoteCount);
    }

    builder
        .Append(indentation)
        .Append(delimiter)
        .AppendLine();

    bool endsWithNewLine =
        value.EndsWith("\n", StringComparison.Ordinal);

    string[] lines = value.Split('\n');

    int lineCount = endsWithNewLine
        ? lines.Length - 1
        : lines.Length;

    for (int index = 0; index < lineCount; index++)
    {
        builder
            .Append(indentation)
            .AppendLine(lines[index]);
    }

    builder
        .Append(indentation)
        .Append(delimiter)
        .AppendLine();
}