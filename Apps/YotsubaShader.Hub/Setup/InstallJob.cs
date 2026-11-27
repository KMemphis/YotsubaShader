using System.Diagnostics;

namespace YotsubaShader.Hub.Setup;

internal sealed record InstallEvent(string Type, string? Step, string? Message);

/// <summary>Runs the selected components one after another and records every event so a late UI can replay them.</summary>
internal sealed class InstallJob
{
    private readonly List<InstallEvent> _events = [];
    private readonly object _lock = new();

    public bool Running { get; private set; }

    public IReadOnlyList<InstallEvent> Snapshot(int from)
    {
        lock (_lock) return _events.Skip(from).ToList();
    }

    private void Emit(string type, string? step = null, string? message = null)
    {
        lock (_lock) _events.Add(new InstallEvent(type, step, message));
    }

    public bool TryStart(IEnumerable<string> ids, string home)
    {
        lock (_lock)
        {
            if (Running) return false;
            Running = true;
            _events.Clear();
        }
        _ = Task.Run(() => RunAsync(Catalog.Resolve(ids), home));
        return true;
    }

    private async Task RunAsync(IReadOnlyList<Component> steps, string home)
    {
        var failed = false;
        try
        {
            var dotnet = Probe.FindDotnet();
            if (dotnet is null)
            {
                Emit("step-fail", Catalog.CliId, "The .NET SDK was not found. Install it from https://dotnet.microsoft.com/download and run the setup again.");
                failed = true;
                return;
            }

            var env = new Dictionary<string, string> { ["YOTSUBA_HOME"] = home };
            foreach (var step in steps)
            {
                Emit("step-start", step.Id, step.Name);
                var code = step.Kind == ComponentKind.Cli
                    ? await Run(step, dotnet, ["tool", "update", "-g", Catalog.CliPackage], env)
                    : await Run(step, Probe.CliPath(), ["plugin", "install", step.Id], env);
                if (code != 0)
                {
                    Emit("step-fail", step.Id, $"Exited with code {code}.");
                    failed = true;
                    break; // a failed CLI install makes every plugin step meaningless
                }
                Emit("step-done", step.Id);
            }
        }
        catch (Exception e)
        {
            Emit("step-fail", null, e.Message);
            failed = true;
        }
        finally
        {
            Emit("done", null, failed ? "failed" : "ok");
            lock (_lock) Running = false;
        }
    }

    private async Task<int> Run(Component step, string file, string[] args, Dictionary<string, string> env)
    {
        var psi = new ProcessStartInfo(file) { RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
        foreach (var a in args) psi.ArgumentList.Add(a);
        foreach (var (k, v) in env) psi.Environment[k] = v;
        // The CLI may live in ~/.dotnet/tools, which a GUI launch does not have on PATH.
        psi.Environment["PATH"] = Path.GetDirectoryName(Probe.CliPath()) + Path.PathSeparator + Environment.GetEnvironmentVariable("PATH");
        Emit("log", step.Id, $"$ {Path.GetFileName(file)} {string.Join(' ', args)}");
        using var p = Process.Start(psi)!;
        void Pump(StreamReader r) { for (string? l; (l = r.ReadLine()) is not null;) if (l.Length > 0) Emit("log", step.Id, l); }
        await Task.WhenAll(Task.Run(() => Pump(p.StandardOutput)), Task.Run(() => Pump(p.StandardError)));
        await p.WaitForExitAsync();
        return p.ExitCode;
    }
}
