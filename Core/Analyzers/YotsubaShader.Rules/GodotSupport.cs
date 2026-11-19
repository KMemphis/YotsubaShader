using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace YotsubaShader.Rules;

/// <summary>
/// Lo que Godot Shading Language (.gdshader) no tiene. Solo se avisa si <c>YotsubaShaderTargets</c> incluye Godot,
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
                    "es un compute shader y Godot Shading Language no tiene compute (solo vertex y fragment)"));
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
                        $"escribe {targetOutputs.Count} render targets y un shader de Godot solo escribe un color por fragment"));
                }
            }

            if (signature.Stage == ShaderStageKind.Vertex)
            {
                foreach (InterfaceMember member in signature.Members.Where(m => m.Direction == InterfaceDirection.Input))
                {
                    if (member.Attributes.Any(a => a.Is(Concepts.Location)))
                    {
                        report(Diagnostic.Create(ShaderRules.NotInGodot, member.Location, member.Name,
                            "usa [Location]: Godot entrega los atributos de vértice por nombre (VERTEX, NORMAL, UV...), así que usa una semántica como [POSITION] o [TEXCOORD(0)]"));
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
            ResourceKind.StorageBuffer => "es un storage buffer y Godot Shading Language no los tiene; usa un uniform o una textura",
            ResourceKind.StorageTexture => "es una textura de almacenamiento y Godot Shading Language no tiene imágenes de escritura",
            _ => null
        };

        if (why is not null)
        {
            report(Diagnostic.Create(ShaderRules.NotInGodot, DeclarationRules.LocationOf(member), member.Name, why));
        }
    }
}
