using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace YotsubaShader.Rules;

/// <summary>
/// Reglas sobre las declaraciones de un shader: puntos de entrada, interfaz entre etapas y recursos.
/// No miran los cuerpos de los métodos (eso es <see cref="BodyRules"/>).
/// </summary>
internal static class DeclarationRules
{
    // Máximos de Direct3D 12 / Metal (los más altos del mercado). Por encima no compila en ninguna API.
    private const int MaxWorkgroupX = 1024, MaxWorkgroupY = 1024, MaxWorkgroupZ = 64, MaxWorkgroupInvocations = 1024;

    public static void AnalyzeShaderType(INamedTypeSymbol type, ShaderTargets targets, Action<Diagnostic> report)
    {
        var entryPoints = ShaderSymbols.EntryPoints(type).ToList();
        if (entryPoints.Count == 0) return;

        foreach (IGrouping<string, IMethodSymbol> group in entryPoints.GroupBy(m => m.Name).Where(g => g.Count() > 1))
        {
            foreach (IMethodSymbol method in group)
            {
                report(Diagnostic.Create(ShaderRules.EntryPointOverloaded, LocationOf(method), method.Name, type.Name));
            }
        }

        var signatures = new List<InterfaceSignature>();
        foreach (IMethodSymbol method in entryPoints)
        {
            InterfaceSignature? signature = AnalyzeEntryPoint(method, targets, report);
            if (signature is not null) signatures.Add(signature);
        }

        LinkStages(signatures, report);
        GodotSupport.AnalyzeSignatures(signatures, targets, report);
        ResourceRules.Analyze(type, targets, report);
    }

    private static InterfaceSignature? AnalyzeEntryPoint(IMethodSymbol method, ShaderTargets targets, Action<Diagnostic> report)
    {
        var stages = ShaderSymbols.GetStages(method);
        if (stages.Count > 1)
        {
            report(Diagnostic.Create(ShaderRules.MultipleStages, LocationOf(method), method.Name));
        }

        ShaderStageKind stage = stages[0].Stage;

        string? invalid = method.MethodKind != MethodKind.Ordinary ? "no es un método normal"
            : method.IsGenericMethod ? "es genérico"
            : method.IsAbstract ? "es abstracto"
            : method.IsAsync ? "es async"
            : method.ReturnsByRef || method.ReturnsByRefReadonly ? "devuelve por referencia"
            : null;
        if (invalid is not null)
        {
            report(Diagnostic.Create(ShaderRules.EntryPointKind, LocationOf(method), method.Name, invalid));
            return null;
        }

        foreach (IParameterSymbol parameter in method.Parameters)
        {
            if (parameter.RefKind is RefKind.Ref or RefKind.In or RefKind.RefReadOnlyParameter)
            {
                report(Diagnostic.Create(ShaderRules.EntryPointParameterRef, LocationOf(parameter), parameter.Name, method.Name,
                    parameter.RefKind.ToString().ToLowerInvariant()));
            }

            if (parameter.IsParams)
            {
                report(Diagnostic.Create(ShaderRules.ParamsParameter, LocationOf(parameter), parameter.Name, method.Name));
            }
        }

        AnalyzeWorkgroupSize(method, stage, targets, report);
        return InterfaceRules.Analyze(method, stage, targets, report);
    }

    private static void AnalyzeWorkgroupSize(IMethodSymbol method, ShaderStageKind stage, ShaderTargets targets, Action<Diagnostic> report)
    {
        ConceptAttribute? size = ShaderSymbols.GetConceptAttributes(method)
            .FirstOrDefault(a => a.Is(Concepts.WorkgroupSizeMethod));

        if (stage != ShaderStageKind.Compute)
        {
            if (size is not null)
            {
                report(Diagnostic.Create(ShaderRules.WorkgroupSizeIgnored, size.GetLocation() ?? LocationOf(method),
                    method.Name, StageName(stage)));
            }

            return;
        }

        if (size is null)
        {
            report(Diagnostic.Create(ShaderRules.ComputeWithoutWorkgroupSize, LocationOf(method), method.Name));
            return;
        }

        // Los argumentos se leen por posición, igual que el compilador (x, y = 1, z = 1).
        var values = size.Data.ConstructorArguments.Select(a => a.Value switch
        {
            int i => (long)i,
            uint u => u,
            _ => 1L
        }).ToList();
        while (values.Count < 3) values.Add(1);

        Location location = size.GetLocation() ?? LocationOf(method);
        string[] axes = { "x", "y", "z" };
        bool valid = true;
        for (int i = 0; i < 3; i++)
        {
            if (values[i] < 1)
            {
                report(Diagnostic.Create(ShaderRules.WorkgroupSizeZero, location, axes[i], method.Name, values[i]));
                valid = false;
            }
        }

        if (!valid) return;

        long total = values[0] * values[1] * values[2];
        if (values[0] > MaxWorkgroupX || values[1] > MaxWorkgroupY || values[2] > MaxWorkgroupZ || total > MaxWorkgroupInvocations)
        {
            report(Diagnostic.Create(ShaderRules.WorkgroupSizeTooLarge, location, values[0], values[1], values[2], method.Name,
                "x ≤ 1024, y ≤ 1024, z ≤ 64 y como mucho 1024 invocaciones en total"));
            return;
        }

        ShaderTargets limited = ShaderTargets.None;
        string detail = "";
        if (total > 256 || values[0] > 256 || values[1] > 256)
        {
            limited |= ShaderTargets.WebGPU | ShaderTargets.Vulkan;
            detail = $"El grupo de {total} invocaciones supera el límite por defecto de WebGPU (256, con x/y ≤ 256) y el mínimo garantizado de Vulkan (128).";
        }
        else if (total > 128 || values[0] > 128 || values[1] > 128)
        {
            limited |= ShaderTargets.Vulkan;
            detail = $"El grupo de {total} invocaciones supera el mínimo garantizado de Vulkan (128 invocaciones, 128x128x64).";
        }

        if ((limited & targets) != 0)
        {
            report(Diagnostic.Create(ShaderRules.PortableLimit, location, detail, ShaderTargetsParser.Describe(limited & targets)));
        }
    }

    /// <summary>Un vertex y un fragment del mismo shader forman un programa: cada entrada del fragment debe tener su salida.</summary>
    private static void LinkStages(IReadOnlyList<InterfaceSignature> signatures, Action<Diagnostic> report)
    {
        var vertices = signatures.Where(s => s.Stage == ShaderStageKind.Vertex).ToList();
        var fragments = signatures.Where(s => s.Stage == ShaderStageKind.Fragment).ToList();
        if (vertices.Count != 1 || fragments.Count != 1) return;

        InterfaceSignature vertex = vertices[0], fragment = fragments[0];
        var outputs = vertex.Members.Where(m => m.Direction == InterfaceDirection.Output && m.LinkKey is not null)
            .GroupBy(m => m.LinkKey!).ToDictionary(g => g.Key, g => g.First());
        bool vertexUsesSemantics = outputs.Keys.Any(k => k.StartsWith("S:"));
        bool vertexUsesLocations = outputs.Keys.Any(k => k.StartsWith("L:"));

        foreach (InterfaceMember input in fragment.Members.Where(m => m.Direction == InterfaceDirection.Input && m.LinkKey is not null))
        {
            bool comparable = input.LinkKey!.StartsWith("S:") ? vertexUsesSemantics && !vertexUsesLocations
                : vertexUsesLocations && !vertexUsesSemantics;
            if (!comparable) continue;

            if (!outputs.TryGetValue(input.LinkKey, out InterfaceMember? output))
            {
                report(Diagnostic.Create(ShaderRules.FragmentInputWithoutVertexOutput, input.Location, input.Name,
                    input.SlotText, fragment.Method.Name, vertex.Method.Name));
            }
            else if (output.Type.ToString() != input.Type.ToString())
            {
                report(Diagnostic.Create(ShaderRules.InterfaceTypeMismatch, input.Location, input.SlotText,
                    output.TypeName, vertex.Method.Name, input.TypeName, fragment.Method.Name));
            }
        }
    }

    public static string StageName(ShaderStageKind stage) => stage switch
    {
        ShaderStageKind.Vertex => "vertex",
        ShaderStageKind.Fragment => "fragment",
        _ => "compute"
    };

    public static Location LocationOf(ISymbol symbol) => symbol.Locations.FirstOrDefault() ?? Location.None;
}
