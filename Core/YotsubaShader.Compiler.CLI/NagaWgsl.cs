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

using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;

/// <summary>
/// SPIR-V → WGSL mediante naga (Rust). Solo vive en la CLI: la librería del compilador sigue siendo C# puro.
/// La biblioteca nativa se busca en <c>runtimes/&lt;rid&gt;/native/</c> junto a la CLI, o en la ruta de la variable
/// de entorno <c>YOTSUBA_NAGA_PATH</c>.
/// </summary>
internal static unsafe partial class NagaWgsl
{
    private const string LibraryName = "yotsuba_naga";

    [LibraryImport(LibraryName)]
    private static partial int ys_naga_spirv_to_wgsl(byte* spirv, nuint length, byte** output, nuint* outputLength);

    [LibraryImport(LibraryName)]
    private static partial void ys_naga_free(byte* pointer, nuint length);

    static NagaWgsl()
    {
        NativeLibrary.SetDllImportResolver(typeof(NagaWgsl).Assembly, Resolve);
    }

    public static string FromSpirV(byte[] spirv)
    {
        // naga no conoce OpCopyLogical (las copias de structs de buffers): se reescribe antes con instrucciones de 1.0.
        spirv = YotsubaShader.Extensions.SpirvVersion.LowerCopyLogical(spirv);

        byte* output = null;
        nuint outputLength = 0;
        int status;

        try
        {
            fixed (byte* input = spirv)
            {
                status = ys_naga_spirv_to_wgsl(input, (nuint)spirv.Length, &output, &outputLength);
            }
        }
        catch (Exception exception) when (exception is DllNotFoundException or EntryPointNotFoundException)
        {
            throw new InvalidOperationException(
                $"No se encontró la biblioteca nativa de naga para {RuntimeInformation.RuntimeIdentifier}. " +
                "Compila Core/YotsubaShader.Compiler.CLI/Native/naga con cargo, o define YOTSUBA_NAGA_PATH.",
                exception);
        }

        try
        {
            string text = Encoding.UTF8.GetString(output, checked((int)outputLength));

            return status == 0
                ? text
                : throw new InvalidOperationException($"naga no pudo convertir el SPIR-V a WGSL: {text}");
        }
        finally
        {
            ys_naga_free(output, outputLength);
        }
    }

    private static nint Resolve(string name, Assembly assembly, DllImportSearchPath? searchPath)
    {
        if (name != LibraryName)
        {
            return 0;
        }

        string file = OperatingSystem.IsWindows() ? "yotsuba_naga.dll"
            : OperatingSystem.IsMacOS() ? "libyotsuba_naga.dylib"
            : "libyotsuba_naga.so";

        string? overridePath = Environment.GetEnvironmentVariable("YOTSUBA_NAGA_PATH");
        string baseDirectory = AppContext.BaseDirectory;

        IEnumerable<string> candidates =
        [
            overridePath is null ? string.Empty : Directory.Exists(overridePath) ? Path.Combine(overridePath, file) : overridePath,
            Path.Combine(baseDirectory, "runtimes", RuntimeInformation.RuntimeIdentifier, "native", file),
            Path.Combine(baseDirectory, "runtimes", PortableRid(), "native", file),
            Path.Combine(baseDirectory, file)
        ];

        foreach (string candidate in candidates)
        {
            if (candidate.Length != 0 && File.Exists(candidate) && NativeLibrary.TryLoad(candidate, out nint handle))
            {
                return handle;
            }
        }

        return 0;
    }

    // RID sin versión de distribución (osx.15-arm64 → osx-arm64) para que coincida con la carpeta runtimes/.
    private static string PortableRid()
    {
        string os = OperatingSystem.IsWindows() ? "win" : OperatingSystem.IsMacOS() ? "osx" : "linux";
        string arch = RuntimeInformation.ProcessArchitecture switch
        {
            Architecture.Arm64 => "arm64",
            Architecture.X64 => "x64",
            Architecture.X86 => "x86",
            _ => RuntimeInformation.ProcessArchitecture.ToString().ToLowerInvariant()
        };

        return $"{os}-{arch}";
    }
}
