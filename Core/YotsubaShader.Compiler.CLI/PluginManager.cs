using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Xml.Linq;
using YotsubaShader.Extensions;

namespace YotsubaShader.Compiler.CLI;

/// <summary>
/// Gestiona los plugins del CLI: el catálogo vive en un JSON en la web y cada plugin
/// es un paquete NuGet del que siempre se usa la última versión publicada.
/// </summary>
public sealed class PluginManager : IDisposable
{
    public const string DefaultCatalogUrl = "https://yotsubashader.com/plugins.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    private readonly string NuGetFlatContainer;
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(30) };
    private readonly string _home;
    private readonly string _catalogSource;
    private PluginMetadata[]? _catalog;

    public PluginManager()
    {
        _home = Environment.GetEnvironmentVariable("YOTSUBA_HOME") is { Length: > 0 } home
            ? home
            : Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".yotsuba");

        _catalogSource = Environment.GetEnvironmentVariable("YOTSUBA_PLUGINS_URL") is { Length: > 0 } url
            ? url
            : DefaultCatalogUrl;

        // Feed NuGet (v3 flat container). Se puede sustituir para pruebas o feeds privados.
        NuGetFlatContainer = Environment.GetEnvironmentVariable("YOTSUBA_NUGET_SOURCE") is { Length: > 0 } feed
            ? feed.TrimEnd('/') + "/"
            : "https://api.nuget.org/v3-flatcontainer/";

        _http.DefaultRequestHeaders.UserAgent.ParseAdd("yotsuba-cli");
    }

    private string PluginsDirectory => Path.Combine(_home, "plugins");

    private string PluginDirectory(string name) => Path.Combine(PluginsDirectory, name);

    public void Dispose() => _http.Dispose();

    // ---------------------------------------------------------------- catálogo

    public async Task<IReadOnlyList<PluginMetadata>> GetCatalogAsync(CancellationToken token)
    {
        if (_catalog is not null)
        {
            return _catalog;
        }

        string cachePath = Path.Combine(_home, "plugins.json");
        string json;

        try
        {
            json = await ReadTextAsync(_catalogSource, token);
            Directory.CreateDirectory(_home);
            await File.WriteAllTextAsync(cachePath, json, token);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or IOException)
        {
            if (!File.Exists(cachePath))
            {
                throw new PluginException(
                    $"No se pudo leer el catálogo de plugins ({_catalogSource}): {exception.Message}");
            }

            Console.Error.WriteLine(
                $"Sin conexión al catálogo de plugins, se usa la copia local. ({exception.Message})");
            json = await File.ReadAllTextAsync(cachePath, token);
        }

        _catalog = JsonSerializer.Deserialize<PluginMetadata[]>(json, JsonOptions) ?? [];

        foreach (PluginMetadata entry in _catalog)
        {
            if (string.IsNullOrWhiteSpace(entry.Name) || string.IsNullOrWhiteSpace(entry.Package))
            {
                throw new PluginException("El catálogo de plugins tiene una entrada sin 'name' o 'package'.");
            }
        }

        return _catalog;
    }

    private async Task<string> ReadTextAsync(string source, CancellationToken token)
    {
        if (Uri.TryCreate(source, UriKind.Absolute, out Uri? uri) &&
            uri.Scheme is "http" or "https")
        {
            return await _http.GetStringAsync(uri, token);
        }

        string path = source.StartsWith("file:", StringComparison.OrdinalIgnoreCase)
            ? new Uri(source).LocalPath
            : source;

        return await File.ReadAllTextAsync(path, token);
    }

    public async Task<PluginMetadata> FindAsync(string name, CancellationToken token)
    {
        IReadOnlyList<PluginMetadata> catalog = await GetCatalogAsync(token);

        return catalog.FirstOrDefault(entry =>
                   entry.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
               ?? throw new PluginException(
                   $"El plugin '{name}' no existe. Disponibles: {string.Join(", ", catalog.Select(entry => entry.Name))}.");
    }

    // ------------------------------------------------------------ instalación

    public InstalledPlugin? GetInstalled(string name)
    {
        string manifest = Path.Combine(PluginDirectory(name), "plugin.json");

        return File.Exists(manifest)
            ? JsonSerializer.Deserialize<InstalledPlugin>(File.ReadAllText(manifest), JsonOptions)
            : null;
    }

    /// <summary>Instala el plugin con la última versión de su paquete NuGet (o la actualiza).</summary>
    public async Task<InstalledPlugin> InstallAsync(string name, CancellationToken token)
    {
        PluginMetadata metadata = await FindAsync(name, token);
        string latest = await GetLatestVersionAsync(metadata.Package, token);
        InstalledPlugin? installed = GetInstalled(metadata.Name);

        if (installed is not null &&
            installed.Version == latest &&
            File.Exists(Path.Combine(PluginDirectory(metadata.Name), installed.Assembly)))
        {
            Console.WriteLine($"Plugin {metadata.Name} {latest} ya está actualizado.");
            return installed;
        }

        Console.WriteLine($"Instalando plugin {metadata.Name} ({metadata.Package} {latest})...");

        string target = PluginDirectory(metadata.Name);
        string staging = target + ".tmp";

        if (Directory.Exists(staging))
        {
            Directory.Delete(staging, recursive: true);
        }

        Directory.CreateDirectory(staging);

        try
        {
            string assembly = await DownloadPackageAsync(metadata.Package, latest, staging, [], token);

            var result = new InstalledPlugin
            {
                Name = metadata.Name,
                Package = metadata.Package,
                Version = latest,
                Assembly = assembly
            };

            await File.WriteAllTextAsync(
                Path.Combine(staging, "plugin.json"),
                JsonSerializer.Serialize(result, JsonOptions),
                token);

            if (Directory.Exists(target))
            {
                Directory.Delete(target, recursive: true);
            }

            Directory.Move(staging, target);
            Console.WriteLine($"Plugin {metadata.Name} {latest} instalado.");
            return result;
        }
        catch
        {
            if (Directory.Exists(staging))
            {
                Directory.Delete(staging, recursive: true);
            }

            throw;
        }
    }

    public void Remove(string name)
    {
        string directory = PluginDirectory(name);

        if (!Directory.Exists(directory))
        {
            throw new PluginException($"El plugin '{name}' no está instalado.");
        }

        Directory.Delete(directory, recursive: true);
        Console.WriteLine($"Plugin {name} eliminado.");
    }

    /// <summary>
    /// Devuelve el plugin cargado. Lo instala si falta y lo actualiza si NuGet tiene una
    /// versión más nueva; sin conexión usa la copia instalada.
    /// </summary>
    public async Task<Plugin> LoadAsync(string name, CancellationToken token)
    {
        PluginMetadata metadata;

        try
        {
            metadata = await FindAsync(name, token);
            await InstallAsync(metadata.Name, token);
        }
        catch (Exception exception) when (
            exception is HttpRequestException or TaskCanceledException &&
            GetInstalled(name) is not null)
        {
            Console.Error.WriteLine(
                $"No se pudo buscar actualizaciones de '{name}', se usa la versión instalada. ({exception.Message})");

            metadata = await FindAsync(name, token);
        }

        InstalledPlugin installed = GetInstalled(metadata.Name)
            ?? throw new PluginException($"El plugin '{name}' no está instalado.");

        string directory = PluginDirectory(metadata.Name);
        var context = new PluginLoadContext(directory);
        var assembly = context.LoadFromAssemblyPath(Path.Combine(directory, installed.Assembly));

        Type backendType = assembly
            .GetTypes()
            .FirstOrDefault(type =>
                typeof(IShaderTargetBackend).IsAssignableFrom(type) &&
                !type.IsAbstract &&
                !type.IsInterface)
            ?? throw new PluginException(
                $"'{installed.Assembly}' no contiene ninguna implementación de IShaderTargetBackend.");

        var backend = (IShaderTargetBackend)Activator.CreateInstance(backendType)!;
        return new Plugin(metadata, installed.Version, backend);
    }

    // ------------------------------------------------------------------ NuGet

    private async Task<string> GetLatestVersionAsync(string package, CancellationToken token)
    {
        string url = $"{NuGetFlatContainer}{package.ToLowerInvariant()}/index.json";

        using HttpResponseMessage response = await _http.GetAsync(url, token);

        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            throw new PluginException($"El paquete NuGet '{package}' no existe en nuget.org.");
        }

        response.EnsureSuccessStatusCode();

        using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(token));
        string[] versions = document.RootElement
            .GetProperty("versions")
            .EnumerateArray()
            .Select(element => element.GetString()!)
            .ToArray();

        string[] stable = versions.Where(version => !version.Contains('-')).ToArray();
        string[] pool = stable.Length != 0 ? stable : versions;

        return pool.Length == 0
            ? throw new PluginException($"El paquete '{package}' no tiene versiones publicadas.")
            : pool.MaxBy(ParseVersion, VersionComparer.Instance)!;
    }

    /// <summary>
    /// Descarga un paquete y, recursivamente, sus dependencias, a una única carpeta.
    /// Devuelve el nombre de la DLL principal del paquete raíz.
    /// </summary>
    private async Task<string> DownloadPackageAsync(
        string package,
        string version,
        string directory,
        HashSet<string> visited,
        CancellationToken token)
    {
        if (!visited.Add(package.ToLowerInvariant()))
        {
            return "";
        }

        string id = package.ToLowerInvariant();
        string url = $"{NuGetFlatContainer}{id}/{version.ToLowerInvariant()}/{id}.{version.ToLowerInvariant()}.nupkg";

        byte[] data = await _http.GetByteArrayAsync(url, token);
        using var archive = new ZipArchive(new MemoryStream(data), ZipArchiveMode.Read);

        string mainAssembly = ExtractLibraries(archive, package, directory);
        ExtractRuntimeAssets(archive, directory);

        ZipArchiveEntry? nuspec = archive.Entries.FirstOrDefault(entry =>
            entry.FullName.EndsWith(".nuspec", StringComparison.OrdinalIgnoreCase) &&
            !entry.FullName.Contains('/'));

        if (nuspec is not null)
        {
            foreach ((string dependency, string minimum) in ReadDependencies(nuspec))
            {
                if (IsProvidedByHost(dependency))
                {
                    continue;
                }

                string dependencyVersion = await ResolveVersionAsync(dependency, minimum, token);
                await DownloadPackageAsync(dependency, dependencyVersion, directory, visited, token);
            }
        }

        return mainAssembly;
    }

    private async Task<string> ResolveVersionAsync(string package, string minimum, CancellationToken token)
    {
        // Se usa la versión mínima que pide el paquete (como hace NuGet) si existe;
        // si no, la última.
        if (minimum.Length != 0)
        {
            string id = package.ToLowerInvariant();
            using var request = new HttpRequestMessage(
                HttpMethod.Head,
                $"{NuGetFlatContainer}{id}/{minimum.ToLowerInvariant()}/{id}.{minimum.ToLowerInvariant()}.nupkg");
            using HttpResponseMessage response = await _http.SendAsync(request, token);

            if (response.IsSuccessStatusCode)
            {
                return minimum;
            }
        }

        return await GetLatestVersionAsync(package, token);
    }

    private static bool IsProvidedByHost(string package)
    {
        if (package.StartsWith("Microsoft.NETCore.", StringComparison.OrdinalIgnoreCase) ||
            package.Equals("NETStandard.Library", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // YotsubaShader.Extensions debe ser el mismo ensamblado que usa el CLI.
        return package.Equals("YotsubaShader.Extensions", StringComparison.OrdinalIgnoreCase) ||
               File.Exists(Path.Combine(AppContext.BaseDirectory, package + ".dll"));
    }

    private static IEnumerable<(string Package, string Minimum)> ReadDependencies(ZipArchiveEntry nuspec)
    {
        using Stream stream = nuspec.Open();
        XDocument document = XDocument.Load(stream);

        XElement[] groups = document
            .Descendants()
            .Where(element => element.Name.LocalName == "group" &&
                              element.Parent?.Name.LocalName == "dependencies")
            .ToArray();

        XElement? group = groups
            .Select(candidate => (Element: candidate, Rank: DependencyRank(candidate.Attribute("targetFramework")?.Value)))
            .Where(candidate => candidate.Rank >= 0)
            .OrderByDescending(candidate => candidate.Rank)
            .Select(candidate => candidate.Element)
            .FirstOrDefault();

        IEnumerable<XElement> dependencies = group is not null
            ? group.Elements().Where(element => element.Name.LocalName == "dependency")
            : document.Descendants().Where(element => element.Name.LocalName == "dependency" &&
                                                      element.Parent?.Name.LocalName == "dependencies");

        foreach (XElement dependency in dependencies)
        {
            string? id = dependency.Attribute("id")?.Value;

            if (string.IsNullOrEmpty(id) ||
                dependency.Attribute("exclude")?.Value.Contains("Runtime", StringComparison.OrdinalIgnoreCase) == true)
            {
                continue;
            }

            yield return (id, MinimumVersion(dependency.Attribute("version")?.Value));
        }
    }

    private static string MinimumVersion(string? range)
    {
        if (string.IsNullOrWhiteSpace(range))
        {
            return "";
        }

        string first = range.Trim('[', ']', '(', ')', ' ').Split(',')[0].Trim();
        return first;
    }

    // Mayor es mejor; -1 descarta el grupo.
    private static int DependencyRank(string? framework) => TargetRank(framework);

    private static int TargetRank(string? framework)
    {
        if (string.IsNullOrEmpty(framework))
        {
            return 0;
        }

        string value = framework.ToLowerInvariant();

        if (value.StartsWith("net", StringComparison.Ordinal) &&
            !value.StartsWith("netstandard", StringComparison.Ordinal) &&
            !value.StartsWith("netcoreapp", StringComparison.Ordinal) &&
            !value.Contains('-') && value.Length > 3 && char.IsDigit(value[3]))
        {
            // net8.0, net9.0, net10.0 ... los net4x (Framework) no sirven.
            return int.TryParse(value[3..].Split('.')[0], out int major) && major >= 5 && major <= 10
                ? 100 + major
                : -1;
        }

        if (value.StartsWith(".netcoreapp,version=v", StringComparison.Ordinal) ||
            value.StartsWith(".netcoreapp", StringComparison.Ordinal))
        {
            string text = value[(value.IndexOf('v') + 1)..];
            return int.TryParse(text.Split('.')[0], out int major) && major >= 5 && major <= 10
                ? 100 + major
                : -1;
        }

        return value switch
        {
            "netstandard2.1" or ".netstandard,version=v2.1" => 21,
            "netstandard2.0" or ".netstandard,version=v2.0" => 20,
            _ => -1
        };
    }

    private static string ExtractLibraries(ZipArchive archive, string package, string directory)
    {
        var folders = archive.Entries
            .Where(entry => entry.FullName.StartsWith("lib/", StringComparison.OrdinalIgnoreCase) &&
                            entry.FullName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            .GroupBy(entry => entry.FullName.Split('/')[1], StringComparer.OrdinalIgnoreCase)
            .Select(group => (Folder: group.Key, Rank: TargetRank(group.Key), Entries: group.ToArray()))
            .Where(candidate => candidate.Rank >= 0)
            .OrderByDescending(candidate => candidate.Rank)
            .ToArray();

        if (folders.Length == 0)
        {
            return "";
        }

        string main = "";

        foreach (ZipArchiveEntry entry in folders[0].Entries)
        {
            string fileName = Path.GetFileName(entry.FullName);

            // Las DLL de las dependencias no pisan lo que ya trajo el plugin.
            string destination = Path.Combine(directory, fileName);

            if (!File.Exists(destination))
            {
                entry.ExtractToFile(destination);
            }

            if (fileName.Equals(package + ".dll", StringComparison.OrdinalIgnoreCase))
            {
                main = fileName;
            }
        }

        return main.Length != 0 ? main : Path.GetFileName(folders[0].Entries[0].FullName);
    }

    private static void ExtractRuntimeAssets(ZipArchive archive, string directory)
    {
        string[] runtimes = CandidateRuntimes();

        foreach (ZipArchiveEntry entry in archive.Entries)
        {
            string[] parts = entry.FullName.Split('/');

            if (parts.Length < 4 || !parts[0].Equals("runtimes", StringComparison.OrdinalIgnoreCase) ||
                !runtimes.Contains(parts[1], StringComparer.OrdinalIgnoreCase) ||
                entry.Name.Length == 0)
            {
                continue;
            }

            string? subdirectory = parts[2].ToLowerInvariant() switch
            {
                "native" => "native",
                "lib" => "",
                _ => null
            };

            if (subdirectory is null)
            {
                continue;
            }

            // ShadowDusk busca los nativos por arquitectura: runtimes/<rid>/native.
            // Si el rid es específico (tiene arquitectura), se conserva en el destino.
            bool perArch = parts[1].Contains('-');
            string folder = subdirectory == "native" && perArch
                ? Path.Combine(directory, "runtimes", parts[1], "native")
                : Path.Combine(directory, subdirectory);
            Directory.CreateDirectory(folder);

            string destination = Path.Combine(folder, entry.Name);

            if (!File.Exists(destination))
            {
                entry.ExtractToFile(destination);
            }
        }
    }

    private static string[] CandidateRuntimes()
    {
        string os = OperatingSystem.IsWindows() ? "win"
            : OperatingSystem.IsMacOS() ? "osx"
            : "linux";

        string arch = RuntimeInformation.ProcessArchitecture.ToString().ToLowerInvariant();

        return
        [
            RuntimeInformation.RuntimeIdentifier,
            $"{os}-{arch}",
            os,
            ..(OperatingSystem.IsWindows() ? Array.Empty<string>() : ["unix"])
        ];
    }

    // --------------------------------------------------------------- versiones

    private static (Version Core, string Pre) ParseVersion(string text)
    {
        string withoutMetadata = text.Split('+')[0];
        string[] parts = withoutMetadata.Split('-', 2);
        string[] numbers = parts[0].Split('.');

        int Number(int index) =>
            index < numbers.Length && int.TryParse(numbers[index], out int value) ? value : 0;

        return (new Version(Number(0), Number(1), Number(2), Number(3)), parts.Length > 1 ? parts[1] : "");
    }

    private sealed class VersionComparer : IComparer<(Version Core, string Pre)>
    {
        public static readonly VersionComparer Instance = new();

        public int Compare((Version Core, string Pre) x, (Version Core, string Pre) y)
        {
            int core = x.Core.CompareTo(y.Core);

            if (core != 0)
            {
                return core;
            }

            // Sin sufijo prerelease es mayor que con él.
            if (x.Pre.Length == 0 || y.Pre.Length == 0)
            {
                return (x.Pre.Length == 0 ? 1 : 0) - (y.Pre.Length == 0 ? 1 : 0);
            }

            return string.CompareOrdinal(x.Pre, y.Pre);
        }
    }
}

public sealed class PluginException(string message) : Exception(message);
