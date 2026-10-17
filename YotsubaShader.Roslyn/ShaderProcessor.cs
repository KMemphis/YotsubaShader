#region MIT License - Copyright (c) 2026 Memphis Yomael
/*
╔════════════════════════════════════════════════════════════════════════════════════╗
║                                                                                    ║
║  ███╗   ███╗██╗████████╗    ██╗     ██╗ ██████╗███████╗███╗   ██╗███████╗███████╗  ║
║  ████╗ ████║██║╚══██╔══╝    ██║     ██║██╔════╝██╔════╝████╗  ██║██╔════╝██╔════╝  ║
║  ██╔████╔██║██║   ██║       ██║     ██║██║     █████╗  ██╔██╗ ██║███████╗█████╗    ║
║  ██║╚██╔╝██║██║   ██║       ██║     ██║██║     ██╔══╝  ██║╚██╗██║╚════██║██╔══╝    ║
║  ██║ ╚═╝ ██║██║   ██║       ███████╗██║╚██████╗███████╗██║ ╚████║███████║███████╗  ║
║  ╚═╝     ╚═╝╚═╝   ╚═╝       ╚══════╝╚═╝ ╚═════╝╚══════╝╚═╝  ╚═══╝╚══════╝╚══════╝  ║
║                                                                                    ║
╠════════════════════════════════════════════════════════════════════════════════════╣
║  Copyright (c) 2026 Memphis Yomael                                                 ║
╠════════════════════════════════════════════════════════════════════════════════════╣
║                                                                                    ║
║  Permission is hereby granted, free of charge, to any person obtaining a copy      ║
║  of this software and associated documentation files (the "Software"), to deal     ║
║  in the Software without restriction, including without limitation the rights      ║
║  to use, copy, modify, merge, publish, distribute, sublicense, and/or sell         ║
║  copies of the Software, and to permit persons to whom the Software is             ║
║  furnished to do so, subject to the following conditions:                          ║
║                                                                                    ║
║  The above copyright notice and this permission notice shall be included           ║
║  in all copies or substantial portions of the Software.                            ║
║                                                                                    ║
║  THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR        ║
║  IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,          ║
║  FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE       ║
║  AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER            ║
║  LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,     ║
║  OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE     ║
║  SOFTWARE.                                                                         ║
║                                                                                    ║
╚════════════════════════════════════════════════════════════════════════════════════╝
*/
#endregion

using System.Collections.Immutable;
using System.Globalization;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using YotsubaShader.IR.Intermediate;
using YotsubaShader.IR.Intermediate.FunctionBody;
using YotsubaShader.IR.Intermediate.Slot;
using YotsubaShader.IR.Intermediate.Slot.Types;
using YotsubaShader.IR.Intermediate.Types;
using YotsubaShader.Language.Native.Types.Vector;
using YotsubaShader.Language.Native.Types.Vector.Exceptions;
using YotsubaShader.Language.Resources;
using YotsubaShader.Language.StylesAttributes;
using YotsubaShader.Language.StylesAttributes.Exceptions;
using YotsubaShader.Language.StylesAttributes.GlslStyle.Resources;
using YotsubaShader.Language.StylesAttributes.HlslStyle.Resorces;
using YotsubaShader.Language.StylesAttributes.HlslStyle.Resources;
using YotsubaShader.Roslyn.Model;
using builtin_position = YotsubaShader.Language.StylesAttributes.WgslStyle.builtin_position;
using BuiltIn_Position = YotsubaShader.Language.StylesAttributes.VulkanStyle.BuiltIn_Position;
using builtin_vertex_index = YotsubaShader.Language.StylesAttributes.WgslStyle.builtin_vertex_index;
using BuiltIn_VertexIndex = YotsubaShader.Language.StylesAttributes.VulkanStyle.BuiltIn_VertexIndex;
using Double = YotsubaShader.IR.Intermediate.Types.Double;
using gl_Position = YotsubaShader.Language.StylesAttributes.GlslStyle.gl_Position;
using gl_VertexIndex = YotsubaShader.Language.StylesAttributes.GlslStyle.gl_VertexIndex;
using LanguageStage = YotsubaShader.Language.Stage;
using IRStage = YotsubaShader.IR.Intermediate.Stage;
using ParameterInfo = YotsubaShader.Roslyn.Model.ParameterInfo;
using position = YotsubaShader.Language.StylesAttributes.MetalStyle.position;
using SV_Position = YotsubaShader.Language.StylesAttributes.HlslStyle.SV_Position;
using SV_VertexID = YotsubaShader.Language.StylesAttributes.HlslStyle.SV_VertexID;
using vertex_id = YotsubaShader.Language.StylesAttributes.MetalStyle.vertex_id;
using Style = YotsubaShader.Language.StylesAttributes;
namespace YotsubaShader.Roslyn;

using ParameterInfo = Model.ParameterInfo;

public class ShaderProcessor
{
    public readonly List<Shader> Shaders = new List<Shader>();

    public void Process(Compilation compilation, CancellationToken token)
    { 
        var csharpShaderMetadata = ShaderDiscovery.Find(compilation, token);
        foreach (ShaderInfo shaderInfo in csharpShaderMetadata)
        {
            
            List<Function> shaderFunction = new List<Function>();
            foreach (var shaderInfoEntryPoint in shaderInfo.EntryPoints)
            {
                IRStage stage = shaderInfoEntryPoint.Stage switch
                {
                    LanguageStage.Compute  => IRStage.Compute,
                    LanguageStage.Fragment => IRStage.Fragment,
                    LanguageStage.Vertex   => IRStage.Vertex,
                    _ => IRStage.None
                };

                List<Storage> inputs = new List<Storage>();
                List<Storage> outputs = new List<Storage>();
                List<Statement> bodyFunc = new List<Statement>();
                
                //Process Inputs
                foreach (var parameterInfo in shaderInfoEntryPoint.Parameters)
                {
                    inputs.Add(ProcessInput(parameterInfo, compilation, stage));
                }
                    
                var syntax = shaderInfoEntryPoint.Method.DeclaringSyntaxReferences[0].GetSyntax();
                var semanticModel = compilation.GetSemanticModel(syntax.SyntaxTree);

                var operations = semanticModel.GetOperation(syntax);

                if (operations is IMethodBodyOperation body)
                {
                    var blockOperations = body.BlockBody ?? body.ExpressionBody ?? throw new NotImplementedException();
                    foreach (var op in blockOperations.Operations)
                    {
                        switch (op)
                        {
                            case IAssignmentOperation assignmentOperation:
                                    
                                break;
                        }
                    }
                }
                
                
                Function fn = new Function(shaderInfoEntryPoint.Name, stage, inputs, outputs, bodyFunc);

            }
        }
    }

    private Storage ProcessInput(ParameterInfo parameterInfo, Compilation compilation, IRStage stage)
    {
        InterfaceSlot? slot = null;
        var atributes = parameterInfo.Symbol.GetAttributes();

        if (atributes.Length > 0)
        {
            slot = GetAttributeData(atributes, stage);
        }

        return GetStorage(parameterInfo.Symbol, null, slot, compilation, stage);
    }

    private static Storage GetStorage(ISymbol symbol, Storage? storage, InterfaceSlot? slot, Compilation compilation, IRStage stage)
    {
        ITypeSymbol? type = symbol switch
        {
            IParameterSymbol param => param.Type,
            IFieldSymbol field => field.Type,
            IPropertySymbol property => property.Type,
            _ => null
        };  

        if (type is null)
        {
            throw new NotSupportedException(
                $"Symbol '{symbol.ToDisplayString()}' cannot be represented as shader storage.");
        }

        var parameter = symbol as IParameterSymbol;
        StorageKind storageKind = GetStorageKind(symbol, compilation);

        switch (type.SpecialType)
        {
            case SpecialType.System_Int32:
                storage = new Int(symbol.Name, storageKind, slot)
                {
                    Bits = 32,
                    Signed = true,
                    InitialValue = parameter is { HasExplicitDefaultValue: true }
                        ? ((int)parameter.ExplicitDefaultValue!).ToString()
                        : null
                };
                break;
            case SpecialType.System_UInt32:
                storage = new Int(symbol.Name, storageKind, slot)
                {
                    Bits = 32,
                    Signed = false,
                    InitialValue = parameter is { HasExplicitDefaultValue: true }
                        ? ((uint)parameter.ExplicitDefaultValue!).ToString()
                        : null
                };
                break;
            case SpecialType.System_Double:
                storage = new Double(symbol.Name, storageKind, slot)
                {
                    InitialValue = parameter is { HasExplicitDefaultValue: true }
                        ? (double)parameter.ExplicitDefaultValue!
                        : null
                };
                break;
            case SpecialType.System_Single:
                storage = new Float(symbol.Name, storageKind, slot)
                {
                    InitialValue = parameter is { HasExplicitDefaultValue: true }
                        ? (float)parameter.ExplicitDefaultValue!
                        : null
                };
                break;
            case SpecialType.System_Boolean:
                storage = new Bool(symbol.Name, storageKind, slot)
                {
                    InitialValue = parameter is { HasExplicitDefaultValue: true }
                        ? (bool)parameter.ExplicitDefaultValue!
                        : null
                };
                break;
        }

        if (storage is null)
        {
            var attributes = type.GetAttributes();

            var attribute =
                attributes
                    .FirstOrDefault(x => x.AttributeClass?.ToDisplayString() == typeof(VectorAttribute).FullName);
            if (attribute is not null)
            {
                var members = type.GetMembers();
                
                Dictionary<int, string> components = new Dictionary<int, string>();
                ITypeSymbol? componentsType = null;
                foreach (var memberInfo in members)
                {
                    if (memberInfo.IsStatic || memberInfo.IsImplicitlyDeclared) continue;
                    if (memberInfo is IPropertySymbol { IsIndexer: true }) continue;
                    ITypeSymbol? memberType = memberInfo switch
                    {
                        IFieldSymbol fieldSymbol => fieldSymbol.Type,
                        IPropertySymbol propertySymbol => propertySymbol.Type,
                        _ => null
                    };

                    
                    if(memberType is null) continue;
                    var memberAttributes = memberInfo.GetAttributes()
                        .FirstOrDefault(x => 
                            x.AttributeClass?.ToDisplayString() == typeof(VectorComponentAttribute).FullName);
                    
                    if(memberAttributes is null) continue;
                    if (componentsType == null)
                    {
                        componentsType = memberType;
                    }

                    if (!SymbolEqualityComparer.Default.Equals(componentsType, memberType))
                    {
                        throw new InconsistentVectorComponentTypeException(componentsType.ToDisplayString(), memberType.ToDisplayString());
                    }

                    string value = GetComponentInitialValue(memberInfo, compilation);
                    components.Add((int)memberAttributes.ConstructorArguments[0].Value!, value);
                }

                components = components.OrderBy(x => x.Key).ToDictionary();
                
                VectorComponentsType vectorComponentsType = componentsType?.SpecialType switch
                {
                    SpecialType.System_Int32 => VectorComponentsType.Int,
                    SpecialType.System_UInt32 => VectorComponentsType.Uint,
                    SpecialType.System_Single => VectorComponentsType.Float,
                    _ => throw new VectorComponentTypeNotValidException(
                        componentsType?.ToDisplayString() ?? "<missing>")
                };
                        
                Storage vector = new Vector(symbol.Name, storageKind, slot)
                {
                    ComponentsDefaultValues = [.. components.Select(x => x.Value)],
                    ComponentsType = vectorComponentsType,
                    ComponentsLength = components.Count
                };

                storage = vector;
            }    
        }
        if (storage is null && type is INamedTypeSymbol { TypeKind: TypeKind.Struct } structType)
        {
            var properties = new List<Storage>();

            foreach (ISymbol member in structType.GetMembers())
            {
                if (member.IsStatic || member.IsImplicitlyDeclared)
                {
                    continue;
                }

                if (member is IPropertySymbol { IsIndexer: true })
                {
                    continue;
                }

                if (member is not IFieldSymbol && member is not IPropertySymbol)
                {
                    continue;
                }

                InterfaceSlot? memberSlot = GetAttributeData(member.GetAttributes(), stage);
                properties.Add(GetStorage(member, null, memberSlot, compilation, stage));
            }

            storage = new CustomStruct(symbol.Name, storageKind, null)
            {
                Properties = properties
            };
        }

        return storage ?? throw new NotSupportedException(
            $"Type '{type.ToDisplayString()}' used by '{symbol.ToDisplayString()}' is not supported as shader storage.");
    }

    private static StorageKind GetStorageKind(ISymbol symbol, Compilation compilation)
    {
        ImmutableArray<AttributeData> attributes = symbol.GetAttributes();
        
        var isUniform = IsUniform(attributes, symbol, compilation);
        
        var isStorageBuffer = IsStorageBuffer(attributes, symbol, compilation);

        if (isUniform && isStorageBuffer)
        {
            throw new InvalidOperationException(
                $"Storage '{symbol.ToDisplayString()}' cannot be both a uniform and a storage buffer.");
        }

        if (isUniform)
        {
            return StorageKind.Uniform;
        }

        if (isStorageBuffer)
        {
            return StorageKind.StorageBuffer;
        }

        return StorageKind.Input;
    }

    private static ITypeSymbol? GetSymbolType(ISymbol symbol) => symbol switch
    {
        IParameterSymbol p => p.Type,
        IFieldSymbol f => f.Type,
        IPropertySymbol p => p.Type,
        _ => null
    };
    
    private static bool IsStorageBuffer(ImmutableArray<AttributeData> attributes, ISymbol symbol, Compilation compilation)
    {

        if (GetSymbolType(symbol) is INamedTypeSymbol { IsGenericType: true } type)
        {
            if (IsOpenGeneric(typeof(StructuredBuffer<>), type, compilation) ||
                IsOpenGeneric(typeof(RWStructuredBuffer<>), type, compilation)) return true;
        }
        
        return attributes.Any(attribute =>
            NameAttribute(attribute) == NameAttribute<StorageBufferAttribute>() ||
            NameAttribute(attribute) == NameAttribute<Style.GlslStyle.Resources.buffer>() ||
            NameAttribute(attribute) == NameAttribute<Style.VulkanStyle.Resources.StorageBuffer>() ||
            NameAttribute(attribute) == NameAttribute<Style.WgslStyle.Resources.storage>() ||
            NameAttribute(attribute) == NameAttribute<Style.MetalStyle.Resources.device>());
    }

    private static bool IsOpenGeneric(Type openType, INamedTypeSymbol symbol, Compilation compilation)
        =>  SymbolEqualityComparer.Default.Equals(
            symbol.OriginalDefinition,
               compilation.GetTypeByMetadataName(openType.FullName!));
    
    private static bool IsUniform(ImmutableArray<AttributeData> attributes, ISymbol symbol, Compilation compilation)
    {

        if (GetSymbolType(symbol) is INamedTypeSymbol { IsGenericType: true } type)
        {
            if (IsOpenGeneric(typeof(ConstantBuffer<>), type, compilation)) return true;
        }

        return attributes.Any(attribute => 
            NameAttribute(attribute) == NameAttribute<UniformAttribute>() ||
            NameAttribute(attribute) == NameAttribute<Style.GlslStyle.Resources.uniform>() || 
            NameAttribute(attribute) == NameAttribute<Style.VulkanStyle.Resources.Uniform>() || 
            NameAttribute(attribute) == NameAttribute<Style.WgslStyle.Resources.uniform>() || 
            NameAttribute(attribute) == NameAttribute<Style.MetalStyle.Resources.constant>() || 
            NameAttribute(attribute) == NameAttribute<Style.HlslStyle.Resources.cbuffer>() 
            );
    }
    
    private static string NameAttribute(AttributeData attribute) => attribute.AttributeClass?.ToDisplayString()!;
    private static string NameAttribute<T>() => typeof(T).FullName;

    private static string GetComponentInitialValue(ISymbol member, Compilation compilation)
    {
        if (member is IFieldSymbol { HasConstantValue: true } constantField)
        {
            return FormatConstantValue(constantField.ConstantValue);
        }

        foreach (SyntaxReference syntaxReference in member.DeclaringSyntaxReferences)
        {
            SyntaxNode syntax = syntaxReference.GetSyntax();
            ExpressionSyntax? expression = syntax switch
            {
                VariableDeclaratorSyntax fieldDeclaration => fieldDeclaration.Initializer?.Value,
                PropertyDeclarationSyntax propertyDeclaration => propertyDeclaration.Initializer?.Value,
                _ => null
            };

            if (expression is null)
            {
                continue;
            }

            SemanticModel semanticModel = compilation.GetSemanticModel(expression.SyntaxTree);
            Optional<object?> constant = semanticModel.GetConstantValue(expression);

            if (constant.HasValue)
            {
                return FormatConstantValue(constant.Value);
            }
        }

        return string.Empty;
    }

    private static string FormatConstantValue(object? value)
    {
        return value switch
        {
            null => string.Empty,
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture) ?? string.Empty,
            _ => value.ToString() ?? string.Empty
        };
    }

    private static InterfaceSlot? GetAttributeData(ImmutableArray<AttributeData> atributes, IRStage stage)
    {
        InterfaceSlot? slot = null;
        foreach (var attributeData in atributes.AsSpan())
        {
            var attributeName = attributeData.AttributeClass?.ToDisplayString();

            var locationAtribute = typeof(LocationAttribute).FullName;

            if (attributeName == locationAtribute)
            {
                if (attributeData.ConstructorArguments is [{ Value: int location }])
                {
                    slot = new LocationSlot(location);
                    return slot;
                }
            }
            else
            {

                bool match<T>()
                {
                    return attributeName == typeof(T).FullName;
                }

                BuiltIn? builtIn = null;

                if (match<SV_Position>()
                    || match<position>()
                    || match<gl_Position>()
                    || match<BuiltIn_Position>()
                    || match<builtin_position>())
                {

                    if (match<gl_Position>()
                        || match<BuiltIn_Position>() && stage == Stage.Fragment)
                    {
                        throw new InvalidOperationInStageException(
                            nameof(gl_Position) + " and " + nameof(BuiltIn_Position) 
                            + " cant be part of Fragment or Compute Stage. Please dont use there.");
                    }
                    
                    if (stage == Stage.Vertex)
                    {
                        builtIn = BuiltIn.Position;
                    }
                    else if(stage == Stage.Fragment)
                    {
                        builtIn = BuiltIn.FragCoord;
                    }
                }
                else if (
                    match<SV_VertexID>()
                    || match<vertex_id>()
                    || match<gl_VertexIndex>()
                    || match<BuiltIn_VertexIndex>()
                    || match<builtin_vertex_index>())
                {
                    builtIn = BuiltIn.VertexIndex;
                }

                if (builtIn != null)
                    slot = new BuiltInSlot(builtIn.Value);
            }
        }

        return slot;
    }
}
