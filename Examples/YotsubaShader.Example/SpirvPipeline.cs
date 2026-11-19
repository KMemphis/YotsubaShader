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


using System.Diagnostics;
using YotsubaShader.Compiler.SpirV;

namespace YotsubaShader.Example;

/// <summary>
/// Del C# al binario SPIR-V validado por <c>spirv-val</c> (Vulkan 1.3, SPIR-V 1.6). Si <c>spirv-val</c> no está instalado
/// (variable SPIRV_VAL o PATH), solo se comprueba que el compilador produce el módulo.
/// </summary>
internal static class SpirvPipeline
{
    public const string Header = """
        using YotsubaShader.Language;
        using YotsubaShader.HlslSyntax;
        using YotsubaShader.HlslSyntax.Types;
        using YotsubaShader.HlslSyntax.Methods;
        using YotsubaShader.HlslSyntax.Resources;
        using YotsubaShader.HlslSyntax.Resources.Types;
        using YotsubaShader.SpirvSyntax.Resources;

        namespace Game;

        """;

    private static readonly string? Validator = FindValidator();

    /// <summary>Compila el shader y lo valida. Devuelve el ensamblador SPIR-V para inspeccionarlo.</summary>
    public static string Compile(string code)
    {
        var shader = ShaderIr.Process(Header + code);
        var module = new IRToSpirvModule().Compile(shader);
        byte[] binary = new SpirVCompiler().Compile(module);
        Assert.NotEmpty(binary);

        if (Validator is not null)
        {
            string file = Path.Combine(Path.GetTempPath(), $"ys_{Guid.NewGuid():N}.spv");
            File.WriteAllBytes(file, binary);
            try
            {
                var start = new ProcessStartInfo(Validator, $"--target-env vulkan1.3 \"{file}\"")
                {
                    RedirectStandardError = true,
                    RedirectStandardOutput = true
                };
                using Process process = Process.Start(start)!;
                string output = process.StandardError.ReadToEnd() + process.StandardOutput.ReadToEnd();
                process.WaitForExit();
                Assert.True(process.ExitCode == 0, "spirv-val rechaza el módulo:\n" + output + "\n" + module.ToText());
            }
            finally
            {
                File.Delete(file);
            }
        }

        return module.ToText();
    }

    private static string? FindValidator()
    {
        string? fromEnvironment = Environment.GetEnvironmentVariable("SPIRV_VAL");
        if (!string.IsNullOrEmpty(fromEnvironment) && File.Exists(fromEnvironment)) return fromEnvironment;

        var candidates = (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator)
            .Append("/opt/local/bin").Append("/opt/homebrew/bin").Append("/usr/local/bin")
            .Select(dir => Path.Combine(dir, OperatingSystem.IsWindows() ? "spirv-val.exe" : "spirv-val"));
        return candidates.FirstOrDefault(File.Exists);
    }
}
