using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace YotsubaShader.Rules;

/// <summary>
/// Lo que Godot Shading Language (.gdshader) no tiene. Solo se avisa si el shader lleva <c>[ShaderEngines(GameEngine.Godot)]</c>,
/// porque el plugin <c>godot</c> del compilador lo rechaza con un error.
/// </summary>
internal static class GodotSupport
{
    public static void AnalyzeSignatures(IReadOnlyList<InterfaceSignature> signatures, ShaderTargets targets, Action<Diagnostic> report)
    {
        if ((targets & ShaderTargets.Godot) == 0) return;

        foreach (InterfaceSignature signature in signatures)
        {
            Location location = DeclarationRules.LocationOf(signature.Method);

            if (signature.Stage == ShaderStageKind.Compute)
            {
                report(Diagnostic.Create(ShaderRules.NotInGodot, location, signature.Method.Name,
                    Loc.T("is a compute shader and Godot Shading Language has no compute (only vertex and fragment)")));
                continue;
            }

            if (signature.Stage == ShaderStageKind.Fragment)
            {
                var targetOutputs = signature.Members
                    .Where(m => m.Direction == InterfaceDirection.Output && m.Attributes.Any(a => a.Name.StartsWith("SV_Target", StringComparison.Ordinal)))
                    .ToList();
                if (targetOutputs.Count > 1)
                {
                    report(Diagnostic.Create(ShaderRules.NotInGodot, targetOutputs[1].Location, signature.Method.Name,
                        Loc.F("writes {0} render targets and a Godot shader only writes one color per fragment", targetOutputs.Count)));
                }
            }

            if (signature.Stage == ShaderStageKind.Vertex)
            {
                foreach (InterfaceMember member in signature.Members.Where(m => m.Direction == InterfaceDirection.Input))
                {
                    if (member.Attributes.Any(a => a.Is(Concepts.Location)))
                    {
                        report(Diagnostic.Create(ShaderRules.NotInGodot, member.Location, member.Name,
                            Loc.T("uses [Location]: Godot delivers vertex attributes by name (VERTEX, NORMAL, UV...), so use a semantic such as [POSITION] or [TEXCOORD(0)]")));
                    }
                }
            }
        }
    }

    public static void AnalyzeResource(ISymbol member, ResourceKind kind, ShaderTargets targets, Action<Diagnostic> report)
    {
        if ((targets & ShaderTargets.Godot) == 0) return;

        string? why = kind switch
        {
            ResourceKind.StorageBuffer => Loc.T("is a storage buffer and Godot Shading Language does not have them; use a uniform or a texture"),
            ResourceKind.StorageTexture => Loc.T("is a storage texture and Godot Shading Language has no write images"),
            _ => null
        };

        if (why is not null)
        {
            report(Diagnostic.Create(ShaderRules.NotInGodot, DeclarationRules.LocationOf(member), member.Name, why));
        }
    }
}
