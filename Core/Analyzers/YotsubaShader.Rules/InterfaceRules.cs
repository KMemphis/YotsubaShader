using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace YotsubaShader.Rules;

/// <summary>Un miembro de la interfaz de un stage ya aplanado (parámetro, retorno o campo de un struct de interfaz).</summary>
internal sealed class InterfaceMember
{
    public InterfaceMember(string name, ITypeSymbol type, InterfaceDirection direction, IReadOnlyList<ConceptAttribute> attributes,
        Location location, bool isStructMember, bool isReturnOrOut)
    {
        Name = name;
        TypeSymbol = type;
        Type = GpuType.Of(type);
        Direction = direction;
        Attributes = attributes;
        Location = location;
        IsStructMember = isStructMember;
        IsReturnOrOut = isReturnOrOut;
    }

    public string Name { get; }
    public ITypeSymbol TypeSymbol { get; }
    public GpuType Type { get; }
    public string TypeName => TypeSymbol.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
    public InterfaceDirection Direction { get; }
    public IReadOnlyList<ConceptAttribute> Attributes { get; }
    public Location Location { get; }
    public bool IsStructMember { get; }
    public bool IsReturnOrOut { get; }

    /// <summary>Clave para emparejar salidas de vertex con entradas de fragment: "S:TEXCOORD:0" o "L:3".</summary>
    public string? LinkKey { get; set; }

    public string SlotText { get; set; } = "";
}

internal sealed class InterfaceSignature
{
    public InterfaceSignature(IMethodSymbol method, ShaderStageKind stage, IReadOnlyList<InterfaceMember> members)
    {
        Method = method;
        Stage = stage;
        Members = members;
    }

    public IMethodSymbol Method { get; }
    public ShaderStageKind Stage { get; }
    public IReadOnlyList<InterfaceMember> Members { get; }
}

internal static class InterfaceRules
{
    public static InterfaceSignature Analyze(IMethodSymbol method, ShaderStageKind stage, TargetSet targets, Action<Diagnostic> report)
    {
        var members = new List<InterfaceMember>();

        foreach (IParameterSymbol parameter in method.Parameters)
        {
            InterfaceDirection direction = parameter.RefKind == RefKind.Out ? InterfaceDirection.Output : InterfaceDirection.Input;
            Collect(parameter.Name, parameter.Type, direction, ShaderSymbols.GetConceptAttributes(parameter),
                DeclarationRules.LocationOf(parameter), parameter.RefKind == RefKind.Out, members, report);
        }

        if (!method.ReturnsVoid)
        {
            Location returnLocation = ReturnLocation(method);
            Collect("return", method.ReturnType, InterfaceDirection.Output, ShaderSymbols.GetReturnConceptAttributes(method),
                returnLocation, true, members, report);
        }

        Validate(method, stage, targets, members, report);
        return new InterfaceSignature(method, stage, members);
    }

    private static Location ReturnLocation(IMethodSymbol method)
    {
        foreach (SyntaxReference reference in method.DeclaringSyntaxReferences)
        {
            // MethodDeclarationSyntax.ReturnType: el subrayado cae en el tipo de retorno.
            var node = reference.GetSyntax();
            foreach (var child in node.ChildNodes())
            {
                if (child.GetType().Name is "PredefinedTypeSyntax" or "IdentifierNameSyntax" or "QualifiedNameSyntax" or "GenericNameSyntax" or "AliasQualifiedNameSyntax")
                {
                    return child.GetLocation();
                }
            }
        }

        return DeclarationRules.LocationOf(method);
    }

    /// <summary>Aplana structs de interfaz en sus campos. Un struct dentro de otro no está permitido.</summary>
    private static void Collect(string name, ITypeSymbol type, InterfaceDirection direction, IReadOnlyList<ConceptAttribute> attributes,
        Location location, bool isReturnOrOut, List<InterfaceMember> members, Action<Diagnostic> report)
    {
        GpuType gpuType = GpuType.Of(type);
        if (gpuType.Kind != GpuTypeKind.Struct)
        {
            members.Add(new InterfaceMember(name, type, direction, attributes, location, false, isReturnOrOut));
            return;
        }

        foreach (ISymbol member in type.GetMembers())
        {
            if (member.IsStatic || member.IsImplicitlyDeclared) continue;
            ITypeSymbol? memberType = member switch
            {
                IFieldSymbol { IsConst: false } field => field.Type,
                IPropertySymbol { IsIndexer: false } property => property.Type,
                _ => null
            };
            if (memberType is null) continue;

            if (GpuType.Of(memberType).Kind == GpuTypeKind.Struct)
            {
                report(Diagnostic.Create(ShaderRules.NestedInterfaceStruct, DeclarationRules.LocationOf(member), member.Name, type.Name));
                continue;
            }

            members.Add(new InterfaceMember(member.Name, memberType, direction, ShaderSymbols.GetConceptAttributes(member),
                DeclarationRules.LocationOf(member), true, isReturnOrOut));
        }
    }

    private static void Validate(IMethodSymbol method, ShaderStageKind stage, TargetSet targets, List<InterfaceMember> members,
        Action<Diagnostic> report)
    {
        string stageName = DeclarationRules.StageName(stage);
        var builtIns = new Dictionary<(InterfaceDirection, string), InterfaceMember>();
        var semantics = new Dictionary<(InterfaceDirection, string), InterfaceMember>();
        var locations = new Dictionary<(InterfaceDirection, int), InterfaceMember>();
        bool writesPosition = false;

        foreach (InterfaceMember member in members)
        {
            var slotAttributes = new List<ConceptAttribute>();
            foreach (ConceptAttribute attribute in member.Attributes)
            {
                if (attribute.Is(Concepts.BindingSlot) || attribute.Is(Concepts.UniformBuffer) ||
                    attribute.Is(Concepts.RWStorageBuffer) || attribute.Is(Concepts.ReadOnlyStorageBuffer))
                {
                    report(Diagnostic.Create(ShaderRules.DescriptorOnInterface, attribute.GetLocation() ?? member.Location,
                        member.Name, attribute.Name));
                    continue;
                }

                if (BuiltInTable.IsBuiltIn(attribute) || attribute.Is(Concepts.Location) || attribute.Is(Concepts.UserSemantic))
                {
                    slotAttributes.Add(attribute);
                }
            }

            if (slotAttributes.Count > 1)
            {
                report(Diagnostic.Create(ShaderRules.SlotConflict, slotAttributes[1].GetLocation() ?? member.Location,
                    member.Name, slotAttributes[0].Name, slotAttributes[1].Name));
            }

            // El tipo, antes que el slot: un recurso o un tipo sin representación no puede pasar entre etapas.
            switch (member.Type.Kind)
            {
                case GpuTypeKind.Texture:
                case GpuTypeKind.Sampler:
                    report(Diagnostic.Create(ShaderRules.ResourceInInterface, member.Location, member.Name,
                        Loc.T(member.Type.Kind == GpuTypeKind.Texture ? "a texture" : "a sampler")));
                    continue;
                case GpuTypeKind.Unsupported:
                    report(Diagnostic.Create(ShaderRules.InterfaceUnsupportedType, member.Location, member.Name, member.TypeName));
                    continue;
            }

            ConceptAttribute? slot = slotAttributes.FirstOrDefault();
            string direction = member.Direction == InterfaceDirection.Input ? Loc.T("input") : Loc.T("output");

            if (slot is not null && BuiltInTable.Resolve(slot, stage) is { } builtIn)
            {
                member.SlotText = slot.Name;
                if (!builtIn.IsAllowed(stage, member.Direction))
                {
                    if (builtIn.Allowed.Any(a => a.Stage == stage))
                    {
                        string expected = Loc.T(builtIn.Allowed.First(a => a.Stage == stage).Direction == InterfaceDirection.Input ? "an input" : "an output");
                        report(Diagnostic.Create(ShaderRules.BuiltInWrongDirection, slot.GetLocation() ?? member.Location,
                            slot.Name, builtIn.SpirvName, expected, stageName, Loc.T(member.Direction == InterfaceDirection.Input ? "an input" : "an output")));
                    }
                    else
                    {
                        string where = string.Join(", ", builtIn.Allowed.Select(a =>
                            DeclarationRules.StageName(a.Stage) + Loc.T(a.Direction == InterfaceDirection.Input ? " (input)" : " (output)")));
                        report(Diagnostic.Create(ShaderRules.BuiltInWrongStage, slot.GetLocation() ?? member.Location,
                            slot.Name, builtIn.SpirvName, stageName, where));
                    }

                    continue;
                }

                if (!builtIn.Supported)
                {
                    report(Diagnostic.Create(ShaderRules.BuiltInNotSupported, slot.GetLocation() ?? member.Location, slot.Name, builtIn.SpirvName));
                    continue;
                }

                if (!builtIn.Accepts(member.Type))
                {
                    report(Diagnostic.Create(ShaderRules.BuiltInWrongType, member.Location, slot.Name, builtIn.SpirvName,
                        builtIn.ExpectedType, member.Name, member.TypeName));
                }

                if (builtIns.TryGetValue((member.Direction, builtIn.SpirvName), out InterfaceMember? previous))
                {
                    report(Diagnostic.Create(ShaderRules.BuiltInDuplicate, member.Location, builtIn.SpirvName, direction, method.Name));
                }
                else
                {
                    builtIns[(member.Direction, builtIn.SpirvName)] = member;
                }

                if (builtIn.SpirvName == "Position" && member.Direction == InterfaceDirection.Output) writesPosition = true;

                ShaderTargets missing = builtIn.MissingOn & targets;
                if (missing != ShaderTargets.None && builtIn.PortabilityNote is not null)
                {
                    report(Diagnostic.Create(ShaderRules.PortableBuiltIn, slot.GetLocation() ?? member.Location, slot.Name,
                        builtIn.SpirvName, Loc.T(builtIn.PortabilityNote), ShaderTargetsParser.Describe(missing)));
                }

                continue;
            }

            // A partir de aquí el miembro es una variable de usuario: location o semántica.
            if (member.Type.IsScalar(GpuScalar.Bool) || member.Type.Kind == GpuTypeKind.Vector && member.Type.Scalar == GpuScalar.Bool)
            {
                report(Diagnostic.Create(ShaderRules.BoolInInterface, member.Location, member.Name));
                continue;
            }

            if (member.Type.Kind == GpuTypeKind.Array && !HasArrayLength(member))
            {
                report(Diagnostic.Create(ShaderRules.InterfaceArrayLength, member.Location, member.Name));
                continue;
            }

            if (stage == ShaderStageKind.Compute)
            {
                if (slot is not null)
                {
                    report(Diagnostic.Create(ShaderRules.LocationInCompute, slot.GetLocation() ?? member.Location, member.Name, slot.Name));
                }
                else
                {
                    report(Diagnostic.Create(ShaderRules.MissingSlot, member.Location, member.Name, direction, method.Name));
                }

                continue;
            }

            if (slot is null)
            {
                // Una salida de fragment sin anotar (return o out que no es struct) es el render target 0.
                bool implicitTarget = stage == ShaderStageKind.Fragment && member.Direction == InterfaceDirection.Output &&
                                      !member.IsStructMember;
                if (!implicitTarget)
                {
                    report(Diagnostic.Create(ShaderRules.MissingSlot, member.Location, member.Name, direction, method.Name));
                    continue;
                }

                MarkLocation(member, 0, locations, method, report);
                continue;
            }

            if (slot.Is(Concepts.UserSemantic))
            {
                int index = slot.IntKey("Index") ?? FirstInt(slot) ?? 0;
                member.SlotText = slot.Name + (index == 0 ? "" : index.ToString());
                if (index < 0)
                {
                    report(Diagnostic.Create(ShaderRules.NegativeSlotIndex, slot.GetLocation() ?? member.Location, slot.Name, member.Name, index));
                    continue;
                }

                if (stage == ShaderStageKind.Fragment && member.Direction == InterfaceDirection.Output)
                {
                    report(Diagnostic.Create(ShaderRules.SemanticInFragmentOutput, slot.GetLocation() ?? member.Location, member.Name, slot.Name));
                    continue;
                }

                string key = slot.Name.ToUpperInvariant() + ":" + index;
                if (semantics.ContainsKey((member.Direction, key)))
                {
                    report(Diagnostic.Create(ShaderRules.BuiltInDuplicate, member.Location, slot.Name + index, direction, method.Name));
                }
                else
                {
                    semantics[(member.Direction, key)] = member;
                }

                member.LinkKey = "S:" + key;
                continue;
            }

            // Location explícita (location(n), SV_Target(n), attribute(n), color(n)...).
            int locationIndex = slot.IntKey("Index") ?? FirstInt(slot) ?? 0;
            member.SlotText = slot.Name + "(" + locationIndex + ")";
            if (locationIndex < 0)
            {
                report(Diagnostic.Create(ShaderRules.NegativeSlotIndex, slot.GetLocation() ?? member.Location, slot.Name, member.Name, locationIndex));
                continue;
            }

            if (stage == ShaderStageKind.Fragment && member.Direction == InterfaceDirection.Output && locationIndex > 7)
            {
                report(Diagnostic.Create(ShaderRules.RenderTargetRange, slot.GetLocation() ?? member.Location, locationIndex, member.Name));
                continue;
            }

            MarkLocation(member, locationIndex, locations, method, report);
        }

        if (stage == ShaderStageKind.Vertex && !writesPosition)
        {
            report(Diagnostic.Create(ShaderRules.VertexWithoutPosition, DeclarationRules.LocationOf(method), method.Name));
        }

        ReportInterfaceLimits(method, stage, targets, members, report);
    }

    private static void MarkLocation(InterfaceMember member, int first, Dictionary<(InterfaceDirection, int), InterfaceMember> locations,
        IMethodSymbol method, Action<Diagnostic> report)
    {
        member.LinkKey = "L:" + first;
        int count = member.Type.LocationCount;
        for (int location = first; location < first + count; location++)
        {
            if (locations.TryGetValue((member.Direction, location), out InterfaceMember? other))
            {
                report(Diagnostic.Create(ShaderRules.LocationOverlap, member.Location, location, member.Name, other.Name,
                    Loc.T(member.Direction == InterfaceDirection.Input ? "inputs" : "outputs"), method.Name));
                return;
            }

            locations[(member.Direction, location)] = member;
        }
    }

    private static void ReportInterfaceLimits(IMethodSymbol method, ShaderStageKind stage, TargetSet targets,
        List<InterfaceMember> members, Action<Diagnostic> report)
    {
        int Count(InterfaceDirection direction) => members
            .Where(m => m.Direction == direction && m.LinkKey is not null)
            .Sum(m => m.Type.LocationCount);

        void Limit(int used, Func<ApiProfile, int?> limit, string what)
        {
            var exceeding = targets.Exceeding(used, limit);
            if (exceeding.Count == 0) return;
            report(Diagnostic.Create(ShaderRules.PortableLimit, DeclarationRules.LocationOf(method),
                Loc.F("'{0}' uses {1} {2}; ", method.Name, used, Loc.T(what)) +
                string.Join(", ", exceeding.Select(p => Loc.F("{0} guarantees {1}", p.Name, limit(p)))) + ".",
                TargetSet.Describe(exceeding)));
        }

        if (stage == ShaderStageKind.Fragment)
        {
            Limit(Count(InterfaceDirection.Output), p => p.MaxColorAttachments, "render targets");
        }

        if (stage == ShaderStageKind.Vertex)
        {
            Limit(Count(InterfaceDirection.Input), p => p.MaxVertexInputs, "vertex inputs");
        }

        int interStage = stage == ShaderStageKind.Vertex ? Count(InterfaceDirection.Output)
            : stage == ShaderStageKind.Fragment ? Count(InterfaceDirection.Input) : 0;
        Limit(interStage, p => p.MaxInterStageVariables, "inter-stage variables");
    }

    // Los arrays de interfaz necesitan una longitud conocida; el compilador todavía no la acepta en la interfaz.
    private static bool HasArrayLength(InterfaceMember member) => false;

    private static int? FirstInt(ConceptAttribute attribute) =>
        attribute.Data.ConstructorArguments.Length > 0 && attribute.Data.ConstructorArguments[0].Value is int value ? value : null;
}
