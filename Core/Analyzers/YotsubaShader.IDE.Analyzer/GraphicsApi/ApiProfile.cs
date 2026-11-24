using System.Collections.Generic;
using YotsubaShader.Compiler.Metadata;

namespace YotsubaShader.Rules;

/// <summary>Cosas del lenguaje de shaders que no todas las APIs ni todas las versiones tienen.</summary>
internal enum ApiFeature
{
    Float64,
    Float16,
    Int16,
    Int64,
    Compute,
    StorageBuffer,
    StorageTexture,

    /// <summary>Leer o escribir una textura de almacenamiento sin declarar su formato.</summary>
    StorageTextureWithoutFormat
}

internal enum Availability
{
    /// <summary>La versión no se modela: no se avisa de nada.</summary>
    Unknown = 0,

    /// <summary>Siempre está.</summary>
    Guaranteed,

    /// <summary>Depende de una característica, extensión o hardware: se avisa, nunca es un error.</summary>
    Optional,

    /// <summary>No existe en esta versión: un error si el shader declara esta API.</summary>
    Unavailable
}

internal readonly struct FeatureSupport
{
    public FeatureSupport(Availability level, string? note)
    {
        Level = level;
        Note = note;
    }

    public Availability Level { get; }
    public string? Note { get; }
}

/// <summary>Máximos que la API garantiza (o impone) a un shader. Null = sin límite modelado.</summary>
internal readonly struct WorkgroupLimits
{
    public WorkgroupLimits(int x, int y, int z, int invocations)
    {
        X = x;
        Y = y;
        Z = z;
        Invocations = invocations;
    }

    public int X { get; }
    public int Y { get; }
    public int Z { get; }
    public int Invocations { get; }
}

/// <summary>
/// Lo que una API gráfica en una versión concreta permite. Hay un perfil por miembro de <see cref="Target"/>
/// (un archivo cada uno); cada perfil parte del de la versión anterior de su familia (<see cref="Next"/>), así que
/// un archivo solo dice qué cambia en esa versión.
/// </summary>
internal sealed class ApiProfile
{
    private readonly Dictionary<ApiFeature, FeatureSupport> _features;

    private ApiProfile(Target target, string api, string version, ShaderTargets family, ApiProfile? previous,
        Dictionary<ApiFeature, FeatureSupport> features)
    {
        Target = target;
        Api = api;
        Version = version;
        Family = family;
        Previous = previous;
        _features = features;
    }

    public Target Target { get; }

    /// <summary>"Vulkan", "OpenGL ES", "Direct3D"...</summary>
    public string Api { get; }

    /// <summary>"1.3", "4.3", "11"...</summary>
    public string Version { get; }

    public string Name => Api + " " + Version;
    public ShaderTargets Family { get; }

    /// <summary>La versión anterior de la misma API; null en la primera.</summary>
    public ApiProfile? Previous { get; }

    /// <summary>Destino del CLI (<c>--target</c>) que genera esta versión, o null si ningún backend la genera.</summary>
    public string? OutputTarget { get; private set; }

    /// <summary>Versión de ese destino (<c>glsl:430</c> → "430"); null = la que el compilador usa por defecto.</summary>
    public string? OutputVersion { get; private set; }

    public int? MaxColorAttachments { get; private set; }
    public int? MaxVertexInputs { get; private set; }
    public int? MaxInterStageVariables { get; private set; }
    public int? MaxDescriptorSets { get; private set; }
    public WorkgroupLimits? Workgroup { get; private set; }

    public bool HasBackend => OutputTarget is not null;

    public FeatureSupport Support(ApiFeature feature) =>
        _features.TryGetValue(feature, out FeatureSupport support) ? support : default;

    public static ApiProfile Create(Target target, string api, string version, ShaderTargets family) =>
        new(target, api, version, family, previous: null, new Dictionary<ApiFeature, FeatureSupport>());

    /// <summary>El perfil de la versión siguiente: copia este y se le cambia lo que haga falta.</summary>
    public ApiProfile Next(Target target, string version)
    {
        var next = new ApiProfile(target, Api, version, Family, this, new Dictionary<ApiFeature, FeatureSupport>(_features))
        {
            MaxColorAttachments = MaxColorAttachments,
            MaxVertexInputs = MaxVertexInputs,
            MaxInterStageVariables = MaxInterStageVariables,
            MaxDescriptorSets = MaxDescriptorSets,
            Workgroup = Workgroup
        };
        return next;
    }

    public ApiProfile Emits(string outputTarget, string? outputVersion)
    {
        OutputTarget = outputTarget;
        OutputVersion = outputVersion;
        return this;
    }

    public ApiProfile Feature(ApiFeature feature, Availability level, string? note = null)
    {
        _features[feature] = new FeatureSupport(level, note);
        return this;
    }

    public ApiProfile Limits(int? colorAttachments = null, int? vertexInputs = null, int? interStageVariables = null,
        int? descriptorSets = null, WorkgroupLimits? workgroup = null)
    {
        if (colorAttachments is not null) MaxColorAttachments = colorAttachments;
        if (vertexInputs is not null) MaxVertexInputs = vertexInputs;
        if (interStageVariables is not null) MaxInterStageVariables = interStageVariables;
        if (descriptorSets is not null) MaxDescriptorSets = descriptorSets;
        if (workgroup is not null) Workgroup = workgroup;
        return this;
    }

    public override string ToString() => Name;
}
