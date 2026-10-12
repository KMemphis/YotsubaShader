namespace YotsubaShader.Compiler.YS_Types;

// Un enum por tipo de operando de la especificación: así dos nombres iguales con valores distintos
// (Uniform = 2 como StorageClass, Uniform = 26 como Decoration) no chocan, y el compilador de C#
// comprueba que cada operando recibe el tipo de enumerante que le toca.
// Valores tomados de spirv.core.grammar.json. Añade aquí los que vayas necesitando.

public enum SpirVCapability : uint
{
    Matrix = 0,
    Shader = 1,
}

public enum SpirVAddressingModel : uint
{
    Logical = 0,
    Physical32 = 1,
    Physical64 = 2,
}

public enum SpirVMemoryModel : uint
{
    Simple = 0,
    GLSL450 = 1,
    Vulkan = 3,
}

public enum SpirVExecutionModel : uint
{
    Vertex = 0,
    TessellationControl = 1,
    TessellationEvaluation = 2,
    Geometry = 3,
    Fragment = 4,
    GLCompute = 5,
}

public enum SpirVExecutionMode : uint
{
    OriginUpperLeft = 7,
    LocalSize = 17,
}

public enum SpirVDecoration : uint
{
    Block = 2,
    ColMajor = 5,
    ArrayStride = 6,
    MatrixStride = 7,
    BuiltIn = 11,
    Location = 30,
    Binding = 33,
    DescriptorSet = 34,
    Offset = 35,
}

/// <summary>Máscara de bits en la especificación: None = 0 y el resto se pueden combinar.</summary>
[Flags]
public enum SpirVFunctionControl : uint
{
    None = 0,
    Inline = 1,
    DontInline = 2,
    Pure = 4,
    Const = 8,
}
