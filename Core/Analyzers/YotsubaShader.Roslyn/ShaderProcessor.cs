#region MS-PL License - Copyright (c) 2026 Memphis Yomael Núñez Cruz - Dominican Republic
/*
╔════════════════════════════════════════════════════════════════════════════════════╗
║                                                                                    ║
║  ███╗   ███╗███████╗      ██████╗ ██╗                                              ║
║  ████╗ ████║██╔════╝      ██╔══██╗██║                                              ║
║  ██╔████╔██║███████╗█████╗██████╔╝██║                                              ║
║  ██║╚██╔╝██║╚════██║╚════╝██╔═══╝ ██║                                              ║
║  ██║ ╚═╝ ██║███████║      ██║     ███████╗                                         ║
║  ╚═╝     ╚═╝╚══════╝      ╚═╝     ╚══════╝                                         ║
║                                                                                    ║
╠════════════════════════════════════════════════════════════════════════════════════╣
║  Copyright (c) 2026 Memphis Yomael Núñez Cruz - Dominican Republic                 ║
╠════════════════════════════════════════════════════════════════════════════════════╣
║                                                                                    ║
║  Microsoft Public License (MS-PL)                                                  ║
║                                                                                    ║
║  This license governs use of the accompanying software. If you use the software,   ║
║  you accept this license. If you do not accept the license, do not use the         ║
║  software.                                                                         ║
║                                                                                    ║
║  1. Definitions                                                                    ║
║  The terms "reproduce," "reproduction," "derivative works," and "distribution"     ║
║  have the same meaning here as under U.S. copyright law.                           ║
║  A "contribution" is the original software, to which copyright notices are         ║
║  attached, and any additions or changes to the software.                           ║
║  A "contributor" is any person that distributes its contribution under this        ║
║  license.                                                                          ║
║  "Licensed patents" are a contributor's patent claims that read directly on its    ║
║  contribution.                                                                     ║
║                                                                                    ║
║  2. Grant of Rights                                                                ║
║  (A) Copyright Grant- Subject to the terms of this license, including the license  ║
║  conditions and limitations in section 3, each contributor grants you a non-       ║
║  exclusive, worldwide, royalty-free copyright license to reproduce its             ║
║  contribution, prepare derivative works of its contribution, and distribute its    ║
║  contribution or any derivative works that you create.                             ║
║  (B) Patent Grant- Subject to the terms of this license, including the license     ║
║  conditions and limitations in section 3, each contributor grants you a non-       ║
║  exclusive, worldwide, royalty-free license under its licensed patents to make,    ║
║  have made, use, sell, offer for sale, import, and/or otherwise dispose of its     ║
║  contribution in the software or derivative works of the contribution in the       ║
║  software.                                                                         ║
║                                                                                    ║
║  3. Conditions and Limitations                                                     ║
║  (A) No Trademark License- This license does not grant you rights to use any       ║
║  contributors' name, logo, or trademarks.                                          ║
║  (B) If you bring a patent claim against any contributor over patents that you     ║
║  claim are infringed by the software, your patent license from such contributor    ║
║  to the software ends automatically.                                               ║
║  (C) If you distribute any portion of the software, you must retain all copyright, ║
║  patent, trademark, and attribution notices that are present in the software.      ║
║  (D) If you distribute any portion of the software in source code form, you may do ║
║  so only under this license by including a complete copy of this license with      ║
║  your distribution. If you distribute any portion of the software in compiled or   ║
║  object code form, you may only do so under a license that complies with this      ║
║  license.                                                                          ║
║  (E) The software is licensed "as-is." You bear the risk of using it. The          ║
║  contributors give no express warranties, guarantees or conditions. You may have   ║
║  additional consumer rights under your local laws which this license cannot        ║
║  change. To the extent permitted under your local laws, the contributors exclude   ║
║  the implied warranties of merchantability, fitness for a particular purpose and   ║
║  non-infringement.                                                                 ║
║                                                                                    ║
╚════════════════════════════════════════════════════════════════════════════════════╝
*/
#endregion

using System.Collections.Immutable;
using System.Globalization;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using YotsubaShader.Concepts;
using YotsubaShader.Concepts.BindingSlotsAndResources;
using YotsubaShader.Concepts.Compute;
using YotsubaShader.Concepts.Fragment;
using YotsubaShader.Concepts.Vertex;
using YotsubaShader.IR.Intermediate;
using YotsubaShader.IR.Intermediate.FunctionBody;
using YotsubaShader.IR.Intermediate.FunctionBody.Expressions.AssignmentTargets;
using YotsubaShader.IR.Intermediate.FunctionBody.Expressions.Literals;
using YotsubaShader.IR.Intermediate.FunctionBody.Statements;
using YotsubaShader.IR.Intermediate.Slot;
using YotsubaShader.IR.Intermediate.Slot.Types;
using YotsubaShader.IR.Intermediate.Types;
using YotsubaShader.Language.Exceptions;
using YotsubaShader.Language.Native.Types;
using YotsubaShader.Language.Native.Types.Texture;
using YotsubaShader.Language.Native.Types.Vector;
using YotsubaShader.Language.Native.Types.Vector.Exceptions;
using YotsubaShader.Roslyn.Model;
using YotsubaShader.Roslyn.Sources;
using Double = YotsubaShader.IR.Intermediate.Types.Double;
using Half = YotsubaShader.IR.Intermediate.Types.Half;
using LanguageStage = YotsubaShader.Language.Stage;
using IRStage = YotsubaShader.IR.Intermediate.Stage;
using TextureDepth = YotsubaShader.Language.Native.Types.Texture.TextureDepth;
using TextureFormat = YotsubaShader.Language.Native.Types.Texture.TextureFormat;
using TextureUsage = YotsubaShader.Language.Native.Types.Texture.TextureUsage;
namespace YotsubaShader.Roslyn;

public class ShaderProcessor
{
    
    [ThreadStatic]
    internal static SourceGraph Sources = null!;
        
    [ThreadStatic]
    internal static Dictionary<SymbolKey, FunctionId> functionIds =
        new();

    [ThreadStatic]
    internal static List<FunctionsInfo> PendingFunctionsInfos;
    
    [ThreadStatic]
    private static Dictionary<SymbolKey, string> structTypeNames =
        new();
    
    /// <summary>La compilación que se está traduciendo (con los polyfills de Math).</summary>
    [ThreadStatic] internal static Compilation? Compilation;

    /// <summary>Modo estricto: cualquier error de validación lanza <see cref="ShaderValidationException"/>.</summary>
    public (string, Shader)[] Process(Compilation compilation, CancellationToken token) =>
        Process(compilation, token, failures: null);

    /// <summary>
    /// Con <paramref name="failures"/> no nulo, un shader que no valida o no se puede traducir se registra ahí y se
    /// omite; el resto se compila. Los errores que no pertenecen a ninguna clase de shader siguen lanzando
    /// <see cref="ShaderValidationException"/>.
    /// </summary>
    public (string, Shader)[] Process(Compilation compilation, CancellationToken token, List<ShaderFailure>? failures)
    {
        // Las mismas reglas que el analizador del IDE: un shader con errores YS no llega a traducirse.
        var validation = YotsubaShader.Rules.ShaderValidator.Validate(compilation, token);
        var errors = validation.Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
        var invalid = new HashSet<string>();
        if (errors.Count > 0)
        {
            if (failures is null)
            {
                throw new ShaderValidationException(errors);
            }

            var unattributed = new List<Diagnostic>();
            var byShader = new Dictionary<string, List<Diagnostic>>();
            var declared = ShaderDiscovery.Find(compilation, token);

            foreach (Diagnostic error in errors)
            {
                ShaderInfo? owner = error.Location.SourceTree is { } tree
                    ? declared.FirstOrDefault(info => info.Type.DeclaringSyntaxReferences.Any(reference =>
                        reference.SyntaxTree == tree && reference.Span.Contains(error.Location.SourceSpan)))
                    : null;

                if (owner is null)
                {
                    unattributed.Add(error);
                }
                else if (byShader.TryGetValue(owner.FullName, out var list))
                {
                    list.Add(error);
                }
                else
                {
                    byShader[owner.FullName] = [error];
                }
            }

            if (unattributed.Count > 0)
            {
                throw new ShaderValidationException(unattributed);
            }

            foreach ((string name, List<Diagnostic> diagnostics) in byShader)
            {
                invalid.Add(name);
                failures.Add(new ShaderFailure(name, new ShaderValidationException(diagnostics).Message, diagnostics));
            }
        }

        // Math sin instrucción de GPU: su implementación en C# entra en la compilación como código del shader.
        compilation = MathPolyfillResolver.AddTo(compilation);
        Compilation = compilation;

        Sources = new SourceGraph(compilation,
            new DecompiledSourceProvider(compilation));
        
        var csharpShaderMetadata = ShaderDiscovery.Find(compilation, token);
        var shaders = new List<(string, Shader)>(csharpShaderMetadata.Length);
        
        foreach (ShaderInfo shaderInfo in csharpShaderMetadata)
        {
            if (invalid.Contains(shaderInfo.FullName))
            {
                continue;
            }

            try
            {
                shaders.Add((shaderInfo.FullName, ProcessShader(compilation, shaderInfo)));
            }
            catch (Exception exception) when (failures is not null && exception is not OperationCanceledException)
            {
                failures.Add(new ShaderFailure(shaderInfo.FullName, exception.Message, []));
            }
        }

        return shaders.ToArray();
    }

    private static Shader ProcessShader(Compilation compilation, ShaderInfo shaderInfo)
    {
        functionIds = new Dictionary<SymbolKey, FunctionId>();
        structTypeNames = new Dictionary<SymbolKey, string>();

        var globals = new Dictionary<ISymbol, Storage>(SymbolEqualityComparer.Default);

        var functions = new List<Function>();
        AddResourceInputs(shaderInfo.Type, globals, compilation);

        PendingFunctionsInfos = new List<FunctionsInfo>();
        foreach (var shaderInfoEntryPoint in shaderInfo.Functions)
        {
            ProcessWithChain(compilation, shaderInfoEntryPoint, functions, PendingFunctionsInfos, globals);
        }

        for (int index = 0;
             index < PendingFunctionsInfos.Count;
             index++)
        {
            var functionInfo = PendingFunctionsInfos[index];
            ProcessWithChain(compilation, functionInfo, functions, PendingFunctionsInfos, globals);
        }

        return new Shader(functions, globals.Values.ToArray());
    }

    private static void ProcessShaderFunction(Compilation compilation, FunctionsInfo shaderInfoEntryPoint, List<Function> shaderFunction, List<FunctionsInfo> extraMethods, Dictionary<ISymbol, Storage> globals)
    {
        FunctionId functionId;
        SymbolKey key = SymbolKey.Of(shaderInfoEntryPoint.Method);
        if (shaderInfoEntryPoint.Id is { } assigned)
        {
            functionId = assigned;
        }
        else if (!functionIds.TryGetValue(key, out functionId))
        {
            functionId = new FunctionId((uint)functionIds.Count);
            functionIds[key] = functionId;
        }
        
        var processor = new StatementOperationProcessor(){ Globals = globals, CurrentFunction = shaderInfoEntryPoint};
        IRStage stage = shaderInfoEntryPoint.Stage switch
        {
            LanguageStage.Compute  => IRStage.Compute,
            LanguageStage.Fragment => IRStage.Fragment,
            LanguageStage.Vertex   => IRStage.Vertex,
            _ => IRStage.None
        };

        var inputs = new Dictionary<ISymbol, Storage>(SymbolEqualityComparer.Default);
        var outputs = new Dictionary<ISymbol, Storage>(SymbolEqualityComparer.Default);
        var bodyFunc = new List<Statement>();
                
        //Process Inputs
        var parameterModes = new List<ParameterMode>();

        // Un método de instancia de un struct recibe 'this' por referencia como primer parámetro.
        if (stage == IRStage.None && StatementOperationProcessor.IsStructInstanceMethod(shaderInfoEntryPoint.Method))
        {
            IMethodSymbol method = shaderInfoEntryPoint.Method;
            Storage thisStorage = GetStorage(method.ContainingType, "this", null, StorageKind.Local, compilation, stage);
            parameterModes.Add(StatementOperationProcessor.IsReadOnlyInstance(method) ? ParameterMode.In : ParameterMode.Ref);
            inputs.Add(method.ContainingType, thisStorage);
            processor.ThisStorage = thisStorage;
        }

        foreach (var parameterInfo in shaderInfoEntryPoint.Parameters)
        {
            // Una función auxiliar recibe todos sus parámetros en orden; ref/in/out llegan como punteros.
            if (stage == IRStage.None)
            {
                ParameterMode mode = parameterInfo.RefKind switch
                {
                    RefKind.Ref => ParameterMode.Ref,
                    RefKind.Out => ParameterMode.Out,
                    RefKind.In or RefKind.RefReadOnlyParameter => ParameterMode.In,
                    _ => ParameterMode.Value
                };
                parameterModes.Add(mode);

                Storage helperParameter = parameterInfo.Type is IArrayTypeSymbol
                    ? processor.CreateArrayParameter(parameterInfo.Symbol,
                        shaderInfoEntryPoint.ArrayLengths.TryGetValue(parameterInfo.Symbol.Ordinal, out uint arrayLength) ? arrayLength : null,
                        compilation, stage)
                    : mode == ParameterMode.Value
                        ? ProcessParameter(parameterInfo, compilation, stage)
                        : GetStorage(parameterInfo.Type, parameterInfo.Name, null, StorageKind.Local, compilation, stage);
                inputs.Add(parameterInfo.Symbol, helperParameter);
                continue;
            }

            var param = ProcessParameter(parameterInfo, compilation, stage);
            switch (parameterInfo.RefKind)
            {
                case RefKind.Out:
                    outputs.Add(parameterInfo.Symbol, param);
                    break;
                case RefKind.None:
                case RefKind.In:
                case RefKind.Ref:
                case RefKind.RefReadOnlyParameter:
                default:
                    inputs.Add(parameterInfo.Symbol, param);
                    break;
            }
        }
                

        // Variables capturadas por una función local: parámetros extra por referencia, después de los suyos.
        foreach (CaptureInfo capture in shaderInfoEntryPoint.Captures)
        {
            parameterModes.Add(capture.Written ? ParameterMode.Ref : ParameterMode.In);
            inputs.Add(capture.Symbol, GetStorage(capture.Type, capture.Name, null, StorageKind.Local, compilation, stage));
        }

        bool isConstructor = shaderInfoEntryPoint.Method.MethodKind == MethodKind.Constructor;
        Storage? self = null;
        
        if (shaderInfoEntryPoint.ReturnType.SpecialType != SpecialType.System_Void)
        {
            Storage? returnType = ProcessReturn(shaderInfoEntryPoint, compilation, stage);
            if (returnType is not null) outputs.Add(shaderInfoEntryPoint.ReturnType, returnType);
        }
        
        if (isConstructor)
        {
            self = GetStorage(shaderInfoEntryPoint.ReturnType, "this", null, StorageKind.Local, compilation, stage);
            processor.ThisStorage = self;
            Expression? zero = self is CustomStruct cs ? new CustomStructExpression(cs, []) : null;
            bodyFunc.Add(new VariableDeclaration(self, zero));        }
                
        var syntax = shaderInfoEntryPoint.Method.DeclaringSyntaxReferences[0].GetSyntax();
        var semanticModel = Sources.GetSemanticModel(syntax.SyntaxTree);

        var operations = semanticModel.GetOperation(syntax);

        
        if (operations is ILocalFunctionOperation localFunction)
        {
            IBlockOperation localBody = localFunction.Body ?? localFunction.IgnoredBody
                ?? throw new NotSupportedException($"La función local '{localFunction.Symbol.Name}' no tiene cuerpo.");
            foreach (IOperation op in localBody.Operations)
            {
                ProcessStatement(op, inputs, outputs, processor, compilation, stage, out var statement);
                if (statement is not null) bodyFunc.Add(statement);
            }
        }
        else if (operations is IMethodBodyBaseOperation body)
        {
            
            if (body is IConstructorBodyOperation { Initializer: not null })
                throw new NotSupportedException(
                    $"El constructor '{shaderInfoEntryPoint.Method.ToDisplayString()}' usa ': this(...)' o ': base(...)', que aún no se soporta.");
            
            var blockOperations = body.BlockBody ?? body.ExpressionBody ?? throw new NotImplementedException();
            var span = blockOperations.Operations.AsSpan();
            foreach (var op in span)
            {
                ProcessStatement(op, inputs, outputs, processor, compilation, stage, out var statement);
                if (statement is null) continue;
                bodyFunc.Add(statement);
            }
            
            if (isConstructor)
                bodyFunc.Add(new Return(new StorageReference(self!)));
        }

        FunctionExtraParams? extraParams = GetStageExtraParams(shaderInfoEntryPoint.Method, stage);

       
        Function fn = new Function(functionId, shaderInfoEntryPoint.Name, stage, [.. inputs.Values], [.. outputs.Values], bodyFunc, extraParams,
            parameterModes.Any(m => m != ParameterMode.Value) ? parameterModes : null);
        shaderFunction.Add(fn);
    }

    private static FunctionExtraParams? GetStageExtraParams(IMethodSymbol method, IRStage stage)
    {
        if (stage != Stage.Compute) return null;

        AttributeData? attribute = method.GetAttributes()
            .FirstOrDefault(HasConcept<WorkgroupSizeMethodAttribute>);

        if (attribute is null)
        {
            throw new NotSupportedException(
                $"El compute shader '{method.Name}' requiere [numthreads(x, y, z)].");
        }

        if (attribute.ConstructorArguments is [{ Value: uint x and > 0 }, { Value: uint y and > 0 }, { Value: uint z and > 0 }])
        {
            return new ComputeParams(x, y, z);
        }
        
        throw new NotSupportedException(
            $"[numthreads] en '{method.Name}' debe contener tres valores positivos.");
    }

    private static void ProcessStatement(
        IOperation op,
        Dictionary<ISymbol, Storage> inputs,
        Dictionary<ISymbol, Storage> outputs,
        StatementOperationProcessor processor,
        Compilation compilation,
        Stage stage,
        out Statement? statement)
    {
        processor.ProcessStatement(op, inputs, outputs, compilation, stage, out statement);
    }

    private static Storage ProcessParameter(ParameterInfo parameterInfo, Compilation compilation, IRStage stage)
    {
        InterfaceSlot? slot
             = GetAttributeData(parameterInfo.Symbol.GetAttributes(), stage);

        if (parameterInfo.RefKind == RefKind.Out)
        {
            return GetStorage(parameterInfo.Type,
                parameterInfo.Name,
                slot,
                StorageKind.Output,
                compilation, stage);    
        }

        return GetStorage(parameterInfo.Symbol, null, slot, compilation, stage);
    }

    /// <summary>
    /// El valor por defecto de un parámetro con su tipo real. Roslyn lo entrega ya con el tipo del parámetro
    /// (un <c>uint</c> llega como <c>uint</c>), así que no se reinterpreta a través de otro tipo.
    /// </summary>
    private static T? DefaultValueOf<T>(IParameterSymbol? parameter) where T : struct
    {
        if (parameter is not { HasExplicitDefaultValue: true })
        {
            return null;
        }

        return parameter.ExplicitDefaultValue is T value
            ? value
            : throw new NotSupportedException(
                $"The default value of parameter '{parameter.Name}' " +
                $"({parameter.ExplicitDefaultValue ?? "null"}) is not a valid {parameter.Type.ToDisplayString()}.");
    }

    internal static Storage GetStorage(ISymbol symbol, Storage? storage, InterfaceSlot? slot, Compilation compilation, IRStage stage, bool isLocal = false)
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

        // T[] en un struct o en la interfaz: la longitud la da [ArrayLength(n)]; sin ella, es un array sin tamaño
        // (solo válido como último miembro de un storage buffer).
        if (type is IArrayTypeSymbol { Rank: 1 } arrayType)
        {
            Storage element = GetStorage(arrayType.ElementType, "element", null, StorageKind.Local, compilation, stage);
            AttributeData? lengthAttribute = symbol.GetAttributes().FirstOrDefault(a =>
                a.AttributeClass?.ToDisplayString() == "YotsubaShader.Language.ArrayLengthAttribute");
            uint? length = lengthAttribute?.ConstructorArguments is [{ Value: int declared }] && declared > 0 ? (uint)declared : null;
            return new IR.Intermediate.Types.Array(symbol.Name, isLocal ? StorageKind.Local : GetStorageKind(symbol, compilation), slot)
            {
                ElementType = element,
                Length = length
            };
        }

        var parameter = symbol as IParameterSymbol;
        StorageKind storageKind;

        if (isLocal) storageKind = StorageKind.Local;
        else storageKind = GetStorageKind(symbol, compilation);
        
        switch (SpecialOf(type))
        {
            case SpecialType.System_Int16:
                storage = new Int(symbol.Name, storageKind, slot) { Bits = 16, Signed = true, InitialValue = null };
                break;
            case SpecialType.System_UInt16:
                storage = new Int(symbol.Name, storageKind, slot) { Bits = 16, Signed = false, InitialValue = null };
                break;
            case SpecialType.System_Int32:
                storage = new Int(symbol.Name, storageKind, slot)
                {
                    Bits = 32,
                    Signed = true,
                    InitialValue = DefaultValueOf<int>(parameter) is { } defaultInt
                        ? new IntLiteral(defaultInt.ToString(CultureInfo.InvariantCulture), true)
                        : null
                };
                break;
            case SpecialType.System_UInt32:
                storage = new Int(symbol.Name, storageKind, slot)
                {
                    Bits = 32,
                    Signed = false,
                    InitialValue = DefaultValueOf<uint>(parameter) is { } defaultUInt
                        ? new IntLiteral(defaultUInt.ToString(CultureInfo.InvariantCulture), false)
                        : null
                };
                break;
            case HalfSpecial:
                storage = new Half(symbol.Name, storageKind, slot) { InitialValue = null };
                break;
            case SpecialType.System_Double:
                storage = new Double(symbol.Name, storageKind, slot)
                {
                    InitialValue = DefaultValueOf<double>(parameter) is { } defaultDouble
                        ? new DoubleLiteral(defaultDouble)
                        : null
                };
                break;
            case SpecialType.System_Single:
                storage = new Float(symbol.Name, storageKind, slot)
                {
                    InitialValue = DefaultValueOf<float>(parameter) is { } defaultFloat
                        ? new FloatLiteral(defaultFloat)
                        : null
                };
                break;
            case SpecialType.System_Boolean:
                storage = new Bool(symbol.Name, storageKind, slot)
                {
                    InitialValue = DefaultValueOf<bool>(parameter) is { } defaultBool
                        ? new BoolLiteral(defaultBool)
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


                    AddVectorComponent(components, type, memberInfo, (int)memberAttributes.ConstructorArguments[0].Value!, value);
                }

                ValidateVectorComponents(components, type);

                VectorComponentsType vectorComponentsType = (componentsType is null ? (SpecialType?)null : SpecialOf(componentsType)) switch
                {
                    SpecialType.System_Int32 => VectorComponentsType.Int,
                    SpecialType.System_UInt32 => VectorComponentsType.Uint,
                    SpecialType.System_Single => VectorComponentsType.Float,
                    SpecialType.System_Double => VectorComponentsType.Double,
                    SpecialType.System_Boolean => VectorComponentsType.Bool,
                    HalfSpecial => VectorComponentsType.Half,
                    _ => throw new VectorComponentTypeNotValidException(
                        componentsType?.ToDisplayString() ?? "<missing>")
                };
                
                components = components.OrderBy(x => x.Key).ToDictionary();

                Expression ComponentValueToExpression(string value)
                {
                    Expression? exp = null;

                    if (!string.IsNullOrWhiteSpace(value))
                        return vectorComponentsType switch
                        {
                            VectorComponentsType.Float =>
                                new FloatLiteral(float.Parse(value, CultureInfo.InvariantCulture)),
                            VectorComponentsType.Int =>
                                new IntLiteral(value, true),
                            VectorComponentsType.Uint =>
                                new IntLiteral(value, false),
                            VectorComponentsType.Double =>
                                new DoubleLiteral(double.Parse(value, CultureInfo.InvariantCulture)),
                            VectorComponentsType.Half =>
                                new HalfLiteral(float.Parse(value, CultureInfo.InvariantCulture)),
                            VectorComponentsType.Bool =>
                                new BoolLiteral(bool.Parse(value)),
                            _ => throw new NotSupportedException(
                                $"Unsupported vector component type '{vectorComponentsType}'.")
                        };

                    return vectorComponentsType switch
                    {
                        VectorComponentsType.Float => new FloatLiteral(0f),
                        VectorComponentsType.Int => new IntLiteral("0", true),
                        VectorComponentsType.Uint => new IntLiteral("0", false),
                        VectorComponentsType.Double => new DoubleLiteral(0d),
                        VectorComponentsType.Half => new HalfLiteral(0f),
                        VectorComponentsType.Bool => new BoolLiteral(false),
                        _ => throw new NotSupportedException(
                            $"Unsupported vector component type '{vectorComponentsType}'.")
                    };
                }

                Storage vector = new Vector(symbol.Name, storageKind, slot)
                {
                    ComponentsDefaultValues = [.. components.Select(x => ComponentValueToExpression(x.Value))],
                    ComponentsType = vectorComponentsType,
                    ComponentsLength = components.Count,
                    ComponentNames = GetVectorComponentNames(type)
                };

                storage = vector;
            }    

            storage ??= TryCreateMatrix(type, symbol.Name, storageKind, slot);
        }

        if (!isLocal)
        {
            if (storage is null && storageKind == StorageKind.Sampler)
                storage = new Sampler(symbol.Name, storageKind, slot);

            if (storage is null && storageKind == StorageKind.Texture)
            {
                string textureAttributeFullName = typeof(TextureAttribute).FullName!;

                var targetType = type is INamedTypeSymbol { IsGenericType: true } named
                    ? named.OriginalDefinition
                    : type;

                var textureAttr = targetType?.GetAttributes().FirstOrDefault(a =>
                                      a.AttributeClass?.ToDisplayString() == textureAttributeFullName)
                                  ?? symbol.GetAttributes().FirstOrDefault(a => a.AttributeClass?.ToDisplayString()
                                      == textureAttributeFullName);

                if (textureAttr is not null)
                {

                    //dimension
                    var dimValue = (int)textureAttr.ConstructorArguments[0].Value!;
                    var dimension = (TextureDimensions)dimValue;

                    //component type
                    var compTypeValue = (int)textureAttr.ConstructorArguments[1].Value!;
                    var compType = (IR.Intermediate.Types.TextureComponentTypes)compTypeValue;

                    if (compType == IR.Intermediate.Types.TextureComponentTypes.GenericDefine &&
                        type is INamedTypeSymbol
                        {
                            TypeArguments: [var typeArg, ..]
                        })
                    {
                        compType = typeArg.SpecialType switch
                        {
                            SpecialType.System_Int32 => IR.Intermediate.Types.TextureComponentTypes.Int,
                            SpecialType.System_UInt32 => IR.Intermediate.Types.TextureComponentTypes.Uint,
                            _ => IR.Intermediate.Types.TextureComponentTypes.Float
                        };
                    }

                    bool arrayed = textureAttr.NamedArguments.FirstOrDefault(x =>
                        x.Key == nameof(TextureAttribute.Arrayed)).Value.Value as bool? ?? false;

                    bool multisampled = textureAttr.NamedArguments
                        .FirstOrDefault(x =>
                            x.Key == nameof(TextureAttribute.Multisampled)).Value.Value as bool? ?? false;

                    var depth = textureAttr.NamedArguments.FirstOrDefault(x => x.Key == nameof(TextureAttribute.Depth))
                        .Value.Value is int d
                        ? (TextureDepth)d
                        : TextureDepth.No;

                    IR.Intermediate.Types.TextureDepth irDepth = (IR.Intermediate.Types.TextureDepth)depth;

                    var usage = textureAttr.NamedArguments.FirstOrDefault(x => x.Key == nameof(TextureAttribute.Usage))
                        .Value.Value is int u
                        ? (TextureUsage)u
                        : TextureUsage.Sampled;

                    IR.Intermediate.Types.TextureUsage irUsage = (IR.Intermediate.Types.TextureUsage)usage;

                    var format =
                        textureAttr.NamedArguments.FirstOrDefault(x => x.Key == nameof(TextureAttribute.Format)).Value
                            .Value is int f
                            ? (TextureFormat)f
                            : TextureFormat.Unknown;

                    IR.Intermediate.Types.TextureFormat irFormat = (IR.Intermediate.Types.TextureFormat)format;
                    storage = new Texture(symbol.Name, storageKind, slot)
                    {
                        Dimensions = dimension,
                        TextureComponentsType = compType,
                        Arrayed = arrayed,
                        Multisampled = multisampled,
                        Depth = irDepth,
                        Usage = irUsage,
                        Format = irFormat,
                        CombinedSampler = HasSamplerAttribute(type)
                    };
                }
            }

        }
        
        if (storage is null && type is INamedTypeSymbol { TypeKind: TypeKind.Struct } structType)
            storage = ProcessStruct(symbol, slot, compilation, stage, isLocal, structType, storageKind);
        

        if (storage is null && type is INamedTypeSymbol { TypeKind: TypeKind.Class } classType)
            storage = ProcessClass(symbol, slot, compilation, stage, isLocal, classType, storageKind);
        

        if (storage is null && type is INamedTypeSymbol { TypeKind: TypeKind.Enum } enumType)
            storage = ProcessEnum(symbol, slot, compilation, stage, isLocal, enumType, storageKind);
        
        if (storage is null && type is INamedTypeSymbol { TypeKind: TypeKind.Interface } interfaceType)
            storage = ProcessInterface(symbol, slot, compilation, stage, isLocal, interfaceType, storageKind);
        
        return storage ?? throw new NotSupportedException(
            $"Type '{type.ToDisplayString()}' used by '{symbol.ToDisplayString()}' is not supported as shader storage.");
    }

    private static Storage? ProcessInterface(ISymbol symbol, InterfaceSlot? slot, Compilation compilation, IRStage stage, bool isLocal, INamedTypeSymbol interfaceType, StorageKind storageKind)
    {
        throw new NotImplementedException("Interfaces is not supported yet");
    }

    private static Storage? ProcessEnum(ISymbol symbol, InterfaceSlot? slot, Compilation compilation, IRStage stage, bool isLocal, INamedTypeSymbol enumType, StorageKind storageKind)
    {
        throw new NotImplementedException("Enum is not supported yet");
    }

    /// <summary>
    /// Los structs y arrays dentro de un buffer llevan el layout del buffer (Offset, ArrayStride): en SPIR-V son
    /// tipos distintos de los mismos structs usados como variables locales, que no pueden tener layout.
    /// </summary>
    private static Storage WithLayoutKind(Storage storage, StorageKind kind) => storage switch
    {
        CustomStruct structure => structure with
        {
            Kind = kind,
            Properties = structure.Properties.Select(p => WithLayoutKind(p, kind)).ToList()
        },
        IR.Intermediate.Types.Array array => array with { Kind = kind, ElementType = WithLayoutKind(array.ElementType, kind) },
        _ => storage
    };

    /// <summary>
    /// Los campos de un struct en orden. Una tupla (ValueTuple) solo tiene Item1..ItemN: sus nombres con alias
    /// (t.Seno) y sus miembros de interfaz no son datos.
    /// </summary>
    internal static IEnumerable<ISymbol> StructMembers(INamedTypeSymbol structType) =>
        structType.IsTupleType
            ? structType.TupleElements.Select(element => (ISymbol)(element.CorrespondingTupleField ?? element))
            : structType.GetMembers();

    private static Storage? ProcessClass(ISymbol symbol, InterfaceSlot? slot, Compilation compilation, IRStage stage, bool isLocal, INamedTypeSymbol classType, StorageKind storageKind)
    {
        throw new NotImplementedException("Class is not supported yet");
    }

    internal static Storage ProcessStruct(ISymbol symbol, InterfaceSlot? slot, Compilation compilation, IRStage stage,
        bool isLocal, INamedTypeSymbol structType, StorageKind storageKind)
    {
        Storage storage;
        var properties = new List<Storage>();

        foreach (ISymbol member in StructMembers(structType))
        {
            if (member.IsStatic || (member.IsImplicitlyDeclared && !structType.IsTupleType))
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

            InterfaceSlot? memberSlot;
            if (isLocal) memberSlot = null;
            else memberSlot = GetAttributeData(member.GetAttributes(), stage);
                
            properties.Add(GetStorage(member, null, memberSlot, compilation, stage, isLocal));
        }
        
        string typeName = structType.OriginalDefinition.Name;
        if (structType.IsGenericType)
        {
            string genericArgs = string.Join("_",
                structType.TypeArguments.Select(t => t.Name));

            typeName = $"{typeName}_{genericArgs}";
        }

        typeName = UniqueStructTypeName(structType, typeName);

        storage = new CustomStruct(symbol.Name, storageKind, slot)
        {
            Properties = properties,
            TypeName = typeName
        };
        return storage;
    }

    /// <summary>Los nombres de los componentes de un vector, ordenados por el índice de [VectorComponent].</summary>
    internal static IReadOnlyList<string> GetVectorComponentNames(ITypeSymbol vectorType)
    {
        var names = new SortedDictionary<int, string>();

        foreach (ISymbol member in vectorType.GetMembers())
        {
            if (member.IsStatic || member.IsImplicitlyDeclared) continue;
            if (member is not (IFieldSymbol or IPropertySymbol { IsIndexer: false })) continue;

            AttributeData? attribute = member.GetAttributes().FirstOrDefault(x =>
                x.AttributeClass?.ToDisplayString() == typeof(VectorComponentAttribute).FullName);

            if (attribute is not null)
            {
                names[(int)attribute.ConstructorArguments[0].Value!] = member.Name;
            }
        }

        return [.. names.Values];
    }

    /// <summary>Registra un componente de vector; el índice no puede ser negativo ni repetirse.</summary>
    internal static void AddVectorComponent(
        Dictionary<int, string> components, ITypeSymbol vectorType, ISymbol member, int index, string value)
    {
        if (index < 0)
            throw new InvalidVectorComponentIndexException(vectorType.Name, member.Name, index,
                "el índice no puede ser negativo");

        if (!components.TryAdd(index, value))
            throw new InvalidVectorComponentIndexException(vectorType.Name, member.Name, index,
                "el índice ya lo usa otro componente");
    }

    /// <summary>Un vector tiene entre 2 y 4 componentes con índices contiguos desde 0.</summary>
    internal static void ValidateVectorComponents(Dictionary<int, string> components, ITypeSymbol vectorType)
    {
        if (components.Count is < 2 or > 4)
            throw new InvalidVectorComponentIndexException(vectorType.Name, null, null,
                $"un vector necesita entre 2 y 4 componentes y tiene {components.Count}");

        for (int i = 0; i < components.Count; i++)
        {
            if (!components.ContainsKey(i))
                throw new InvalidVectorComponentIndexException(vectorType.Name, null, i,
                    $"falta el componente {i}: los índices deben ser contiguos desde 0");
        }
    }
    
    private static Storage? ProcessReturn(
        FunctionsInfo functions,
        Compilation compilation,
        IRStage stage)
    {
        IMethodSymbol method = functions.Method;
        
        if (functions.ReturnType.SpecialType == SpecialType.System_Void)
            return null;

        InterfaceSlot? returnSlot =
            GetAttributeData(method.GetReturnTypeAttributes(), stage);

        return GetStorage(
            functions.ReturnType,
            "__return",
            returnSlot,
            StorageKind.Output,
            compilation,
            stage);
    }
    
    internal static Storage GetStorage(
        ITypeSymbol symbol,
        string name,
        InterfaceSlot? slot,
        StorageKind kind,
        Compilation compilation,
        IRStage stage)
    {

        Storage? storage = null;
        StorageKind storageKind = kind;

        string symbolName = name;
        switch (SpecialOf(symbol))
        {
            case SpecialType.System_Int16:
                storage = new Int(symbolName, storageKind, slot) { Bits = 16, Signed = true, InitialValue = null };
                break;
            case SpecialType.System_UInt16:
                storage = new Int(symbolName, storageKind, slot) { Bits = 16, Signed = false, InitialValue = null };
                break;
            case SpecialType.System_Int32:
                storage = new Int(symbolName, storageKind, slot)
                {
                    Bits = 32,
                    Signed = true,
                    InitialValue = null
                };
                break;
            case SpecialType.System_UInt32:
                storage = new Int(symbolName, storageKind, slot)
                {
                    Bits = 32,
                    Signed = false,
                    InitialValue = null
                };
                break;
            case HalfSpecial:
                storage = new Half(symbolName, storageKind, slot) { InitialValue = null };
                break;
            case SpecialType.System_Double:
                storage = new Double(symbolName, storageKind, slot)
                {
                    InitialValue = null
                };
                break;
            case SpecialType.System_Single:
                storage = new Float(symbolName, storageKind, slot)
                {
                    InitialValue = null
                };
                break;
            case SpecialType.System_Boolean:
                storage = new Bool(symbolName, storageKind, slot)
                {
                    InitialValue = null
                };
                break;
        }

        if (storage is null)
        {
            var attributes = symbol.GetAttributes();

            var attribute =
                attributes
                    .FirstOrDefault(x => x.AttributeClass?.ToDisplayString() == typeof(VectorAttribute).FullName);
            if (attribute is not null)
            {
                var members = symbol.GetMembers();
                
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
                    AddVectorComponent(components, symbol, memberInfo, (int)memberAttributes.ConstructorArguments[0].Value!, value);
                }

                ValidateVectorComponents(components, symbol);

                components = components.OrderBy(x => x.Key).ToDictionary();
                
                VectorComponentsType vectorComponentsType = (componentsType is null ? (SpecialType?)null : SpecialOf(componentsType)) switch
                {
                    SpecialType.System_Int32 => VectorComponentsType.Int,
                    SpecialType.System_UInt32 => VectorComponentsType.Uint,
                    SpecialType.System_Single => VectorComponentsType.Float,
                    SpecialType.System_Double => VectorComponentsType.Double,
                    SpecialType.System_Boolean => VectorComponentsType.Bool,
                    HalfSpecial => VectorComponentsType.Half,
                    _ => throw new VectorComponentTypeNotValidException(
                        componentsType?.ToDisplayString() ?? "<missing>")
                };
                        
                Expression ComponentValueToExpression(string value)
                {
                    Expression? exp = null;

                    if (!string.IsNullOrWhiteSpace(value))
                        return vectorComponentsType switch
                        {
                            VectorComponentsType.Float =>
                                new FloatLiteral(float.Parse(value, CultureInfo.InvariantCulture)),
                            VectorComponentsType.Int =>
                                new IntLiteral(value, true),
                            VectorComponentsType.Uint =>
                                new IntLiteral(value, false),
                            VectorComponentsType.Double =>
                                new DoubleLiteral(double.Parse(value, CultureInfo.InvariantCulture)),
                            VectorComponentsType.Half =>
                                new HalfLiteral(float.Parse(value, CultureInfo.InvariantCulture)),
                            VectorComponentsType.Bool =>
                                new BoolLiteral(bool.Parse(value)),
                            _ => throw new NotSupportedException(
                                $"Unsupported vector component type '{vectorComponentsType}'.")
                        };

                    return vectorComponentsType switch
                    {
                        VectorComponentsType.Float => new FloatLiteral(0f),
                        VectorComponentsType.Int => new IntLiteral("0", true),
                        VectorComponentsType.Uint => new IntLiteral("0", false),
                        VectorComponentsType.Double => new DoubleLiteral(0d),
                        VectorComponentsType.Half => new HalfLiteral(0f),
                        VectorComponentsType.Bool => new BoolLiteral(false),
                        _ => throw new NotSupportedException(
                            $"Unsupported vector component type '{vectorComponentsType}'.")
                    };
                }
                
                Storage vector = new Vector(symbolName, storageKind, slot)
                {
                    ComponentsDefaultValues = [.. components.Select(x => ComponentValueToExpression(x.Value))],
                    ComponentsType = vectorComponentsType,
                    ComponentsLength = components.Count,
                    ComponentNames = GetVectorComponentNames(symbol)
                };

                storage = vector;
            }    

            storage ??= TryCreateMatrix(symbol, symbolName, storageKind, slot);
        }

        if (storage is null && storageKind == StorageKind.Sampler)
            storage = new Sampler(symbolName, storageKind, slot);

        if (storage is null && storageKind == StorageKind.Texture)
        {
            string textureAttributeFullName = typeof(TextureAttribute).FullName!;

            var targetType = symbol is INamedTypeSymbol { IsGenericType: true } named
                ? named.OriginalDefinition
                : symbol;

            var textureAttr = targetType?.GetAttributes().FirstOrDefault(a =>
                                  a.AttributeClass?.ToDisplayString() == textureAttributeFullName)
                              ?? symbol.GetAttributes().FirstOrDefault(a => a.AttributeClass?.ToDisplayString()
                                                                            == textureAttributeFullName);

            if (textureAttr is not null)
            {
                
                //dimension
                var dimValue = (int)textureAttr.ConstructorArguments[0].Value!;
                var dimension = (TextureDimensions)dimValue;
                
                //component type
                var compTypeValue = (int)textureAttr.ConstructorArguments[1].Value!;
                var compType = (IR.Intermediate.Types.TextureComponentTypes)compTypeValue;

                if (compType == IR.Intermediate.Types.TextureComponentTypes.GenericDefine && symbol is INamedTypeSymbol
                    {
                        TypeArguments: [var typeArg, ..]
                    })
                {
                    compType = typeArg.SpecialType switch
                    {
                        SpecialType.System_Int32 => IR.Intermediate.Types.TextureComponentTypes.Int,
                        SpecialType.System_UInt32 => IR.Intermediate.Types.TextureComponentTypes.Uint,
                        _ => IR.Intermediate.Types.TextureComponentTypes.Float
                    };
                }

                bool arrayed = textureAttr.NamedArguments.FirstOrDefault(x =>
                    x.Key == nameof(TextureAttribute.Arrayed)).Value.Value as bool? ?? false;
                   
                bool multisampled = textureAttr.NamedArguments
                    .FirstOrDefault(x => 
                        x.Key == nameof(TextureAttribute.Multisampled)).Value.Value as bool? ?? false;
                
                var depth = textureAttr.NamedArguments.FirstOrDefault(x => x.Key == nameof(TextureAttribute.Depth)).Value.Value is int d 
                    ? (TextureDepth)d 
                    : TextureDepth.No;
                
                IR.Intermediate.Types.TextureDepth irDepth = (IR.Intermediate.Types.TextureDepth)depth;
                
                var usage = textureAttr.NamedArguments.FirstOrDefault(x => x.Key == nameof(TextureAttribute.Usage)).Value.Value is int u 
                    ? (TextureUsage)u 
                    : TextureUsage.Sampled;

                IR.Intermediate.Types.TextureUsage irUsage = (IR.Intermediate.Types.TextureUsage)usage;
                
                var format = textureAttr.NamedArguments.FirstOrDefault(x => x.Key == nameof(TextureAttribute.Format)).Value.Value is int f 
                    ? (TextureFormat)f 
                    : TextureFormat.Unknown;

                IR.Intermediate.Types.TextureFormat irFormat = (IR.Intermediate.Types.TextureFormat)format;
                storage = new Texture(symbolName, storageKind, slot)
                {
                    Dimensions = dimension,
                    TextureComponentsType = compType,
                    Arrayed = arrayed,
                    Multisampled = multisampled,
                    Depth = irDepth,
                    Usage = irUsage,
                    Format = irFormat,
                    CombinedSampler = HasSamplerAttribute(symbol)
                };
            }
        }
        
        if (storage is null && symbol is INamedTypeSymbol { TypeKind: TypeKind.Struct } structType)
        {
            var properties = new List<Storage>();

            foreach (ISymbol member in StructMembers(structType))
            {
                if (member.IsStatic || (member.IsImplicitlyDeclared && !structType.IsTupleType))
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
                ITypeSymbol memberType = member switch
                {
                    IFieldSymbol field => field.Type,
                    IPropertySymbol property => property.Type,
                    _ => throw new NotSupportedException()
                };
                properties.Add(GetStorage(memberType, member.Name, memberSlot, storageKind, compilation, stage));
            }

            string typeName = structType.OriginalDefinition.Name;
            if (structType.IsGenericType)
            {
                string genericArgs = string.Join("_",
                    structType.TypeArguments.Select(t => t.Name));

                typeName = $"{typeName}_{genericArgs}";
            }

            typeName = UniqueStructTypeName(structType, typeName);
            
            storage = new CustomStruct(symbolName, storageKind, slot)
            {
                Properties = properties,
                TypeName = typeName
            };
        }

        return storage ?? throw new NotSupportedException(
            $"Type '{symbol.ToDisplayString()}' used by '{symbol.ToDisplayString()}' is not supported as shader storage.");
        // Aquí va la lógica que actualmente usa:
        // type.SpecialType
        // VectorAttribute
        // TextureAttribute
        // CustomStruct
    }

    private static StorageKind GetStorageKind(ISymbol symbol, Compilation compilation)
    {
        ImmutableArray<AttributeData> attributes = symbol.GetAttributes();
        
        var isUniform = IsUniform(attributes, symbol, compilation);
        
        var isStorageBuffer = IsStorageBuffer(attributes, symbol, compilation);

        var isTexture = IsTexture(attributes, symbol, compilation);
        
        var isSampler = IsSampler(attributes, symbol, compilation);

        if (isTexture) return StorageKind.Texture;

        if (isSampler) return StorageKind.Sampler;
        
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

    private static bool IsSampler(ImmutableArray<AttributeData> attributes, ISymbol symbol, Compilation compilation)
    {
        ITypeSymbol? type = GetSymbolType(symbol);
        if (type is null) return false;

        string textureAttrName = typeof(SamplerAttribute).FullName!;

        var targetType = type is INamedTypeSymbol { IsGenericType: true } 
            named ? named.OriginalDefinition : type;

        if (targetType.GetAttributes().Any(a => a.AttributeClass?.ToDisplayString() ==
                                                textureAttrName))
        {
            return true;
        }

        return attributes.Any(a => a.AttributeClass?.ToDisplayString() == textureAttrName);
    }

    private static bool IsTexture(ImmutableArray<AttributeData> attributes, ISymbol symbol, Compilation compilation)
    {
        ITypeSymbol? type = GetSymbolType(symbol);
        if (type is null) return false;

        string textureAttrName = typeof(TextureAttribute).FullName!;

        var targetType = type is INamedTypeSymbol { IsGenericType: true } 
            named ? named.OriginalDefinition : type;

        if (targetType.GetAttributes().Any(a => a.AttributeClass?.ToDisplayString() ==
                                                textureAttrName))
        {
            return true;
        }

        return attributes.Any(a => a.AttributeClass?.ToDisplayString() == textureAttrName);
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
        if (GetSymbolType(symbol) is INamedTypeSymbol { IsGenericType: true } type && 
            (TypeHasConcept<RWStorageBuffer>(type) || TypeHasConcept<ReadOnlyStorageBuffer>(type)))
        {
            return true;
        }

        return attributes.Any(attribute =>
            HasConcept<RWStorageBuffer>(attribute) ||
            HasConcept<ReadOnlyStorageBuffer>(attribute));
    }

    /// <summary>
    /// ImmutableArray/ImmutableList, StructuredBuffer&lt;T&gt;, <c>device(readOnly: true)</c>, <c>storage(read)</c>...: el mismo
    /// criterio que la regla YS0308, que impide escribir en ellos.
    /// </summary>
    private static bool IsReadOnlyBuffer(IFieldSymbol field) => YotsubaShader.Rules.ResourceRules.IsReadOnlyBuffer(field);

    private static bool IsUniform(ImmutableArray<AttributeData> attributes, ISymbol symbol, Compilation compilation)
    {

        if (GetSymbolType(symbol) is INamedTypeSymbol { IsGenericType: true } type && TypeHasConcept<UniformBuffer>(type))
        {
            return true;
        }

        return attributes.Any(attribute => 
            HasConcept<UniformBuffer>(attribute)
            );
    }

    /// <summary>
    /// Un struct [Matrix] cuyas columnas son vectores [Vector] de float o double se convierte en <see cref="Matrix"/>.
    /// Las matrices de bool, int o uint no existen en SPIR-V (OpTypeMatrix solo admite floats): devuelven null
    /// y se tratan como un struct normal de vectores.
    /// </summary>
    internal static Matrix? TryCreateMatrix(ITypeSymbol type, string name, StorageKind kind, InterfaceSlot? slot)
    {
        string matrixAttribute = typeof(YotsubaShader.Language.Native.Types.Matrix.MatrixAttribute).FullName!;
        if (!type.GetAttributes().Any(a => a.AttributeClass?.ToDisplayString() == matrixAttribute))
        {
            return null;
        }

        string columnAttribute = typeof(YotsubaShader.Language.Native.Types.Matrix.MatrixColumnAttribute).FullName!;
        var columns = new SortedDictionary<int, ITypeSymbol>();
        var columnNames = new SortedDictionary<int, string>();

        foreach (ISymbol member in type.GetMembers())
        {
            if (member.IsStatic || member.IsImplicitlyDeclared) continue;

            ITypeSymbol? memberType = member switch
            {
                IFieldSymbol field => field.Type,
                IPropertySymbol { IsIndexer: false } property => property.Type,
                _ => null
            };
            if (memberType is null) continue;

            AttributeData? attribute = member.GetAttributes()
                .FirstOrDefault(a => a.AttributeClass?.ToDisplayString() == columnAttribute);
            if (attribute is null) continue;

            int index = (int)attribute.ConstructorArguments[0].Value!;
            if (!columns.TryAdd(index, memberType))
            {
                throw new InvalidOperationException(
                    $"The matrix '{type.ToDisplayString()}' declares the column {index} more than once.");
            }
            columnNames[index] = member.Name;
        }

        if (columns.Count == 0) return null;

        for (int i = 0; i < columns.Count; i++)
        {
            if (!columns.ContainsKey(i))
            {
                throw new InvalidOperationException(
                    $"The matrix '{type.ToDisplayString()}' is missing the column {i}: " +
                    "the [MatrixColumn] indices must be contiguous from 0.");
            }
        }

        VectorComponentsType? componentsType = null;
        int rows = -1;
        foreach (ITypeSymbol column in columns.Values)
        {
            if (GetVectorInfo(column) is not var (columnType, length)) return null;
            if (componentsType is not null && (componentsType != columnType || rows != length)) return null;
            componentsType = columnType;
            rows = length;
        }

        if (componentsType is not (VectorComponentsType.Float or VectorComponentsType.Double or VectorComponentsType.Half)) return null;
        if (columns.Count is < 2 or > 4 || rows is < 2 or > 4) return null;

        return new Matrix(name, kind, slot)
        {
            ComponentsType = componentsType == VectorComponentsType.Double
                ? MatrixComponentType.Double
                : componentsType == VectorComponentsType.Half ? MatrixComponentType.Half : MatrixComponentType.Float,
            Rows = (uint)rows,
            Columns = (uint)columns.Count,
            ColumnNames = columnNames.Values.ToList()
        };
    }

    /// <summary>System.Half no tiene SpecialType: se le da un valor propio para poder usarlo en los switch.</summary>
    internal const SpecialType HalfSpecial = unchecked((SpecialType)200);

    internal static SpecialType SpecialOf(ITypeSymbol type) =>
        type.ToDisplayString() == "System.Half" ? HalfSpecial : type.SpecialType;

    private static (VectorComponentsType Type, int Length)? GetVectorInfo(ITypeSymbol type)
    {
        if (!type.GetAttributes().Any(a => a.AttributeClass?.ToDisplayString() == typeof(VectorAttribute).FullName))
        {
            return null;
        }

        ITypeSymbol? componentType = null;
        int length = 0;
        foreach (ISymbol member in type.GetMembers())
        {
            if (member.IsStatic || member.IsImplicitlyDeclared) continue;

            ITypeSymbol? memberType = member switch
            {
                IFieldSymbol field => field.Type,
                IPropertySymbol { IsIndexer: false } property => property.Type,
                _ => null
            };
            if (memberType is null) continue;

            bool isComponent = member.GetAttributes()
                .Any(a => a.AttributeClass?.ToDisplayString() == typeof(VectorComponentAttribute).FullName);
            if (!isComponent) continue;

            componentType ??= memberType;
            if (!SymbolEqualityComparer.Default.Equals(componentType, memberType)) return null;
            length++;
        }

        VectorComponentsType? result = (componentType is null ? (SpecialType?)null : SpecialOf(componentType)) switch
        {
            SpecialType.System_Single => VectorComponentsType.Float,
            SpecialType.System_Double => VectorComponentsType.Double,
            SpecialType.System_Int32 => VectorComponentsType.Int,
            SpecialType.System_UInt32 => VectorComponentsType.Uint,
            SpecialType.System_Boolean => VectorComponentsType.Bool,
            HalfSpecial => VectorComponentsType.Half,
            _ => null
        };

        return result is { } r && length > 0 ? (r, length) : null;
    }

    /// <summary>Un tipo con [Sampler] y [Texture] a la vez es un sampler combinado (sampler2D de GLSL).</summary>
    private static bool HasSamplerAttribute(ITypeSymbol type)
    {
        ITypeSymbol target = type is INamedTypeSymbol { IsGenericType: true } named ? named.OriginalDefinition : type;
        string samplerAttribute = typeof(SamplerAttribute).FullName!;
        return target.GetAttributes().Any(a => a.AttributeClass?.ToDisplayString() == samplerAttribute);
    }

    internal static string GetComponentInitialValue(ISymbol member, Compilation compilation)
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

            SemanticModel semanticModel = Sources.GetSemanticModel(expression.SyntaxTree);
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

    internal static InterfaceSlot? GetAttributeData(ImmutableArray<AttributeData> atributes, IRStage stage)
    {

        int? descriptorSet = null;
        int? descriptorBinding = null;
        bool hasDescriptorAttribute = false;

        
        
        foreach (var attributeData in atributes.AsSpan())
        {
            if(!HasConcept<BindingSlot>(attributeData)) continue;

            hasDescriptorAttribute = true;

            if (TryGetConceptArgument(
                    attributeData,
                    BindingSlot.SET,
                    out TypedConstant setArgument) &&
                setArgument.Value is int set)
            {
                descriptorSet = set;
            }
            
            if (TryGetConceptArgument(attributeData, BindingSlot.BINDING, out TypedConstant bindingArgument) &&
                bindingArgument.Value is int binding)
            {
                descriptorBinding = binding;
            }

            if (TryGetConceptArgument(attributeData, BindingSlot.RegisterString, out TypedConstant registerArgument)
                && registerArgument.Value is string register)
            {
                ParseRegisterString(register, out int registerSet, out int registerBinding);
                descriptorBinding = registerBinding;
                descriptorSet = registerSet;
            }
        }

        if (hasDescriptorAttribute)
        {
            descriptorSet ??= 0;

            if (descriptorBinding is null)
            {
                throw new InvalidOperationException(
                    "A shader resource descriptor set was specified without a binding.");
            }

            if (descriptorSet < 0)
            {
                throw new InvalidOperationException(
                    $"Descriptor set cannot be negative: {descriptorSet}.");
            }
            
            if (descriptorBinding < 0)
            {
                throw new InvalidOperationException(
                    $"Descriptor binding cannot be negative: {descriptorBinding}.");
            }

            return new DescriptorSlot(descriptorSet.Value, descriptorBinding.Value);
        }
        
        InterfaceSlot? slot = null;

        foreach (var attributeData in atributes.AsSpan())
        {
            if (HasConcept<LocationAttribute>(attributeData)) 
            {
                // Sin argumento (por ejemplo [SV_Target]) es la Location 0, el valor por defecto del atributo.
                int location = attributeData.ConstructorArguments is [{ Value: int explicitLocation }]
                    ? explicitLocation
                    : 0;

                slot = new LocationSlot(location);
                return slot;
            }
            else if (HasConcept<UserSemanticAttribute>(attributeData))
            {
                int index = attributeData.ConstructorArguments is [{ Value: int idx }] ? idx : 0;

                VertexUsage usage = attributeData.AttributeClass?.Name switch
                {
                    "POSITION" => VertexUsage.Position,
                    "COLOR" => VertexUsage.Color,
                    "TEXCOORD" => VertexUsage.TextureCoordinate,
                    "NORMAL" => VertexUsage.Normal,
                    "TANGENT" => VertexUsage.Tangent,
                    "BINORMAL" => VertexUsage.Binormal,
                    _ => throw new NotSupportedException(
                        $"Semántica '{attributeData.AttributeClass?.Name}' no soportada.")
                };

                slot = new VertexSlot(new VertexSemantic(usage, index));
                return slot;
            }
            else
            {
                // La misma tabla que usa el analizador del IDE (YotsubaShader.Rules.BuiltInTable).
                BuiltIn? builtIn = ResolveBuiltIn(attributeData, stage);
                if (builtIn != null)
                    slot = new BuiltInSlot(builtIn.Value);
            }
        }

        return slot;
    }
    
    /// <summary>El built-in de IR que representa un atributo (SV_Position, gl_FragDepth...) en una etapa.</summary>
    private static BuiltIn? ResolveBuiltIn(AttributeData attribute, IRStage stage)
    {
        YotsubaShader.Rules.ShaderStageKind? kind = stage switch
        {
            IRStage.Vertex => YotsubaShader.Rules.ShaderStageKind.Vertex,
            IRStage.Fragment => YotsubaShader.Rules.ShaderStageKind.Fragment,
            IRStage.Compute => YotsubaShader.Rules.ShaderStageKind.Compute,
            _ => null
        };
        if (kind is null) return null;

        var concept = YotsubaShader.Rules.ShaderSymbols.GetConceptAttributes([attribute]).FirstOrDefault();
        if (concept is null || YotsubaShader.Rules.BuiltInTable.Resolve(concept, kind.Value) is not { Supported: true } rule)
        {
            return null;
        }

        string name = attribute.AttributeClass?.Name ?? "";
        return rule.SpirvName switch
        {
            "Position" => BuiltIn.Position,
            "VertexIndex" => BuiltIn.VertexIndex,
            "InstanceIndex" => BuiltIn.InstanceIndex,
            "BaseVertex" => BuiltIn.BaseVertex,
            "BaseInstance" => BuiltIn.BaseInstance,
            "FragCoord" => BuiltIn.FragCoord,
            "FrontFacing" => BuiltIn.FrontFacing,
            "FragDepth" when rule.Concept == "ConservativeDepth" || attribute.AttributeClass?.Name == "depth" =>
                name.Contains("Greater") || DepthMode(attribute) == "greater" ? BuiltIn.FragDepthGreater
                : name.Contains("Less") || DepthMode(attribute) == "less" ? BuiltIn.FragDepthLess
                : BuiltIn.FragDepth,
            "FragDepth" => BuiltIn.FragDepth,
            "SampleId" => BuiltIn.SampleId,
            "SamplePosition" => BuiltIn.SamplePosition,
            "PrimitiveId" => BuiltIn.PrimitiveId,
            "HelperInvocation" => BuiltIn.HelperInvocation,
            "GlobalInvocationId" => BuiltIn.GlobalInvocationId,
            "LocalInvocationId" => BuiltIn.LocalInvocationId,
            "LocalInvocationIndex" => BuiltIn.LocalInvocationIndex,
            "WorkgroupId" => BuiltIn.WorkgroupId,
            "NumWorkgroups" => BuiltIn.NumWorkgroups,
            _ => null
        };
    }

    /// <summary>MSL depth(greater|less|any): el nombre del valor del enum, en minúsculas.</summary>
    private static string? DepthMode(AttributeData attribute) =>
        attribute.ConstructorArguments is [{ Type: INamedTypeSymbol { TypeKind: TypeKind.Enum } enumType, Value: { } value }]
            ? enumType.GetMembers().OfType<IFieldSymbol>().FirstOrDefault(f => Equals(f.ConstantValue, value))?.Name.ToLowerInvariant()
            : null;

    private static bool HasConcept<TConcept>(AttributeData attribute) 
        where TConcept : YotsubaShaderConcept
    {
        string conceptFullName = typeof(TConcept).FullName!;

        // 1. Si el atributo es directamente el concepto (ej: [UniformBuffer])
        if (attribute.AttributeClass?.ToDisplayString() == conceptFullName)
            return true;

        // 2. Si la clase del atributo (ej: layout, cbuffer, @binding, etc.) está decorada con el concepto
        return attribute.AttributeClass?.GetAttributes()
            .Any(a => a.AttributeClass?.ToDisplayString() == conceptFullName) == true;
    }
    
    /// <summary>StructuredBuffer&lt;T&gt;, RWStructuredBuffer&lt;T&gt;, ConstantBuffer&lt;T&gt;: buffers declarados como tipo envoltorio.</summary>
    internal static bool IsBufferWrapper(ITypeSymbol? type) =>
        type is INamedTypeSymbol { IsGenericType: true, TypeArguments.Length: 1 } wrapper &&
        (TypeHasConcept<UniformBuffer>(wrapper) || TypeHasConcept<RWStorageBuffer>(wrapper) || TypeHasConcept<ReadOnlyStorageBuffer>(wrapper) ||
         CollectionElement(wrapper, out _) is not null);

    /// <summary>
    /// Colecciones de .NET que en un campo del shader son un storage buffer <c>{ T Items[]; }</c>:
    /// T[] y List&lt;T&gt; de lectura y escritura; ImmutableArray&lt;T&gt; e ImmutableList&lt;T&gt; de solo lectura.
    /// </summary>
    internal static ITypeSymbol? CollectionElement(ITypeSymbol type, out bool readOnly)
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

    /// <summary>¿El buffer del campo es un array de elementos (StructuredBuffer, T[], List...)? Si no, es un bloque (ConstantBuffer).</summary>
    internal static bool IsElementBuffer(ITypeSymbol type) =>
        CollectionElement(type, out _) is not null ||
        type is INamedTypeSymbol { IsGenericType: true } wrapper && (TypeHasConcept<RWStorageBuffer>(wrapper) || TypeHasConcept<ReadOnlyStorageBuffer>(wrapper));

    /// <summary>
    /// El bloque de un buffer envoltorio. ConstantBuffer&lt;T&gt; es un uniform cuyo bloque es T;
    /// (RW)StructuredBuffer&lt;T&gt; es un storage buffer <c>{ T Items[]; }</c>.
    /// </summary>
    private static Storage? TryBuildBufferWrapper(IFieldSymbol field, InterfaceSlot? slot, Compilation compilation)
    {
        if (CollectionElement(field.Type, out _) is { } collectionElement)
        {
            return ElementBlock(field, slot, compilation, field.Type, collectionElement);
        }

        if (!IsBufferWrapper(field.Type)) return null;
        var wrapper = (INamedTypeSymbol)field.Type;
        ITypeSymbol element = wrapper.TypeArguments[0];

        if (TypeHasConcept<UniformBuffer>(wrapper))
        {
            Storage block = GetStorage(element, field.Name, slot, StorageKind.Uniform, compilation, Stage.None);
            return block as CustomStruct ?? throw new NotSupportedException(
                $"ConstantBuffer<{element.Name}> '{field.Name}' necesita un struct como contenido.");
        }

        return ElementBlock(field, slot, compilation, wrapper, element);
    }

    /// <summary>El bloque <c>{ T Items[]; }</c> de un buffer de elementos.</summary>
    private static Storage ElementBlock(IFieldSymbol field, InterfaceSlot? slot, Compilation compilation, ITypeSymbol container, ITypeSymbol element)
    {
        Storage elementStorage = GetStorage(element, "element", null, StorageKind.Local, compilation, Stage.None);
        string containerName = container is IArrayTypeSymbol ? "Array" : container.Name;
        return new CustomStruct(field.Name, StorageKind.StorageBuffer, slot)
        {
            TypeName = UniqueStructTypeName(container, $"{containerName}_{element.Name}"),
            Properties =
            [
                new IR.Intermediate.Types.Array("Items", StorageKind.StorageBuffer, null)
                {
                    ElementType = elementStorage,
                    Length = null
                }
            ]
        };
    }

    private static bool TypeHasConcept<TConcept>(INamedTypeSymbol type) 
        where TConcept : YotsubaShaderConcept
    {
        string conceptFullName = typeof(TConcept).FullName!;

        // OriginalDefinition desenrolla el genérico: ConstantBuffer<MisDatos> -> ConstantBuffer<T>
        return type.OriginalDefinition.GetAttributes()
            .Any(a => a.AttributeClass?.ToDisplayString() == conceptFullName);
    }


    private static void AddResourceInputs(
        INamedTypeSymbol shaderType,
        Dictionary<ISymbol, Storage> inputs,
        Compilation compilation)
    {
        foreach (ISymbol member in shaderType.GetMembers())
        {
            if (member.IsStatic || member.IsImplicitlyDeclared) continue;
            if (member is not (IFieldSymbol or IPropertySymbol)) continue;

            StorageKind kind = GetStorageKind(member, compilation);

            // T[], List<T>, ImmutableArray<T>, ImmutableList<T> como campo del shader: un storage buffer.
            if (kind is not StorageKind.Uniform && member is IFieldSymbol { Type: var collectionType } &&
                CollectionElement(collectionType, out _) is not null)
            {
                kind = StorageKind.StorageBuffer;
            }

            bool isBuffer = kind is StorageKind.Uniform or StorageKind.StorageBuffer;

            if (member is IPropertySymbol)
            {
                if (isBuffer)
                {
                    throw new NotSupportedException(
                        $"The {kind} resource '{member.Name}' is declared as a property. " +
                        "Declare it as a field.");
                }

                continue;
            }

            if (kind is not (StorageKind.Texture or StorageKind.Sampler or
                StorageKind.Uniform or StorageKind.StorageBuffer)) continue;

            InterfaceSlot? slot = GetAttributeData(member.GetAttributes(), Stage.None);

            if (isBuffer && slot is not DescriptorSlot)
            {
                throw new NotSupportedException(
                    $"The {kind} resource '{member.Name}' has no descriptor binding. " +
                    "Give it a set and a binding, for example [DescriptorSet(0)] [Binding(0)].");
            }

            if (isBuffer && ((IFieldSymbol)member).Type.TypeKind != TypeKind.Struct &&
                CollectionElement(((IFieldSymbol)member).Type, out _) is null)
            {
                throw new NotSupportedException(
                    $"The {kind} resource '{member.Name}' has type " +
                    $"'{((IFieldSymbol)member).Type.ToDisplayString()}'. Only scalars, vectors and structs are supported.");
            }

            Storage resource = TryBuildBufferWrapper((IFieldSymbol)member, slot, compilation)
                               ?? GetStorage(member, null, slot, compilation, Stage.None);
            if (isBuffer) resource = WithLayoutKind(resource, kind);
            if (kind == StorageKind.StorageBuffer && IsReadOnlyBuffer((IFieldSymbol)member))
            {
                resource = resource with { ReadOnly = true };
            }

            inputs.TryAdd(member, resource);
        }
    }
    private static bool TryGetConceptArgument(
        AttributeData attribute,
        string conceptKey,
        out TypedConstant argument)
    {
        argument = default;

        if (attribute.AttributeConstructor is not { } constructor)
            return false;

        var parameters = constructor.Parameters;
        var arguments = attribute.ConstructorArguments;

        int count = Math.Min(parameters.Length, arguments.Length);

        for (int index = 0; index < count; index++)
        {
            bool hasRequestedKey = parameters[index]
                .GetAttributes()
                .Any(parameterAttribute =>
                    parameterAttribute.AttributeClass?.ToDisplayString()
                    == typeof(ConceptKeyAttribute).FullName &&
                    parameterAttribute.ConstructorArguments
                        is [{ Value: string key }] &&
                    key == conceptKey);

            if (!hasRequestedKey)
                continue;

            argument = arguments[index];
            return true;
        }

        return false;
    }
    
    private static void ParseRegisterString(
        string register,
        out int set,
        out int binding)
    {
        if (string.IsNullOrWhiteSpace(register) || register.Length < 2)
        {
            throw new InvalidOperationException(
                $"Invalid HLSL register: '{register}'.");
        }

        set = register[0] switch
        {
            'b' => 0,
            't' => 1,
            's' => 2,
            'u' => 3,
            'c' => 4,
            _ => throw new InvalidOperationException(
                $"Unknown HLSL register prefix: '{register[0]}'.")
        };

        if (!int.TryParse(
                register.AsSpan(1),
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out binding) ||
            binding < 0)
        {
            throw new InvalidOperationException(
                $"Invalid HLSL register: '{register}'.");
        }
    }
    
    internal static string UniqueStructTypeName(ITypeSymbol type, string preferredName)
    {
        // (float s, float c) y (float, float) son el mismo tipo en la GPU: los nombres de una tupla son alias.
        if (type is INamedTypeSymbol { IsTupleType: true, TupleUnderlyingType: { } underlying }) type = underlying;

        SymbolKey key = SymbolKey.OfType(type);
        if (structTypeNames.TryGetValue(key, out string? existing))
        {
            return existing;
        }

        string name = preferredName;

        for (int suffix = 1; structTypeNames.ContainsValue(name); suffix++)
        {
            name = $"{preferredName}_{suffix}";
        }

        structTypeNames[key] = name;

        return name;
    }


    private static void ProcessWithChain(Compilation compilation, FunctionsInfo f,
        List<Function> functions, List<FunctionsInfo> pending, Dictionary<ISymbol, Storage> globals)
    {
        try { ProcessShaderFunction(compilation, f, functions, pending, globals); }
        catch (NotSupportedException ex)
        {
            throw new NotSupportedException($"{ex.Message}\n{DescribeChain(f)}", ex);
        }
        static string DescribeChain(FunctionsInfo? f)
        {
            var lines = new List<string>();
            for (; f is not null; f = f.CalledFrom)
                lines.Add($"  → {f.Method.ContainingType.Name}.{f.Method.Name} ({f.Method.ContainingAssembly.Name})");
            lines.Reverse();
            return string.Join("\n", lines);
        }
    }
    
    
}
