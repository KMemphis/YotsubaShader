using System;
using System.Collections.Generic;
using System.Linq;

namespace YotsubaShader.Rules;

/// <summary>Qué tipos de C# acepta un built-in.</summary>
internal enum BuiltInShape
{
    Float4,
    Float3,
    Float2,
    Float,
    Bool,
    IntScalar,
    IntVector3,
    FloatOrFloatArray
}

/// <summary>
/// Una fila de la tabla de built-ins: dónde es válido el concepto (stage y sentido), qué tipo exige, si el
/// compilador ya lo traduce y en qué APIs no existe o necesita una extensión.
/// </summary>
internal sealed class BuiltInRule
{
    public BuiltInRule(string concept, string spirvName, BuiltInShape shape, (ShaderStageKind, InterfaceDirection)[] allowed,
        bool supported = true, ShaderTargets missingOn = ShaderTargets.None, string? portabilityNote = null)
    {
        Concept = concept;
        SpirvName = spirvName;
        Shape = shape;
        Allowed = allowed;
        Supported = supported;
        MissingOn = missingOn;
        PortabilityNote = portabilityNote;
    }

    public string Concept { get; }

    /// <summary>Nombre del BuiltIn de SPIR-V (para los mensajes y el compilador).</summary>
    public string SpirvName { get; }

    public BuiltInShape Shape { get; }

    public IReadOnlyList<(ShaderStageKind Stage, InterfaceDirection Direction)> Allowed { get; }

    /// <summary>false: el concepto existe en la sintaxis pero el compilador todavía no lo traduce.</summary>
    public bool Supported { get; }

    /// <summary>APIs donde no existe o requiere una extensión/feature opcional.</summary>
    public ShaderTargets MissingOn { get; }

    public string? PortabilityNote { get; }

    public bool IsAllowed(ShaderStageKind stage, InterfaceDirection direction) =>
        Allowed.Any(a => a.Stage == stage && a.Direction == direction);

    public bool Accepts(GpuType type) => Shape switch
    {
        BuiltInShape.Float4 => type.IsVector(GpuScalar.Float, 4),
        BuiltInShape.Float3 => type.IsVector(GpuScalar.Float, 3),
        BuiltInShape.Float2 => type.IsVector(GpuScalar.Float, 2),
        BuiltInShape.Float => type.IsScalar(GpuScalar.Float),
        BuiltInShape.Bool => type.IsScalar(GpuScalar.Bool),
        BuiltInShape.IntScalar => type.Kind == GpuTypeKind.Scalar && type.Scalar is GpuScalar.Int32 or GpuScalar.UInt32,
        BuiltInShape.IntVector3 => type.Kind == GpuTypeKind.Vector && type.Components == 3 &&
                                   type.Scalar is GpuScalar.Int32 or GpuScalar.UInt32,
        BuiltInShape.FloatOrFloatArray => type.IsScalar(GpuScalar.Float) || type.Kind == GpuTypeKind.Array,
        _ => false
    };

    public string ExpectedType => Shape switch
    {
        BuiltInShape.Float4 => "float4",
        BuiltInShape.Float3 => "float3",
        BuiltInShape.Float2 => "float2",
        BuiltInShape.Float => "float",
        BuiltInShape.Bool => "bool",
        BuiltInShape.IntScalar => "int o uint",
        BuiltInShape.IntVector3 => "uint3 o int3",
        BuiltInShape.FloatOrFloatArray => "float o float[]",
        _ => "?"
    };
}

internal static class BuiltInTable
{
    private static readonly (ShaderStageKind, InterfaceDirection) VIn = (ShaderStageKind.Vertex, InterfaceDirection.Input);
    private static readonly (ShaderStageKind, InterfaceDirection) VOut = (ShaderStageKind.Vertex, InterfaceDirection.Output);
    private static readonly (ShaderStageKind, InterfaceDirection) FIn = (ShaderStageKind.Fragment, InterfaceDirection.Input);
    private static readonly (ShaderStageKind, InterfaceDirection) FOut = (ShaderStageKind.Fragment, InterfaceDirection.Output);
    private static readonly (ShaderStageKind, InterfaceDirection) CIn = (ShaderStageKind.Compute, InterfaceDirection.Input);

    public static readonly IReadOnlyList<BuiltInRule> Rules = new[]
    {
        // Vertex
        new BuiltInRule("ClipSpacePosition", "Position", BuiltInShape.Float4, new[] { VOut }),
        new BuiltInRule("VertexIndex", "VertexIndex", BuiltInShape.IntScalar, new[] { VIn }),
        new BuiltInRule("InstanceIndex", "InstanceIndex", BuiltInShape.IntScalar, new[] { VIn }),
        new BuiltInRule("BaseVertex", "BaseVertex", BuiltInShape.IntScalar, new[] { VIn },
            missingOn: ShaderTargets.WebGPU, portabilityNote: "necesita DrawParameters en Vulkan, SM 6.8 en Direct3D 12 y no existe en WebGPU"),
        new BuiltInRule("BaseInstance", "BaseInstance", BuiltInShape.IntScalar, new[] { VIn },
            missingOn: ShaderTargets.WebGPU, portabilityNote: "necesita DrawParameters en Vulkan, SM 6.8 en Direct3D 12 y no existe en WebGPU"),
        new BuiltInRule("ClipInstance", "ClipDistance", BuiltInShape.FloatOrFloatArray, new[] { VOut, FIn }, supported: false,
            missingOn: ShaderTargets.WebGPU, portabilityNote: "en WebGPU necesita la feature 'clip-distances'"),
        new BuiltInRule("RenderTargetLayer", "Layer", BuiltInShape.IntScalar, new[] { VOut, FIn }, supported: false,
            missingOn: ShaderTargets.WebGPU | ShaderTargets.OpenGL, portabilityNote: "desde vertex necesita shaderOutputLayer (Vulkan 1.2) y no existe en WebGPU"),
        new BuiltInRule("ViewportIndex", "ViewportIndex", BuiltInShape.IntScalar, new[] { VOut, FIn }, supported: false,
            missingOn: ShaderTargets.WebGPU | ShaderTargets.OpenGL, portabilityNote: "desde vertex necesita shaderOutputViewportIndex y no existe en WebGPU"),

        // Fragment
        new BuiltInRule("FragmentCoordinate", "FragCoord", BuiltInShape.Float4, new[] { FIn }),
        new BuiltInRule("FrontFacingFlag", "FrontFacing", BuiltInShape.Bool, new[] { FIn }),
        new BuiltInRule("FragDepth", "FragDepth", BuiltInShape.Float, new[] { FOut }),
        new BuiltInRule("ConservativeDepth", "FragDepth", BuiltInShape.Float, new[] { FOut },
            missingOn: ShaderTargets.WebGPU, portabilityNote: "la profundidad conservadora (greater/less) no existe en WGSL; se trata como profundidad normal"),
        new BuiltInRule("SampleIndex", "SampleId", BuiltInShape.IntScalar, new[] { FIn }),
        new BuiltInRule("SamplePosition", "SamplePosition", BuiltInShape.Float2, new[] { FIn },
            missingOn: ShaderTargets.WebGPU | ShaderTargets.D3D12, portabilityNote: "no es un built-in en WGSL ni en HLSL"),
        new BuiltInRule("SampleMask", "SampleMask", BuiltInShape.IntScalar, new[] { FIn, FOut }, supported: false),
        new BuiltInRule("PrimitiveIndex", "PrimitiveId", BuiltInShape.IntScalar, new[] { FIn },
            missingOn: ShaderTargets.WebGPU, portabilityNote: "necesita geometryShader en Vulkan y la feature 'primitive-index' en WebGPU"),
        new BuiltInRule("HelperInvocation", "HelperInvocation", BuiltInShape.Bool, new[] { FIn },
            missingOn: ShaderTargets.WebGPU, portabilityNote: "no existe en WGSL y necesita SM 6.6 (IsHelperLane) en Direct3D 12"),
        new BuiltInRule("StencilReference", "FragStencilRefEXT", BuiltInShape.IntScalar, new[] { FOut }, supported: false,
            missingOn: ShaderTargets.WebGPU, portabilityNote: "necesita VK_EXT_shader_stencil_export y no existe en WebGPU"),
        new BuiltInRule("BarycentricCoordinates", "BaryCoordKHR", BuiltInShape.Float3, new[] { FIn }, supported: false,
            missingOn: ShaderTargets.WebGPU, portabilityNote: "necesita VK_KHR_fragment_shader_barycentric y no existe en WebGPU"),
        new BuiltInRule("BarycentricCoordinatesNoPerspective", "BaryCoordNoPerspKHR", BuiltInShape.Float3, new[] { FIn }, supported: false,
            missingOn: ShaderTargets.WebGPU, portabilityNote: "necesita VK_KHR_fragment_shader_barycentric y no existe en WebGPU"),

        // Compute
        new BuiltInRule("GlobalInvocationId", "GlobalInvocationId", BuiltInShape.IntVector3, new[] { CIn }),
        new BuiltInRule("LocalInvocationId", "LocalInvocationId", BuiltInShape.IntVector3, new[] { CIn }),
        new BuiltInRule("LocalInvocationIndex", "LocalInvocationIndex", BuiltInShape.IntScalar, new[] { CIn }),
        new BuiltInRule("WorkgroupId", "WorkgroupId", BuiltInShape.IntVector3, new[] { CIn }),
        new BuiltInRule("NumberOfWorkgroups", "NumWorkgroups", BuiltInShape.IntVector3, new[] { CIn }),
        new BuiltInRule("WorkgroupSizeBuiltInValue", "WorkgroupSize", BuiltInShape.IntVector3, new[] { CIn }, supported: false),
        new BuiltInRule("SubgroupSize", "SubgroupSize", BuiltInShape.IntScalar, new[] { CIn, FIn }, supported: false),
        new BuiltInRule("SubgroupInvocationId", "SubgroupLocalInvocationId", BuiltInShape.IntScalar, new[] { CIn, FIn }, supported: false),
        new BuiltInRule("SubgroupId", "SubgroupId", BuiltInShape.IntScalar, new[] { CIn }, supported: false),
        new BuiltInRule("NumberOfSubgroups", "NumSubgroups", BuiltInShape.IntScalar, new[] { CIn }, supported: false),
    };

    private static readonly Dictionary<string, BuiltInRule> ByConcept = Rules.ToDictionary(r => r.Concept, StringComparer.Ordinal);

    /// <summary>
    /// El built-in que representa un atributo en un stage. <c>SV_Position</c> lleva dos conceptos:
    /// en vertex es la posición de recorte y en fragment la coordenada del fragmento.
    /// </summary>
    public static BuiltInRule? Resolve(ConceptAttribute attribute, ShaderStageKind stage)
    {
        bool clip = attribute.Is(Concepts.ClipSpacePosition);
        bool coord = attribute.Is(Concepts.FragmentCoordinate);
        if (clip && coord) return ByConcept[stage == ShaderStageKind.Fragment ? Concepts.FragmentCoordinate : Concepts.ClipSpacePosition];

        foreach (string concept in attribute.Concepts)
        {
            if (ByConcept.TryGetValue(concept, out BuiltInRule? rule)) return rule;
        }

        return null;
    }

    public static bool IsBuiltIn(ConceptAttribute attribute) => attribute.Concepts.Any(ByConcept.ContainsKey);
}
