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

using System.Buffers.Binary;

namespace YotsubaShader.Extensions;

/// <summary>
/// Cambia la versión de un módulo SPIR-V ya compilado. El compilador emite SPIR-V 1.6; las GPU y drivers más
/// antiguos (Vulkan 1.1 admite hasta 1.3, Vulkan 1.2 hasta 1.5) necesitan un módulo de la versión que soportan.
/// </summary>
/// <remarks>
/// Soporta 1.3 a 1.6 (Vulkan 1.1 en adelante). Es C# puro: reescribe la cabecera y, antes de 1.4, la lista de
/// interfaz de cada <c>OpEntryPoint</c>, que hasta 1.3 sólo puede nombrar variables Input y Output (desde 1.4 debe
/// nombrar todas las globales que usa el entry point). No cambia la semántica del shader.
/// </remarks>
public static class SpirvVersion
{
    public const int MinMinor = 3;
    public const int MaxMinor = 6;

    private const uint Magic = 0x07230203;
    private const ushort OpEntryPoint = 15;
    private const ushort OpExtension = 10;
    private const ushort OpDecorate = 71;
    private const ushort OpMemberDecorate = 72;
    private const ushort OpDecorateId = 332;
    private const ushort OpDecorateString = 5632;
    private const ushort OpMemberDecorateString = 5633;
    private const ushort OpVariable = 59;
    private const uint StorageInput = 1;
    private const uint StorageOutput = 3;

    // Instrucciones que sólo existen a partir de una versión: si el módulo las usa no se puede bajar de versión.
    private static readonly (ushort Opcode, int Minor, string Name)[] VersionedOpcodes =
    [
        (400, 4, "OpCopyLogical"), (401, 4, "OpPtrEqual"), (402, 4, "OpPtrNotEqual"), (403, 4, "OpPtrDiff"),
        (4416, 6, "OpTerminateInvocation"), (5380, 6, "OpDemoteToHelperInvocation"),
    ];

    /// <summary>La versión (major, minor) con la que se emitió el módulo.</summary>
    public static (int Major, int Minor) Read(byte[] spirv)
    {
        uint[] words = ToWords(spirv);
        return ((int)(words[1] >> 16) & 0xFF, (int)(words[1] >> 8) & 0xFF);
    }

    /// <summary>Devuelve el módulo con la versión pedida (1.3 a 1.6). Lanza si el shader usa algo que esa versión no tiene.</summary>
    public static byte[] Retarget(byte[] spirv, int major, int minor)
    {
        if (major != 1 || minor < MinMinor || minor > MaxMinor)
        {
            throw new ArgumentOutOfRangeException(
                nameof(minor),
                $"SPIR-V {major}.{minor} no está soportado. Versiones disponibles: 1.{MinMinor} a 1.{MaxMinor}.");
        }

        // OpCopyLogical (1.4) se puede expresar con instrucciones de 1.0: se reescribe en vez de rechazar el shader.
        uint[] words = ToWords(minor < 4 ? LowerCopyLogical(spirv) : spirv);
        uint[] result = new uint[words.Length];
        Array.Copy(words, result, 5);
        result[1] = (uint)((major << 16) | (minor << 8));

        // Primera pasada: la clase de almacenamiento de cada variable (para filtrar las interfaces).
        var storage = new Dictionary<uint, uint>();
        for (int at = 5; at < words.Length;)
        {
            ushort opcode = (ushort)(words[at] & 0xFFFF);
            int count = (int)(words[at] >> 16);
            if (count == 0 || at + count > words.Length)
            {
                throw new InvalidDataException("El módulo SPIR-V está truncado o malformado.");
            }

            if (opcode == OpVariable && count >= 4)
            {
                storage[words[at + 2]] = words[at + 3];
            }

            foreach ((ushort versionedOpcode, int requiredMinor, string name) in VersionedOpcodes)
            {
                if (opcode == versionedOpcode && minor < requiredMinor)
                {
                    throw new NotSupportedException(
                        $"El shader usa {name}, que requiere SPIR-V 1.{requiredMinor}; no se puede emitir 1.{minor}.");
                }
            }

            at += count;
        }

        int outAt = 5;
        for (int at = 5; at < words.Length;)
        {
            ushort opcode = (ushort)(words[at] & 0xFFFF);
            int count = (int)(words[at] >> 16);

            if (opcode == OpEntryPoint && minor < 4)
            {
                // [cabecera, modelo, id, nombre (palabras hasta el NUL), interfaz...]
                int nameStart = at + 3;
                int nameEnd = nameStart;
                while (nameEnd < at + count && !HasZeroByte(words[nameEnd]))
                {
                    nameEnd++;
                }

                nameEnd++; // la palabra con el NUL pertenece al nombre

                var kept = new List<uint>();
                for (int index = nameEnd; index < at + count; index++)
                {
                    uint id = words[index];
                    if (storage.TryGetValue(id, out uint cls) && cls is StorageInput or StorageOutput)
                    {
                        kept.Add(id);
                    }
                }

                int newCount = (nameEnd - at) + kept.Count;
                result[outAt] = (uint)((newCount << 16) | OpEntryPoint);
                Array.Copy(words, at + 1, result, outAt + 1, nameEnd - at - 1);
                for (int index = 0; index < kept.Count; index++)
                {
                    result[outAt + (nameEnd - at) + index] = kept[index];
                }

                outAt += newCount;
            }
            else
            {
                Array.Copy(words, at, result, outAt, count);
                outAt += count;
            }

            at += count;
        }

        byte[] bytes = new byte[outAt * sizeof(uint)];
        for (int index = 0; index < outAt; index++)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(index * sizeof(uint)), result[index]);
        }

        return bytes;
    }

    private const ushort OpTypeArray = 28;
    private const ushort OpTypeStruct = 30;
    private const ushort OpConstant = 43;
    private const ushort OpCompositeConstruct = 80;
    private const ushort OpCompositeExtract = 81;
    private const ushort OpCopyLogical = 400;

    /// <summary>
    /// Reescribe cada <c>OpCopyLogical</c> (copia entre dos tipos con la misma forma y distinto layout, como un struct
    /// de un buffer y el mismo struct en una variable local) con <c>OpCompositeExtract</c> + <c>OpCompositeConstruct</c>
    /// miembro a miembro. Es equivalente, existe desde SPIR-V 1.0, y lo aceptan los consumidores que no conocen
    /// <c>OpCopyLogical</c> (naga para WGSL) o las versiones anteriores a 1.4.
    /// </summary>
    public static byte[] LowerCopyLogical(byte[] spirv)
    {
        uint[] words = ToWords(spirv);
        var structs = new Dictionary<uint, uint[]>();
        var arrays = new Dictionary<uint, (uint Element, uint Length)>();
        var constants = new Dictionary<uint, uint>();
        bool found = false;

        for (int at = 5; at < words.Length;)
        {
            ushort opcode = (ushort)(words[at] & 0xFFFF);
            int count = (int)(words[at] >> 16);
            if (count == 0 || at + count > words.Length)
            {
                throw new InvalidDataException("El módulo SPIR-V está truncado o malformado.");
            }

            switch (opcode)
            {
                case OpTypeStruct:
                    structs[words[at + 1]] = words[(at + 2)..(at + count)];
                    break;
                case OpTypeArray:
                    arrays[words[at + 1]] = (words[at + 2], words[at + 3]);
                    break;
                case OpConstant when count == 4:
                    constants[words[at + 2]] = words[at + 3];
                    break;
                case OpCopyLogical:
                    found = true;
                    break;
            }

            at += count;
        }

        if (!found)
        {
            return spirv;
        }

        // Tipo de cada id de resultado (para conocer el tipo del operando de cada OpCopyLogical).
        var resultTypes = new Dictionary<uint, uint>();
        for (int at = 5; at < words.Length;)
        {
            ushort opcode = (ushort)(words[at] & 0xFFFF);
            int count = (int)(words[at] >> 16);
            if (HasResultType(opcode) && count >= 3)
            {
                resultTypes[words[at + 2]] = words[at + 1];
            }

            at += count;
        }

        uint bound = words[3];
        var output = new List<uint>(words.Length * 2);
        output.AddRange(words.Take(5));

        for (int at = 5; at < words.Length;)
        {
            ushort opcode = (ushort)(words[at] & 0xFFFF);
            int count = (int)(words[at] >> 16);

            if (opcode != OpCopyLogical)
            {
                output.AddRange(words.AsSpan(at, count).ToArray());
                at += count;
                continue;
            }

            uint targetType = words[at + 1];
            uint result = words[at + 2];
            uint operand = words[at + 3];
            uint sourceType = resultTypes.TryGetValue(operand, out uint type)
                ? type
                : throw new InvalidDataException($"OpCopyLogical: no se conoce el tipo de %{operand}.");

            Convert(targetType, sourceType, operand, result);
            at += count;
        }

        output[3] = bound;

        byte[] bytes = new byte[output.Count * sizeof(uint)];
        for (int index = 0; index < output.Count; index++)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(index * sizeof(uint)), output[index]);
        }

        return bytes;

        // Emite las instrucciones que dejan en `result` (o en un id nuevo) el valor de `value` convertido a `target`.
        uint Convert(uint target, uint source, uint value, uint? result = null)
        {
            if (target == source)
            {
                if (result is null)
                {
                    return value;
                }

                // Mismo tipo: el resultado es una copia del valor (OpCopyObject, opcode 83).
                Emit(83, target, result.Value, value);
                return result.Value;
            }

            uint[] targetMembers;
            uint[] sourceMembers;

            if (structs.TryGetValue(target, out uint[]? targetStruct) &&
                structs.TryGetValue(source, out uint[]? sourceStruct) &&
                targetStruct.Length == sourceStruct.Length)
            {
                targetMembers = targetStruct;
                sourceMembers = sourceStruct;
            }
            else if (arrays.TryGetValue(target, out var targetArray) &&
                     arrays.TryGetValue(source, out var sourceArray) &&
                     constants.TryGetValue(targetArray.Length, out uint length) &&
                     constants.TryGetValue(sourceArray.Length, out uint sourceLength) &&
                     length == sourceLength)
            {
                targetMembers = Enumerable.Repeat(targetArray.Element, (int)length).ToArray();
                sourceMembers = Enumerable.Repeat(sourceArray.Element, (int)length).ToArray();
            }
            else
            {
                throw new NotSupportedException(
                    $"OpCopyLogical entre %{source} y %{target}: los tipos no tienen la misma forma.");
            }

            var parts = new uint[targetMembers.Length];
            for (int index = 0; index < targetMembers.Length; index++)
            {
                uint extracted = bound++;
                Emit(OpCompositeExtract, sourceMembers[index], extracted, value, (uint)index);
                parts[index] = Convert(targetMembers[index], sourceMembers[index], extracted);
            }

            uint id = result ?? bound++;
            Emit(OpCompositeConstruct, target, id, parts);
            return id;
        }

        void Emit(ushort code, uint resultType, uint resultId, params uint[] operands)
        {
            output.Add((uint)((3 + operands.Length) << 16) | code);
            output.Add(resultType);
            output.Add(resultId);
            output.AddRange(operands);
        }
    }

    // Instrucciones con <tipo de resultado, id de resultado> que pueden ser el operando de un OpCopyLogical.
    private static bool HasResultType(ushort opcode) => opcode is
        43 or 44 or 46 // OpConstant, OpConstantComposite, OpConstantNull
        or 57 // OpFunctionCall
        or 61 // OpLoad
        or 80 or 81 or 82 or 83 // OpCompositeConstruct, Extract, Insert, OpCopyObject
        or 245 // OpPhi
        or 169 // OpSelect
        or OpCopyLogical;

    /// <summary>
    /// Quita las extensiones <c>SPV_GOOGLE_decorate_string</c> / <c>SPV_GOOGLE_hlsl_functionality1</c> y las
    /// decoraciones que introducen (semánticas HLSL, tipo de usuario, buffer contador). Son informativas: no cambian
    /// la ejecución, pero Vulkan sin <c>VK_GOOGLE_*</c> y algunos consumidores (naga) rechazan el módulo con ellas.
    /// </summary>
    public static byte[] StripGoogleExtensions(byte[] spirv)
    {
        uint[] words = ToWords(spirv);
        var result = new List<uint>(words.Length);
        result.AddRange(words.Take(5));

        for (int at = 5; at < words.Length;)
        {
            ushort opcode = (ushort)(words[at] & 0xFFFF);
            int count = (int)(words[at] >> 16);
            if (count == 0 || at + count > words.Length)
            {
                throw new InvalidDataException("El módulo SPIR-V está truncado o malformado.");
            }

            bool drop = opcode switch
            {
                OpExtension => ReadString(words, at + 1, at + count).StartsWith("SPV_GOOGLE_", StringComparison.Ordinal),
                OpDecorateString or OpMemberDecorateString => true,
                OpDecorate or OpDecorateId => IsGoogleDecoration(words[at + 2]),
                OpMemberDecorate => IsGoogleDecoration(words[at + 3]),
                _ => false
            };

            if (!drop)
            {
                for (int index = 0; index < count; index++)
                {
                    result.Add(words[at + index]);
                }
            }

            at += count;
        }

        byte[] bytes = new byte[result.Count * sizeof(uint)];
        for (int index = 0; index < result.Count; index++)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(index * sizeof(uint)), result[index]);
        }

        return bytes;
    }

    // HlslCounterBufferGOOGLE (5634), HlslSemanticGOOGLE/UserSemantic (5635), UserTypeGOOGLE (5636).
    private static bool IsGoogleDecoration(uint decoration) => decoration is >= 5634 and <= 5636;

    private static string ReadString(uint[] words, int start, int end)
    {
        var bytes = new List<byte>();
        for (int index = start; index < end; index++)
        {
            for (int shift = 0; shift < 32; shift += 8)
            {
                byte value = (byte)(words[index] >> shift);
                if (value == 0)
                {
                    return System.Text.Encoding.UTF8.GetString(bytes.ToArray());
                }

                bytes.Add(value);
            }
        }

        return System.Text.Encoding.UTF8.GetString(bytes.ToArray());
    }

    private static bool HasZeroByte(uint word) =>
        (word & 0xFF) == 0 || ((word >> 8) & 0xFF) == 0 || ((word >> 16) & 0xFF) == 0 || (word >> 24) == 0;

    private static uint[] ToWords(byte[] spirv)
    {
        if (spirv.Length < 20 || spirv.Length % 4 != 0)
        {
            throw new InvalidDataException("El módulo SPIR-V tiene un tamaño inválido.");
        }

        uint[] words = new uint[spirv.Length / 4];
        for (int index = 0; index < words.Length; index++)
        {
            words[index] = BinaryPrimitives.ReadUInt32LittleEndian(spirv.AsSpan(index * 4));
        }

        if (words[0] != Magic)
        {
            throw new InvalidDataException("El módulo no empieza con el número mágico de SPIR-V.");
        }

        return words;
    }
}
