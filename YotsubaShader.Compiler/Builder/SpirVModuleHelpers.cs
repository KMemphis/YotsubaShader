using Microsoft.Xna.Framework.Graphics;
using YotsubaShader.Compiler.YS_Types;
using XnaVector2 = Microsoft.Xna.Framework.Vector2;
using NumVector2 = System.Numerics.Vector2;
using XnaVector3 = Microsoft.Xna.Framework.Vector3;
using NumVector3 = System.Numerics.Vector3;
using XnaVector4 = Microsoft.Xna.Framework.Vector4;
using NumVector4 = System.Numerics.Vector4;
using XnaMatrix = Microsoft.Xna.Framework.Matrix;
using NumMatrix = System.Numerics.Matrix4x4;

namespace YotsubaShader.Compiler.Builder;

public static class SpirVModuleHelpers
{
    /// <summary>
    /// Declara (una sola vez) el tipo SPIR-V equivalente y devuelve su ID. Si el tipo ya existe,
    /// devuelve el ID anterior: en SPIR-V los tipos simples no pueden repetirse. Los tipos de los
    /// que depende (p. ej. %float para un vector) se declaran antes, en orden de dependencias.
    /// </summary>
    public static string DeclareType(this SpirVModule module, Type type)
    {
        if (module.TryGetDeclaredType(type, out string existing))
        {
            return existing;
        }

        string id = EmitType(module, type);
        module.RegisterType(type, id);
        return id;
    }

    /// <summary><c>T[length]</c> en SPIR-V: <c>%arr_T_N = OpTypeArray %T %uint_N</c> (la longitud es una constante).</summary>
    public static string DeclareArray(this SpirVModule module, Type elementType, int length)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(length);

        string element = module.DeclareType(elementType);
        string lengthId = module.DeclareUIntConstant(length);
        string id = $"%arr_{element.TrimStart('%')}_{length}";

        return Define(module, id, SpirVOpTable.OpTypeArray,
            Id(element), Id(lengthId));
    }

    /// <summary><c>T[]</c> sin tamaño (solo storage buffers): <c>%rta_T = OpTypeRuntimeArray %T</c>.</summary>
    public static string DeclareRuntimeArray(this SpirVModule module, Type elementType)
    {
        string element = module.DeclareType(elementType);
        return Define(module, $"%rta_{element.TrimStart('%')}", SpirVOpTable.OpTypeRuntimeArray,
            Id(element));
    }

    /// <summary>Constante <c>uint</c>: <c>%uint_N = OpConstant %uint N</c>.</summary>
    public static string DeclareUIntConstant(this SpirVModule module, int value)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(value);

        string uintId = module.DeclareType(typeof(uint));
        return Define(module, SpirVTableTypesIds.UIntDinamicId(value), SpirVOpTable.OpConstant,
            Id(uintId), new SpirVInstructionParameter(value.ToString(), SpirVInstructionType.UIntValue));
    }

    private static string EmitType(SpirVModule module, Type type)
    {
        if (type == typeof(void))
            return Define(module, SpirVTableTypesIds.VoidID, SpirVOpTable.OpTypeVoid);

        if (type == typeof(bool))
            return Define(module, SpirVTableTypesIds.BoolID, SpirVOpTable.OpTypeBool);

        if (type == typeof(float))
            return Define(module, SpirVTableTypesIds.FloatID, SpirVOpTable.OpTypeFloat, Int(32));

        // La signatura del literal (1/0) es lo único que distingue int de uint.
        if (type == typeof(int))
            return Define(module, SpirVTableTypesIds.IntID, SpirVOpTable.OpTypeInt, Int(32), Int(1));

        if (type == typeof(uint))
            return Define(module, SpirVTableTypesIds.UIntID, SpirVOpTable.OpTypeInt, Int(32), Int(0));

        if (type == typeof(XnaVector2) || type == typeof(NumVector2))
            return DeclareFloatVector(module, 2);

        if (type == typeof(XnaVector3) || type == typeof(NumVector3))
            return DeclareFloatVector(module, 3);

        if ((type == typeof(XnaVector4) || type == typeof(NumVector4)))
            return DeclareFloatVector(module, 4);
        
        // Matriz = tipo de columna primero, número de columnas después.
        if (type == typeof(XnaMatrix) || type == typeof(NumMatrix))
            return Define(module, SpirVTableTypesIds.Mat4ID, SpirVOpTable.OpTypeMatrix,
                Id(DeclareFloatVector(module, 4)), Int(4));

        // OpTypeImage <tipo muestreado> <Dim> <Depth> <Arrayed> <MS> <Sampled> <Format>
        if (type == typeof(Texture2D))
            return Define(module, SpirVTableTypesIds.Image2DID, SpirVOpTable.OpTypeImage,
                Id(module.DeclareType(typeof(float))), Enumerant("2D", 1),
                Int(0), Int(0), Int(0), Int(1), Enumerant("Unknown", 0));

        if (type == typeof(SamplerState))
            return Define(module, SpirVTableTypesIds.SamplerID, SpirVOpTable.OpTypeSampler);

        // El tipo de elemento y la longitud no se pueden deducir de un List<object> o ImmutableArray.
        if (IsCollection(type))
            throw new NotSupportedException(
                $"{type} no tiene tamaño ni elemento conocidos; usa DeclareArray(elemento, longitud) " +
                "o DeclareRuntimeArray(elemento).");

        throw new NotSupportedException(
            $"El tipo {type.FullName} no está soportado por esta versión del compilador de shaders.");
    }

    private static string DeclareFloatVector(SpirVModule module, int components) =>
        Define(module, SpirVTableTypesIds.VectorFloatID(components), SpirVOpTable.OpTypeVector,
            Id(module.DeclareType(typeof(float))), Int(components));

    public static string DeclarePointer(this SpirVModule module, SpirVStorageClass storage, string pointeeId)
    {
        string id = $"%ptr_{storage.ToString().ToLowerInvariant()}_{pointeeId.TrimStart('%')}";
        return Define(module, id, SpirVOpTable.OpTypePointer,
            SpirVInstructionParameter.Enumerant(storage), Id(pointeeId));
    }

    /// <summary>
    /// Emite <c>id = op ...</c> salvo que el ID ya exista (Vector2 de XNA y de System.Numerics
    /// comparten el mismo %v2float).
    /// </summary>
    private static string Define(SpirVModule module, string id, SpirVOperation op,
        params SpirVInstructionParameter[] parameters)
    {
        return module.IsDefined(id) ? id : module.AddResultLine(id, op, parameters);
    }

    private static bool IsCollection(Type type) =>
        type.IsArray ||
        type == typeof(System.Collections.Immutable.ImmutableArray) ||
        type == typeof(System.Collections.Immutable.ImmutableList) ||
        (type.IsGenericType && type.GetGenericTypeDefinition() is var g &&
         (g == typeof(List<>) || g == typeof(System.Collections.Immutable.ImmutableArray<>) ||
          g == typeof(System.Collections.Immutable.ImmutableList<>)));

    private static SpirVInstructionParameter Id(string id) => new(id, SpirVInstructionType.IdRef);

    private static SpirVInstructionParameter Int(int value) =>
        new(value.ToString(), SpirVInstructionType.IntValue);

    private static SpirVInstructionParameter Enumerant(string name, uint value) =>
        new(name, SpirVInstructionType.Enumerant, value);
}
