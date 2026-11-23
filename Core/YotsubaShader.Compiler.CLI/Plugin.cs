using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Loader;
using System.Text.Json.Serialization;
using YotsubaShader.Extensions;

namespace YotsubaShader.Compiler.CLI;

/// <summary>Entrada del catálogo de plugins publicado en la web (plugins.json).</summary>
public sealed class PluginMetadata
{
    public string Name { get; set; } = "";

    public string Description { get; set; } = "";

    /// <summary>Id del paquete NuGet que contiene la DLL del plugin.</summary>
    public string Package { get; set; } = "";

    /// <summary>Extensión del archivo que produce el backend (por ejemplo ".mgfx").</summary>
    public string Extension { get; set; } = ".bin";

    /// <summary>Nombre del método generado por "yotsuba add" (por ejemplo "ToMGFX").</summary>
    public string? Method { get; set; }

    /// <summary>Enum generado por "yotsuba add" cuando el backend tiene opciones.</summary>
    public string? OptionsEnum { get; set; }

    /// <summary>Nombre del parámetro del método generado (por ejemplo "platform"). Por defecto "option".</summary>
    public string? OptionsParameter { get; set; }

    /// <summary>
    /// El backend produce texto UTF-8 (por ejemplo un .gdshader). El método generado por "yotsuba add"
    /// devuelve <c>string</c> en lugar de <c>byte[]</c>.
    /// </summary>
    public bool Text { get; set; }

    [JsonIgnore]
    public string OptionsParameterName => string.IsNullOrWhiteSpace(OptionsParameter) ? "option" : OptionsParameter;

    /// <summary>Valores que se pasan como "options" a <see cref="IShaderTargetBackend.Compile"/>.</summary>
    public PluginOption[] Options { get; set; } = [];

    [JsonIgnore]
    public string MethodName => string.IsNullOrWhiteSpace(Method)
        ? "To" + char.ToUpperInvariant(Name[0]) + Name[1..]
        : Method;
}

public sealed class PluginOption
{
    public string Name { get; set; } = "";

    public uint Value { get; set; }
}

/// <summary>Estado persistido de un plugin instalado (plugin.json dentro de su carpeta).</summary>
public sealed class InstalledPlugin
{
    public string Name { get; set; } = "";

    public string Package { get; set; } = "";

    public string Version { get; set; } = "";

    public string Assembly { get; set; } = "";
}

/// <summary>Plugin ya cargado en memoria, listo para compilar.</summary>
public sealed class Plugin
{
    public Plugin(PluginMetadata metadata, string version, IShaderTargetBackend backend)
    {
        Metadata = metadata;
        Version = version;
        Backend = backend;
    }

    public PluginMetadata Metadata { get; }

    public string Version { get; }

    public IShaderTargetBackend Backend { get; }
}

/// <summary>
/// Carga la DLL del plugin y sus dependencias desde su carpeta. Lo que el host ya
/// tiene (por ejemplo YotsubaShader.Extensions) se comparte para que
/// <see cref="IShaderTargetBackend"/> sea el mismo tipo en ambos lados.
/// </summary>
internal sealed class PluginLoadContext(string directory) : AssemblyLoadContext(isCollectible: false)
{
    protected override Assembly? Load(AssemblyName assemblyName)
    {
        // El contrato con el host (YotsubaShader.Extensions, IR, YotsubaShaderCross...) tiene que ser el mismo ensamblado
        // en los dos lados, aunque el plugin se compilara contra una revisión distinta.
        if (assemblyName.Name is { } simpleName &&
            simpleName.StartsWith("YotsubaShader", StringComparison.Ordinal) &&
            (Default.Assemblies.FirstOrDefault(loaded => loaded.GetName().Name == simpleName) ??
             (File.Exists(Path.Combine(AppContext.BaseDirectory, simpleName + ".dll"))
                 ? Default.LoadFromAssemblyPath(Path.Combine(AppContext.BaseDirectory, simpleName + ".dll"))
                 : null)) is { } shared)
        {
            return shared;
        }

        try
        {
            return Default.LoadFromAssemblyName(assemblyName);
        }
        catch (Exception exception) when (exception is FileNotFoundException or FileLoadException)
        {
        }

        string path = Path.Combine(directory, assemblyName.Name + ".dll");
        return File.Exists(path) ? LoadFromAssemblyPath(path) : null;
    }

    protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
    {
        string nativeDirectory = Path.Combine(directory, "native");

        if (!Directory.Exists(nativeDirectory))
        {
            return IntPtr.Zero;
        }

        string[] candidates =
        [
            unmanagedDllName,
            unmanagedDllName + ".dll",
            unmanagedDllName + ".dylib",
            unmanagedDllName + ".so",
            "lib" + unmanagedDllName + ".dylib",
            "lib" + unmanagedDllName + ".so"
        ];

        foreach (string candidate in candidates)
        {
            string path = Path.Combine(nativeDirectory, candidate);

            if (File.Exists(path))
            {
                return NativeLibrary.Load(path);
            }
        }

        return IntPtr.Zero;
    }
}
