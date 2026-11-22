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
using YotsubaShader.IR.Intermediate;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using YotsubaShader.Extensions;


const string usage = """
                     Uso:
                       yotsuba compile <proyecto.csproj> --target <destino> [--target <destino> ...] [--output <directorio>]
                       yotsuba add <proyecto.csproj> --target <destino> [--target <destino> ...]
                       yotsuba plugin list | install <nombre> | update [nombre] | remove <nombre>
                       yotsuba lsp <proyecto.csproj>     (servidor de lenguaje por stdio: diagnósticos en vivo para editores)

                     Destinos incluidos: spirv, hlsl, glsl, gles, msl, cpp.
                     Destinos por plugin (se instalan solos desde NuGet, última versión): monogame, kni, fna, godot.
                     Ver más con: yotsuba plugin list
                     Usa --help para mostrar esta ayuda.
                     Ejecutable del Release: ./YotsubaShader.Compiler.CLI (Linux/macOS) o YotsubaShader.Compiler.CLI.exe (Windows).
                     """;

if (args.Length > 0 && args[0].Equals("plugin", StringComparison.OrdinalIgnoreCase))
{
    Environment.ExitCode = await PluginCommand.RunAsync(args[1..]);
    return;
}

if (args.Length > 0 && args[0].Equals("lsp", StringComparison.OrdinalIgnoreCase))
{
    Environment.ExitCode = await LanguageServer.RunAsync(args[1..]);
    return;
}

bool topLevelHelp = args.Length == 1 &&
    (args[0] is "--help" or "-h" or "help");
bool commandHelp = args.Length == 2 &&
    (args[0] is "compile" or "add") &&
    (args[1] is "--help" or "-h");

if (topLevelHelp || commandHelp)
{
    Console.WriteLine(usage);
    return;
}

Args parameters;
try
{
    parameters = new Args(args);
}
catch (CliUsageException exception)
{
    Console.Error.WriteLine(exception.Message);
    Console.Error.WriteLine("Usa --help para ver los comandos y destinos disponibles.");
    Environment.ExitCode = 2;
    return;
}

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

        if (parameters.PluginTargets.Count != 0)
        {
            using var pluginManager = new PluginManager();

            foreach (string pluginName in parameters.PluginTargets)
            {
                Plugin plugin;

                try
                {
                    plugin = await pluginManager.LoadAsync(pluginName, token);
                }
                catch (PluginException exception)
                {
                    Console.Error.WriteLine(exception.Message);
                    Environment.ExitCode = 1;
                    continue;
                }

                foreach (CompiledCode compiledCode in shaders)
                {
                    foreach (PluginOption? option in GetOptions(plugin.Metadata))
                    {
                        string suffix = option is null
                            ? plugin.Metadata.Extension
                            : $".{option.Name.ToLowerInvariant()}{plugin.Metadata.Extension}";

                        string pluginPath = GetShaderPath(
                            compiledCode,
                            parameters,
                            suffix,
                            stage: null);

                        try
                        {
                            byte[] bytes = plugin.Backend.Compile(compiledCode, option?.Value ?? 0);
                            CreateParentDirectory(pluginPath);
                            await File.WriteAllBytesAsync(pluginPath, bytes, token);
                        }
                        catch (Exception exception)
                        {
                            Console.Error.WriteLine(
                                $"{compiledCode.CSharpFullClassName}{suffix}: {exception.Message}");
                            Environment.ExitCode = 1;
                        }
                    }
                }
            }
        }

        break;

    case Command.Add:
        var loadedPlugins = new List<Plugin>();

        if (parameters.PluginTargets.Count != 0)
        {
            using var addPluginManager = new PluginManager();

            foreach (string pluginName in parameters.PluginTargets)
            {
                try
                {
                    loadedPlugins.Add(await addPluginManager.LoadAsync(pluginName, token));
                }
                catch (PluginException exception)
                {
                    Console.Error.WriteLine(exception.Message);
                    Environment.ExitCode = 1;
                }
            }
        }

        var stringBuilder = new StringBuilder();

        stringBuilder
            .AppendLine("// <auto-generated />")
            .AppendLine("// Generated by Yotsuba Shader.")
            .AppendLine("#nullable enable")
            .AppendLine()
            .AppendLine("namespace YotsubaShader;")
            .AppendLine();

        foreach (PluginMetadata optionsMetadata in loadedPlugins
                     .Select(plugin => plugin.Metadata)
                     .Where(metadata => !string.IsNullOrEmpty(metadata.OptionsEnum) && metadata.Options.Length != 0)
                     .DistinctBy(metadata => metadata.OptionsEnum))
        {
            stringBuilder
                .AppendLine($"public enum {optionsMetadata.OptionsEnum}")
                .AppendLine("{")
                .AppendLine(string.Join(
                    ",\n",
                    optionsMetadata.Options.Select(option => $"    {option.Name} = {option.Value}")))
                .AppendLine("}")
                .AppendLine();
        }

        stringBuilder
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

            foreach (Plugin plugin in loadedPlugins)
            {
                AppendPluginMethod(stringBuilder, shaderType, compiledCode, plugin);
            }
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
    return target is ShaderTarget.SpirV;
}

// Cada binario se construye de forma diferida para poder reportar el error de un
// solo archivo sin abortar el resto.
static IEnumerable<(string Extension, Func<byte[]> Build)> GetBinaries(
    ShaderTarget target,
    CompiledCode compiledCode)
{
    if (target != ShaderTarget.SpirV)
    {
        throw new ArgumentOutOfRangeException(
            nameof(target),
            target,
            "Target binario no soportado.");
    }

    yield return (".spv", () => compiledCode.SpirV);
}

// Un plugin sin opciones produce un solo archivo; con opciones, uno por cada una.
static IEnumerable<PluginOption?> GetOptions(PluginMetadata metadata)
{
    if (metadata.Options.Length == 0)
    {
        yield return null;
        yield break;
    }

    foreach (PluginOption option in metadata.Options)
    {
        yield return option;
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
static string BuildEngineBody(string description, Func<byte[]> build, string indentation, string returnPrefix, bool text = false)
{
    try
    {
        byte[] bytes = build();

        if (text)
        {
            string literal = Microsoft.CodeAnalysis.CSharp.SymbolDisplay.FormatLiteral(
                System.Text.Encoding.UTF8.GetString(bytes),
                quote: true);
            return $"{indentation}{returnPrefix}{literal};";
        }

        string base64 = Convert.ToBase64String(bytes);
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

static void AppendPluginMethod(
    StringBuilder builder,
    string shaderType,
    CompiledCode compiledCode,
    Plugin plugin)
{
    PluginMetadata metadata = plugin.Metadata;
    bool hasOptions = metadata.Options.Length != 0 && !string.IsNullOrEmpty(metadata.OptionsEnum);
    string returnType = metadata.Text ? "string" : "byte[]";

    if (!hasOptions)
    {
        builder
            .AppendLine($"    public static {returnType} {metadata.MethodName}(this {shaderType} _shader)")
            .AppendLine("    {")
            .AppendLine(BuildEngineBody(
                $"{compiledCode.CSharpFullClassName}.{metadata.MethodName}",
                () => plugin.Backend.Compile(compiledCode, 0),
                "        ",
                "return ",
                metadata.Text))
            .AppendLine("    }")
            .AppendLine();
        return;
    }

    builder
        .AppendLine($"    public static {returnType} {metadata.MethodName}(")
        .AppendLine($"        this {shaderType} _shader,")
        .AppendLine($"        global::YotsubaShader.{metadata.OptionsEnum} {metadata.OptionsParameterName})")
        .AppendLine("    {")
        .AppendLine($"        switch ({metadata.OptionsParameterName})")
        .AppendLine("        {");

    // Las opciones que producen exactamente los mismos bytes comparten un solo literal.
    var bodies = new List<(string Body, List<PluginOption> Options)>();

    foreach (PluginOption option in metadata.Options)
    {
        string body = BuildEngineBody(
            $"{compiledCode.CSharpFullClassName}.{metadata.MethodName}({option.Name})",
            () => plugin.Backend.Compile(compiledCode, option.Value),
            "                ",
            "return ",
            metadata.Text);

        int existing = bodies.FindIndex(entry => entry.Body == body);

        if (existing >= 0)
        {
            bodies[existing].Options.Add(option);
        }
        else
        {
            bodies.Add((body, [option]));
        }
    }

    foreach ((string body, List<PluginOption> options) in bodies)
    {
        foreach (PluginOption option in options)
        {
            builder.AppendLine(
                $"            case global::YotsubaShader.{metadata.OptionsEnum}.{option.Name}:");
        }

        builder.AppendLine(body);
    }

    builder
        .AppendLine("            default:")
        .AppendLine("                throw new global::System.ArgumentOutOfRangeException(")
        .AppendLine($"                    nameof({metadata.OptionsParameterName}),")
        .AppendLine($"                    {metadata.OptionsParameterName},")
        .AppendLine($"                    \"Opción no soportada para {metadata.MethodName}.\");")
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
