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

namespace YotsubaShader.Compiler.CLI;

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
    Cpp,
    Wgsl
}

/// <summary>
/// Un destino pedido con <c>--target nombre[:versión]</c>. <see cref="Version"/> es la versión del lenguaje de salida
/// ya validada (null = la que el compilador usa por defecto).
/// </summary>
public readonly record struct TargetRequest(ShaderTarget Target, ShaderVersion? Version = null);

/// <summary>Versión de un lenguaje de salida. <see cref="Number"/> es el entero del lenguaje (430, 300, 51...).</summary>
public readonly record struct ShaderVersion(int Major, int Minor, int Patch, uint Number, string Text)
{
    public override string ToString() => Text;
}

public sealed class Args
{
    public Command Command { get; }
    public string ProjectPath { get; }
    public IReadOnlyList<TargetRequest> Targets { get; }

    /// <summary>Destinos que resuelve un plugin (monogame, kni, fna...).</summary>
    public IReadOnlyList<string> PluginTargets { get; }
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
        var targets = new List<TargetRequest>();
        var pluginTargets = new List<string>();

        for (int index = 1; index < args.Length; index++)
        {
            string argument = args[index];

            switch (argument)
            {
                case "--target":
                    AddTarget(ReadValue(args, ref index, "--target"), targets, pluginTargets);
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

        if (targets.Count == 0 && pluginTargets.Count == 0)
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

        // `--target glsl:430 --target glsl:460`: dos versiones del mismo destino necesitan nombres de archivo distintos.
        foreach (IGrouping<ShaderTarget, TargetRequest> group in Targets.GroupBy(request => request.Target))
        {
            if (group.Count() > 1 && group.Any(request => request.Version is null))
            {
                throw new CliUsageException(
                    $"El destino '{group.Key}' se pidió con y sin versión; indica la versión en todas las peticiones.");
            }
        }

        // `add` escribe un solo método por destino (ToGlslEs, ToHlsl...): no puede llevar dos versiones del mismo.
        if (Command == Command.Add &&
            Targets.GroupBy(request => request.Target).FirstOrDefault(group => group.Count() > 1) is { } repeated)
        {
            throw new CliUsageException(
                $"'add' genera un solo método por destino; pide una sola versión de '{repeated.Key.ToString().ToLowerInvariant()}'.");
        }

        PluginTargets = pluginTargets
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static Command ParseCommand(string value) =>
        value.Equals("compile", StringComparison.OrdinalIgnoreCase)
            ? Command.Compile
            : value.Equals("add", StringComparison.OrdinalIgnoreCase) ?
                Command.Add:
            throw new CliUsageException($"Comando desconocido '{value}'.");

    private static void AddTarget(
        string value,
        List<TargetRequest> targets,
        List<string> pluginTargets)
    {
        string? versionText = null;
        int colon = value.IndexOf(':');
        string targetName = value;
        if (colon >= 0)
        {
            targetName = value[..colon];
            versionText = value[(colon + 1)..];
        }

        if (TryParseBuiltInTarget(targetName, out ShaderTarget target))
        {
            targets.Add(new TargetRequest(
                target,
                versionText is null ? null : ParseVersion(target, versionText)));
            return;
        }

        if (versionText is not null)
        {
            throw new CliUsageException(
                $"El destino '{targetName}' no admite versión. Se puede usar con: spirv, glsl, gles, msl, hlsl.");
        }

        string name = value.ToLowerInvariant() switch
        {
            "mgfx" => "monogame",
            "kni-webgl" => "kni",
            "gdshader" => "godot",
            var other => other
        };

        if (name.Length == 0 ||
            !name.All(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_' or '.'))
        {
            throw new CliUsageException($"Target desconocido '{value}'.");
        }

        pluginTargets.Add(name);
    }

    /// <summary>
    /// Interpreta la versión de <c>--target nombre:versión</c>.
    /// spirv: 1.3 a 1.6 · glsl: 330, 430, 460... o 3.3, 4.3, 4.6 · gles: 300, 310, 320 o 3.0, 3.1, 3.2 ·
    /// msl: 2.0, 2.4, 3.1... · hlsl: 5.0, 5.1, 6.0... o 50, 51, 60 (modelo de shader).
    /// </summary>
    private static ShaderVersion ParseVersion(ShaderTarget target, string text)
    {
        string trimmed = text.Trim();
        string digits = trimmed.EndsWith("es", StringComparison.OrdinalIgnoreCase) ? trimmed[..^2] : trimmed;
        string[] parts = digits.Split('.');

        if (digits.Length == 0 ||
            parts.Length is < 1 or > 3 ||
            !parts.All(part => part.Length != 0 && part.All(char.IsAsciiDigit)))
        {
            throw new CliUsageException($"Versión inválida '{text}' para el destino '{target}'.");
        }

        int[] numbers = parts.Select(int.Parse).ToArray();

        switch (target)
        {
            case ShaderTarget.SpirV:
            {
                if (numbers.Length != 2 || numbers[0] != 1 || numbers[1] < 3 || numbers[1] > 6)
                {
                    throw new CliUsageException(
                        $"SPIR-V '{text}' no está soportado. Versiones: 1.3 (Vulkan 1.1), 1.4, 1.5 (Vulkan 1.2) y 1.6 (Vulkan 1.3).");
                }

                return new ShaderVersion(1, numbers[1], 0, (uint)(numbers[0] * 100 + numbers[1] * 10), $"1.{numbers[1]}");
            }

            case ShaderTarget.Glsl:
            case ShaderTarget.GlslEs:
            {
                // «430» o «4.3». En GLSL ES: «300»/«3.0», «310»/«3.1», «320»/«3.2».
                int number = numbers.Length == 1 ? numbers[0] : numbers[0] * 100 + numbers[1] * 10 + (numbers.Length == 3 ? numbers[2] : 0);
                int[] allowed = target == ShaderTarget.Glsl
                    ? [110, 120, 130, 140, 150, 330, 400, 410, 420, 430, 440, 450, 460]
                    : [100, 300, 310, 320];

                if (!allowed.Contains(number))
                {
                    throw new CliUsageException(
                        $"{(target == ShaderTarget.Glsl ? "GLSL" : "GLSL ES")} '{text}' no está soportado. Versiones: {string.Join(", ", allowed)}.");
                }

                return new ShaderVersion(number / 100, number / 10 % 10, number % 10, (uint)number, number.ToString());
            }

            case ShaderTarget.Msl:
            {
                int major = numbers[0];
                int minor = numbers.Length > 1 ? numbers[1] : 0;
                int patch = numbers.Length > 2 ? numbers[2] : 0;

                if (major < 1 || major > 4)
                {
                    throw new CliUsageException($"MSL '{text}' no está soportado. Versiones: 1.0 a 4.x (p. ej. 2.4, 3.1).");
                }

                return new ShaderVersion(major, minor, patch, 0, $"{major}.{minor}" + (patch != 0 ? $".{patch}" : string.Empty));
            }

            case ShaderTarget.Hlsl:
            {
                // «5.0», «5.1», «6.0»... o directamente «50», «51», «60».
                int model = numbers.Length >= 2 ? numbers[0] * 10 + numbers[1] : numbers[0];
                int[] allowed = [30, 40, 41, 50, 51, 60, 61, 62, 63, 64, 65, 66, 67, 68];

                if (!allowed.Contains(model))
                {
                    throw new CliUsageException(
                        $"HLSL SM '{text}' no está soportado. Modelos: 3.0, 4.0, 4.1, 5.0, 5.1, 6.0 a 6.8 (o 30, 40, 41, 50, 51, 60...).");
                }

                return new ShaderVersion(model / 10, model % 10, 0, (uint)model, $"{model / 10}.{model % 10}");
            }

            default:
                throw new CliUsageException($"El destino '{target}' no admite versión.");
        }
    }

    private static bool TryParseBuiltInTarget(string value, out ShaderTarget target)
    {
        ShaderTarget? parsed =
        value.ToLowerInvariant() switch
        {
            "spirv" or "spv" => ShaderTarget.SpirV,
            "hlsl" => ShaderTarget.Hlsl,
            "glsl" => ShaderTarget.Glsl,
            "gles" or "glsles" => ShaderTarget.GlslEs,
            "msl" or "metal" => ShaderTarget.Msl,
            "cpp" => ShaderTarget.Cpp,
            "wgsl" => ShaderTarget.Wgsl,
            _ => null
        };

        target = parsed.GetValueOrDefault();
        return parsed is not null;
    }

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