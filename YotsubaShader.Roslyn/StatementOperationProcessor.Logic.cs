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
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;
using YotsubaShader.IR.Intermediate;
using YotsubaShader.IR.Intermediate.FunctionBody;
using System.Globalization;
using YotsubaShader.IR.Intermediate.FunctionBody.Expressions;
using YotsubaShader.IR.Intermediate.FunctionBody.Expressions.AssignmentTargets;
using YotsubaShader.IR.Intermediate.FunctionBody.Expressions.Literals;
using YotsubaShader.IR.Intermediate.ValueConvertions;
using YotsubaShader.Language.Native.Types.Vector;
using YotsubaShader.Language.Native.Types.Vector.Exceptions;
using YotsubaShader.IR.Intermediate.FunctionBody.Expressions.FunctionCalls;
using YotsubaShader.IR.Intermediate.FunctionBody.Statements;
using YotsubaShader.Roslyn.Model;
using IRTypes = YotsubaShader.IR.Intermediate.Types;
namespace YotsubaShader.Roslyn;

public partial class StatementOperationProcessor
{
    private readonly Dictionary<ISymbol, Storage> _locals =
        new(SymbolEqualityComparer.Default);

    private readonly Dictionary<string, int> _localNameCounts = new();

    private string UniqueLocalName(string name)
    {
        if (!_localNameCounts.TryGetValue(name, out int count))
        {
            _localNameCounts[name] = 1;
            return name;
        }

        _localNameCounts[name] = count + 1;
        return $"{name}_{count}";
    }
    
    private Expression? ProcessExpression(
        IOperation initializerOperationValue, 
        Dictionary<ISymbol, Storage> inputs, 
        Dictionary<ISymbol, Storage> outputs, 
        Compilation compilation, Stage stage)
    {
        switch (initializerOperationValue)
        {
            case ILiteralOperation literalOperation:
                return ProcessLiteral(literalOperation);
            case ILocalReferenceOperation localReferenceOperation:
                return new StorageReference(GetStorage(localReferenceOperation.Local, inputs, outputs));
            case IParameterReferenceOperation parameterReferenceOperation:
                return new StorageReference(GetStorage(parameterReferenceOperation.Parameter, inputs, outputs));
            case IFieldReferenceOperation fieldReferenceOperation:
                return ProcessMemberReference(fieldReferenceOperation.Instance,
                    fieldReferenceOperation.Field.Name,
                    inputs,
                    outputs, compilation, stage);
            case IPropertyReferenceOperation { Instance: not null, Property: { Name: "Length", ContainingType.SpecialType: SpecialType.System_Array } } arrayLength:
                return ProcessArrayLength(arrayLength, inputs, outputs, compilation, stage);
            case IPropertyReferenceOperation propertyReferenceOperation:
                return ProcessMemberReference(propertyReferenceOperation.Instance, propertyReferenceOperation.Property.Name,
                    inputs, outputs, compilation, stage);
            case IParenthesizedOperation parenthesizedOperation:
                return ProcessExpression(parenthesizedOperation.Operand, inputs, outputs, compilation, stage);
            case IBinaryOperation binaryOperation:
                return ProcessBinaryExpression(
                    binaryOperation, inputs, outputs, compilation, stage);
            case IConversionOperation conversionOperation:
                return ProcessConversionExpression(conversionOperation, inputs, outputs, compilation, stage);
            case IObjectCreationOperation objectCreationOperation: 
                return ProcessObjectCreationExpression(
                    objectCreationOperation, inputs, outputs, compilation, stage);
            case IInvocationOperation invocationOperation:
                return ProcessInvocationExpression(invocationOperation, inputs, outputs, compilation, stage);
            case IUnaryOperation unaryOperation:
                return ProcessUnaryExpression(unaryOperation, inputs, outputs, compilation, stage);
            case IConditionalOperation { WhenFalse: not null } conditionalExpression:
                return ProcessConditionalExpression(conditionalExpression, inputs, outputs, compilation, stage);
            case IIncrementOrDecrementOperation incrementExpression:
                return ProcessIncrementExpression(incrementExpression, inputs, outputs, compilation, stage);
            case IInstanceReferenceOperation:
                return new StorageReference(ThisStorage 
                                            ?? throw new NotSupportedException("'this' solo existe dentro de un constructor."));
            case IArrayElementReferenceOperation arrayElement:
            {
                if (arrayElement.Indices.Length != 1)
                {
                    throw new NotSupportedException("Only single-index array access is supported.");
                }

                var array = ProcessExpression(arrayElement.ArrayReference, inputs, outputs, compilation, stage)
                            ?? throw new NotSupportedException("The array reference did not produce an expression.");
                var index = ProcessExpression(arrayElement.Indices[0], inputs, outputs, compilation, stage)
                            ?? throw new NotSupportedException("The array index did not produce an expression.");

                ValidateArrayIndex(arrayElement.Indices[0], array);

                return new StorageArrayElementReference(array, index);
            }
        }

        throw new NotSupportedException(
            $"Expression operation '{initializerOperationValue.Kind}' is not supported yet.");
    }

    private Expression? ProcessUnaryExpression(IUnaryOperation operation,
        Dictionary<ISymbol, Storage> inputs, Dictionary<ISymbol, Storage> outputs,
        Compilation compilation, Stage stage)
    {
        if (operation.OperatorMethod is { MethodKind: MethodKind.UserDefinedOperator } userOperator)
        {
            var (functionId, arguments) = ProcessMethodCall(
                userOperator, [operation.Operand], inputs, outputs, compilation, stage);

            return new FunctionCallExpression(functionId, arguments);
        }

        var operand = ProcessExpression(operation.Operand, inputs, outputs, compilation, stage)
                      ?? throw new NotSupportedException("The unary operand did not produce an expression.");

        return operation.OperatorKind switch
        {
            UnaryOperatorKind.Minus => new UnaryExpression(UnaryOperator.Negate, operand),
            UnaryOperatorKind.Not   => new UnaryExpression(UnaryOperator.LogicalNot, operand),
            UnaryOperatorKind.BitwiseNegation => new UnaryExpression(UnaryOperator.BitwiseNot, operand),
            UnaryOperatorKind.Plus  => operand,
            _ => throw new NotSupportedException(
                $"The unary operator '{operation.OperatorKind}' is not supported.")
        };
    }
    
    private (
    FunctionId FunctionId,
    IReadOnlyList<Expression> Arguments)
    ProcessInvocation(
        IInvocationOperation operation,
        Dictionary<ISymbol, Storage> inputs,
        Dictionary<ISymbol, Storage> outputs,
        Compilation compilation,
        Stage stage)
{
    IMethodSymbol method = operation.TargetMethod;

    IOperation[] orderedArguments = operation.Arguments
        .OrderBy(argument =>
            argument.Parameter?.Ordinal
            ?? throw new NotSupportedException(
                $"No se pudo resolver un parámetro de " +
                $"'{method.Name}'."))
        .Select(argument => argument.Value)
        .ToArray();

    return ProcessMethodCall(method, orderedArguments, inputs, outputs, compilation, stage);
}

    /// <summary>
    /// Llama a un método del shader (ordinario u operador definido por el usuario) con sus argumentos
    /// ya ordenados por parámetro; lo registra para que se compile como función auxiliar.
    /// </summary>
    private (
    FunctionId FunctionId,
    IReadOnlyList<Expression> Arguments)
    ProcessMethodCall(
        IMethodSymbol targetMethod,
        IReadOnlyList<IOperation> argumentOperations,
        Dictionary<ISymbol, Storage> inputs,
        Dictionary<ISymbol, Storage> outputs,
        Compilation compilation,
        Stage stage,
        IReadOnlyList<Expression>? preparedArguments = null)
{
    if (targetMethod.MethodKind is not (MethodKind.Ordinary or MethodKind.UserDefinedOperator or MethodKind.Constructor) ||
        targetMethod.DeclaringSyntaxReferences.IsDefaultOrEmpty)
    {
        throw new NotSupportedException(
            $"El método '{targetMethod.ToDisplayString()}' no tiene un cuerpo de shader compilable.");
    }

    if (targetMethod.IsGenericMethod)
    {
        throw new NotSupportedException(
            $"El método genérico '{targetMethod.Name}' " +
            "aún no está soportado.");
    }

    if (targetMethod.Parameters.Any(parameter =>
            parameter.RefKind != RefKind.None))
    {
        throw new NotSupportedException(
            $"El método '{targetMethod.Name}' no puede usar " +
            "parámetros ref, in u out todavía.");
    }

    if (targetMethod.Parameters.Any(parameter => parameter.IsParams))
    {
        throw new NotSupportedException(
            $"El método '{targetMethod.Name}' no puede usar params todavía.");
    }

    if (targetMethod.GetAttributes().Any(attribute =>
            attribute.AttributeClass?.ToDisplayString() ==
            ShaderDiscovery.ShaderStageAttributeMetadataName))
    {
        throw new NotSupportedException(
            $"No se puede llamar directamente a la etapa " +
            $"'{targetMethod.Name}'.");
    }

    var arguments = new List<Expression>(
        preparedArguments?.Count ?? argumentOperations.Count);

    // Los argumentos ya pueden venir convertidos a expresiones (asignación compuesta con operador del usuario).
    if (preparedArguments is not null)
    {
        arguments.AddRange(preparedArguments);
    }
    else
    foreach (IOperation argumentOperation in argumentOperations)
    {
        Expression argument =
            ProcessExpression(
                argumentOperation,
                inputs,
                outputs,
                compilation,
                stage)
            ?? throw new NotSupportedException(
                $"El argumento de '{targetMethod.Name}' " +
                "no produjo una expresión.");

        arguments.Add(argument);
    }

    if (!ShaderProcessor.functionIds.TryGetValue(
            targetMethod,
            out FunctionId functionId))
    {
        functionId = new FunctionId(
            (uint)ShaderProcessor.functionIds.Count);

        ShaderProcessor.functionIds.Add(
            targetMethod,
            functionId);

        var parameters = targetMethod.Parameters
            .OrderBy(parameter => parameter.Ordinal)
            .Select(parameter => new ParameterInfo(
                parameter,
                IsField: false))
            .ToImmutableArray();

        var returnType = targetMethod.MethodKind == MethodKind.Constructor
            ? targetMethod.ContainingType
            : targetMethod.ReturnType;
        
        var functionInfo = new FunctionsInfo(
            targetMethod,
            Language.Stage.None,
            parameters,
            returnType);

        ShaderProcessor.PendingFunctionsInfos.Add(functionInfo);
    }

    return (functionId, arguments);
}
    private Expression? ProcessInvocationExpression(IInvocationOperation invocationOperation, Dictionary<ISymbol, Storage> inputs, Dictionary<ISymbol, Storage> outputs, Compilation compilation, Stage stage)
    {
        if (invocationOperation.TargetMethod.ReturnsVoid)
        {
            throw new NotSupportedException(
                $"El método '{invocationOperation.TargetMethod.Name}' " +
                "no retorna un valor y no puede usarse como expresión.");
        }
        
        (FunctionId functionId, IReadOnlyList<Expression> arguments) =
            ProcessInvocation(
                invocationOperation,
                inputs,
                outputs,
                compilation,
                stage);
        
        return new FunctionCallExpression(
            functionId,
            arguments);
    }

    private Expression? ProcessBinaryExpression(IBinaryOperation binaryOperation, Dictionary<ISymbol, Storage> inputs, Dictionary<ISymbol, Storage> outputs, Compilation compilation, Stage stage)
    {
        // Un operador definido por el usuario se compila como la función que Roslyn ya resolvió.
        if (binaryOperation.OperatorMethod is { MethodKind: MethodKind.UserDefinedOperator } userOperator)
        {
            var (functionId, arguments) = ProcessMethodCall(
                userOperator,
                [binaryOperation.LeftOperand, binaryOperation.RightOperand],
                inputs, outputs, compilation, stage);

            return new FunctionCallExpression(functionId, arguments);
        }

        var left = ProcessExpression(binaryOperation.LeftOperand, inputs, outputs, compilation, stage)   ?? throw new NotSupportedException(
            "The left binary operand did not produce an expression.");

        var right = ProcessExpression(binaryOperation.RightOperand, inputs, outputs, compilation, stage) ?? throw new NotSupportedException(
            "The right binary operand did not produce an expression.");

        // `a && b` / `a || b` no evalúan b si a decide el resultado. OpLogicalAnd/Or evalúan ambos lados,
        // lo cual solo importa si b tiene efectos secundarios reales (++, --, ?: con ellos): ahí se baja a un If.
        if (binaryOperation.OperatorKind is BinaryOperatorKind.ConditionalAnd or BinaryOperatorKind.ConditionalOr &&
            ContainsSequence(right))
        {
            Storage result = new IRTypes.Bool(UniqueLocalName("__shortcircuit"), StorageKind.Local, null);
            var resultReference = new StorageReference(result);

            Expression runRight = binaryOperation.OperatorKind == BinaryOperatorKind.ConditionalAnd
                ? resultReference
                : new UnaryExpression(UnaryOperator.LogicalNot, resultReference);

            return new SequenceExpression(
                [
                    new VariableDeclaration(result, left),
                    new If(runRight, new Assignment(resultReference, right), null)
                ],
                resultReference);
        }

        BinaryOperator binaryOperator = GetBinaryOperator(
            binaryOperation.OperatorKind,
            binaryOperation.LeftOperand.Type?.SpecialType == SpecialType.System_Boolean);

        return new BinaryExpression(binaryOperator, left, right);
    }

    private Expression? ProcessConversionExpression(IConversionOperation conversionOperation, Dictionary<ISymbol, Storage> inputs, Dictionary<ISymbol, Storage> outputs, Compilation compilation, Stage stage)
    {
        Expression operand = ProcessExpression(conversionOperation.Operand, inputs, outputs, compilation, stage)
            ?? throw new NotSupportedException(
            "The conversion did not produce an expression.");

        if (conversionOperation.Conversion.IsIdentity) return operand;
        if (!conversionOperation.Conversion.IsNumeric || conversionOperation.Type is null)
        {
            throw new NotSupportedException(
                $"The '{conversionOperation.Conversion}' conversion is not supported.");
        }

        ScalarType targetType = conversionOperation.Type.SpecialType switch
        {
            SpecialType.System_UInt32 => ScalarType.UInt32,
            SpecialType.System_Int32 => ScalarType.Int32,
            SpecialType.System_Single => ScalarType.Float32,
            SpecialType.System_Double => ScalarType.Float64,
            _ => throw new NotSupportedException(
                $"El destino '{conversionOperation.Type}' no es un escalar soportado.")
        };

        return new NumericConversion(operand, targetType);
    }

    private Expression? ProcessObjectCreationExpression(IObjectCreationOperation objectCreationOperation, Dictionary<ISymbol, Storage> inputs, Dictionary<ISymbol, Storage> outputs, Compilation compilation, Stage stage)
    {
        if (objectCreationOperation.Type is null)
        {
            throw new NotSupportedException(
                "The object creation does not have a resolved type.");
        }

        var storageType = ShaderProcessor.GetStorage(objectCreationOperation.Type,
            objectCreationOperation.Type.Name,
            null,
            StorageKind.Local,
            compilation, stage);
        
        switch (storageType)
        {
            case IRTypes.CustomStruct or IRTypes.Vector: 
            {
                IMethodSymbol ctor = objectCreationOperation.Constructor
                                     ?? throw new NotSupportedException($"No se resolvió un constructor para '{objectCreationOperation.Type}'.");

                Expression created;
                if (ctor.IsImplicitlyDeclared)
                {
                    created = storageType is IRTypes.CustomStruct customStruct
                        ? new CustomStructExpression(customStruct, [])
                        : throw new NotSupportedException(
                            $"'new {objectCreationOperation.Type}()' sin constructor declarado no está soportado para vectores.");
                }
                else
                {
                    var ordered = objectCreationOperation.Arguments
                        .OrderBy(a => a.Parameter!.Ordinal)
                        .Select(a => a.Value)
                        .ToArray();
                    var (functionId, arguments) = ProcessMethodCall(ctor, ordered, inputs, outputs, compilation, stage);
                    created = new FunctionCallExpression(functionId, arguments);
                }

                if (objectCreationOperation.Initializer is null) return created;

                // new T(...) { A = 1 }: primero el constructor, luego las asignaciones en orden.
                Storage temp = CreateTemporary(objectCreationOperation.Type, "init", compilation, stage);
                var tempRef = new StorageReference(temp);
                var prelude = new List<Statement> { new VariableDeclaration(temp, created) };

                foreach (IOperation item in objectCreationOperation.Initializer.Initializers)
                {
                    if (item is not ISimpleAssignmentOperation { Target: IFieldReferenceOperation field } assignment)
                        throw new NotSupportedException("El inicializador solo admite 'Campo = valor'.");

                    Expression value = ProcessExpression(assignment.Value, inputs, outputs, compilation, stage)
                                       ?? throw new NotSupportedException($"El inicializador de '{field.Field.Name}' no produjo una expresión.");

                    prelude.Add(new Assignment(
                        new StorageStructPropertyReference(tempRef, field.Field.Name),
                        value));
                }

                return new SequenceExpression(prelude, tempRef);
            }
            default:
                throw new NotSupportedException(
                    $"The creation of '{objectCreationOperation.Type}' is not supported yet.");
        }
    }

    private Expression ProcessMemberReference(
        IOperation? instance, 
        string propertyName, 
        Dictionary<ISymbol, Storage> inputs, 
        Dictionary<ISymbol, Storage> outputs, Compilation compilation, Stage stage)
    {
        if (instance is null)
        {
            throw new NotSupportedException(
                $"Static member '{propertyName}' is not supported in shader expressions.");
        }

        var reference = ProcessExpression(instance, inputs, outputs, compilation, stage);

        return reference is null ?
            throw new NotSupportedException("this member assignment is not supported") :
            new StorageStructPropertyReference(reference, propertyName);
    }
    
    private Expression ProcessLiteral(
        ILiteralOperation literalOperation)
    {
        var constantValue = literalOperation.ConstantValue;
        if(!constantValue.HasValue) throw new NotSupportedException("A literal without a constant value is not supported.");

        switch (constantValue.Value)
        {
            case int value:
                return new IntLiteral(value.ToString(CultureInfo.InvariantCulture), true);
            case uint value:
                return new IntLiteral(value.ToString(CultureInfo.InvariantCulture), false);
            case float value:
                return new FloatLiteral(value);
            case double value:
                return new DoubleLiteral(value);
            case bool value:
                return new BoolLiteral(value);
        }
        
        throw new NotSupportedException(
            $"Literal type '{literalOperation.Type?.ToDisplayString()}' is not supported.");
    }
    
    private Storage GetStorage(
            ISymbol symbol,
            Dictionary<ISymbol, Storage> inputs,
            Dictionary<ISymbol, Storage> outputs)
        {
            if (_locals.TryGetValue(symbol, out var local) ||
                inputs.TryGetValue(symbol, out local) ||
                outputs.TryGetValue(symbol, out local))
            {
                return local;
            }
            
            throw new InvalidOperationException(
                $"Storage for symbol '{symbol.ToDisplayString()}' was not found.");
        }
    
    /// <summary>Lo que hace falta de un símbolo para crear su Storage: un tipo y un nombre.</summary>
    private readonly record struct StorageSource(ITypeSymbol Type, string Name, RefKind RefKind = RefKind.None)
    {
        public string ToDisplayString() => Name;
    }

    private Storage CreateLocalStorage(
        ILocalSymbol symbol, 
        Compilation compilation, 
        Stage stage) =>
        CreateStorageFor(new StorageSource(symbol.Type, symbol.Name, symbol.RefKind), compilation, stage);

    /// <summary>Variable temporal con nombre único, del tipo C# indicado (escalar, vector o struct).</summary>
    private Storage CreateTemporary(ITypeSymbol type, string prefix, Compilation compilation, Stage stage)
    {
        Storage storage = CreateStorageFor(new StorageSource(type, UniqueLocalName(prefix)), compilation, stage);

        if (storage is IRTypes.Array)
        {
            throw new NotSupportedException(
                $"An expression of array type '{type.ToDisplayString()}' cannot be used here.");
        }

        return storage;
    }

    private Storage CreateStorageFor(
        StorageSource symbol, 
        Compilation compilation, 
        Stage stage)
    {
        if (symbol.RefKind != RefKind.None)
        {
            throw new NotSupportedException(
                $"Ref local '{symbol.Name}' is not supported.");
        }

        Storage? storage = null;
        var storageKind = StorageKind.Local;
        switch (symbol.Type.SpecialType)
        {
            case SpecialType.System_Int32:
                storage = new IRTypes.Int(symbol.Name, storageKind, null)
                {
                    Bits = 32,
                    Signed = true
                };
                break;
            case SpecialType.System_UInt32:
                storage = new IRTypes.Int(symbol.Name, storageKind, null)
                {
                    Bits = 32,
                    Signed = false
                };
                break;
            case SpecialType.System_Double:
                storage = new IRTypes.Double(symbol.Name, storageKind, null)
                {
                };
                break;
            case SpecialType.System_Single:
                storage = new IRTypes.Float(symbol.Name, storageKind, null)
                {
                };
                break;
            case SpecialType.System_Boolean:
                storage = new IRTypes.Bool(symbol.Name, storageKind, null)
                {
                };
                break;
        }

        if(storage is not null)
            return storage;

        if (symbol.Type is IArrayTypeSymbol arrayType)
        {
            if (arrayType.Rank != 1)
            {
                throw new NotSupportedException(
                    $"Multi-dimensional array '{symbol.Name}' is not supported.");
            }

            if (arrayType.ElementType is IArrayTypeSymbol)
            {
                throw new NotSupportedException(
                    $"Array of arrays '{symbol.Name}' is not supported; use a flat array.");
            }

            // Escalares directamente; vectores y structs con el mismo camino que cualquier local.
            Storage element = CreateScalarStorage(arrayType.ElementType, "element", storageKind)
                ?? CreateStorageFor(new StorageSource(arrayType.ElementType, "element"), compilation, stage);

            return new IRTypes.Array(symbol.Name, storageKind, null)
            {
                ElementType = element,
                Length = null
            };
        }
        
        var type = symbol.Type;

       
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

                    string value = ShaderProcessor.GetComponentInitialValue(memberInfo, compilation);
                    ShaderProcessor.AddVectorComponent(components, type, memberInfo, (int)memberAttributes.ConstructorArguments[0].Value!, value);
                }

                ShaderProcessor.ValidateVectorComponents(components, type);
                components = components.OrderBy(x => x.Key).ToDictionary();
                
                IRTypes.VectorComponentsType vectorComponentsType = componentsType?.SpecialType switch
                {
                    SpecialType.System_Int32 => IRTypes.VectorComponentsType.Int,
                    SpecialType.System_UInt32 => IRTypes.VectorComponentsType.Uint,
                    SpecialType.System_Single => IRTypes.VectorComponentsType.Float,
                    _ => throw new VectorComponentTypeNotValidException(
                        componentsType?.ToDisplayString() ?? "<missing>")
                };
                
                Expression ComponentValueToExpression(string value)
                {
                    Expression? exp = null;

                    if (!string.IsNullOrWhiteSpace(value))
                        return vectorComponentsType switch
                        {
                            IRTypes.VectorComponentsType.Float =>
                                new FloatLiteral(float.Parse(value, CultureInfo.InvariantCulture)),
                            IRTypes.VectorComponentsType.Int =>
                                new IntLiteral(value, true),
                            IRTypes.VectorComponentsType.Uint =>
                                new IntLiteral(value, false),
                            _ => throw new NotSupportedException(
                                $"Unsupported vector component type '{vectorComponentsType}'.")
                        };

                    return vectorComponentsType switch
                    {
                        IRTypes.VectorComponentsType.Float => new FloatLiteral(0f),
                        IRTypes.VectorComponentsType.Int => new IntLiteral("0", true),
                        IRTypes.VectorComponentsType.Uint => new IntLiteral("0", false),
                        _ => throw new NotSupportedException(
                            $"Unsupported vector component type '{vectorComponentsType}'.")
                    };
                }
                        
                Storage vector = new IRTypes.Vector(symbol.Name, storageKind, null)
                {
                    ComponentsDefaultValues = [.. components.Select(x => ComponentValueToExpression(x.Value))],
                    ComponentsType = vectorComponentsType,
                    ComponentsLength = components.Count,
                    ComponentNames = ShaderProcessor.GetVectorComponentNames(type)
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

                Storage? strg = null;
                strg = ShaderProcessor.GetStorage(member, strg, null, compilation, stage, true);
                properties.Add(strg);
            }
            
            string typeName = structType.OriginalDefinition.Name;
            if (structType.IsGenericType)
            {
                string genericArgs = string.Join("_",
                    structType.TypeArguments.Select(t => t.Name));

                typeName = $"{typeName}_{genericArgs}";
            }

            typeName = ShaderProcessor.UniqueStructTypeName(structType, typeName);

            storage = new IRTypes.CustomStruct(symbol.Name, storageKind, null)
            {
                Properties = properties,
                TypeName = typeName
            };
        }

        return storage ?? throw new NotSupportedException(
            $"Type '{type.ToDisplayString()}' used by '{symbol.ToDisplayString()}' is not supported as shader local variable.");
    }

        
    private static BinaryOperator GetBinaryOperator(
        BinaryOperatorKind operatorKind,
        bool booleanOperands = false)
    {
        return operatorKind switch
        {
            // En bool, & | ^ son lógicos y evalúan ambos lados (igual que OpLogicalAnd/Or).
            BinaryOperatorKind.And => booleanOperands ? BinaryOperator.LogicalAnd : BinaryOperator.BitwiseAnd,
            BinaryOperatorKind.Or => booleanOperands ? BinaryOperator.LogicalOr : BinaryOperator.BitwiseOr,
            BinaryOperatorKind.ExclusiveOr => booleanOperands ? BinaryOperator.NotEqual : BinaryOperator.BitwiseXor,
            BinaryOperatorKind.LeftShift => BinaryOperator.ShiftLeft,
            BinaryOperatorKind.RightShift => BinaryOperator.ShiftRight,
            BinaryOperatorKind.Add => BinaryOperator.Add,
            BinaryOperatorKind.Subtract => BinaryOperator.Subtract,
            BinaryOperatorKind.Multiply => BinaryOperator.Multiply,
            BinaryOperatorKind.Divide => BinaryOperator.Divide,
            BinaryOperatorKind.Remainder => BinaryOperator.Remainder,
            BinaryOperatorKind.ConditionalAnd => BinaryOperator.LogicalAnd,
            BinaryOperatorKind.ConditionalOr  => BinaryOperator.LogicalOr,
            BinaryOperatorKind.Equals             => BinaryOperator.Equal,
            BinaryOperatorKind.NotEquals          => BinaryOperator.NotEqual,
            BinaryOperatorKind.LessThan           => BinaryOperator.Less,
            BinaryOperatorKind.LessThanOrEqual    => BinaryOperator.LessEqual,
            BinaryOperatorKind.GreaterThan        => BinaryOperator.Greater,
            BinaryOperatorKind.GreaterThanOrEqual => BinaryOperator.GreaterEqual,
            _ => throw new NotSupportedException(
                $"The binary operator '{operatorKind}' is not supported.")
        };
    }
    
    /// <summary>
    /// Procesa el destino de una asignación evaluando una sola vez los índices con efectos secundarios:
    /// `arr[F()] += 1` llama a F una vez, no dos.
    /// </summary>
    private Expression ProcessAssignableTarget(
        IOperation target,
        List<Statement> prelude,
        Dictionary<ISymbol, Storage> inputs,
        Dictionary<ISymbol, Storage> outputs,
        Compilation compilation,
        Stage stage)
    {
        switch (target)
        {
            case IInstanceReferenceOperation:
                return new StorageReference(ThisStorage
                                            ?? throw new NotSupportedException("'this' solo existe dentro de un constructor."));
            
            case IParenthesizedOperation parenthesized:
                return ProcessAssignableTarget(parenthesized.Operand, prelude, inputs, outputs, compilation, stage);

            case IFieldReferenceOperation { Instance: not null } field:
                return new StorageStructPropertyReference(
                    ProcessAssignableTarget(field.Instance, prelude, inputs, outputs, compilation, stage),
                    field.Field.Name);

            case IPropertyReferenceOperation { Instance: not null } property:
                return new StorageStructPropertyReference(
                    ProcessAssignableTarget(property.Instance, prelude, inputs, outputs, compilation, stage),
                    property.Property.Name);

            case IArrayElementReferenceOperation { Indices.Length: 1 } element:
            {
                Expression array = ProcessAssignableTarget(
                    element.ArrayReference, prelude, inputs, outputs, compilation, stage);

                ValidateArrayIndex(element.Indices[0], array);

                Expression index = ProcessExpression(element.Indices[0], inputs, outputs, compilation, stage)
                    ?? throw new NotSupportedException("The array index did not produce an expression.");

                if (HasSideEffects(index))
                {
                    bool signed = element.Indices[0].Type?.SpecialType switch
                    {
                        SpecialType.System_Int32 => true,
                        SpecialType.System_UInt32 => false,
                        _ => throw new NotSupportedException("Array indices must be int or uint.")
                    };

                    Storage temporary = new IRTypes.Int(UniqueLocalName("__index"), StorageKind.Local, null)
                    {
                        Bits = 32,
                        Signed = signed
                    };

                    prelude.Add(new VariableDeclaration(temporary, index));
                    index = new StorageReference(temporary);
                }

                return new StorageArrayElementReference(array, index);
            }

            default:
                return ProcessExpression(target, inputs, outputs, compilation, stage)
                    ?? throw new NotSupportedException("The assignment target did not produce an expression.");
        }
    }

    /// <summary>¿Contiene la expresión sentencias con efectos reales (++, --, ?:)? Las llamadas no cuentan: son puras.</summary>
    private static bool ContainsSequence(Expression expression) => expression switch
    {
        SequenceExpression => true,
        BinaryExpression binary => ContainsSequence(binary.Left) || ContainsSequence(binary.Right),
        UnaryExpression unary => ContainsSequence(unary.Operand),
        NumericConversion conversion => ContainsSequence(conversion.Operand),
        StorageArrayElementReference element => ContainsSequence(element.ArrayReference) || ContainsSequence(element.Index),
        StorageStructPropertyReference property => ContainsSequence(property.Reference),
        FunctionCallExpression call => call.Arguments.Any(ContainsSequence),
        VectorExpression vector => vector.Values.Any(ContainsSequence),
        _ => false
    };

    private Expression ProcessConditionalExpression(
        IConditionalOperation operation,
        Dictionary<ISymbol, Storage> inputs,
        Dictionary<ISymbol, Storage> outputs,
        Compilation compilation,
        Stage stage)
    {
        Expression condition = ProcessExpression(operation.Condition, inputs, outputs, compilation, stage)
            ?? throw new NotSupportedException("The condition did not produce an expression.");
        Expression whenTrue = ProcessExpression(operation.WhenTrue, inputs, outputs, compilation, stage)
            ?? throw new NotSupportedException("The true branch did not produce an expression.");
        Expression whenFalse = ProcessExpression(operation.WhenFalse!, inputs, outputs, compilation, stage)
            ?? throw new NotSupportedException("The false branch did not produce an expression.");

        Storage result = CreateTemporary(
            operation.Type ?? throw new NotSupportedException("The conditional expression has no type."),
            "__select", compilation, stage);
        var resultReference = new StorageReference(result);

        // Solo se evalúa la rama elegida, como en C#.
        return new SequenceExpression(
            [
                new VariableDeclaration(result, null),
                new If(
                    condition,
                    new Assignment(resultReference, whenTrue),
                    new Assignment(resultReference, whenFalse))
            ],
            resultReference);
    }

    private Expression BuildIncrementedValue(
        IIncrementOrDecrementOperation operation,
        Expression current,
        Dictionary<ISymbol, Storage> inputs,
        Dictionary<ISymbol, Storage> outputs,
        Compilation compilation,
        Stage stage)
    {
        // `operator ++` / `operator --` del usuario: se llama a la función que Roslyn resolvió.
        if (operation.OperatorMethod is { MethodKind: MethodKind.UserDefinedOperator } userOperator)
        {
            var (functionId, arguments) = ProcessMethodCall(
                userOperator, [], inputs, outputs, compilation, stage,
                preparedArguments: [current]);

            return new FunctionCallExpression(functionId, arguments);
        }

        Expression one = operation.Target.Type?.SpecialType switch
        {
            SpecialType.System_Int32 => new IntLiteral("1", true),
            SpecialType.System_UInt32 => new IntLiteral("1", false),
            SpecialType.System_Single => new FloatLiteral(1f),
            SpecialType.System_Double => new DoubleLiteral(1d),
            _ => throw new NotSupportedException(
                $"Increment of '{operation.Target.Type}' is not supported.")
        };

        return new BinaryExpression(
            operation.Kind == OperationKind.Increment ? BinaryOperator.Add : BinaryOperator.Subtract,
            current,
            one);
    }

    /// <summary>
    /// `i++` / `++i` usados como valor. El prefijo produce el valor nuevo, el sufijo el anterior;
    /// el destino se evalúa una sola vez.
    /// </summary>
    private Expression ProcessIncrementExpression(
        IIncrementOrDecrementOperation operation,
        Dictionary<ISymbol, Storage> inputs,
        Dictionary<ISymbol, Storage> outputs,
        Compilation compilation,
        Stage stage)
    {
        var prelude = new List<Statement>();
        Expression target = ProcessAssignableTarget(operation.Target, prelude, inputs, outputs, compilation, stage);

        if (operation.IsPostfix)
        {
            Storage previous = CreateTemporary(
                operation.Target.Type ?? throw new NotSupportedException("The increment target has no type."),
                "__old", compilation, stage);
            var previousReference = new StorageReference(previous);

            prelude.Add(new VariableDeclaration(previous, target));
            prelude.Add(new Assignment(
                target,
                BuildIncrementedValue(operation, previousReference, inputs, outputs, compilation, stage)));

            return new SequenceExpression(prelude, previousReference);
        }

        prelude.Add(new Assignment(
            target,
            BuildIncrementedValue(operation, target, inputs, outputs, compilation, stage)));

        return new SequenceExpression(prelude, target);
    }

    private static bool HasSideEffects(Expression expression) => expression switch
    {
        FunctionCallExpression => true,
        SequenceExpression => true,
        BinaryExpression binary => HasSideEffects(binary.Left) || HasSideEffects(binary.Right),
        UnaryExpression unary => HasSideEffects(unary.Operand),
        NumericConversion conversion => HasSideEffects(conversion.Operand),
        StorageArrayElementReference element => HasSideEffects(element.ArrayReference) || HasSideEffects(element.Index),
        StorageStructPropertyReference property => HasSideEffects(property.Reference),
        _ => false
    };

    private static ScalarType? TryGetScalarType(ITypeSymbol? type) => type?.SpecialType switch
    {
        SpecialType.System_Int32 => ScalarType.Int32,
        SpecialType.System_UInt32 => ScalarType.UInt32,
        SpecialType.System_Single => ScalarType.Float32,
        SpecialType.System_Double => ScalarType.Float64,
        _ => null
    };

    /// <summary>Convierte entre escalares numéricos distintos; devuelve la misma expresión si no hace falta.</summary>
    private static Expression ConvertScalar(Expression operand, ITypeSymbol? from, ITypeSymbol? to)
    {
        ScalarType? source = TryGetScalarType(from);
        ScalarType? target = TryGetScalarType(to);

        return source is not null && target is not null && source != target
            ? new NumericConversion(operand, target.Value)
            : operand;
    }

    private static Storage? CreateScalarStorage(ITypeSymbol type, string name, StorageKind kind)
{
    return type.SpecialType switch
    {
        SpecialType.System_Int32 => new IRTypes.Int(name, kind, null) { Bits = 32, Signed = true },
        SpecialType.System_UInt32 => new IRTypes.Int(name, kind, null) { Bits = 32, Signed = false },
        SpecialType.System_Single => new IRTypes.Float(name, kind, null),
        SpecialType.System_Double => new IRTypes.Double(name, kind, null),
        SpecialType.System_Boolean => new IRTypes.Bool(name, kind, null),
        _ => null
    };
}

/// <summary>`array.Length` es la longitud fija del array local: se emite como constante entera.</summary>
private Expression ProcessArrayLength(
    IPropertyReferenceOperation operation,
    Dictionary<ISymbol, Storage> inputs,
    Dictionary<ISymbol, Storage> outputs,
    Compilation compilation,
    Stage stage)
{
    Expression array = ProcessExpression(operation.Instance!, inputs, outputs, compilation, stage)
        ?? throw new NotSupportedException("The array reference did not produce an expression.");

    if (array is StorageReference { Storage: IRTypes.Array { Length: { } length } })
    {
        return new IntLiteral(length.ToString(CultureInfo.InvariantCulture), true);
    }

    throw new NotSupportedException("'Length' is only supported on local arrays with a known length.");
}

/// <summary>
/// El índice debe ser int o uint y, si es constante, caer dentro del array: así el error
/// aparece con su nombre y no como un SPIR-V inválido más adelante.
/// </summary>
private static void ValidateArrayIndex(IOperation indexOperation, Expression array)
{
    if (indexOperation.Type?.SpecialType is not (SpecialType.System_Int32 or SpecialType.System_UInt32))
    {
        throw new NotSupportedException("Array indices must be int or uint.");
    }

    if (indexOperation.ConstantValue is not { HasValue: true, Value: { } constant })
    {
        return;
    }

    long value = Convert.ToInt64(constant, CultureInfo.InvariantCulture);

    if (value < 0)
    {
        throw new NotSupportedException($"Array index {value} is negative.");
    }

    if (array is StorageReference { Storage: IRTypes.Array { Length: { } length } } && value >= length)
    {
        throw new NotSupportedException(
            $"Array index {value} is out of range for an array of length {length}.");
    }
}

private void ProcessLocalArrayDeclaration(
    ILocalSymbol symbol,
    IRTypes.Array arrayStorage,
    IVariableInitializerOperation? initializerOperation,
    Dictionary<ISymbol, Storage> inputs,
    Dictionary<ISymbol, Storage> outputs,
    Compilation compilation,
    Stage stage,
    List<Statement> statements)
{
    if (initializerOperation?.Value is not IArrayCreationOperation creation)
    {
        throw new NotSupportedException(
            $"Array '{symbol.Name}' must be initialized with 'new T[N]' or 'new T[] {{ ... }}'.");
    }

    if (creation.DimensionSizes.Length != 1 ||
        creation.DimensionSizes[0].ConstantValue is not { HasValue: true, Value: int size } ||
        size <= 0)
    {
        throw new NotSupportedException(
            $"The length of array '{symbol.Name}' must be a positive compile-time constant.");
    }

    Storage storage = arrayStorage with { Length = (uint)size };
    _locals.Add(symbol, storage);

    // Sin inicializador explícito, el emisor la deja a cero (como C#).
    statements.Add(new VariableDeclaration(storage, null));

    if (creation.Initializer is null)
    {
        return;
    }

    for (int index = 0; index < creation.Initializer.ElementValues.Length; index++)
    {
        Expression value = ProcessExpression(
                creation.Initializer.ElementValues[index], inputs, outputs, compilation, stage)
            ?? throw new NotSupportedException("An array element did not produce an expression.");

        statements.Add(new Assignment(
            new StorageArrayElementReference(
                new StorageReference(storage),
                new IntLiteral(index.ToString(CultureInfo.InvariantCulture), true)),
            value));
    }
}
}