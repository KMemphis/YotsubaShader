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

using System.Globalization;
using YotsubaShader.Compiler.SpirV.Builder;
using YotsubaShader.Compiler.SpirV.YS_Types;
using YotsubaShader.IR.Intermediate;
using IRTypes = YotsubaShader.IR.Intermediate.Types;

namespace YotsubaShader.Compiler.SpirV;

/// <summary>La regla de disposición de un bloque de memoria compartido con la aplicación.</summary>
internal enum BufferLayoutPolicy
{
    /// <summary>Uniform buffers: std140. Alineaciones de struct y de vectores de 3 o más componentes a 16 bytes.</summary>
    Std140,

    /// <summary>Storage buffers y push constants: std430. Como std140, sin redondear a 16 los structs.</summary>
    Std430
}

/// <summary>
/// Política de disposición de los recursos de tipo buffer.
///
/// Un recurso Uniform sigue std140; uno StorageBuffer o PushConstant sigue std430. Cada struct de recurso
/// se decora como Block, y cada miembro recibe su Offset, que la aplicación debe usar tal cual en su lado.
///
/// Reglas (N = tamaño de un componente: 4 para int, uint y float; 8 para double):
///   escalar        alineación N, tamaño N
///   vector de 2    alineación 2N, tamaño 2N
///   vector de 3    alineación 4N, tamaño 3N (el miembro siguiente puede ocupar el cuarto componente)
///   vector de 4    alineación 4N, tamaño 4N
///   struct         alineación del mayor de sus miembros (std140: al menos 16); tamaño redondeado a su alineación
/// Cada miembro empieza en el siguiente múltiplo de su alineación.
///
/// Un miembro que no se puede representar (bool, matriz, array...) falla con un mensaje claro.
/// </summary>
internal static class BufferLayout
{
    public static bool IsBuffer(StorageKind kind) =>
        kind is StorageKind.Uniform or StorageKind.StorageBuffer or StorageKind.PushConstant;

    public static BufferLayoutPolicy PolicyFor(StorageKind kind) => kind switch
    {
        StorageKind.Uniform => BufferLayoutPolicy.Std140,
        StorageKind.StorageBuffer or StorageKind.PushConstant => BufferLayoutPolicy.Std430,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Only buffer resources have a layout.")
    };

    /// <summary>Alineación y tamaño de un miembro; falla si no tiene una disposición definida.</summary>
    public static (int Alignment, int Size) Measure(Storage storage, BufferLayoutPolicy policy, string path)
    {
        switch (storage)
        {
            case IRTypes.Float:
                return (4, 4);

            case IRTypes.Double:
                return (8, 8);

            case IRTypes.Half:
                return (2, 2);

            case IRTypes.Int { Bits: 16 }:
                return (2, 2);

            case IRTypes.Int { Bits: 32 }:
                return (4, 4);

            case IRTypes.Int { Bits: 64 }:
                return (8, 8);

            case IRTypes.Vector vector:
            {
                int component = vector.ComponentsType == IRTypes.VectorComponentsType.Double ? 8
                    : vector.ComponentsType == IRTypes.VectorComponentsType.Half ? 2 : 4;
                int length = vector.ComponentsLength;

                if (length is < 2 or > 4)
                {
                    throw Unsupported(path, $"a vector of {length} components");
                }

                return (length == 2 ? 2 * component : 4 * component, length * component);
            }

            case IRTypes.CustomStruct structure:
            {
                var (alignment, size, _) = MeasureStruct(structure, policy, path);
                return (alignment, size);
            }

            case IRTypes.Bool:
                throw Unsupported(path, "a bool, which is not allowed in a shared buffer; use a uint");

            case IRTypes.Matrix matrix:
            {
                // Columna a columna (ColMajor). std140 redondea el paso de cada columna a 16 bytes.
                int stride = MatrixStride(matrix, policy);
                return (stride, stride * (int)matrix.Columns);
            }

            case IRTypes.Array array:
            {
                var (_, stride, alignment) = ArrayLayout(array, policy, path);
                // Sin longitud: array sin tamaño, ocupa el resto del buffer (solo como último miembro).
                return (alignment, array.Length is uint length ? stride * (int)length : 0);
            }

            default:
                throw Unsupported(path, $"the type {storage.GetType().Name}");
        }
    }

    /// <summary>Alineación, tamaño y desplazamiento de cada miembro de un struct.</summary>
    public static (int Alignment, int Size, IReadOnlyList<int> Offsets) MeasureStruct(
        IRTypes.CustomStruct structure, BufferLayoutPolicy policy, string path)
    {
        var offsets = new List<int>(structure.Properties.Count);
        int cursor = 0;
        int alignment = 1;

        foreach (Storage member in structure.Properties)
        {
            var (memberAlignment, memberSize) =
                Measure(member, policy, $"{path}.{member.HumanName}");

            cursor = RoundUp(cursor, memberAlignment);
            offsets.Add(cursor);
            cursor += memberSize;
            alignment = Math.Max(alignment, memberAlignment);
        }

        if (policy == BufferLayoutPolicy.Std140)
        {
            alignment = RoundUp(alignment, 16);
        }

        return (alignment, RoundUp(cursor, alignment), offsets);
    }

    /// <summary>
    /// Decora el struct de un recurso como Block y da su Offset a cada miembro, también a los structs anidados.
    /// <paramref name="decorated"/> recuerda cómo se decoró cada struct: un mismo tipo no puede tener dos
    /// disposiciones distintas, pero sí repetirse si las dos políticas dan los mismos desplazamientos.
    /// </summary>
    public static void DecorateResource(SpirVModule module, Storage resource, Dictionary<string, (BufferLayoutPolicy Policy, IReadOnlyList<int> Offsets)> decorated)
    {
        if (resource is not IRTypes.CustomStruct structure)
        {
            throw Unsupported(resource.HumanName,
                $"a {resource.GetType().Name}; a {resource.Kind} resource must be a struct, which becomes a Block. " +
                "Wrap the value in a struct");
        }

        BufferLayoutPolicy policy = PolicyFor(resource.Kind);
        string path = $"{resource.Kind} resource '{resource.HumanName}'";

        // Falla antes de decorar nada si algún miembro no tiene disposición.
        MeasureStruct(structure, policy, path);

        string blockId = SpirVTableTypesIds.StructID(structure, Stage.None);

        // Dos recursos del mismo tipo comparten el struct: el Block se declara una sola vez.
        if (!decorated.ContainsKey(blockId))
        {
            module.AddLine(
                SpirVOpTable.OpDecorate,
                SpirVModuleHelpers.Id(blockId),
                SpirVInstructionParameter.Enumerant(SpirVDecoration.Block));
        }

        DecorateMembers(module, structure, policy, path, decorated);
    }

    private static void DecorateMembers(
        SpirVModule module,
        IRTypes.CustomStruct structure,
        BufferLayoutPolicy policy,
        string path,
        Dictionary<string, (BufferLayoutPolicy Policy, IReadOnlyList<int> Offsets)> decorated)
    {
        string structId = SpirVTableTypesIds.StructID(structure, Stage.None);

        var (_, _, offsets) = MeasureStruct(structure, policy, path);

        if (decorated.TryGetValue(structId, out var existing))
        {
            if (!existing.Offsets.SequenceEqual(offsets))
            {
                throw new NotSupportedException(
                    $"The struct '{structure.TypeName}' is used with two different buffer layouts " +
                    $"({existing.Policy} and {policy}), but a type can only have one. " +
                    "Use a separate struct for each kind of buffer.");
            }

            return;
        }

        decorated[structId] = (policy, offsets);

        for (int index = 0; index < structure.Properties.Count; index++)
        {
            module.AddLine(
                SpirVOpTable.OpMemberDecorate,
                SpirVModuleHelpers.Id(structId),
                new(index.ToString(CultureInfo.InvariantCulture), SpirVInstructionType.UIntValue),
                SpirVInstructionParameter.Enumerant(SpirVDecoration.Offset),
                new(offsets[index].ToString(CultureInfo.InvariantCulture), SpirVInstructionType.UIntValue));

            Storage member = structure.Properties[index];
            string memberPath = $"{path}.{member.HumanName}";

            // Arrays: ArrayStride en su tipo. Matrices (sueltas o dentro de un array): ColMajor y MatrixStride en el miembro.
            Storage inner = member;
            while (inner is IRTypes.Array array)
            {
                string arrayTypeId = module.DeclareType(array, Stage.None);
                var (_, stride, _) = ArrayLayout(array, policy, memberPath);
                if (!decorated.ContainsKey(arrayTypeId))
                {
                    decorated[arrayTypeId] = (policy, [stride]);
                    module.AddLine(SpirVOpTable.OpDecorate, SpirVModuleHelpers.Id(arrayTypeId),
                        SpirVInstructionParameter.Enumerant(SpirVDecoration.ArrayStride),
                        new(stride.ToString(CultureInfo.InvariantCulture), SpirVInstructionType.UIntValue));
                }

                inner = array.ElementType;
            }

            if (inner is IRTypes.Matrix matrix)
            {
                module.AddLine(SpirVOpTable.OpMemberDecorate, SpirVModuleHelpers.Id(structId),
                    new(index.ToString(CultureInfo.InvariantCulture), SpirVInstructionType.UIntValue),
                    SpirVInstructionParameter.Enumerant(SpirVDecoration.ColMajor));
                module.AddLine(SpirVOpTable.OpMemberDecorate, SpirVModuleHelpers.Id(structId),
                    new(index.ToString(CultureInfo.InvariantCulture), SpirVInstructionType.UIntValue),
                    SpirVInstructionParameter.Enumerant(SpirVDecoration.MatrixStride),
                    new(MatrixStride(matrix, policy).ToString(CultureInfo.InvariantCulture), SpirVInstructionType.UIntValue));
            }

            if (inner is IRTypes.CustomStruct nested)
            {
                DecorateMembers(module, nested, policy, memberPath, decorated);
            }
        }
    }

    /// <summary>Paso entre columnas de una matriz (decoración MatrixStride).</summary>
    public static int MatrixStride(IRTypes.Matrix matrix, BufferLayoutPolicy policy)
    {
        int component = matrix.ComponentsType switch
        {
            IRTypes.MatrixComponentType.Double => 8,
            IRTypes.MatrixComponentType.Half => 2,
            _ => 4
        };
        int columnAlignment = matrix.Rows == 2 ? 2 * component : 4 * component;
        return policy == BufferLayoutPolicy.Std140 ? RoundUp(columnAlignment, 16) : columnAlignment;
    }

    /// <summary>Paso entre elementos (ArrayStride) y alineación de un array según la política.</summary>
    public static (int ElementSize, int Stride, int Alignment) ArrayLayout(IRTypes.Array array, BufferLayoutPolicy policy, string path)
    {
        var (elementAlignment, elementSize) = Measure(array.ElementType, policy, path + "[]");
        int stride = RoundUp(elementSize, elementAlignment);
        int alignment = elementAlignment;
        if (policy == BufferLayoutPolicy.Std140)
        {
            stride = RoundUp(stride, 16);
            alignment = RoundUp(alignment, 16);
        }

        return (elementSize, stride, alignment);
    }

    private static int RoundUp(int value, int multiple) => (value + multiple - 1) / multiple * multiple;

    private static NotSupportedException Unsupported(string path, string what) =>
        new($"'{path}' is {what}, which has no defined buffer layout.");
}
