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
using System.Globalization;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace YotsubaShader.Compiler.CLI;

/// <summary>Versión del CLI (la del paquete NuGet, sin el hash del commit).</summary>
internal static class CliVersion
{
    public static string Informational { get; } =
        (typeof(CliVersion).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
         ?? typeof(CliVersion).Assembly.GetName().Version?.ToString(3)
         ?? "0.0.0").Split('+')[0];
}

/// <summary>
/// Un proyecto sin restaurar se abre sin referencias: <c>object</c>, <c>float</c>... no existen y cada línea da error.
/// Se detecta así (no por la ruta de <c>project.assets.json</c>, que depende de la configuración del proyecto) y se
/// restaura una vez con <c>dotnet restore</c>.
/// </summary>
internal static class ProjectRestore
{
    public static bool NeedsRestore(Compilation compilation) =>
        compilation.GetSpecialType(SpecialType.System_Object).TypeKind == TypeKind.Error;

    public static async Task<bool> RestoreAsync(string projectPath, string? msbuildPath, CancellationToken token)
    {
        Console.Error.WriteLine($"Restaurando {Path.GetFileName(projectPath)} (dotnet restore)...");

        var start = new ProcessStartInfo(FindDotnet(msbuildPath))
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        start.ArgumentList.Add("restore");
        start.ArgumentList.Add(projectPath);
        start.ArgumentList.Add("--verbosity");
        start.ArgumentList.Add("quiet");

        // MSBuildLocator apunta MSBuild de este proceso a un SDK con variables de entorno (MSBUILD_EXE_PATH,
        // MSBuildExtensionsPath, MSBuildSDKsPath); heredadas, hacen fallar al dotnet hijo.
        foreach (string variable in start.Environment.Keys
                     .Where(key => key.StartsWith("MSBuild", StringComparison.OrdinalIgnoreCase))
                     .ToArray())
        {
            start.Environment.Remove(variable);
        }

        try
        {
            using Process process = Process.Start(start)
                                    ?? throw new InvalidOperationException("no se pudo iniciar dotnet");
            Task<string> output = process.StandardOutput.ReadToEndAsync(token);
            Task<string> error = process.StandardError.ReadToEndAsync(token);
            await process.WaitForExitAsync(token);

            if (process.ExitCode == 0)
            {
                return true;
            }

            Console.Error.WriteLine((await output + await error).Trim());
        }
        catch (Exception exception) when (exception is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            Console.Error.WriteLine($"No se pudo ejecutar dotnet restore: {exception.Message}");
        }

        Console.Error.WriteLine($"El proyecto no se pudo restaurar. Ejecuta 'dotnet restore \"{projectPath}\"' y vuelve a intentarlo.");
        return false;
    }

    private static string FindDotnet(string? msbuildPath)
    {
        string executable = OperatingSystem.IsWindows() ? "dotnet.exe" : "dotnet";

        if (Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") is { Length: > 0 } host && File.Exists(host))
        {
            return host;
        }

        // `dotnet yotsuba ...`: el propio proceso es dotnet.
        if (Environment.ProcessPath is { } processPath &&
            Path.GetFileName(processPath).Equals(executable, StringComparison.OrdinalIgnoreCase))
        {
            return processPath;
        }

        // .../dotnet/sdk/10.0.100/ → .../dotnet/dotnet (el SDK que eligió MSBuildLocator).
        if (msbuildPath is not null &&
            Directory.GetParent(Path.TrimEndingDirectorySeparator(msbuildPath))?.Parent is { } root &&
            File.Exists(Path.Combine(root.FullName, executable)))
        {
            return Path.Combine(root.FullName, executable);
        }

        return executable;
    }
}

/// <summary>
/// El <c>YotsubaShader.g.cs</c> que escribe <c>yotsuba add</c> no forma parte de los shaders. Se quita de la compilación
/// antes de analizarla: si una clase shader cambió de nombre, el archivo anterior ya no compila y bloquearía su propia
/// regeneración.
/// </summary>
internal static class GeneratedAccessors
{
    public const string FileName = "YotsubaShader.g.cs";

    private const string ClassName = "YotsubaShader_ShadersCompilations";

    // Lo que genera `add` aunque no haya un archivo anterior (por ejemplo, si se borró a mano).
    private static readonly string[] KnownNames =
    [
        ClassName, "ToSpirV", "ToHlsl", "ToGlsl", "ToGlslEs", "ToMSL", "ToWgsl",
        "ToMGFX", "ToKNIWebGL_FX", "ToFNA_FX", "ToGodotShader", "MonoGamePlatform", "GodotShaderType"
    ];

    // Miembro, tipo o nombre inexistente; método de extensión sobre otro tipo.
    private static readonly HashSet<string> MissingMemberIds =
        ["CS1061", "CS0117", "CS0103", "CS0246", "CS0234", "CS1929", "CS0426"];

    public static async Task<(Project Project, IReadOnlySet<string> Names)> RemoveAsync(
        Project project,
        CancellationToken token)
    {
        var names = new HashSet<string>(KnownNames, StringComparer.Ordinal);

        foreach (Document document in project.Documents.ToArray())
        {
            if (!string.Equals(Path.GetFileName(document.FilePath), FileName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            SyntaxTree? tree = await document.GetSyntaxTreeAsync(token);
            string text = tree is null ? string.Empty : (await tree.GetTextAsync(token)).ToString();

            if (tree is null || !IsGenerated(text))
            {
                continue;
            }

            SyntaxNode root = await tree.GetRootAsync(token);

            foreach (MethodDeclarationSyntax method in root.DescendantNodes().OfType<MethodDeclarationSyntax>())
            {
                names.Add(method.Identifier.ValueText);
            }

            foreach (EnumDeclarationSyntax enumeration in root.DescendantNodes().OfType<EnumDeclarationSyntax>())
            {
                names.Add(enumeration.Identifier.ValueText);
            }

            project = project.RemoveDocument(document.Id);
        }

        return (project, names);
    }

    public static bool IsGenerated(string text) =>
        text.StartsWith("// <auto-generated />", StringComparison.Ordinal) &&
        text.Contains("Generated by Yotsuba Shader", StringComparison.Ordinal);

    /// <summary>Error de código que usa un método o enum del archivo generado, que todavía no existe o cambió.</summary>
    public static bool IsMissingGeneratedMember(Diagnostic diagnostic, IReadOnlySet<string> names)
    {
        if (!MissingMemberIds.Contains(diagnostic.Id))
        {
            return false;
        }

        string message = diagnostic.GetMessage(CultureInfo.InvariantCulture);
        return names.Any(name => message.Contains($"'{name}'", StringComparison.Ordinal));
    }
}

/// <summary>Mensajes de error de los plugins con la causa probable.</summary>
internal static class PluginErrors
{
    public static string Describe(Exception exception) => exception switch
    {
        MissingMethodException or MissingFieldException or TypeLoadException or FileNotFoundException or FileLoadException =>
            $"{exception.Message} (el plugin no es compatible con esta versión del CLI {CliVersion.Informational}; " +
            "ejecuta 'yotsuba plugin update' o actualiza el CLI)",
        DllNotFoundException or BadImageFormatException =>
            $"{exception.Message} (faltan las bibliotecas nativas del plugin para {System.Runtime.InteropServices.RuntimeInformation.RuntimeIdentifier})",
        _ => exception.Message
    };
}
