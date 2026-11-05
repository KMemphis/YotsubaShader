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

using System.Globalization;
using YotsubaShader.Compiler.SpirV;
using YotsubaShader.Compiler.SpirV.Builder;
using YotsubaShader.Compiler.SpirV.YS_Types;
using YotsubaShader.IR.Intermediate;
using YotsubaShader.IR.Intermediate.FunctionBody;
using YotsubaShader.IR.Intermediate.FunctionBody.Expressions;
using YotsubaShader.IR.Intermediate.FunctionBody.Expressions.AssignmentTargets;
using YotsubaShader.IR.Intermediate.FunctionBody.Expressions.FunctionCalls;
using YotsubaShader.IR.Intermediate.FunctionBody.Expressions.Literals;
using YotsubaShader.IR.Intermediate.FunctionBody.Statements;
using YotsubaShader.IR.Intermediate.Types;
using YotsubaShader.IR.Intermediate.ValueConvertions;

namespace YotsubaShader.Compiler.SpirV;

internal sealed partial class FunctionBodyEmitter
{

    private void EmitLocal(VariableDeclaration declaration, ref Function function)
    {
        Storage storage = declaration.Storage;
        string typeId = _module.DeclareType(storage, function.Stage);

        string pointerTypeId = _module.DeclarePointer(SpirVStorageClass.Function, typeId);
        string pointerId = $"%local_{storage.HumanName}_{function.HumanName}";

        _module.AddResultLine(pointerId, SpirVOpTable.OpVariable, SpirVModuleHelpers.Id(pointerTypeId),
            SpirVInstructionParameter.Enumerant(
                SpirVStorageClass.Function));

        _module.Name(pointerId, storage.HumanName);
        _pointers.Add(storage, pointerId);
        
        if (declaration.Initializer is not null)
        {
            string valueId = EmitValue(declaration.Initializer, ref function);

            _module.AddLine(
                SpirVOpTable.OpStore,
                SpirVModuleHelpers.Id(pointerId),
                SpirVModuleHelpers.Id(valueId));
        }
    }
    
    private void EmitAssignment(Assignment assignment,ref  Function function)
    {
        string targetPointerId = EmitAddress(assignment.Target, ref function);
        string valueId = EmitValue(assignment.Value, ref function);
        
        _module.AddLine(
            SpirVOpTable.OpStore,
            SpirVModuleHelpers.Id(targetPointerId),
            SpirVModuleHelpers.Id(valueId));
        }
    private void EmitReturn(
        Return returnStatement,
        ref Function function)
    {
        if (returnStatement.Value is null)
        {
            _module.AddLine(SpirVOpTable.OpReturn);
            return;
        }

        string valueId =
            EmitValue(returnStatement.Value, ref function);

        Storage? returnOutput = function.Outputs
            .SingleOrDefault(output =>
                output.HumanName == "__return");

        if (function.Stage == Stage.None)
        {
            if (returnOutput is null)
            {
                throw new InvalidOperationException(
                    $"Function '{function.HumanName}' has no return output.");
            }

            if (returnOutput is CustomStruct targetStruct &&
                TryGetStructType(
                    returnStatement.Value,
                    out CustomStruct? sourceStruct) &&
                sourceStruct is not null)
            {
                valueId = RepackStruct(
                    valueId,
                    sourceStruct,
                    targetStruct,
                    ref function);
            }

            _module.AddLine(
                SpirVOpTable.OpReturnValue,
                SpirVModuleHelpers.Id(valueId));

            return;
        }

        // Los entry points escriben en su variable de salida.
        if (returnOutput is null)
        {
            throw new InvalidOperationException(
                $"Function '{function.HumanName}' returns a value " +
                "but has no '__return' output.");
        }

        string returnPointerId =
            EmitAddress(
                new StorageReference(returnOutput),
                ref function);

        if (returnOutput is CustomStruct entryPointStruct &&
            TryGetStructType(
                returnStatement.Value,
                out CustomStruct? entrySourceStruct) &&
            entrySourceStruct is not null)
        {
            valueId = RepackStruct(
                valueId,
                entrySourceStruct,
                entryPointStruct,
                ref function);
        }

        _module.AddLine(
            SpirVOpTable.OpStore,
            SpirVModuleHelpers.Id(returnPointerId),
            SpirVModuleHelpers.Id(valueId));

        _module.AddLine(SpirVOpTable.OpReturn);
    }
    private static bool TryGetStructType(
        Expression expression,
        out CustomStruct? structure)
    {
        structure = expression switch
        {
            StorageReference { Storage: CustomStruct value } => value,

            CustomStructExpression value => value.StructType,

            _ => null
        };

        return structure is not null;
    }
    
    private string RepackStruct(
    string sourceValueId,
    CustomStruct sourceStruct,
    CustomStruct targetStruct,
    ref Function function)
{
    string sourceTypeId = SpirVTableTypesIds.StructID(
        sourceStruct,
        function.Stage);

    string targetTypeId = SpirVTableTypesIds.StructID(
        targetStruct,
        function.Stage);

    if (sourceTypeId == targetTypeId)
    {
        return sourceValueId;
    }

    if (sourceStruct.Properties.Count != targetStruct.Properties.Count)
    {
        throw new InvalidOperationException(
            $"Cannot repack '{sourceStruct.TypeName}' into " +
            $"'{targetStruct.TypeName}': different member counts.");
    }

    if (!_module.IsDefined(sourceTypeId) ||
        !_module.IsDefined(targetTypeId))
    {
        throw new InvalidOperationException(
            "The source or target struct type was not declared.");
    }

    var constructParameters = new List<SpirVInstructionParameter>
    {
        SpirVModuleHelpers.Id(targetTypeId)
    };

    for (int index = 0; index < sourceStruct.Properties.Count; index++)
    {
        Storage sourceMember = sourceStruct.Properties[index];
        Storage targetMember = targetStruct.Properties[index];

        if (sourceMember is CustomStruct ||
            targetMember is CustomStruct)
        {
            throw new NotSupportedException(
                "Nested struct repacking is not supported yet.");
        }

        string memberTypeId = _module.DeclareType(
            targetMember,
            function.Stage);

        if (!_module.IsDefined(memberTypeId))
        {
            throw new InvalidOperationException(
                $"Member type '{memberTypeId}' was not declared.");
        }

        string extractedValueId =
            $"%value_{function.HumanName}_{_nextTemporaryValue++}";

        _module.AddResultLine(
            extractedValueId,
            SpirVOpTable.OpCompositeExtract,
            SpirVModuleHelpers.Id(memberTypeId),
            SpirVModuleHelpers.Id(sourceValueId),
            new SpirVInstructionParameter(
                index.ToString(),
                SpirVInstructionType.UIntValue));

        constructParameters.Add(
            SpirVModuleHelpers.Id(extractedValueId));
    }

    string repackedValueId =
        $"%value_{function.HumanName}_{_nextTemporaryValue++}";

    _module.AddResultLine(
        repackedValueId,
        SpirVOpTable.OpCompositeConstruct,
        [.. constructParameters]);

    return repackedValueId;
}
    
    private string EmitAddress(Expression expression, ref Function function)
    {
        return expression switch
        {
            StorageReference reference =>
                GetStoragePointer(reference.Storage, ref function),

            StorageStructPropertyReference property =>
                EmitStructPropertyAddress(property, ref function),

            _ => throw new NotSupportedException(
                $"Assignment target '{expression.GetType().Name}' is not supported yet.")
        };
    }
    
    private string GetStoragePointer(Storage storage, ref Function function)
    {
        // Local: EmitLocal ya creó su OpVariable.
        if (_pointers.TryGetValue(storage, out string? localPointerId))
        {
            return localPointerId;
        }

        // Input/output: BuildIrTypes ya creó el OpVariable global de interfaz.
        if (storage.Kind is StorageKind.Input or StorageKind.Output)
        {
            return BuildIrTypes.InterfaceVariableId(function, storage);
        }

        // Recursos globales.
        if (storage.Kind is
            StorageKind.Uniform or
            StorageKind.StorageBuffer or
            StorageKind.PushConstant or
            StorageKind.Texture or
            StorageKind.Sampler)
        {
            return BuildIrTypes.GlobalVariableId(storage);
        }

        throw new NotSupportedException(
            $"Storage '{storage.HumanName}' of kind '{storage.Kind}' " +
            "does not have a SPIR-V pointer.");
    }
    
    private string EmitStructPropertyAddress(
    StorageStructPropertyReference property,
    ref Function function)
{
    // Por ahora soporta: variableStruct.Propiedad
    if (property.Reference is not StorageReference baseReference)
    {
        throw new NotSupportedException(
            "Nested struct-property access is not supported yet.");
    }

    if (baseReference.Storage is not CustomStruct structure)
    {
        throw new NotSupportedException(
            $"'{baseReference.Storage.HumanName}' is not a struct.");
    }

    int memberIndex = -1;

    for (int i = 0; i < structure.Properties.Count; i++)
    {
        if (structure.Properties[i].HumanName == property.PropertyName)
        {
            memberIndex = i;
            break;
        }
    }

    if (memberIndex < 0)
    {
        throw new InvalidOperationException(
            $"Struct '{structure.HumanName}' does not have " +
            $"a property named '{property.PropertyName}'.");
    }

    Storage member = structure.Properties[memberIndex];

    string basePointerId = GetStoragePointer(
        baseReference.Storage,
        ref function);

    SpirVStorageClass storageClass = ToSpirVStorageClass(
        baseReference.Storage.Kind);

    // Estos ya deben existir antes de OpFunction.
    string memberTypeId = _module.DeclareType(member, function.Stage);
    string memberPointerTypeId = _module.DeclarePointer(
        storageClass,
        memberTypeId);

    string memberIndexId = SpirVTableTypesIds.UIntDinamicId(memberIndex);

    if (!_module.IsDefined(memberIndexId))
    {
        throw new InvalidOperationException(
            $"Member index constant '{memberIndexId}' was not declared.");
    }

    string resultId =
        $"%address_{function.HumanName}_{_nextTemporaryValue++}";

    _module.AddResultLine(
        resultId,
        SpirVOpTable.OpAccessChain,
        SpirVModuleHelpers.Id(memberPointerTypeId),
        SpirVModuleHelpers.Id(basePointerId),
        SpirVModuleHelpers.Id(memberIndexId));

    return resultId;
}
    
    private static SpirVStorageClass ToSpirVStorageClass(
        StorageKind kind)
    {
        return kind switch
        {
            StorageKind.Local => SpirVStorageClass.Function,
            StorageKind.Input => SpirVStorageClass.Input,
            StorageKind.Output => SpirVStorageClass.Output,
            StorageKind.Uniform => SpirVStorageClass.Uniform,
            StorageKind.StorageBuffer => SpirVStorageClass.StorageBuffer,
            StorageKind.PushConstant => SpirVStorageClass.PushConstant,
            StorageKind.Texture => SpirVStorageClass.UniformConstant,
            StorageKind.Sampler => SpirVStorageClass.UniformConstant,

            _ => throw new NotSupportedException(
                $"Storage kind '{kind}' has no SPIR-V storage class.")
        };
    }
    
    private string EmitValue(Expression expression, ref Function function)
    {
        string id;
        switch (expression)
        {
            case FloatLiteral literal:
                id = $"%float_{unchecked((uint)BitConverter.SingleToInt32Bits(literal.Value)):X8}";
                break;
            case DoubleLiteral literal:
                id = $"%double_{unchecked((ulong)BitConverter.DoubleToInt64Bits(literal.Value)):X16}";
                break;
            case BoolLiteral literal:
                id = $"%bool_{literal.Value}";
                break;
            case IntLiteral literal:
                id = GetIntConstantId(literal);
                break;
            // new VertexOutput() = todos los campos con cero/default.
            case CustomStructExpression structure when structure.Properties.Count == 0:
                id = GetNullStructId(structure, function.Stage);
                break;
            // new Vector2(...), new Vector3(...), new Vector4(...)
            case VectorExpression vector:
                id = EmitVector(vector, ref function);
                break;
            // Para new MiStruct(a, b, c), cuando el frontend lo produzca.
            case CustomStructExpression structure:
                id = EmitStruct(structure, ref function);
                break;
            case NumericConversion numericConversion:
                id = EmitNumericConversion(numericConversion, ref function);
                break;
            
            case StorageReference reference:
                if (_values.TryGetValue(
                        reference.Storage,
                        out string parameterValueId))
                {
                    id = parameterValueId;
                }
                else
                {
                    id = EmitLoad(
                        reference,
                        ref function);
                }
                break;
            case StorageStructPropertyReference property:
                id = EmitStructPropertyValue(property, ref function);
                break;
            default:
                throw new NotSupportedException($"Expression '{expression.GetType().Name}' is not supported yet.");
        }

        if (!_module.IsDefined(id))
        {
            throw new InvalidOperationException(
                $"Value '{id}' was not declared before function emission.");
        }

        return id;
    }
    
    private string EmitStructPropertyValue(
        StorageStructPropertyReference property,
        ref Function function)
    {
        // Caso: vertex.Uv.X
        if (property.Reference is StorageStructPropertyReference vectorProperty &&
            GetStructMember(vectorProperty) is Vector vector &&
            TryGetVectorComponentIndex(property.PropertyName, out uint componentIndex))
        {
            // Primero carga vertex.Uv como Vector2.
            string vectorValueId = EmitStructPropertyValue(
                vectorProperty,
                ref function);

            string componentTypeId = vector.ComponentsType switch
            {
                VectorComponentsType.Float => SpirVTableTypesIds.FloatID,
                VectorComponentsType.Int => SpirVTableTypesIds.IntID,
                VectorComponentsType.Uint => SpirVTableTypesIds.UIntID,
                VectorComponentsType.Double =>
                    SpirVTableTypesIds.FloatDinamicId(64),

                _ => throw new NotSupportedException(
                    $"Vector component type '{vector.ComponentsType}' is not supported.")
            };

            string resultId =
                $"%value_{function.HumanName}_{_nextTemporaryValue++}";

            _module.AddResultLine(
                resultId,
                SpirVOpTable.OpCompositeExtract,
                SpirVModuleHelpers.Id(componentTypeId),
                SpirVModuleHelpers.Id(vectorValueId),
                new SpirVInstructionParameter(
                    componentIndex.ToString(),
                    SpirVInstructionType.UIntValue));

            return resultId;
        }

        // Caso: vertex.Uv o vertex.Position.
        Storage member = GetStructMember(property);

        string pointerId = EmitAddress(property, ref function);
        string memberTypeId = _module.DeclareType(member, function.Stage);

        return EmitLoad(pointerId, memberTypeId, ref function);
    }
    
    private static bool TryGetVectorComponentIndex(
        string propertyName,
        out uint index)
    {
        index = propertyName switch
        {
            "X" or "x" => 0,
            "Y" or "y" => 1,
            "Z" or "z" => 2,
            "W" or "w" => 3,
            _ => uint.MaxValue
        };

        return index != uint.MaxValue;
    }
    
    private static Storage GetStructMember(
        StorageStructPropertyReference property)
    {
        if (property.Reference is not StorageReference reference ||
            reference.Storage is not CustomStruct structure)
        {
            throw new NotSupportedException(
                "Only direct struct members are supported here.");
        }

        foreach (Storage member in structure.Properties)
        {
            if (member.HumanName == property.PropertyName)
            {
                return member;
            }
        }

        throw new InvalidOperationException(
            $"Struct '{structure.HumanName}' has no member " +
            $"named '{property.PropertyName}'.");
    }
    private string EmitLoad(
        StorageReference reference,
        ref Function function)
    {
        string pointerId = EmitAddress(reference, ref function);

        string valueTypeId = _module.DeclareType(reference.Storage, function.Stage);

        if (!_module.IsDefined(valueTypeId))
        {
            throw new InvalidOperationException(
                $"Type '{valueTypeId}' was not declared before function emission.");
        }

        string resultId =
            $"%value_{function.HumanName}_{_nextTemporaryValue++}";

        _module.AddResultLine(
            resultId,
            SpirVOpTable.OpLoad,
            SpirVModuleHelpers.Id(valueTypeId),
            SpirVModuleHelpers.Id(pointerId));

        return resultId;
    }
    
    private static ScalarType GetScalarType(Expression expression)
    {
        return expression switch
        {
            FloatLiteral => ScalarType.Float32,
            DoubleLiteral => ScalarType.Float64,

            IntLiteral { Signed: true, Bits: 32 } => ScalarType.Int32,
            IntLiteral { Signed: false, Bits: 32 } => ScalarType.UInt32,

            NumericConversion conversion => conversion.TargetType,

            _ => throw new NotSupportedException(
                $"Cannot infer the scalar type of '{expression.GetType().Name}'.")
        };
    }
    
    private string EmitNumericConversion(
        NumericConversion conversion,
        ref Function function)
    {
        string operandId = EmitValue(conversion.Operand, ref function);

        ScalarType sourceType = GetScalarType(conversion.Operand);
        ScalarType targetType = conversion.TargetType;

        if (sourceType == targetType)
        {
            return operandId;
        }

        string resultTypeId = targetType switch
        {
            ScalarType.Int32 => SpirVTableTypesIds.IntID,
            ScalarType.UInt32 => SpirVTableTypesIds.UIntID,
            ScalarType.Float32 => SpirVTableTypesIds.FloatID,
            ScalarType.Float64 => SpirVTableTypesIds.FloatDinamicId(64),

            _ => throw new NotSupportedException(
                $"Target type '{targetType}' is not supported.")
        };

        SpirVOperation operation = (sourceType, targetType) switch
        {
            (ScalarType.Int32, ScalarType.Float32) or
                (ScalarType.Int32, ScalarType.Float64) =>
                SpirVOpTable.OpConvertSToF,

            (ScalarType.UInt32, ScalarType.Float32) or
                (ScalarType.UInt32, ScalarType.Float64) =>
                SpirVOpTable.OpConvertUToF,

            (ScalarType.Float32, ScalarType.Int32) or
                (ScalarType.Float64, ScalarType.Int32) =>
                SpirVOpTable.OpConvertFToS,

            (ScalarType.Float32, ScalarType.UInt32) or
                (ScalarType.Float64, ScalarType.UInt32) =>
                SpirVOpTable.OpConvertFToU,

            (ScalarType.Float32, ScalarType.Float64) or
                (ScalarType.Float64, ScalarType.Float32) =>
                SpirVOpTable.OpFConvert,

            _ => throw new NotSupportedException(
                $"Conversion '{sourceType}' → '{targetType}' is not supported yet.")
        };

        string resultId =
            $"%value_{function.HumanName}_{_nextTemporaryValue++}";

        _module.AddResultLine(
            resultId,
            operation,
            SpirVModuleHelpers.Id(resultTypeId),
            SpirVModuleHelpers.Id(operandId));

        return resultId;
    }
    
    private string EmitLoad(
        string pointerId,
        string valueTypeId,
        ref Function function)
    {
        if (!_module.IsDefined(valueTypeId))
        {
            throw new InvalidOperationException(
                $"Type '{valueTypeId}' was not declared before function emission.");
        }

        string resultId =
            $"%value_{function.HumanName}_{_nextTemporaryValue++}";

        _module.AddResultLine(
            resultId,
            SpirVOpTable.OpLoad,
            SpirVModuleHelpers.Id(valueTypeId),
            SpirVModuleHelpers.Id(pointerId));

        return resultId;
    }
    
    
    private static string GetNullStructId(
        CustomStructExpression structure, Stage stage)
    {
        string structTypeId = SpirVTableTypesIds.StructID(
            structure.StructType, stage);

        return $"%null_{structTypeId.TrimStart('%')}";
    }
    
    private string EmitStruct(
        CustomStructExpression structure,
        ref Function function)
    {
        if (structure.Properties.Count != structure.StructType.Properties.Count)
        {
            throw new InvalidOperationException(
                $"Struct '{structure.StructType.HumanName}' expects " +
                $"{structure.StructType.Properties.Count} properties, but received " +
                $"{structure.Properties.Count}.");
        }

        string structTypeId = SpirVTableTypesIds.StructID(
            structure.StructType, function.Stage);

        if (!_module.IsDefined(structTypeId))
        {
            throw new InvalidOperationException(
                $"Struct type '{structTypeId}' was not declared before function emission.");
        }

        string resultId =
            $"%value_{function.HumanName}_{_nextTemporaryValue++}";

        var parameters = new List<SpirVInstructionParameter>
        {
            SpirVModuleHelpers.Id(structTypeId)
        };

        foreach (Expression property in structure.Properties)
        {
            parameters.Add(
                SpirVModuleHelpers.Id(EmitValue(property, ref function)));
        }

        _module.AddResultLine(
            resultId,
            SpirVOpTable.OpCompositeConstruct,
            [.. parameters]);

        return resultId;
    }
    
    private string EmitVector(
        VectorExpression vector,
        ref Function function)
    {
        string vectorTypeId = SpirVTableTypesIds.VectorID(
            vector.Values.Count,
            vector.ComponentsType);

        if (!_module.IsDefined(vectorTypeId))
        {
            throw new InvalidOperationException(
                $"Vector type '{vectorTypeId}' was not declared before function emission.");
        }

        string resultId =
            $"%value_{function.HumanName}_{_nextTemporaryValue++}";

        var parameters = new List<SpirVInstructionParameter>
        {
            SpirVModuleHelpers.Id(vectorTypeId)
        };

        foreach (Expression component in vector.Values)
        {
            string componentId = EmitValue(component, ref function);

            parameters.Add(SpirVModuleHelpers.Id(componentId));
        }

        _module.AddResultLine(
            resultId,
            SpirVOpTable.OpCompositeConstruct,
            [.. parameters]);

        return resultId;
    }
    
    private static string GetIntConstantId(IntLiteral literal)
    {
        return (literal.Bits, literal.Signed) switch
        {
            (16, true) =>
                $"%int16_{unchecked((ushort)short.Parse(literal.Value, CultureInfo.InvariantCulture)):X4}",

            (16, false) =>
                $"%uint16_{ushort.Parse(literal.Value, CultureInfo.InvariantCulture):X4}",

            (32, true) =>
                $"%int_{unchecked((uint)int.Parse(literal.Value, CultureInfo.InvariantCulture)):X8}",

            (32, false) =>
                $"%uint_{uint.Parse(literal.Value, CultureInfo.InvariantCulture):X8}",

            (64, true) =>
                $"%int64_{unchecked((ulong)long.Parse(literal.Value, CultureInfo.InvariantCulture)):X16}",

            (64, false) =>
                $"%uint64_{ulong.Parse(literal.Value, CultureInfo.InvariantCulture):X16}",

            _ => throw new NotSupportedException(
                $"Integer width '{literal.Bits}' is not supported.")
        };
    }

    private string EmitFunctionCall(FunctionCallStatement functionCallStatement, Function function, 
        IReadOnlyList<Function> otherFuncs)
    {
        var dictionary = otherFuncs.ToDictionary(x => x.Id);
     
        if (!dictionary.TryGetValue(
                functionCallStatement.Target,
                out Function functionCalled))
        {
            throw new InvalidOperationException(
                $"No se encontró la función con ID " +
                $"{functionCallStatement.Target.Value}.");
        }
        
        if (functionCalled.Stage != Stage.None)
        {
            throw new NotSupportedException(
                "Only helper functions can be called.");
        }
        
        if (functionCallStatement.Arguments.Count != functionCalled.Inputs.Count)
        {
            throw new InvalidOperationException(
                $"Function '{functionCalled.HumanName}' expects " +
                $"{functionCalled.Inputs.Count} arguments, " +
                $"but received {functionCallStatement.Arguments.Count}.");
        }

        Storage? returnStorage = functionCalled.Outputs.SingleOrDefault(x => x.HumanName == "__return");

        string returnTypeId = returnStorage is null
            ? SpirVTableTypesIds.VoidID
            : _module.DeclareType(returnStorage, Stage.None);

        var parameters = new List<SpirVInstructionParameter>()
        {
            new SpirVInstructionParameter(returnTypeId,
                SpirVInstructionType.IdRef),
            new SpirVInstructionParameter(
                GetFunctionId(functionCalled),
                SpirVInstructionType.IdRef)
        };

        foreach (var expression in functionCallStatement.Arguments)
        {
            string argumentId = EmitValue(expression, ref function);
            parameters.Add(
                SpirVModuleHelpers.Id(argumentId));
        }

        string resultId = $"%call_{function.Id.Value}_{_nextTemporaryValue++}";

        _module.AddResultLine(resultId, SpirVOpTable.OpFunctionCall,
            parameters.ToArray());

        return resultId;
    }
    
    private static string GetFunctionId(Function function)
    {
        return $"%fn_{function.HumanName}_{function.Id.Value}";
    }
}
