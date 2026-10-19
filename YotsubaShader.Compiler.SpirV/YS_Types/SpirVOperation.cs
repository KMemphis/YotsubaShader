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


namespace YotsubaShader.Compiler.SpirV.YS_Types;

/// <summary>
/// Metadatos de una instrucción SPIR-V, generados desde <c>spirv.core.grammar.json</c>.
/// </summary>
/// <param name="Name">Nombre oficial, p. ej. <c>"OpFAdd"</c>.</param>
/// <param name="Opcode">Código de operación (16 bits bajos de la palabra 0).</param>
/// <param name="Class">Clase de instrucción de la especificación.</param>
/// <param name="HasResultType">Lleva operando <c>&lt;id&gt; Result Type</c>.</param>
/// <param name="HasResult">Produce un <c>Result &lt;id&gt;</c>.</param>
/// <param name="MinWordCount">Palabras mínimas, cabecera incluida.</param>
/// <param name="IsVariableLength">Admite operandos opcionales, listas, cadenas o parámetros de enumerantes.</param>
/// <param name="Version">Versión mínima del núcleo en formato de palabra de cabecera
/// (<c>0x00010300</c> = 1.3); <c>0</c> si solo existe mediante capacidad o extensión.</param>
public readonly record struct SpirVOperation(
    string Name,
    SpirVOp Opcode,
    SpirVOpClass Class,
    bool HasResultType,
    bool HasResult,
    ushort MinWordCount,
    bool IsVariableLength,
    uint Version)
{
    /// <summary>El opcode como número (el antiguo <c>No</c>).</summary>
     public ushort Value => (ushort)Opcode;

    /// <summary>Pertenece al núcleo de alguna versión de SPIR-V.</summary>
    public bool IsCore => Version != 0;

    /// <summary>¿Puede usarse en un módulo con esta versión (formato de palabra 1 del módulo)?</summary>
    public bool IsAvailableIn(uint moduleVersion) => IsCore && Version <= moduleVersion;

    /// <summary>
    /// Palabra 0 de la instrucción: <c>(wordCount &lt;&lt; 16) | opcode</c>.
    /// </summary>
    /// <param name="wordCount">Total de palabras de la instrucción, cabecera incluida.</param>
    public uint Header(int wordCount)
    {
        if (wordCount < MinWordCount || wordCount > ushort.MaxValue ||
            (!IsVariableLength && wordCount != MinWordCount))
        {
            throw new ArgumentOutOfRangeException(nameof(wordCount), wordCount,
                $"{Name} no admite {wordCount} palabras (mínimo {MinWordCount}" +
                (IsVariableLength ? ")" : ", longitud fija)"));
        }

        return ((uint)wordCount << 16) | Value;
    }

    /// <summary>Palabra 0 de una instrucción de longitud fija.</summary>
    public uint FixedHeader => IsVariableLength
        ? throw new InvalidOperationException($"{Name} tiene longitud variable; usa Header(wordCount)")
        : ((uint)MinWordCount << 16) | Value;

    public override string ToString() => Name;

    public static implicit operator SpirVOp(SpirVOperation operation) => operation.Opcode;
}