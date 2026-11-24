using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using YotsubaShader.Compiler.Metadata;

namespace YotsubaShader.Rules;

/// <summary>Por qué una característica no está en todos los perfiles elegidos.</summary>
internal sealed class FeatureIssue
{
    public FeatureIssue(string reasons, string targets, bool unavailable)
    {
        Reasons = reasons;
        Targets = targets;
        Unavailable = unavailable;
    }

    /// <summary>"Vulkan 1.3: opcional (shaderFloat64); WebGPU: no existe en WGSL".</summary>
    public string Reasons { get; }

    /// <summary>"Vulkan 1.3, WebGPU".</summary>
    public string Targets { get; }

    /// <summary>Alguna API elegida no la tiene en absoluto (y no es el destino por defecto): es un error.</summary>
    public bool Unavailable { get; }
}

/// <summary>
/// Los destinos que se validan para un shader: lo que declara <c>[ShaderTargets]</c> y <c>[ShaderEngines]</c>,
/// o <see cref="Target.Vulkan_13"/> si no declara nada.
/// </summary>
internal sealed class TargetSet
{
    public static readonly TargetSet Default = new(Target.Vulkan_13, GameEngine.None, isDefault: true);

    public TargetSet(Target declared, GameEngine engines, bool isDefault)
    {
        Declared = declared;
        Engines = engines;
        IsDefault = isDefault;
        Profiles = ApiRegistry.For(declared);

        ShaderTargets coarse = ShaderTargets.None;
        foreach (ApiProfile profile in Profiles) coarse |= profile.Family;
        if ((engines & GameEngine.Godot) != 0) coarse |= ShaderTargets.Godot;
        Coarse = coarse;
    }

    public Target Declared { get; }
    public GameEngine Engines { get; }

    /// <summary>No hay <c>[ShaderTargets]</c>: solo se avisa, no se dan errores de compatibilidad.</summary>
    public bool IsDefault { get; }

    public IReadOnlyList<ApiProfile> Profiles { get; }

    /// <summary>Las APIs elegidas sin versión, para las reglas que solo distinguen la familia.</summary>
    public ShaderTargets Coarse { get; }

    public static implicit operator ShaderTargets(TargetSet set) => set.Coarse;

    /// <summary>Los perfiles en los que <paramref name="feature"/> no está garantizada, o null si está en todos.</summary>
    public FeatureIssue? Check(ApiFeature feature)
    {
        var affected = new List<(ApiProfile Profile, FeatureSupport Support)>();
        foreach (ApiProfile profile in Profiles)
        {
            FeatureSupport support = profile.Support(feature);
            if (support.Level is Availability.Optional or Availability.Unavailable) affected.Add((profile, support));
        }

        if (affected.Count == 0) return null;

        string reasons = string.Join("; ", affected
            .GroupBy(item => item.Support.Note ?? "", StringComparer.Ordinal)
            .Select(group => Describe(group.Select(item => item.Profile)) +
                             (group.Key.Length == 0 ? "" : ": " + Loc.T(group.Key))));
        bool unavailable = !IsDefault && affected.Any(item => item.Support.Level == Availability.Unavailable);
        return new FeatureIssue(reasons, Describe(affected.Select(item => item.Profile)), unavailable);
    }

    /// <summary>Los perfiles cuyo límite es menor que <paramref name="used"/>.</summary>
    public IReadOnlyList<ApiProfile> Exceeding(int used, Func<ApiProfile, int?> limit) =>
        Profiles.Where(profile => limit(profile) is { } max && used > max).ToList();

    /// <summary>"Vulkan 1.3, OpenGL 2.0–3.3": las versiones consecutivas de una API se agrupan.</summary>
    public static string Describe(IEnumerable<ApiProfile> profiles) =>
        string.Join(", ", profiles
            .GroupBy(profile => profile.Api)
            .Select(group =>
            {
                var versions = group.OrderBy(profile => (ulong)profile.Target).ToList();
                return versions.Count <= 2
                    ? string.Join(", ", versions.Select(profile => profile.Name))
                    : versions[0].Name + "–" + versions[versions.Count - 1].Version;
            }));
}
