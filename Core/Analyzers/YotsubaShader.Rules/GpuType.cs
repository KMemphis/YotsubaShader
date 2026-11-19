using System.Linq;
using Microsoft.CodeAnalysis;

namespace YotsubaShader.Rules;

internal enum GpuScalar
{
    None,
    Bool,
    Int16,
    UInt16,
    Int32,
    UInt32,
    Int64,
    UInt64,
    Half,
    Float,
    Double
}

internal enum GpuTypeKind
{
    /// <summary>Un tipo que la GPU no puede representar (clase, string, decimal, delegado...).</summary>
    Unsupported,
    Scalar,
    Vector,
    Matrix,
    Array,
    Struct,
    Texture,
    Sampler
}

/// <summary>Cómo ve la GPU un tipo de C#: escalar, vector de N componentes, matriz, struct...</summary>
internal readonly struct GpuType
{
    public GpuType(GpuTypeKind kind, GpuScalar scalar = GpuScalar.None, int components = 1, int columns = 0, ITypeSymbol? element = null)
    {
        Kind = kind;
        Scalar = scalar;
        Components = components;
        Columns = columns;
        Element = element;
    }

    public GpuTypeKind Kind { get; }

    /// <summary>Tipo de cada componente (escalar, vector o matriz).</summary>
    public GpuScalar Scalar { get; }

    /// <summary>Componentes de un vector, o filas de una matriz.</summary>
    public int Components { get; }

    public int Columns { get; }

    /// <summary>Tipo de los elementos de un array.</summary>
    public ITypeSymbol? Element { get; }

    public bool IsScalar(GpuScalar scalar) => Kind == GpuTypeKind.Scalar && Scalar == scalar;

    public bool IsVector(GpuScalar scalar, int components) =>
        Kind == GpuTypeKind.Vector && Scalar == scalar && Components == components;

    public bool IsInteger => Scalar is GpuScalar.Int16 or GpuScalar.UInt16 or GpuScalar.Int32 or GpuScalar.UInt32
        or GpuScalar.Int64 or GpuScalar.UInt64;

    /// <summary>Cuántas locations de la interfaz ocupa (vec3/vec4 de 64 bits ocupan dos).</summary>
    public int LocationCount
    {
        get
        {
            bool wide = Scalar is GpuScalar.Double or GpuScalar.Int64 or GpuScalar.UInt64;
            return Kind switch
            {
                GpuTypeKind.Scalar => 1,
                GpuTypeKind.Vector => wide && Components >= 3 ? 2 : 1,
                GpuTypeKind.Matrix => Columns * (wide && Components >= 3 ? 2 : 1),
                _ => 1
            };
        }
    }

    public override string ToString() => Kind switch
    {
        GpuTypeKind.Scalar => Scalar.ToString(),
        GpuTypeKind.Vector => Scalar + "x" + Components,
        GpuTypeKind.Matrix => Scalar + Columns + "x" + Components,
        _ => Kind.ToString()
    };

    public static GpuType Of(ITypeSymbol? type)
    {
        if (type is null) return new GpuType(GpuTypeKind.Unsupported);

        GpuScalar scalar = ScalarOf(type);
        if (scalar != GpuScalar.None) return new GpuType(GpuTypeKind.Scalar, scalar);

        if (type is IArrayTypeSymbol array)
        {
            return array.Rank == 1
                ? new GpuType(GpuTypeKind.Array, element: array.ElementType)
                : new GpuType(GpuTypeKind.Unsupported);
        }

        if (ShaderSymbols.HasAttribute(type, ShaderNames.Texture)) return new GpuType(GpuTypeKind.Texture);
        if (ShaderSymbols.HasAttribute(type, ShaderNames.Sampler)) return new GpuType(GpuTypeKind.Sampler);

        if (ShaderSymbols.HasAttribute(type, ShaderNames.Vector))
        {
            var components = type.GetMembers()
                .Where(m => !m.IsStatic && ShaderSymbols.HasAttribute(m, ShaderNames.VectorComponent))
                .Select(MemberType)
                .ToList();
            GpuScalar componentScalar = components.Count > 0 && components[0] is { } first ? ScalarOf(first) : GpuScalar.None;
            return componentScalar == GpuScalar.None
                ? new GpuType(GpuTypeKind.Unsupported)
                : new GpuType(GpuTypeKind.Vector, componentScalar, components.Count);
        }

        if (ShaderSymbols.HasAttribute(type, ShaderNames.Matrix))
        {
            var columns = type.GetMembers()
                .Where(m => !m.IsStatic && ShaderSymbols.HasAttribute(m, ShaderNames.MatrixColumn))
                .Select(MemberType)
                .ToList();
            if (columns.Count == 0) return new GpuType(GpuTypeKind.Unsupported);
            GpuType column = Of(columns[0]);
            return column.Kind == GpuTypeKind.Vector
                ? new GpuType(GpuTypeKind.Matrix, column.Scalar, column.Components, columns.Count)
                : new GpuType(GpuTypeKind.Unsupported);
        }

        if (type.TypeKind == TypeKind.Struct && type.SpecialType == SpecialType.None &&
            ShaderSymbols.FullName(type) is not ("System.Decimal" or "System.Char" or "System.DateTime" or "System.TimeSpan" or "System.Guid" or "System.IntPtr" or "System.UIntPtr"))
        {
            return new GpuType(GpuTypeKind.Struct);
        }

        if (type.TypeKind == TypeKind.Enum && type is INamedTypeSymbol { EnumUnderlyingType: { } underlying })
        {
            GpuScalar enumScalar = ScalarOf(underlying);
            return enumScalar == GpuScalar.None ? new GpuType(GpuTypeKind.Unsupported) : new GpuType(GpuTypeKind.Scalar, enumScalar);
        }

        return new GpuType(GpuTypeKind.Unsupported);
    }

    public static GpuScalar ScalarOf(ITypeSymbol type) => type.SpecialType switch
    {
        SpecialType.System_Boolean => GpuScalar.Bool,
        SpecialType.System_Int16 => GpuScalar.Int16,
        SpecialType.System_UInt16 => GpuScalar.UInt16,
        SpecialType.System_Int32 => GpuScalar.Int32,
        SpecialType.System_UInt32 => GpuScalar.UInt32,
        SpecialType.System_Int64 => GpuScalar.Int64,
        SpecialType.System_UInt64 => GpuScalar.UInt64,
        SpecialType.System_Single => GpuScalar.Float,
        SpecialType.System_Double => GpuScalar.Double,
        _ => ShaderSymbols.FullName(type) == "System.Half" ? GpuScalar.Half : GpuScalar.None
    };

    private static ITypeSymbol? MemberType(ISymbol member) => member switch
    {
        IFieldSymbol field => field.Type,
        IPropertySymbol property => property.Type,
        _ => null
    };

    public static GpuScalar ScalarOf(ITypeSymbol? type, GpuScalar fallback) => type is null ? fallback : ScalarOf(type);
}
