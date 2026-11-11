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

namespace YotsubaShader.CLI;

public enum Command
{
    Compile,
    Add
}

public enum ShaderTarget
{
    SpirV,
    Hlsl,
    Glsl,
    GlslEs,
    Msl,
    Cpp
}

public sealed class Args
{
    public Command Command { get; }
    public string ProjectPath { get; }
    public IReadOnlyList<ShaderTarget> Targets { get; }
    public string OutputDirectory { get; }

    public Args(string[] args)
    {
        if (args.Length == 0)
        {
            throw new CliUsageException("Falta un comando. Usa: yotsuba compile <proyecto.csproj>.");
        }

        Command = ParseCommand(args[0]);

        string? projectPath = null;
        string? outputDirectory = null;
        var targets = new List<ShaderTarget>();

        for (int index = 1; index < args.Length; index++)
        {
            string argument = args[index];

            switch (argument)
            {
                case "--target":
                    targets.Add(ParseTarget(ReadValue(args, ref index, "--target")));
                    break;

                case "--output":
                    outputDirectory = ReadValue(args, ref index, "--output");
                    break;

                default:
                    if (argument.StartsWith("--", StringComparison.Ordinal))
                    {
                        throw new CliUsageException(
                            $"Opción desconocida '{argument}'.");
                    }

                    if (projectPath is not null)
                    {
                        throw new CliUsageException(
                            $"Sólo se admite un proyecto. El valor extra es '{argument}'.");
                    }

                    projectPath = argument;
                    break;
            }
        }

        if (projectPath is null)
        {
            throw new CliUsageException(
                "Falta el proyecto .csproj.");
        }

        if (targets.Count == 0)
        {
            throw new CliUsageException(
                "Debes indicar al menos un --target.");
        }

        ProjectPath = Path.GetFullPath(projectPath);

        string projectDirectory =
            Path.GetDirectoryName(ProjectPath)
            ?? throw new CliUsageException(
                $"No se pudo resolver el directorio de '{projectPath}'.");

        OutputDirectory = Path.GetFullPath(
            outputDirectory ?? "artifacts",
            projectDirectory);

        Targets = targets
            .Distinct()
            .ToArray();
    }

    private static Command ParseCommand(string value) =>
        value.Equals("compile", StringComparison.OrdinalIgnoreCase)
            ? Command.Compile
            : value.Equals("add", StringComparison.OrdinalIgnoreCase) ?
                Command.Add:
            throw new CliUsageException($"Comando desconocido '{value}'.");

    private static ShaderTarget ParseTarget(string value) =>
        value.ToLowerInvariant() switch
        {
            "spirv" or "spv" => ShaderTarget.SpirV,
            "hlsl" => ShaderTarget.Hlsl,
            "glsl" => ShaderTarget.Glsl,
            "gles" or "glsles" => ShaderTarget.GlslEs,
            "msl" or "metal" => ShaderTarget.Msl,
            "cpp" => ShaderTarget.Cpp,
            _ => throw new CliUsageException(
                $"Target desconocido '{value}'.")
        };

    private static string ReadValue(
        string[] args,
        ref int index,
        string option)
    {
        if (++index >= args.Length ||
            args[index].StartsWith("--", StringComparison.Ordinal))
        {
            throw new CliUsageException(
                $"La opción '{option}' requiere un valor.");
        }

        return args[index];
    }
}

public sealed class CliUsageException(string message)
    : Exception(message);