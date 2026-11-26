using System;
using System.Linq;
using Microsoft.CodeAnalysis;
using YotsubaShader.Compiler.Metadata;

namespace YotsubaShader.Rules;

/// <summary>Reglas que dependen de la API declarada: comprobar el atributo y las características que faltan.</summary>
internal static class TargetRules
{
    /// <summary>
    /// YS0711: algo que el shader usa no está en alguna de las APIs declaradas. Es un error si la API no lo tiene en
    /// absoluto (y el shader declaró sus destinos) y un aviso si solo es opcional.
    /// </summary>
    public static void ReportFeature(TargetSet targets, ApiFeature feature, Location location, string subject, Action<Diagnostic> report)
    {
        if (targets.Check(feature) is not { } issue) return;

        DiagnosticDescriptor descriptor = issue.Unavailable ? ShaderRules.AsError(ShaderRules.FeatureUnavailable) : ShaderRules.FeatureUnavailable;
        report(Diagnostic.Create(descriptor, location, subject, Loc.F("is not available in all graphics APIs: {0}", issue.Reasons), issue.Targets));
    }

    /// <summary>YS0712: el atributo pide una API que ningún backend genera, o un valor que no existe.</summary>
    public static void AnalyzeAttribute(AttributeData? attribute, Action<Diagnostic> report)
    {
        if (attribute is null) return;

        Target declared = TargetResolver.Read(attribute, out _);
        Location location = attribute.ApplicationSyntaxReference is { } reference
            ? Location.Create(reference.SyntaxTree, reference.Span)
            : Location.None;

        // [ShaderTargets] only says which graphics APIs the shader must be valid for. Whether the CLI can emit code
        // for one of them is not an error here: a shader can target Direct3D 9 for validation and still be compiled
        // to another language.
        Target unknown = ApiRegistry.Unknown(declared);
        if (unknown != Target.None)
        {
            report(Diagnostic.Create(ShaderRules.TargetWithoutBackend, location,
                Loc.F("[ShaderTargets] contains values that are not an API ({0}).", unknown)));
        }
    }
}
