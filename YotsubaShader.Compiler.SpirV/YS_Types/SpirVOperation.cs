#region MIT License - Copyright (c) 2026 Memphis Yomael
/*
╔════════════════════════════════════════════════════════════════════════════════════╗
║                                                                                    ║
║  ███╗   ███╗██╗████████╗    ██╗     ██╗ ██████╗███████╗███╗   ██╗███████╗███████╗  ║
║  ████╗ ████║██║╚══██╔══╝    ██║     ██║██╔════╝██╔════╝████╗  ██║██╔════╝██╔════╝  ║
║  ██╔████╔██║██║   ██║       ██║     ██║██║     █████╗  ██╔██╗ ██║███████╗█████╗    ║
║  ██║╚██╔╝██║██║   ██║       ██║     ██║██║     ██╔══╝  ██║╚██╗██║╚════██║██╔══╝    ║
║  ██║ ╚═╝ ██║██║   ██║       ███████╗██║╚██████╗███████╗██║ ╚████║███████║███████╗  ║
║  ╚═╝     ╚═╝╚═╝   ╚═╝       ╚══════╝╚═╝ ╚═════╝╚══════╝╚═╝  ╚═══╝╚══════╝╚══════╝  ║
║                                                                                    ║
╠════════════════════════════════════════════════════════════════════════════════════╣
║  Copyright (c) 2026 Memphis Yomael                                                 ║
╠════════════════════════════════════════════════════════════════════════════════════╣
║                                                                                    ║
║  Permission is hereby granted, free of charge, to any person obtaining a copy      ║
║  of this software and associated documentation files (the "Software"), to deal     ║
║  in the Software without restriction, including without limitation the rights      ║
║  to use, copy, modify, merge, publish, distribute, sublicense, and/or sell         ║
║  copies of the Software, and to permit persons to whom the Software is             ║
║  furnished to do so, subject to the following conditions:                          ║
║                                                                                    ║
║  The above copyright notice and this permission notice shall be included           ║
║  in all copies or substantial portions of the Software.                            ║
║                                                                                    ║
║  THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR        ║
║  IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,          ║
║  FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE       ║
║  AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER            ║
║  LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,     ║
║  OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE     ║
║  SOFTWARE.                                                                         ║
║                                                                                    ║
╚════════════════════════════════════════════════════════════════════════════════════╝
*/
#endregion

using YotsubaShader.Compiler.YS_Types;

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