using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Xml.Linq;
using YotsubaShader.Extensions;

namespace YotsubaShader.Compiler.CLI;

/// <summary>
/// Gestiona los plugins del CLI: el catálogo vive en un JSON en la web y cada plugin es un paquete NuGet. Se usa la
/// versión más nueva que sea compatible con este CLI: la misma versión mayor.menor de <c>YotsubaShader.Extensions</c>
/// y nunca una posterior, porque un plugin compilado contra un contrato más nuevo no carga en un CLI más viejo.
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

    // Sin límite total en el HttpClient: un paquete de plugin puede pesar cientos de MB (YotsubaShader.ShadowDusk.HLSL
    // trae DXC) y con una conexión normal tarda más de 30 s. Los límites son por petición (cabeceras y metadatos) y por
    // inactividad (una descarga sin recibir un solo byte), así que una descarga lenta termina y una colgada falla.
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan StallTimeout = TimeSpan.FromSeconds(30);
    private const int DownloadAttempts = 3;
    private const long ProgressThreshold = 5 * 1024 * 1024;

    private readonly HttpClient _http = new() { Timeout = Timeout.InfiniteTimeSpan };
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

    /// <summary>Versión del contrato de plugins que trae este CLI (la de YotsubaShader.Extensions).</summary>
    public static Version HostVersion { get; } = Normalize(typeof(IShaderTargetBackend).Assembly.GetName().Version);

    private static Version Normalize(Version? version) =>
        version is null ? new Version(0, 0, 0) : new Version(version.Major, version.Minor, Math.Max(version.Build, 0));

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
            _catalog = ParseCatalog(json);
            Directory.CreateDirectory(_home);
            await WriteAtomicAsync(cachePath, json, token);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or IOException
                                              or JsonException or UnauthorizedAccessException)
        {
            _catalog = ReadCachedCatalog() ?? throw new PluginException(
                $"No se pudo leer el catálogo de plugins ({_catalogSource}): {exception.Message}");

            Console.Error.WriteLine(
                $"Sin conexión al catálogo de plugins, se usa la copia local. ({exception.Message})");
        }

        foreach (PluginMetadata entry in _catalog)
        {
            if (string.IsNullOrWhiteSpace(entry.Name) || string.IsNullOrWhiteSpace(entry.Package))
            {
                throw new PluginException("El catálogo de plugins tiene una entrada sin 'name' o 'package'.");
            }
        }

        return _catalog;
    }

    private static PluginMetadata[] ParseCatalog(string json) =>
        JsonSerializer.Deserialize<PluginMetadata[]>(json, JsonOptions) ?? [];

    private PluginMetadata[]? ReadCachedCatalog()
    {
        try
        {
            string cachePath = Path.Combine(_home, "plugins.json");
            return File.Exists(cachePath) ? ParseCatalog(File.ReadAllText(cachePath)) : null;
        }
        catch (Exception exception) when (exception is IOException or JsonException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    // Varios `yotsuba` en paralelo (un build con varios proyectos) no deben leer un archivo a medio escribir.
    private static async Task WriteAtomicAsync(string path, string text, CancellationToken token)
    {
        string temporary = $"{path}.{Environment.ProcessId}.tmp";
        await File.WriteAllTextAsync(temporary, text, token);
        File.Move(temporary, path, overwrite: true);
    }

    /// <summary>Envía una petición y espera solo las cabeceras, con <see cref="RequestTimeout"/>.</summary>
    private async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
        timeout.CancelAfter(RequestTimeout);

        try
        {
            return await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
        }
        catch (OperationCanceledException) when (!token.IsCancellationRequested)
        {
            throw new HttpRequestException(
                $"Sin respuesta de {request.RequestUri?.Host} en {RequestTimeout.TotalSeconds:0} s. Comprueba la conexión.");
        }
    }

    /// <summary>Lee un cuerpo pequeño (JSON de catálogo o de versiones) con <see cref="RequestTimeout"/>.</summary>
    private static async Task<string> ReadWithTimeoutAsync(Func<CancellationToken, Task<string>> read, CancellationToken token)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
        timeout.CancelAfter(RequestTimeout);

        try
        {
            return await read(timeout.Token);
        }
        catch (OperationCanceledException) when (!token.IsCancellationRequested)
        {
            throw new HttpRequestException($"La respuesta no terminó de llegar en {RequestTimeout.TotalSeconds:0} s.");
        }
    }

    /// <summary>
    /// Descarga un paquete. No hay límite de tiempo total: solo falla si pasan <see cref="StallTimeout"/> sin recibir
    /// datos, y entonces reintenta. Los paquetes grandes muestran el avance cada 10 %.
    /// </summary>
    private async Task<byte[]> DownloadAsync(string url, string label, CancellationToken token)
    {
        for (int attempt = 1; ; attempt++)
        {
            try
            {
                return await DownloadOnceAsync(url, label, token);
            }
            catch (Exception exception) when (
                attempt < DownloadAttempts &&
                !token.IsCancellationRequested &&
                exception is HttpRequestException or IOException)
            {
                Console.Error.WriteLine(
                    $"Se interrumpió la descarga de {label} ({exception.Message}). Reintento {attempt}/{DownloadAttempts - 1}...");
                await Task.Delay(TimeSpan.FromSeconds(2 * attempt), token);
            }
        }
    }

    private async Task<byte[]> DownloadOnceAsync(string url, string label, CancellationToken token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        using HttpResponseMessage response = await SendAsync(request, token);
        response.EnsureSuccessStatusCode();

        long? total = response.Content.Headers.ContentLength;
        bool showProgress = total is >= ProgressThreshold;
        bool announce = total is >= 1024 * 1024;

        // Líneas completas y no '\r': el CLI se llama desde un csproj (Exec) o un IDE, que registran la salida por líneas.
        // Con 10 % de paso o 5 s sin novedad siempre se ve que sigue vivo, aunque la conexión sea lenta.
        var clock = System.Diagnostics.Stopwatch.StartNew();
        TimeSpan lastReport = TimeSpan.Zero;
        int reportedStep = 0;

        if (announce)
        {
            Console.WriteLine($"Descargando {label} ({FormatMegabytes(total!.Value)})...");
        }

        using var stall = CancellationTokenSource.CreateLinkedTokenSource(token);
        stall.CancelAfter(StallTimeout);

        await using Stream stream = await response.Content.ReadAsStreamAsync(stall.Token);
        using var data = new MemoryStream(total is > 0 and < int.MaxValue ? (int)total.Value : 0);
        byte[] chunk = new byte[81920];

        while (true)
        {
            int read;

            try
            {
                read = await stream.ReadAsync(chunk, stall.Token);
            }
            catch (OperationCanceledException) when (!token.IsCancellationRequested)
            {
                throw new IOException($"sin datos durante {StallTimeout.TotalSeconds:0} s");
            }

            if (read == 0)
            {
                break;
            }

            stall.CancelAfter(StallTimeout);
            data.Write(chunk, 0, read);

            if (showProgress)
            {
                int percent = (int)(data.Length * 100 / total!.Value);
                int step = percent / 10;

                if (percent < 100 && (step > reportedStep || (clock.Elapsed - lastReport).TotalSeconds >= 5))
                {
                    reportedStep = Math.Max(reportedStep, step);
                    lastReport = clock.Elapsed;
                    double speed = data.Length / 1048576.0 / Math.Max(clock.Elapsed.TotalSeconds, 0.001);
                    Console.WriteLine(
                        $"  {label}: {percent} % ({FormatMegabytes(data.Length)} de {FormatMegabytes(total.Value)}, {speed:0.0} MB/s)");
                }
            }
        }

        if (showProgress)
        {
            Console.WriteLine($"  {label}: 100 % ({FormatMegabytes(data.Length)} en {clock.Elapsed.TotalSeconds:0} s)");
        }

        // Con el tamaño anunciado el búfer ya es exacto: se evita copiar un paquete de cientos de MB.
        return data.Length == data.Capacity ? data.GetBuffer() : data.ToArray();
    }

    private static string FormatMegabytes(long bytes) => $"{bytes / 1048576.0:0.#} MB";

    private async Task<string> ReadTextAsync(string source, CancellationToken token)
    {
        if (Uri.TryCreate(source, UriKind.Absolute, out Uri? uri) &&
            uri.Scheme is "http" or "https")
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            using HttpResponseMessage response = await SendAsync(request, token);
            response.EnsureSuccessStatusCode();
            return await ReadWithTimeoutAsync(response.Content.ReadAsStringAsync, token);
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

        try
        {
            return File.Exists(manifest)
                ? JsonSerializer.Deserialize<InstalledPlugin>(File.ReadAllText(manifest), JsonOptions)
                : null;
        }
        catch (Exception exception) when (exception is IOException or JsonException or UnauthorizedAccessException)
        {
            // Un plugin.json dañado equivale a no tenerlo instalado: se vuelve a instalar.
            return null;
        }
    }

    /// <summary>Instala el plugin con la última versión de su paquete NuGet (o la actualiza).</summary>
    public async Task<InstalledPlugin> InstallAsync(string name, CancellationToken token)
    {
        Console.WriteLine($"Buscando la versión del plugin {name} compatible con el CLI {HostVersion.Major}.{HostVersion.Minor}.x...");
        PluginMetadata metadata = await FindAsync(name, token);
        string latest = await GetCompatibleVersionAsync(metadata.Package, token);

        // Otro yotsuba (un build en paralelo) puede estar instalando lo mismo: uno espera al otro.
        using PluginLock pluginLock = await PluginLock.AcquireAsync(PluginsDirectory, token);

        InstalledPlugin? installed = GetInstalled(metadata.Name);

        if (installed is not null &&
            installed.Version == latest &&
            File.Exists(Path.Combine(PluginDirectory(metadata.Name), installed.Assembly)))
        {
            Console.WriteLine($"Plugin {metadata.Name} {latest} ya está actualizado.");
            return installed;
        }

        Console.WriteLine(installed is null
            ? $"Instalando plugin {metadata.Name} ({metadata.Package} {latest})..."
            : $"Actualizando plugin {metadata.Name} de {installed.Version} a {latest} ({metadata.Package})...");

        string target = PluginDirectory(metadata.Name);
        string staging = $"{target}.{Environment.ProcessId}.tmp";

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
                try
                {
                    Directory.Delete(target, recursive: true);
                }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
                {
                    throw new PluginException(
                        $"No se pudo reemplazar el plugin '{metadata.Name}' en {target}: {exception.Message} " +
                        "Cierra los otros procesos de yotsuba (o Godot/el IDE que lo use) y vuelve a intentarlo.");
                }
            }

            Directory.Move(staging, target);
            Console.WriteLine($"Plugin {metadata.Name} {latest} {(installed is null ? "instalado" : "actualizado")}.");
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
    /// Devuelve el plugin cargado. Lo instala si falta y lo actualiza si NuGet tiene una versión compatible más nueva;
    /// sin conexión usa la copia instalada. Si la copia instalada ya es la de este CLI no se consulta la red.
    /// Cualquier fallo se convierte en <see cref="PluginException"/> con un mensaje legible.
    /// </summary>
    public async Task<Plugin> LoadAsync(string name, CancellationToken token)
    {
        try
        {
            return await LoadCoreAsync(name, token);
        }
        catch (Exception exception) when (exception is not PluginException and not OperationCanceledException)
        {
            Exception cause = exception is System.Reflection.ReflectionTypeLoadException { LoaderExceptions: { Length: > 0 } loader }
                ? loader.FirstOrDefault(inner => inner is not null) ?? exception
                : exception;

            throw new PluginException(cause is HttpRequestException or TaskCanceledException
                ? $"No se pudo descargar el plugin '{name}' de NuGet ({cause.Message}). Comprueba la conexión; " +
                  "una vez instalado, funciona sin red."
                : $"No se pudo cargar el plugin '{name}': {PluginErrors.Describe(cause)}");
        }
    }

    private async Task<Plugin> LoadCoreAsync(string name, CancellationToken token)
    {
        PluginMetadata metadata;
        InstalledPlugin? current = GetInstalled(name);

        if (current is not null &&
            TryParseVersion(current.Version, out Version? installedVersion) &&
            installedVersion == HostVersion &&
            ReadCachedCatalog()?.FirstOrDefault(entry => entry.Name.Equals(name, StringComparison.OrdinalIgnoreCase)) is { } cached)
        {
            // La versión instalada es exactamente la del CLI: no hay nada mejor que buscar.
            metadata = cached;
        }
        else
        {
            try
            {
                metadata = await FindAsync(name, token);
                await InstallAsync(metadata.Name, token);
            }
            catch (Exception exception) when (
                exception is HttpRequestException or TaskCanceledException or IOException &&
                current is not null)
            {
                Console.Error.WriteLine(
                    $"No se pudo actualizar el plugin '{name}' ({exception.Message}). Se usa la versión instalada {current.Version}" +
                    (TryParseVersion(current.Version, out Version? old) && old != HostVersion
                        ? $", distinta de la {HostVersion} de este CLI: el código generado puede ser de una versión anterior. " +
                          $"Reintenta con 'yotsuba plugin update {name}'."
                        : "."));

                metadata = await FindAsync(name, token);
            }
        }

        InstalledPlugin installed = GetInstalled(metadata.Name)
            ?? throw new PluginException($"El plugin '{name}' no está instalado.");

        string directory = PluginDirectory(metadata.Name);
        string assemblyPath = Path.Combine(directory, installed.Assembly);

        if (installed.Assembly.Length == 0 || !File.Exists(assemblyPath))
        {
            throw new PluginException(
                $"La instalación del plugin '{name}' está incompleta. Ejecuta 'yotsuba plugin install {name}'.");
        }

        var context = new PluginLoadContext(directory);
        var assembly = context.LoadFromAssemblyPath(assemblyPath);

        Type backendType = assembly
            .GetTypes()
            .FirstOrDefault(type =>
                typeof(IShaderTargetBackend).IsAssignableFrom(type) &&
                !type.IsAbstract &&
                !type.IsInterface)
            ?? throw new PluginException(
                $"'{installed.Assembly}' no contiene ninguna implementación de IShaderTargetBackend compatible con este CLI " +
                $"({CliVersion.Informational}).");

        var backend = (IShaderTargetBackend)Activator.CreateInstance(backendType)!;
        return new Plugin(metadata, installed.Version, backend);
    }

    private static bool TryParseVersion(string text, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out Version? version)
    {
        (Version core, string pre) = ParseVersion(text);
        version = pre.Length == 0 ? Normalize(core) : null;
        return version is not null;
    }

    // ------------------------------------------------------------------ NuGet

    /// <summary>
    /// La versión del plugin para este CLI: la más nueva con la misma mayor.menor que el CLI que no lo supere. Si no
    /// hay ninguna, la más nueva anterior al CLI. Una posterior no sirve: su contrato (YotsubaShader.Extensions) es más
    /// nuevo que el del CLI.
    /// </summary>
    private async Task<string> GetCompatibleVersionAsync(string package, CancellationToken token)
    {
        string[] versions = await GetVersionsAsync(package, token);
        Version host = HostVersion;

        var stable = versions
            .Select(text => (Text: text, Parsed: ParseVersion(text)))
            .Where(candidate => candidate.Parsed.Pre.Length == 0)
            .Select(candidate => (candidate.Text, Version: Normalize(candidate.Parsed.Core)))
            .Where(candidate => candidate.Version <= host)
            .OrderByDescending(candidate => candidate.Version)
            .ToArray();

        var sameMinor = stable.Where(candidate =>
            candidate.Version.Major == host.Major && candidate.Version.Minor == host.Minor).ToArray();

        if (sameMinor.Length != 0)
        {
            return sameMinor[0].Text;
        }

        if (stable.Length != 0)
        {
            Console.Error.WriteLine(
                $"Aviso: no hay {package} {host.Major}.{host.Minor}.x; se usa {stable[0].Text}, que puede no ser compatible con el CLI {CliVersion.Informational}.");
            return stable[0].Text;
        }

        throw new PluginException(
            $"No hay ninguna versión de {package} compatible con el CLI {CliVersion.Informational} " +
            $"(publicadas: {string.Join(", ", versions)}). Actualiza el CLI: dotnet tool update -g YotsubaShader.Compiler.CLI");
    }

    private async Task<string[]> GetVersionsAsync(string package, CancellationToken token)
    {
        string url = $"{NuGetFlatContainer}{package.ToLowerInvariant()}/index.json";

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        using HttpResponseMessage response = await SendAsync(request, token);

        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            throw new PluginException($"El paquete NuGet '{package}' no existe en nuget.org.");
        }

        response.EnsureSuccessStatusCode();

        using JsonDocument document = JsonDocument.Parse(
            await ReadWithTimeoutAsync(response.Content.ReadAsStringAsync, token));
        return document.RootElement
            .GetProperty("versions")
            .EnumerateArray()
            .Select(element => element.GetString()!)
            .ToArray();
    }

    private async Task<string> GetLatestVersionAsync(string package, CancellationToken token)
    {
        string[] versions = await GetVersionsAsync(package, token);

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

        byte[] data = await DownloadAsync(url, $"{package} {version}", token);
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
            using HttpResponseMessage response = await SendAsync(request, token);

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

/// <summary>
/// Cerrojo entre procesos para instalar plugins: un archivo abierto en exclusiva dentro de la carpeta de plugins.
/// </summary>
internal sealed class PluginLock : IDisposable
{
    private readonly FileStream _stream;

    private PluginLock(FileStream stream) => _stream = stream;

    public static async Task<PluginLock> AcquireAsync(string directory, CancellationToken token)
    {
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, ".install.lock");
        DateTime deadline = DateTime.UtcNow.AddMinutes(5);

        while (true)
        {
            try
            {
                return new PluginLock(new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None));
            }
            catch (IOException) when (DateTime.UtcNow < deadline)
            {
                await Task.Delay(200, token);
            }
            catch (IOException exception)
            {
                throw new PluginException(
                    $"Otro proceso de yotsuba lleva más de 5 minutos instalando plugins ({path}): {exception.Message}");
            }
        }
    }

    public void Dispose() => _stream.Dispose();
}
