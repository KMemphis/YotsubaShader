using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.FileProviders;
using Photino.NET;
using YotsubaShader.Hub;
using YotsubaShader.Hub.Setup;
using YotsubaShader.Hub.Forum;
using YotsubaShader.Hub.Updates;
using Velopack;

// Must run first: Velopack uses this process for its install/update hooks and exits when it is one.
VelopackApp.Build().Run();


// Three loopback origins: the Hub UI, the offline docs (learn) and the site with the playground.
// learn and the site use root-absolute URLs (/css/..., /playground-app/...), so each one needs its own origin.
var content = ContentLocator.Find();
var builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();
var ports = new[] { PortPicker.Free(), PortPicker.Free(), PortPicker.Free() };
builder.WebHost.UseUrls(ports.Select(p => $"http://127.0.0.1:{p}").ToArray());
var app = builder.Build();

void Serve(int port, string? root, string missing, Action<IApplicationBuilder>? before = null)
{
    app.MapWhen(c => c.Connection.LocalPort == port, branch =>
    {
        if (root is null)
        {
            branch.Run(c => { c.Response.ContentType = "text/html; charset=utf-8"; return c.Response.WriteAsync(missing); });
            return;
        }
        before?.Invoke(branch);
        var files = new PhysicalFileProvider(root);
        branch.UseDefaultFiles(new DefaultFilesOptions { FileProvider = files });
        branch.UseStaticFiles(new StaticFileOptions { FileProvider = files, ServeUnknownFileTypes = true });
    });
}

var forum = ForumSession.Create();
var updates = new UpdateService();
var appUpdater = new AppUpdater();
var token = Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(16));
Serve(ports[0], content.Ui, ContentLocator.Missing("Hub UI", "npm run build (Apps/YotsubaShader.Hub/ui)"),
    b => { b.UseRouting(); b.UseEndpoints(e => Api.Map(e, token, content.License, updates, appUpdater, forum)); });
// The narration audio (hundreds of MB) is left out of the bundle; when online, missing audio comes from the public site.
Serve(ports[1], content.Docs, ContentLocator.Missing("Docs", "npm run build (Web/learn)"), b => b.Use(async (ctx, next) =>
{
    if (content.Docs is not null && ctx.Request.Path.StartsWithSegments("/audio")
        && !File.Exists(Path.Combine(content.Docs, ctx.Request.Path.Value!.TrimStart('/'))))
    {
        ctx.Response.Redirect("https://learn.yotsubashader.com" + ctx.Request.Path + ctx.Request.QueryString);
        return;
    }
    await next();
}));
Serve(ports[2], content.Site, ContentLocator.Missing("Playground", "npm run build (Web)"));

await app.StartAsync();

// `--selftest <result-file>`: verifies the bundle without opening a window, so CI can check every platform's package.
if (args.Length >= 1 && args[0] == "--selftest")
    return await SelfTest.RunAsync(ports, content, args.ElementAtOrDefault(1), app);

// The UI receives the origins through the query string; it never needs anything else from the host.
var url = $"http://127.0.0.1:{ports[0]}/?docs={ports[1]}&site={ports[2]}&t={token}";
var window = new PhotinoWindow()
    .SetTitle("Yotsuba Shader Hub")
    .SetUseOsDefaultSize(false)
    .SetSize(1280, 820)
    .SetMinSize(960, 640)
    .Center()
    .SetContextMenuEnabled(false);
// Windows and Linux take the window icon from a file; on macOS the icon comes from the .app bundle that Velopack builds.
if (!OperatingSystem.IsMacOS() && Path.Combine(AppContext.BaseDirectory, OperatingSystem.IsWindows() ? "icon.ico" : "icon.png") is { } iconFile && File.Exists(iconFile))
    window.SetIconFile(iconFile);
window.RegisterWindowCreatedHandler((_, _) => MacDockIcon.Set(Path.Combine(AppContext.BaseDirectory, "icon.png")));
window.Load(url);
using var stop = new CancellationTokenSource();
// On macOS, UNUserNotificationCenter throws an Objective-C exception that kills the process unless the app runs from a
// .app bundle (so not with `dotnet run`). In that case the in-app banner is the only notice.
var nativeNotificationsSafe = !OperatingSystem.IsMacOS() || AppContext.BaseDirectory.Contains(".app/Contents/", StringComparison.Ordinal);
if (nativeNotificationsSafe)
    updates.NewRelease += r => window.SendNotification($"YotsubaShader {r.Version} is available", "Open Yotsuba Shader Hub to see what is new.");
updates.StartBackground(stop.Token);
window.WaitForClose();
stop.Cancel();
await app.StopAsync();
return 0;
