using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace YotsubaShader.Rules;

internal enum ResourceKind
{
    None,
    SampledTexture,
    StorageTexture,
    Sampler,
    UniformBuffer,
    StorageBuffer
}

/// <summary>Reglas de los recursos de un shader: texturas, samplers y buffers declarados como campos.</summary>
internal static class ResourceRules
{
    // Valores de YotsubaShader.Language.Native.Types.Texture.TextureUsage.
    private const int TextureUsageStorage = 2;

    // Valores de YotsubaShader.HlslSyntax.Resources.RegisterPrefix.
    private static readonly string[] RegisterPrefixes = { "b", "t", "s", "u", "c" };

    public static ResourceKind KindOf(ISymbol member, ITypeSymbol type)
    {
        GpuType gpuType = GpuType.Of(type);
        if (gpuType.Kind == GpuTypeKind.Texture)
        {
            return TextureUsage(type) == TextureUsageStorage ? ResourceKind.StorageTexture : ResourceKind.SampledTexture;
        }

        if (gpuType.Kind == GpuTypeKind.Sampler) return ResourceKind.Sampler;

        // T[], List<T>, ImmutableArray<T>, ImmutableList<T>: storage buffer de elementos (salvo que se marque [Uniform]).
        if (member is IFieldSymbol && CollectionElement(type, out _) is not null &&
            !ShaderSymbols.GetConceptAttributes(member).Any(a => a.Is(Concepts.UniformBuffer)))
        {
            return ResourceKind.StorageBuffer;
        }

        // StructuredBuffer<T>, ConstantBuffer<T>: el concepto está en el tipo envoltorio.
        var attributes = ShaderSymbols.GetConceptAttributes(member).ToList();
        if (WrapperElement(type) is not null) attributes.AddRange(ShaderSymbols.GetConceptAttributes(type.OriginalDefinition));
        if (attributes.Any(a => a.Is(Concepts.UniformBuffer))) return ResourceKind.UniformBuffer;
        if (attributes.Any(a => a.Is(Concepts.RWStorageBuffer) || a.Is(Concepts.ReadOnlyStorageBuffer))) return ResourceKind.StorageBuffer;
        return ResourceKind.None;
    }

    public static int TextureUsage(ITypeSymbol type)
    {
        foreach (AttributeData attribute in type.GetAttributes())
        {
            if (ShaderSymbols.FullName(attribute.AttributeClass) != ShaderNames.Texture) continue;
            foreach (KeyValuePair<string, TypedConstant> named in attribute.NamedArguments)
            {
                if (named.Key == "Usage" && named.Value.Value is int usage) return usage;
            }
        }

        return 1;
    }

    public static int TextureFormat(ITypeSymbol type)
    {
        foreach (AttributeData attribute in type.GetAttributes())
        {
            if (ShaderSymbols.FullName(attribute.AttributeClass) != ShaderNames.Texture) continue;
            foreach (KeyValuePair<string, TypedConstant> named in attribute.NamedArguments)
            {
                if (named.Key == "Format" && named.Value.Value is int format) return format;
            }
        }

        return 0;
    }

    private static string Describe(ResourceKind kind) => kind switch
    {
        ResourceKind.SampledTexture => Loc.T("a texture"),
        ResourceKind.StorageTexture => Loc.T("a storage texture"),
        ResourceKind.Sampler => Loc.T("a sampler"),
        ResourceKind.UniformBuffer => Loc.T("a uniform buffer"),
        ResourceKind.StorageBuffer => Loc.T("a storage buffer"),
        _ => Loc.T("a resource")
    };

    public static void Analyze(INamedTypeSymbol shader, TargetSet targets, Action<Diagnostic> report)
    {
        var bindings = new Dictionary<(int Set, int Binding), ISymbol>();
        int textures = 0, samplers = 0, buffers = 0;

        foreach (ISymbol member in shader.GetMembers())
        {
            if (member.IsImplicitlyDeclared) continue;

            ITypeSymbol? type = member switch
            {
                IFieldSymbol field => field.Type,
                IPropertySymbol { IsIndexer: false } property => property.Type,
                _ => null
            };
            if (type is null) continue;

            if (member is IFieldSymbol { IsConst: true }) continue;

            ResourceKind kind = KindOf(member, type);
            var attributes = ShaderSymbols.GetConceptAttributes(member);


            if (kind == ResourceKind.None)
            {
                // Un campo con semántica o built-in ([POSITION], [SV_Position]...) es una entrada del stage que lo lee.
                if (attributes.Any(a => a.Is(Concepts.Location) || a.Is(Concepts.UserSemantic) || BuiltInTable.IsBuiltIn(a)))
                {
                    AnalyzeInterfaceField(shader, member, type, attributes, report);
                    continue;
                }

                if (member is IFieldSymbol { IsStatic: true, IsReadOnly: false })
                {
                    report(Diagnostic.Create(ShaderRules.MutableStaticField, DeclarationRules.LocationOf(member), member.Name));
                }
                else if (member is IFieldSymbol { IsStatic: false })
                {
                    report(Diagnostic.Create(ShaderRules.PlainFieldInShader, DeclarationRules.LocationOf(member), member.Name));
                }

                continue;
            }

            if (member is IPropertySymbol)
            {
                report(Diagnostic.Create(ShaderRules.ResourceDeclaration, DeclarationRules.LocationOf(member), member.Name, Loc.T("is a property")));
                continue;
            }

            if (member.IsStatic)
            {
                report(Diagnostic.Create(ShaderRules.ResourceDeclaration, DeclarationRules.LocationOf(member), member.Name, Loc.T("is static")));
                continue;
            }

            foreach (ConceptAttribute attribute in attributes)
            {
                if (attribute.Is(Concepts.Location) || attribute.Is(Concepts.UserSemantic) || BuiltInTable.IsBuiltIn(attribute))
                {
                    report(Diagnostic.Create(ShaderRules.LocationOnResource, attribute.GetLocation() ?? DeclarationRules.LocationOf(member),
                        member.Name, attribute.Name));
                }
            }

            GodotSupport.AnalyzeResource(member, kind, targets, report);

            switch (kind)
            {
                case ResourceKind.SampledTexture:
                case ResourceKind.StorageTexture:
                    textures++;
                    break;
                case ResourceKind.Sampler:
                    samplers++;
                    break;
                default:
                    buffers++;
                    break;
            }

            if (kind is ResourceKind.UniformBuffer or ResourceKind.StorageBuffer && WrapperElement(type) is { } element)
            {
                BufferRules.AnalyzeWrapper(member, element, kind, targets, report);
                if (kind == ResourceKind.UniformBuffer && GpuType.Of(element).Kind == GpuTypeKind.Struct)
                {
                    UniformLayout.CheckSize(member, (INamedTypeSymbol)element, targets, report);
                }
            }
            else if (kind is ResourceKind.UniformBuffer or ResourceKind.StorageBuffer)
            {
                if (GpuType.Of(type).Kind != GpuTypeKind.Struct)
                {
                    report(Diagnostic.Create(ShaderRules.BufferNotStruct, DeclarationRules.LocationOf(member), member.Name,
                        type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)));
                }
                else
                {
                    BufferRules.Analyze(member, (INamedTypeSymbol)type, kind, targets, report);
                    if (kind == ResourceKind.UniformBuffer) UniformLayout.CheckSize(member, (INamedTypeSymbol)type, targets, report);
                }
            }

            if (kind == ResourceKind.StorageBuffer)
            {
                TargetRules.ReportFeature(targets, ApiFeature.StorageBuffer, DeclarationRules.LocationOf(member), Loc.F("The storage buffer '{0}'", member.Name), report);
            }

            if (kind == ResourceKind.StorageTexture)
            {
                TargetRules.ReportFeature(targets, ApiFeature.StorageTexture, DeclarationRules.LocationOf(member), Loc.F("The storage texture '{0}'", member.Name), report);

                if (TextureFormat(type) == 0 && targets.Check(ApiFeature.StorageTextureWithoutFormat) is { } noFormat)
                {
                    DiagnosticDescriptor descriptor = noFormat.Unavailable ? ShaderRules.AsError(ShaderRules.PortableResource) : ShaderRules.PortableResource;
                    report(Diagnostic.Create(descriptor, DeclarationRules.LocationOf(member), member.Name,
                        Loc.F("is a storage texture without a format: {0}", noFormat.Reasons), noFormat.Targets));
                }
            }

            AnalyzeBinding(member, kind, attributes, bindings, targets, report);
        }

        if ((targets & ShaderTargets.Metal) != 0)
        {
            if (textures > 31 || samplers > 16 || buffers > 31)
            {
                report(Diagnostic.Create(ShaderRules.PortableLimit, DeclarationRules.LocationOf(shader),
                    Loc.F("'{0}' uses {1} textures, {2} samplers and {3} buffers; Metal supports 31, 16 and 31 per stage.", shader.Name, textures, samplers, buffers),
                    "Metal"));
            }
        }
    }

    /// <summary>
    /// Un campo con semántica o built-in se lee como entrada de la etapa que lo usa: tiene que ser una entrada
    /// válida en alguna de las etapas del shader.
    /// </summary>
    private static void AnalyzeInterfaceField(INamedTypeSymbol shader, ISymbol member, ITypeSymbol type,
        IReadOnlyList<ConceptAttribute> attributes, Action<Diagnostic> report)
    {
        var stages = ShaderSymbols.EntryPoints(shader).SelectMany(ShaderSymbols.GetStages).Select(s => s.Stage).Distinct().ToList();
        ConceptAttribute slot = attributes.First(a => a.Is(Concepts.Location) || a.Is(Concepts.UserSemantic) || BuiltInTable.IsBuiltIn(a));
        Location location = slot.GetLocation() ?? DeclarationRules.LocationOf(member);

        if (member.IsStatic || member is IPropertySymbol)
        {
            report(Diagnostic.Create(ShaderRules.ResourceDeclaration, DeclarationRules.LocationOf(member), member.Name,
                Loc.T("is a shader input and must be an instance field")));
            return;
        }

        if (BuiltInTable.IsBuiltIn(slot))
        {
            bool validSomewhere = stages.Any(stage => BuiltInTable.Resolve(slot, stage) is { } rule && rule.IsAllowed(stage, InterfaceDirection.Input));
            if (!validSomewhere)
            {
                BuiltInRule? rule = stages.Select(stage => BuiltInTable.Resolve(slot, stage)).FirstOrDefault(r => r is not null);
                report(Diagnostic.Create(ShaderRules.BuiltInWrongStage, location, slot.Name, rule?.SpirvName ?? slot.Name,
                    string.Join("/", stages.Select(DeclarationRules.StageName)),
                    rule is null ? "?" : string.Join(", ", rule.Allowed.Select(a => DeclarationRules.StageName(a.Stage)))));
                return;
            }

            BuiltInRule supported = stages.Select(stage => BuiltInTable.Resolve(slot, stage)).First(r => r is not null)!;
            if (!supported.Supported)
            {
                report(Diagnostic.Create(ShaderRules.BuiltInNotSupported, location, slot.Name, supported.SpirvName));
            }
            else if (!supported.Accepts(GpuType.Of(type)))
            {
                report(Diagnostic.Create(ShaderRules.BuiltInWrongType, DeclarationRules.LocationOf(member), slot.Name, supported.SpirvName,
                    supported.ExpectedType, member.Name, type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)));
            }

            return;
        }

        if (!stages.Any(s => s is ShaderStageKind.Vertex or ShaderStageKind.Fragment))
        {
            report(Diagnostic.Create(ShaderRules.LocationInCompute, location, member.Name, slot.Name));
        }
        else if (GpuType.Of(type) is { Kind: GpuTypeKind.Scalar or GpuTypeKind.Vector, Scalar: GpuScalar.Bool })
        {
            report(Diagnostic.Create(ShaderRules.BoolInInterface, DeclarationRules.LocationOf(member), member.Name));
        }
    }

    private static void AnalyzeBinding(ISymbol member, ResourceKind kind, IReadOnlyList<ConceptAttribute> attributes,
        Dictionary<(int, int), ISymbol> bindings, TargetSet targets, Action<Diagnostic> report)
    {
        int? set = null, binding = null;
        ConceptAttribute? register = null;
        Location location = DeclarationRules.LocationOf(member);

        // Igual que ShaderProcessor.GetAttributeData: Set y Binding pueden venir de atributos distintos.
        foreach (ConceptAttribute attribute in attributes.Where(a => a.Is(Concepts.BindingSlot)))
        {
            if (attribute.IntKey("Set") is { } s) set = s;
            if (attribute.IntKey("Binding") is { } b) binding = b;
            if (attribute.Keys.TryGetValue("RegisterString", out object? text) && text is string registerText)
            {
                if (TryParseRegister(registerText, out int registerSet, out int registerBinding))
                {
                    set = registerSet;
                    binding = registerBinding;
                }
            }

            if (attribute.Name == "register") register = attribute;
        }

        if (binding is null)
        {
            report(Diagnostic.Create(ShaderRules.ResourceWithoutBinding, location, member.Name, Describe(kind)));
            return;
        }

        set ??= 0;
        if (set < 0 || binding < 0)
        {
            report(Diagnostic.Create(ShaderRules.NegativeBinding, location, member.Name, Loc.T(set < 0 ? "the set" : "the binding"),
                set < 0 ? set : binding));
            return;
        }

        if (register is not null && RegisterPrefixOf(register) is { } prefix)
        {
            string expected = kind switch
            {
                ResourceKind.SampledTexture => "t",
                ResourceKind.StorageTexture => "u",
                ResourceKind.Sampler => "s",
                ResourceKind.UniformBuffer => "b",
                ResourceKind.StorageBuffer => ShaderSymbols.GetConceptAttributes(member).Any(a => a.Is(Concepts.RWStorageBuffer)) ? "u" : "t",
                _ => prefix
            };

            if (prefix != expected && !(kind == ResourceKind.StorageBuffer && prefix is "t" or "u"))
            {
                report(Diagnostic.Create(ShaderRules.RegisterKindMismatch, register.GetLocation() ?? location, member.Name,
                    Describe(kind), prefix, expected));
            }
        }

        if (bindings.TryGetValue((set.Value, binding.Value), out ISymbol? other))
        {
            report(Diagnostic.Create(ShaderRules.DuplicateBinding, location, member.Name, other.Name, set, binding));
        }
        else
        {
            bindings[(set.Value, binding.Value)] = member;
        }

        if (set is { } setIndex && targets.Exceeding(setIndex + 1, p => p.MaxDescriptorSets) is { Count: > 0 } exceeding)
        {
            report(Diagnostic.Create(ShaderRules.PortableResource, location, member.Name,
                Loc.F("uses set {0}; ", setIndex) + string.Join(", ", exceeding.Select(p => Loc.F("{0} guarantees {1} sets", p.Name, p.MaxDescriptorSets))),
                TargetSet.Describe(exceeding)));
        }
    }

    private static string? RegisterPrefixOf(ConceptAttribute register)
    {
        foreach (TypedConstant argument in register.Data.ConstructorArguments)
        {
            if (argument.Value is string text && text.Length > 0) return text.Substring(0, 1);
            if (argument.Type?.TypeKind == TypeKind.Enum && argument.Value is int value && value >= 0 && value < RegisterPrefixes.Length)
            {
                return RegisterPrefixes[value];
            }
        }

        return null;
    }

    /// <summary>
    /// ¿Es un buffer de solo lectura? MSL device(readOnly: true), WGSL storage(read) o un atributo que solo
    /// declara el concepto ReadOnlyStorageBuffer.
    /// </summary>
    /// <summary>T de un buffer envoltorio (StructuredBuffer&lt;T&gt;, RWStructuredBuffer&lt;T&gt;, ConstantBuffer&lt;T&gt;).</summary>
    /// <summary>
    /// Colecciones de .NET que en un campo del shader son un storage buffer: T[] y List&lt;T&gt; de lectura y
    /// escritura; ImmutableArray&lt;T&gt; e ImmutableList&lt;T&gt; de solo lectura. Igual que ShaderProcessor.CollectionElement.
    /// </summary>
    public static ITypeSymbol? CollectionElement(ITypeSymbol type, out bool readOnly)
    {
        readOnly = false;
        if (type is IArrayTypeSymbol { Rank: 1 } array) return array.ElementType;
        if (type is not INamedTypeSymbol { IsGenericType: true, TypeArguments.Length: 1 } named) return null;
        switch (named.OriginalDefinition.ToDisplayString())
        {
            case "System.Collections.Generic.List<T>":
                return named.TypeArguments[0];
            case "System.Collections.Immutable.ImmutableArray<T>":
            case "System.Collections.Immutable.ImmutableList<T>":
                readOnly = true;
                return named.TypeArguments[0];
            default:
                return null;
        }
    }

    public static ITypeSymbol? WrapperElement(ITypeSymbol type) =>
        CollectionElement(type, out _) ??
        (type is INamedTypeSymbol { IsGenericType: true, TypeArguments.Length: 1 } wrapper &&
        ShaderSymbols.GetConceptAttributes(wrapper.OriginalDefinition).Any(a =>
            a.Is(Concepts.UniformBuffer) || a.Is(Concepts.RWStorageBuffer) || a.Is(Concepts.ReadOnlyStorageBuffer))
            ? wrapper.TypeArguments[0]
            : null);

    public static bool IsReadOnlyBuffer(ISymbol member)
    {
        ITypeSymbol? memberType = member switch { IFieldSymbol f => f.Type, IPropertySymbol p => p.Type, _ => null };
        if (memberType is not null && CollectionElement(memberType, out bool immutable) is not null && immutable) return true;
        var concepts = ShaderSymbols.GetConceptAttributes(member).ToList();
        if (memberType is not null && WrapperElement(memberType) is not null)
        {
            concepts.AddRange(ShaderSymbols.GetConceptAttributes(memberType.OriginalDefinition));
        }

        foreach (ConceptAttribute attribute in concepts)
        {
            if (attribute.Is(Concepts.ReadOnlyStorageBuffer) && !attribute.Is(Concepts.RWStorageBuffer)) return true;
            if (!attribute.Keys.TryGetValue("Use", out object? use)) continue;
            if (use is true) return true;
            if (attribute.Data.ConstructorArguments.FirstOrDefault() is { Type: INamedTypeSymbol { TypeKind: TypeKind.Enum } enumType, Value: { } value } &&
                enumType.GetMembers().OfType<IFieldSymbol>().FirstOrDefault(f => Equals(f.ConstantValue, value))?.Name == "read")
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Mismo criterio que <c>register.Parse</c>: prefijo de una letra (su valor es el set) y número.</summary>
    public static bool TryParseRegister(string text, out int set, out int binding)
    {
        set = binding = 0;
        if (text.Length < 2) return false;
        int prefix = Array.IndexOf(RegisterPrefixes, text.Substring(0, 1));
        if (prefix < 0) return false;
        if (!int.TryParse(text.Substring(1), System.Globalization.NumberStyles.None,
                System.Globalization.CultureInfo.InvariantCulture, out binding)) return false;
        set = prefix;
        return true;
    }
}
