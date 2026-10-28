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
using IRTypes = YotsubaShader.IR.Intermediate.Types;
namespace YotsubaShader.Roslyn;

public partial class StatementOperationProcessor
{
    private readonly Dictionary<ISymbol, Storage> _locals =
        new(SymbolEqualityComparer.Default);

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
        }

        throw new NotSupportedException(
            $"Expression operation '{initializerOperationValue.Kind}' is not supported yet.");
    }

    private Expression? ProcessBinaryExpression(IBinaryOperation binaryOperation, Dictionary<ISymbol, Storage> inputs, Dictionary<ISymbol, Storage> outputs, Compilation compilation, Stage stage)
    {
        var left = ProcessExpression(binaryOperation.LeftOperand, inputs, outputs, compilation, stage)   ?? throw new NotSupportedException(
            "The left binary operand did not produce an expression.");

        var right = ProcessExpression(binaryOperation.RightOperand, inputs, outputs, compilation, stage) ?? throw new NotSupportedException(
            "The right binary operand did not produce an expression.");

        BinaryOperator binaryOperator = GetBinaryOperator(binaryOperation.OperatorKind);

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
        if(objectCreationOperation.Initializer is not null)
            throw new NotSupportedException(
                "The object initializer syntax is not supported yet");
    
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
            case IRTypes.Vector vector:
                if(objectCreationOperation.Arguments.Length != vector.ComponentsLength)
                    throw new NotSupportedException(
                        $"'{objectCreationOperation.Type}' require " +
                        $"{vector.ComponentsLength} components.");
                var values = objectCreationOperation.Arguments
                    .Select(argument => ProcessExpression(argument.Value, inputs, outputs, compilation, stage))
                    .Cast<Expression>()
                    .ToArray();
                return new VectorExpression(values, vector.ComponentsType);
            case IRTypes.CustomStruct customStruct when objectCreationOperation.Arguments.Length == 0:
                return new CustomStructExpression(customStruct, []);         
            case IRTypes.CustomStruct:
                throw new NotSupportedException(
                    $"Constructor of '{objectCreationOperation.Type}' with arguments " +
                    "is not supported yet.");
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
    
    private Storage CreateLocalStorage(
        ILocalSymbol symbol, 
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
                    components.Add((int)memberAttributes.ConstructorArguments[0].Value!, value);
                }

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
                    switch (vectorComponentsType)
                    {
                        case IRTypes.VectorComponentsType.Float:
                            exp = new FloatLiteral(float.Parse(value));
                            break;
                        case IRTypes.VectorComponentsType.Int:
                            exp = new IntLiteral(value, true);
                            break;
                        case IRTypes.VectorComponentsType.Uint:
                            exp = new IntLiteral(value, false);
                            break;
                        default:
                            throw new NotSupportedException($"ComponentValueToExpression cant find a switch case for: {value}");
                    }

                    return exp;
                }
                        
                Storage vector = new IRTypes.Vector(symbol.Name, storageKind, null)
                {
                    ComponentsDefaultValues = [.. components.Select(x => ComponentValueToExpression(x.Value))],
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

                Storage? strg = null;
                strg = ShaderProcessor.GetStorage(member, strg, null, compilation, stage, true);
                properties.Add(strg);
            }

            storage = new IRTypes.CustomStruct(symbol.Name, storageKind, null)
            {
                Properties = properties
            };
        }

        return storage ?? throw new NotSupportedException(
            $"Type '{type.ToDisplayString()}' used by '{symbol.ToDisplayString()}' is not supported as shader local variable.");
    }

        
    private static BinaryOperator GetBinaryOperator(
        BinaryOperatorKind operatorKind)
    {
        return operatorKind switch
        {
            BinaryOperatorKind.Add => BinaryOperator.Add,
            BinaryOperatorKind.Subtract => BinaryOperator.Subtract,
            BinaryOperatorKind.Multiply => BinaryOperator.Multiply,
            BinaryOperatorKind.Divide => BinaryOperator.Divide,
            BinaryOperatorKind.Remainder => BinaryOperator.Remainder,
            _ => throw new NotSupportedException(
                $"The binary operator '{operatorKind}' is not supported.")
        };
    }
}