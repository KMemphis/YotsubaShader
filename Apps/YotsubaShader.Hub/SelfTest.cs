using System.Text;
using Microsoft.AspNetCore.Builder;

namespace YotsubaShader.Hub;

/// <summary>Headless check that a built or packaged Hub serves everything it should. Exit code 0 means every check passed.</summary>
internal static class SelfTest
{
    public static async Task<int> RunAsync(int[] ports, Content content, string? resultFile, WebApplication app)
    {
        var report = new StringBuilder();
        var failed = false;

        async Task Check(string name, string url, Func<string, bool> ok)
        {
            try
            {
                using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
                using var res = await http.GetAsync(url);
                var body = res.IsSuccessStatusCode ? await res.Content.ReadAsStringAsync() : "";
                var pass = res.IsSuccessStatusCode && ok(body);
                report.AppendLine($"{(pass ? "ok  " : "FAIL")} {name} ({(int)res.StatusCode})");
                failed |= !pass;
            }
            catch (Exception e)
            {
                report.AppendLine($"FAIL {name}: {e.Message}");
                failed = true;
            }
        }

        report.AppendLine($"content: ui={content.Ui ?? "-"} docs={content.Docs ?? "-"} site={content.Site ?? "-"}");
        await Check("Hub UI", $"http://127.0.0.1:{ports[0]}/", b => b.Contains("<title>Yotsuba Shader Hub</title>"));
        await Check("Docs (learn)", $"http://127.0.0.1:{ports[1]}/en/", b => b.Contains("Yotsuba"));
        await Check("Site", $"http://127.0.0.1:{ports[2]}/", b => b.Length > 0);
        // The playground page asks for its Blazor backend under /playground-app/; without it the editor cannot compile.
        await Check("Playground page", $"http://127.0.0.1:{ports[2]}/playground/", b => b.Length > 0);
        await Check("Playground backend", $"http://127.0.0.1:{ports[2]}/playground-app/_framework/blazor.boot.json", b => b.Contains("resources"));
        if (content.License is null) { report.AppendLine("FAIL license file missing"); failed = true; }

        report.AppendLine(failed ? "RESULT failed" : "RESULT ok");
        if (resultFile is not null) await File.WriteAllTextAsync(resultFile, report.ToString());
        Console.Write(report);
        await app.StopAsync();
        return failed ? 1 : 0;
    }
}
