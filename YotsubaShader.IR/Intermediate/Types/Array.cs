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

using YotsubaShader.IR.Intermediate.Slot;

namespace YotsubaShader.IR.Intermediate.Types;

/// <summary>
/// Representa una secuencia homogénea e indexable de elementos en el shader.
/// </summary>
/// <remarks>
/// Lectura y escritura:
/// - T[]: acceso directo mediante [i].
/// - Span&lt;T&gt;: acceso directo; solo para parámetros o variables locales.
/// - Memory&lt;T&gt;: admite campos globales; se accede mediante .Span[i].
/// - ArraySegment&lt;T&gt;: vista indexable de una parte de un array.
///
/// Solo lectura desde el shader:
/// - ReadOnlySpan&lt;T&gt;: acceso directo; solo para parámetros o variables locales.
/// - ReadOnlyMemory&lt;T&gt;: admite campos globales; se accede mediante .Span[i].
/// - ImmutableArray&lt;T&gt;: admite campos globales y acceso directo mediante [i].
///
/// Adaptables si el frontend copia sus elementos a un buffer:
/// - List&lt;T&gt; / IList&lt;T&gt;: lectura y escritura por índice.
/// - IReadOnlyList&lt;T&gt; / ReadOnlyCollection&lt;T&gt;: lectura por índice.
///
/// Add, Remove y cambios de longitud no son operaciones del array de GPU.
/// IEnumerable&lt;T&gt; por sí solo no basta: no garantiza índice ni longitud.
/// El frontend determina cuáles de estas formas son recursos globales
/// y cuáles son arrays locales o parámetros.
/// </remarks>
public record Array(string HumanName, StorageKind Kind, InterfaceSlot? Slot) : Storage(HumanName, Kind, Slot)
{
    public required Storage ElementType;
    public required uint? Length;
}