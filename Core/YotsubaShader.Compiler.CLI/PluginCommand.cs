namespace YotsubaShader.Compiler.CLI;

/// <summary>yotsuba plugin list | install | update | remove</summary>
public static class PluginCommand
{
    public static async Task<int> RunAsync(string[] args)
    {
        string? action = args.FirstOrDefault()?.ToLowerInvariant();
        string? name = args.Length > 1 ? args[1] : null;

        using var manager = new PluginManager();
        CancellationToken token = CancellationToken.None;

        try
        {
            switch (action)
            {
                case "list":
                    foreach (PluginMetadata entry in await manager.GetCatalogAsync(token))
                    {
                        InstalledPlugin? installed = manager.GetInstalled(entry.Name);
                        string state = installed is null ? "no instalado" : $"instalado {installed.Version}";
                        Console.WriteLine($"{entry.Name,-10} {state,-22} {entry.Description}");
                    }

                    return 0;

                case "install" or "update" when name is not null:
                    await manager.InstallAsync(name, token);
                    return 0;

                case "update":
                    foreach (PluginMetadata entry in await manager.GetCatalogAsync(token))
                    {
                        if (manager.GetInstalled(entry.Name) is not null)
                        {
                            await manager.InstallAsync(entry.Name, token);
                        }
                    }

                    return 0;

                case "remove" when name is not null:
                    manager.Remove(name);
                    return 0;

                default:
                    Console.Error.WriteLine(
                        "Uso: yotsuba plugin list | install <nombre> | update [nombre] | remove <nombre>");
                    return 2;
            }
        }
        catch (Exception exception) when (exception is PluginException or HttpRequestException or TaskCanceledException)
        {
            Console.Error.WriteLine(exception.Message);
            return 1;
        }
    }
}
