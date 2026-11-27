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
using System.Text;

namespace YotsubaShader.Extensions;

/// <summary>
/// Separa cada <c>sampler2D</c> de GLSL (una variable de tipo imagen+sampler) en una textura y un sampler, que es lo que
/// WGSL (y por tanto naga) exige. La textura conserva el set y el binding originales y el sampler recibe el siguiente
/// binding libre del mismo set. Cada <c>OpLoad</c> de la variable pasa a cargar las dos y a combinarlas con
/// <c>OpSampledImage</c>, así que las instrucciones de muestreo no cambian.
/// </summary>
/// <remarks>
/// No toca arrays de samplers ni variables que se pasan como argumento de una función: en esos casos devuelve el módulo
/// tal cual y quien llama decide qué hacer. Es C# puro y no cambia la semántica del shader.
/// </remarks>
public static class SpirvSamplers
{
    private const ushort OpName = 5;
    private const ushort OpEntryPoint = 15;
    private const ushort OpTypeImage = 25;
    private const ushort OpTypeSampler = 26;
    private const ushort OpTypeSampledImage = 27;
    private const ushort OpTypePointer = 32;
    private const ushort OpVariable = 59;
    private const ushort OpLoad = 61;
    private const ushort OpSampledImage = 86;
    private const ushort OpDecorate = 71;
    private const uint StorageUniformConstant = 0;
    private const uint DecorationBinding = 33;
    private const uint DecorationDescriptorSet = 34;

    /// <summary>Devuelve el módulo con los samplers combinados separados, o el mismo módulo si no hay ninguno que separar.</summary>
    public static byte[] SplitCombined(byte[] spirv)
    {
        if (spirv.Length < 20 || spirv.Length % 4 != 0)
        {
            return spirv;
        }

        uint[] words = new uint[spirv.Length / 4];
        for (int index = 0; index < words.Length; index++)
        {
            words[index] = BinaryPrimitives.ReadUInt32LittleEndian(spirv.AsSpan(index * 4));
        }

        if (words[0] != 0x07230203)
        {
            return spirv;
        }

        var instructions = new List<uint[]>();
        for (int at = 5; at < words.Length;)
        {
            int count = (int)(words[at] >> 16);
            if (count == 0 || at + count > words.Length)
            {
                return spirv; // módulo dañado: no se toca
            }

            instructions.Add(words[at..(at + count)]);
            at += count;
        }

        uint Op(uint[] instruction) => instruction[0] & 0xFFFF;

        // ---- 1. Qué hay ----
        var sampledImageOf = new Dictionary<uint, uint>();   // tipo imagen+sampler -> tipo imagen
        var pointerToSampled = new Dictionary<uint, uint>(); // puntero UniformConstant a imagen+sampler -> tipo imagen+sampler
        var pointerTo = new Dictionary<uint, uint>();        // pointee -> puntero UniformConstant ya declarado
        uint samplerType = 0;
        int samplerTypeAt = -1;
        var imageTypeAt = new Dictionary<uint, int>();
        var pointerAt = new Dictionary<uint, int>();
        var names = new Dictionary<uint, int>();
        var setOf = new Dictionary<uint, uint>();
        var bindingOf = new Dictionary<uint, uint>();
        var lastDecorationAt = new Dictionary<uint, int>();

        for (int index = 0; index < instructions.Count; index++)
        {
            uint[] i = instructions[index];
            switch (Op(i))
            {
                case OpName when i.Length >= 3:
                    names[i[1]] = index;
                    break;
                case OpTypeImage:
                    imageTypeAt[i[1]] = index;
                    break;
                case OpTypeSampler:
                    samplerType = i[1];
                    samplerTypeAt = index;
                    break;
                case OpTypeSampledImage:
                    sampledImageOf[i[1]] = i[2];
                    break;
                case OpTypePointer when i[2] == StorageUniformConstant:
                    pointerTo[i[3]] = i[1];
                    pointerAt[i[1]] = index;
                    if (sampledImageOf.ContainsKey(i[3]))
                    {
                        pointerToSampled[i[1]] = i[3];
                    }

                    break;
                case OpDecorate when i.Length >= 4 && i[2] == DecorationDescriptorSet:
                    setOf[i[1]] = i[3];
                    lastDecorationAt[i[1]] = index;
                    break;
                case OpDecorate when i.Length >= 4 && i[2] == DecorationBinding:
                    bindingOf[i[1]] = i[3];
                    lastDecorationAt[i[1]] = index;
                    break;
            }
        }

        var variables = new List<(int At, uint Id, uint Sampled, uint Image)>();
        for (int index = 0; index < instructions.Count; index++)
        {
            uint[] i = instructions[index];
            if (Op(i) == OpVariable && i.Length >= 4 && i[3] == StorageUniformConstant && pointerToSampled.TryGetValue(i[1], out uint sampled))
            {
                variables.Add((index, i[2], sampled, sampledImageOf[sampled]));
            }
        }

        if (variables.Count == 0)
        {
            return spirv;
        }

        // Sólo se puede separar si la variable se usa únicamente con OpLoad (y nombre, decoración e interfaz del entry point).
        // Se miran sólo las instrucciones que podrían llevarla como operando de otro modo (llamadas, accesos, copias, phi):
        // comparar cualquier palabra confundiría un literal con un id.
        var ids = variables.Select(v => v.Id).ToHashSet();
        foreach (uint[] i in instructions)
        {
            uint op = Op(i);
            bool risky = op is 57 or 62 or 65 or 66 or 67 or 83 or 245; // FunctionCall, Store, AccessChain*, CopyObject, Phi
            if (!risky)
            {
                continue;
            }

            for (int w = 1; w < i.Length; w++)
            {
                if (ids.Contains(i[w]))
                {
                    return spirv;
                }
            }
        }

        // ---- 2. Ids y tipos nuevos ----
        uint next = words[3];
        uint NewId() => next++;
        int firstVariableAt = variables.Min(v => v.At);
        var newTypes = new List<uint[]>();

        if (samplerType == 0)
        {
            samplerType = NewId();
            newTypes.Add([(2u << 16) | OpTypeSampler, samplerType]);
        }
        else if (samplerTypeAt > firstVariableAt)
        {
            return spirv; // el tipo existe pero se declara después de donde hace falta: no se reordena
        }

        uint PointerTo(uint pointee)
        {
            if (pointerTo.TryGetValue(pointee, out uint existing))
            {
                return pointerAt[existing] < firstVariableAt ? existing : 0;
            }

            uint created = NewId();
            newTypes.Add([(4u << 16) | OpTypePointer, created, StorageUniformConstant, pointee]);
            pointerTo[pointee] = created;
            pointerAt[created] = -1;
            return created;
        }

        uint samplerPointer = PointerTo(samplerType);
        if (samplerPointer == 0)
        {
            return spirv;
        }

        var imagePointer = new Dictionary<uint, uint>();
        foreach (uint image in variables.Select(v => v.Image).Distinct())
        {
            if (imageTypeAt.TryGetValue(image, out int imageAt) && imageAt > firstVariableAt)
            {
                return spirv;
            }

            uint pointer = PointerTo(image);
            if (pointer == 0)
            {
                return spirv;
            }

            imagePointer[image] = pointer;
        }

        // ---- 3. Reescritura ----
        var samplerVariable = new Dictionary<uint, uint>(); // variable original -> variable del sampler
        var nextBinding = new Dictionary<uint, uint>();      // set -> siguiente binding libre
        foreach (var (id, binding) in bindingOf)
        {
            uint set = setOf.GetValueOrDefault(id);
            nextBinding[set] = Math.Max(nextBinding.GetValueOrDefault(set), binding + 1);
        }

        var insertAfter = new Dictionary<int, List<uint[]>>();
        void After(int index, uint[] instruction)
        {
            if (!insertAfter.TryGetValue(index, out List<uint[]>? list))
            {
                insertAfter[index] = list = [];
            }

            list.Add(instruction);
        }

        foreach (var (at, id, _, image) in variables)
        {
            uint sampler = NewId();
            samplerVariable[id] = sampler;
            instructions[at] = [instructions[at][0], imagePointer[image], id, StorageUniformConstant];
            After(at, [(4u << 16) | OpVariable, samplerPointer, sampler, StorageUniformConstant]);

            if (names.TryGetValue(id, out int nameAt))
            {
                After(nameAt, Name(sampler, ReadName(instructions[nameAt]) + "_sampler"));
            }

            uint set = setOf.GetValueOrDefault(id);
            uint binding = nextBinding.GetValueOrDefault(set);
            nextBinding[set] = binding + 1;
            int decorationAt = lastDecorationAt.TryGetValue(id, out int found) ? found : at;
            After(decorationAt, [(4u << 16) | OpDecorate, sampler, DecorationDescriptorSet, set]);
            After(decorationAt, [(4u << 16) | OpDecorate, sampler, DecorationBinding, binding]);
        }

        var output = new List<uint>(words.Length + 64) { words[0], words[1], words[2], 0, words[4] };
        void Emit(uint[] instruction) => output.AddRange(instruction);

        for (int index = 0; index < instructions.Count; index++)
        {
            uint[] i = instructions[index];
            if (index == firstVariableAt)
            {
                foreach (uint[] type in newTypes)
                {
                    Emit(type);
                }
            }

            uint op = Op(i);
            if (op == OpLoad && i.Length >= 4 && ids.Contains(i[3]) && variables.First(v => v.Id == i[3]) is var (_, _, sampled, imageType))
            {
                // %r = OpLoad %sampled %v  ->  %img = OpLoad %image %v ; %smp = OpLoad %sampler %v_sampler ; %r = OpSampledImage %sampled %img %smp
                uint img = NewId();
                uint smp = NewId();
                Emit([(4u << 16) | OpLoad, imageType, img, i[3]]);
                Emit([(4u << 16) | OpLoad, samplerType, smp, samplerVariable[i[3]]]);
                Emit([(5u << 16) | OpSampledImage, sampled, i[2], img, smp]);
            }
            else if (op == OpEntryPoint)
            {
                Emit(WithInterface(i, samplerVariable));
            }
            else
            {
                Emit(i);
            }

            if (insertAfter.TryGetValue(index, out List<uint[]>? extra))
            {
                foreach (uint[] instruction in extra)
                {
                    Emit(instruction);
                }
            }
        }

        output[3] = next;
        byte[] result = new byte[output.Count * 4];
        for (int index = 0; index < output.Count; index++)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(index * 4), output[index]);
        }

        return result;
    }

    // OpEntryPoint: [1] modelo, [2] función, [3..] nombre (cadena), luego la interfaz. Desde SPIR-V 1.4 la interfaz nombra
    // todas las variables globales que usa el entry point, así que el sampler nuevo debe ir donde está la textura.
    private static uint[] WithInterface(uint[] entry, Dictionary<uint, uint> samplerVariable)
    {
        int at = 3;
        while (at < entry.Length && !HasZeroByte(entry[at]))
        {
            at++;
        }

        at++; // la palabra con el cero que cierra la cadena
        var extra = new List<uint>();
        for (int w = at; w < entry.Length; w++)
        {
            if (samplerVariable.TryGetValue(entry[w], out uint sampler))
            {
                extra.Add(sampler);
            }
        }

        if (extra.Count == 0)
        {
            return entry;
        }

        uint[] result = new uint[entry.Length + extra.Count];
        entry.CopyTo(result, 0);
        extra.CopyTo(result, entry.Length);
        result[0] = ((uint)result.Length << 16) | (entry[0] & 0xFFFF);
        return result;
    }

    private static bool HasZeroByte(uint word) =>
        (word & 0xFF) == 0 || ((word >> 8) & 0xFF) == 0 || ((word >> 16) & 0xFF) == 0 || (word >> 24) == 0;

    private static string ReadName(uint[] instruction)
    {
        var bytes = new List<byte>();
        for (int w = 2; w < instruction.Length; w++)
        {
            for (int shift = 0; shift < 32; shift += 8)
            {
                byte value = (byte)(instruction[w] >> shift);
                if (value == 0)
                {
                    return Encoding.UTF8.GetString(bytes.ToArray());
                }

                bytes.Add(value);
            }
        }

        return Encoding.UTF8.GetString(bytes.ToArray());
    }

    private static uint[] Name(uint target, string name)
    {
        byte[] text = Encoding.UTF8.GetBytes(name);
        int wordCount = text.Length / 4 + 1; // siempre hay al menos un byte cero al final
        uint[] instruction = new uint[2 + wordCount];
        instruction[0] = ((uint)instruction.Length << 16) | OpName;
        instruction[1] = target;
        for (int index = 0; index < text.Length; index++)
        {
            instruction[2 + index / 4] |= (uint)text[index] << (8 * (index % 4));
        }

        return instruction;
    }
}
