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