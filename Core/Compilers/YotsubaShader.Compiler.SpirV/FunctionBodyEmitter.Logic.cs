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
using IRTypes = YotsubaShader.IR.Intermediate.Types;
namespace YotsubaShader.Compiler.SpirV;

internal sealed partial class FunctionBodyEmitter
{

    private void EmitLocal(VariableDeclaration declaration, ref Function function)
    {
        Storage storage = declaration.Storage;
        string typeId = _module.DeclareType(storage, function.Stage);

        string pointerTypeId = _module.DeclarePointer(SpirVStorageClass.Function, typeId);
        string pointerId = $"%local_{storage.HumanName}_{function.HumanName}_{function.Id.Value}";

        _module.AddResultLine(pointerId, SpirVOpTable.OpVariable, SpirVModuleHelpers.Id(pointerTypeId),
            SpirVInstructionParameter.Enumerant(
                SpirVStorageClass.Function));

        _module.Name(pointerId, storage.HumanName);
        _pointers.Add(storage, pointerId);
        
        if (storage is IRTypes.Array && declaration.Initializer is null)
        {
            string nullId = $"%null_{typeId.TrimStart('%')}";

            if (!_module.IsDefined(nullId))
            {
                _module.AddResultLine(nullId, SpirVOpTable.OpConstantNull, SpirVModuleHelpers.Id(typeId));

                _module.Name(nullId, $"{typeId.TrimStart('%')}_Null");
            }

            _module.AddLine(
                SpirVOpTable.OpStore,
                SpirVModuleHelpers.Id(pointerId),
                SpirVModuleHelpers.Id(nullId));
        }
        
        if (declaration.Initializer is not null)
        {
            string valueId = ToLayout(EmitValue(declaration.Initializer, ref function), storage, function);

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
        if (TryGetExpressionStorage(assignment.Target) is { } targetStorage)
        {
            valueId = ToLayout(valueId, targetStorage, function);
        }
        
        _module.AddLine(
            SpirVOpTable.OpStore,
            SpirVModuleHelpers.Id(targetPointerId),
            SpirVModuleHelpers.Id(valueId));
        }
    private void EmitReturn(
        Return returnStatement,
        ref Function function)
    {
        if (function.Stage == Stage.None)
        {
            if (returnStatement.Value is null)
            {
                _module.AddLine(SpirVOpTable.OpReturn);
                return;
            }

            string valueId = EmitValue(returnStatement.Value, ref function);

            _module.AddLine(
                SpirVOpTable.OpReturnValue,
                SpirVModuleHelpers.Id(valueId));

            return;
        }

        // Solo el retorno real (__return).
        Storage? returnStorage = function.Outputs
            .SingleOrDefault(storage => storage.HumanName == "__return");

        if (returnStorage is not null && returnStatement.Value is not null)
        {
            string valueId = EmitValue(returnStatement.Value, ref function);

            if (BuildIrTypes.IsInterfaceStruct(function, returnStorage))
            {
                // Struct de salida aplanado: se reparte cada miembro en su variable de interfaz.
                var structure = (CustomStruct)returnStorage;

                for (int index = 0; index < structure.Properties.Count; index++)
                {
                    Storage member = structure.Properties[index];
                    string memberTypeId = _module.DeclareType(member, function.Stage);
                    string extractedId = $"%value_{function.HumanName}_{function.Id.Value}_{_nextTemporaryValue++}";

                    _module.AddResultLine(
                        extractedId,
                        SpirVOpTable.OpCompositeExtract,
                        SpirVModuleHelpers.Id(memberTypeId),
                        SpirVModuleHelpers.Id(valueId),
                        new SpirVInstructionParameter(index.ToString(), SpirVInstructionType.UIntValue));

                    _module.AddLine(
                        SpirVOpTable.OpStore,
                        SpirVModuleHelpers.Id(
                            BuildIrTypes.InterfaceMemberVariableId(function, structure, member)),
                        SpirVModuleHelpers.Id(extractedId));
                }
            }
            else
            {
                string pointerId = GetStoragePointer(returnStorage, ref function);

                _module.AddLine(
                    SpirVOpTable.OpStore,
                    SpirVModuleHelpers.Id(pointerId),
                    SpirVModuleHelpers.Id(valueId));
            }
        }

        // Los parámetros out ya deben haberse almacenado
        // cuando se procesaron sus asignaciones.
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
            $"%value_{function.HumanName}_{function.Id.Value}_{_nextTemporaryValue++}";

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
        $"%value_{function.HumanName}_{function.Id.Value}_{_nextTemporaryValue++}";

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

            StorageArrayElementReference arrayElement =>
                EmitArrayElementAddress(arrayElement, ref function),
            
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
            if (BuildIrTypes.IsInterfaceStruct(function, storage))
            {
                throw new NotSupportedException(
                    $"'{storage.HumanName}' is a stage interface struct; access its members " +
                    "(for example input.Position) instead of using it as a whole value.");
            }

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
        Storage? ownerStorage = TryGetExpressionStorage(property.Reference);

// v.X = ...: la dirección de un componente es un AccessChain sobre el puntero del vector.
        if (ownerStorage is IRTypes.Vector vector &&
            TryGetVectorComponentIndex(vector, property.PropertyName, out uint componentIndex))
        {
            string vectorPointerId = EmitAddress(property.Reference, ref function);
            SpirVStorageClass vectorStorageClass = ToSpirVStorageClass(GetRootStorage(property.Reference).Kind);

            string componentTypeId = ComponentTypeIdOf(vector);
            string componentPointerTypeId = _module.DeclarePointer(vectorStorageClass, componentTypeId);
            string componentIndexId = _module.DeclareUIntConstant((int)componentIndex);

            string componentAddressId = $"%address_{function.HumanName}_{function.Id.Value}_{_nextTemporaryValue++}";

            _module.AddResultLine(
                componentAddressId,
                SpirVOpTable.OpAccessChain,
                SpirVModuleHelpers.Id(componentPointerTypeId),
                SpirVModuleHelpers.Id(vectorPointerId),
                SpirVModuleHelpers.Id(componentIndexId));

            return componentAddressId;
        }

        if (ownerStorage is IRTypes.Vector notAComponentOwner)
        {
            throw NotAVectorComponent(notAComponentOwner, property.PropertyName);
        }

        if (ownerStorage is not CustomStruct structure)
        {
            throw new NotSupportedException(
                $"'{property.PropertyName}' has no address: its owner is neither a struct nor a vector.");
        }

        int memberIndex = IndexOfMember(structure, property.PropertyName);
        Storage member = structure.Properties[memberIndex];

        // Entrada/salida de un entry point: cada miembro es su propia variable de interfaz.
        if (property.Reference is StorageReference { Storage: var owner } &&
            BuildIrTypes.IsInterfaceStruct(function, owner))
        {
            return BuildIrTypes.InterfaceMemberVariableId(function, owner, member);
        }

        string basePointerId = EmitAddress(property.Reference, ref function);

        SpirVStorageClass storageClass = ToSpirVStorageClass(GetRootStorage(property.Reference).Kind);

        string memberTypeId = _module.DeclareType(member, function.Stage);
        string memberPointerTypeId = _module.DeclarePointer(storageClass, memberTypeId);
        string memberIndexId = _module.DeclareUIntConstant(memberIndex);

        string resultId = $"%address_{function.HumanName}_{function.Id.Value}_{_nextTemporaryValue++}";

        _module.AddResultLine(
            resultId,
            SpirVOpTable.OpAccessChain,
            SpirVModuleHelpers.Id(memberPointerTypeId),
            SpirVModuleHelpers.Id(basePointerId),
            SpirVModuleHelpers.Id(memberIndexId));

        return resultId;
    }

    private static string ComponentTypeIdOf(IRTypes.Vector vector) => vector.ComponentsType switch
    {
        VectorComponentsType.Float => SpirVTableTypesIds.FloatID,
        VectorComponentsType.Int => SpirVTableTypesIds.IntID,
        VectorComponentsType.Uint => SpirVTableTypesIds.UIntID,
        VectorComponentsType.Double => SpirVTableTypesIds.FloatDinamicId(64),
        VectorComponentsType.Half => SpirVTableTypesIds.FloatDinamicId(16),
        VectorComponentsType.Bool => SpirVTableTypesIds.BoolID,
        _ => throw new NotSupportedException(
            $"Vector component type '{vector.ComponentsType}' is not supported.")
    };
    
    private static int IndexOfMember(CustomStruct structure, string name)
    {
        for (int index = 0; index < structure.Properties.Count; index++)
        {
            if (structure.Properties[index].HumanName == name)
            {
                return index;
            }
        }

        throw new InvalidOperationException(
            $"Struct '{structure.HumanName}' does not have a property named '{name}'.");
    }

    private static Storage GetRootStorage(Expression expression) => expression switch
    {
        StorageReference reference => reference.Storage,
        StorageStructPropertyReference property => GetRootStorage(property.Reference),
        StorageArrayElementReference element => GetRootStorage(element.ArrayReference),
        _ => throw new NotSupportedException(
            $"'{expression.GetType().Name}' is not a storage location.")
    };

    /// <summary>El tipo (Storage) que describe el valor de una expresión, si se puede determinar.</summary>
    private Storage? TryGetExpressionStorage(Expression expression)
    {
        switch (expression)
        {
            case StorageReference reference:
                return reference.Storage;

            case StorageStructPropertyReference property:
                return TryGetExpressionStorage(property.Reference) is CustomStruct structure
                    ? structure.Properties.FirstOrDefault(member => member.HumanName == property.PropertyName)
                    : null;

            case StorageArrayElementReference element:
                return (TryGetExpressionStorage(element.ArrayReference) as IRTypes.Array)?.ElementType;

            case FunctionCallExpression call:
                return ReturnStorageOf(call.Target);

            case IntrinsicCallExpression intrinsic:
                return intrinsic.ResultType;

            case ZeroValue zero:
                return zero.Type;

            case RuntimeArrayLength:
                return null;

            case SequenceExpression sequence:
                return TryGetExpressionStorage(sequence.Value);

            default:
                return null;
        }
    }

    /// <summary>¿Tiene la expresión una dirección en memoria, o es solo un valor (parámetro por valor, resultado de llamada)?</summary>
    private bool IsAddressable(Expression expression) => expression switch
    {
        StorageReference reference => !_values.ContainsKey(reference.Storage),
        StorageStructPropertyReference property => IsAddressable(property.Reference),
        StorageArrayElementReference element => IsAddressable(element.ArrayReference),
        _ => false
    };

    /// <summary>Un struct de interfaz aplanado, reconstruido como valor a partir de sus variables de interfaz.</summary>
    private string EmitInterfaceStructValue(CustomStruct structure, ref Function function)
    {
        string structTypeId = _module.DeclareType(structure, function.Stage);

        var parameters = new List<SpirVInstructionParameter> { SpirVModuleHelpers.Id(structTypeId) };

        foreach (Storage member in structure.Properties)
        {
            string memberTypeId = _module.DeclareType(member, function.Stage);

            parameters.Add(SpirVModuleHelpers.Id(EmitLoad(
                BuildIrTypes.InterfaceMemberVariableId(function, structure, member),
                memberTypeId,
                ref function)));
        }

        string resultId = $"%value_{function.HumanName}_{function.Id.Value}_{_nextTemporaryValue++}";

        _module.AddResultLine(resultId, SpirVOpTable.OpCompositeConstruct, [.. parameters]);

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
            case HalfLiteral literal:
                id = $"%half_{BitConverter.HalfToUInt16Bits((System.Half)literal.Value):X4}";
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
                if (BuildIrTypes.IsInterfaceStruct(function, reference.Storage))
                {
                    id = EmitInterfaceStructValue((CustomStruct)reference.Storage, ref function);
                }
                else if (_values.TryGetValue(
                        reference.Storage,
                        out var parameterValueId))
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
            case StorageArrayElementReference arrayElement:
                id = EmitArrayElementValue(arrayElement, ref function);
                break;
            case SequenceExpression sequence:
                // Las sentencias previas se emiten en línea y luego se produce el valor.
                foreach (Statement statement in sequence.Prelude)
                {
                    EmitStatement(statement, function, _otherFuncs);
                }

                id = EmitValue(sequence.Value, ref function);
                break;
            case FunctionCallExpression functionCallExpression:
                id = EmitFunctionCall(
                    functionCallExpression.Target,
                    functionCallExpression.Arguments,
                    function,
                    requiresValue: true);
                break;
            case IntrinsicCallExpression intrinsicCall:
                id = EmitIntrinsicValue(intrinsicCall, ref function);
                break;
            case ZeroValue zero:
                id = EmitZeroValue(zero.Type, function);
                break;
            case RuntimeArrayLength runtimeLength:
                id = EmitRuntimeArrayLength(runtimeLength, ref function);
                break;
            case BinaryExpression binary:
                id = EmitBinary(binary, ref function);
                break;
            case UnaryExpression unary:
                id = EmitUnary(unary, ref function);
                break;
            default:
                throw new NotSupportedException($"Expression '{expression.GetType().Name}' is not supported yet.");
        }

        // Un struct o array leído de un buffer tiene el tipo con layout: se copia al tipo sin layout para usarlo.
        if (expression is StorageReference or StorageStructPropertyReference or StorageArrayElementReference &&
            TryGetExpressionStorage(expression) is { } loaded && HasExplicitLayout(loaded))
        {
            id = CopyLogical(id, _module.DeclareType(WithoutLayout(loaded), function.Stage), function);
        }

        if (!_module.IsDefined(id))
        {
            throw new InvalidOperationException(
                $"Value '{id}' was not declared before function emission.");
        }

        return id;
    }
    
    /// <summary>OpArrayLength sobre el struct del buffer y el índice del miembro; C# espera un int.</summary>
    private string EmitRuntimeArrayLength(RuntimeArrayLength length, ref Function function)
    {
        StorageStructPropertyReference member = length.Array;
        if (TryGetExpressionStorage(member.Reference) is not CustomStruct structure)
        {
            throw new NotSupportedException("The length of an unsized array is only available inside a storage buffer.");
        }

        int index = structure.Properties.ToList().FindIndex(p => p.HumanName == member.PropertyName);
        string structPointer = EmitAddress(member.Reference, ref function);

        string unsignedId = $"%value_{function.HumanName}_{function.Id.Value}_{_nextTemporaryValue++}";
        _module.AddResultLine(unsignedId, SpirVOpTable.OpArrayLength,
            SpirVModuleHelpers.Id(_module.DeclareType(typeof(uint))),
            SpirVModuleHelpers.Id(structPointer),
            new SpirVInstructionParameter(index.ToString(), SpirVInstructionType.UIntValue));

        string signedId = $"%value_{function.HumanName}_{function.Id.Value}_{_nextTemporaryValue++}";
        _module.AddResultLine(signedId, SpirVOpTable.OpBitcast,
            SpirVModuleHelpers.Id(_module.DeclareType(typeof(int))), SpirVModuleHelpers.Id(unsignedId));
        return signedId;
    }

    private static bool HasExplicitLayout(Storage storage) =>
        storage.Kind is StorageKind.Uniform or StorageKind.StorageBuffer or StorageKind.PushConstant &&
        storage is CustomStruct or IRTypes.Array;

    /// <summary>El mismo tipo sin layout de buffer: el de las variables locales.</summary>
    private static Storage WithoutLayout(Storage storage) => storage switch
    {
        CustomStruct structure => structure with
        {
            Kind = StorageKind.Local,
            Properties = structure.Properties.Select(WithoutLayout).ToList()
        },
        IRTypes.Array array => array with { Kind = StorageKind.Local, ElementType = WithoutLayout(array.ElementType) },
        _ => storage
    };

    /// <summary>Un valor sin layout que se guarda en un buffer pasa al tipo con layout.</summary>
    private string ToLayout(string valueId, Storage target, Function function) =>
        HasExplicitLayout(target) ? CopyLogical(valueId, _module.DeclareType(target, function.Stage), function) : valueId;

    /// <summary>OpCopyLogical: copia entre dos tipos con la misma forma y distinto layout (SPIR-V 1.4+).</summary>
    private string CopyLogical(string valueId, string targetTypeId, Function function)
    {
        string id = $"%value_{function.HumanName}_{function.Id.Value}_{_nextTemporaryValue++}";
        _module.AddResultLine(id, SpirVOpTable.OpCopyLogical, SpirVModuleHelpers.Id(targetTypeId), SpirVModuleHelpers.Id(valueId));
        return id;
    }

    /// <summary>El valor cero de cualquier tipo: un OpConstantNull compartido por todo el módulo.</summary>
    private string EmitZeroValue(Storage type, Function function)
    {
        string typeId = _module.DeclareType(type, function.Stage);
        string nullId = $"%null_{typeId.TrimStart('%')}";
        if (!_module.IsDefined(nullId))
        {
            _module.AddResultLine(nullId, SpirVOpTable.OpConstantNull, SpirVModuleHelpers.Id(typeId));
        }

        return nullId;
    }

    private string NewLabel(string kind, Function function) =>
    $"%{kind}_{function.Id.Value}_{_nextLabel++}";

private void BeginBlock(string labelId)
{
    _module.AddResultLine(labelId, SpirVOpTable.OpLabel);
    _terminated = false;
}

private void Branch(string targetId)
{
    _module.AddLine(SpirVOpTable.OpBranch, SpirVModuleHelpers.Id(targetId));
    _terminated = true;
}

private void EmitIf(If statement, Function function, IReadOnlyList<Function> otherFuncs)
{
    string conditionId = EmitValue(statement.Condition, ref function);

    string thenLabel = NewLabel("if_then", function);
    string? elseLabel = statement.Else is null ? null : NewLabel("if_else", function);
    string mergeLabel = NewLabel("if_merge", function);

    _module.AddLine(SpirVOpTable.OpSelectionMerge, SpirVModuleHelpers.Id(mergeLabel), NoneControl);
    _module.AddLine(
        SpirVOpTable.OpBranchConditional,
        SpirVModuleHelpers.Id(conditionId),
        SpirVModuleHelpers.Id(thenLabel),
        SpirVModuleHelpers.Id(elseLabel ?? mergeLabel));
    _terminated = true;

    BeginBlock(thenLabel);
    EmitStatement(statement.Then, function, otherFuncs);
    if (!_terminated) Branch(mergeLabel);

    if (elseLabel is not null)
    {
        BeginBlock(elseLabel);
        EmitStatement(statement.Else!, function, otherFuncs);
        if (!_terminated) Branch(mergeLabel);
    }

    BeginBlock(mergeLabel);
}

/// <summary>
/// switch → OpSelectionMerge + OpSwitch. Cada sección es un bloque; las secciones con varios valores comparten
/// bloque. Sin sección por defecto, los valores no listados van directos al merge.
/// </summary>
private void EmitSwitch(Switch statement, Function function, IReadOnlyList<Function> otherFuncs)
{
    string selectorId = EmitValue(statement.Selector, ref function);
    string mergeLabel = NewLabel("switch_merge", function);

    var sectionLabels = statement.Sections.Select(_ => NewLabel("switch_case", function)).ToList();
    int defaultIndex = statement.Sections.ToList().FindIndex(s => s.IsDefault);
    string defaultLabel = defaultIndex >= 0 ? sectionLabels[defaultIndex] : mergeLabel;

    var operands = new List<SpirVInstructionParameter>
    {
        SpirVModuleHelpers.Id(selectorId),
        SpirVModuleHelpers.Id(defaultLabel)
    };

    var seen = new HashSet<uint>();
    for (int i = 0; i < statement.Sections.Count; i++)
    {
        foreach (long value in statement.Sections[i].Values)
        {
            uint literal = unchecked((uint)value);
            if (!seen.Add(literal)) continue;
            operands.Add(new SpirVInstructionParameter(literal.ToString(), SpirVInstructionType.UIntValue));
            operands.Add(SpirVModuleHelpers.Id(sectionLabels[i]));
        }
    }

    _module.AddLine(SpirVOpTable.OpSelectionMerge, SpirVModuleHelpers.Id(mergeLabel), NoneControl);
    _module.AddLine(SpirVOpTable.OpSwitch, operands.ToArray());
    _terminated = true;

    _breakTargets.Push(mergeLabel);
    try
    {
        for (int i = 0; i < statement.Sections.Count; i++)
        {
            BeginBlock(sectionLabels[i]);
            EmitStatement(statement.Sections[i].Body, function, otherFuncs);
            if (!_terminated) Branch(mergeLabel);
        }
    }
    finally
    {
        _breakTargets.Pop();
    }

    BeginBlock(mergeLabel);
}

private void EmitLoop(Loop loop, Function function, IReadOnlyList<Function> otherFuncs)
{
    string header = NewLabel("loop_header", function);
    string? conditionLabel = loop.Condition is null ? null : NewLabel("loop_cond", function);
    string body = NewLabel("loop_body", function);
    string continueLabel = NewLabel("loop_continue", function);
    string merge = NewLabel("loop_merge", function);

    Branch(header);

    BeginBlock(header);
    _module.AddLine(
        SpirVOpTable.OpLoopMerge,
        SpirVModuleHelpers.Id(merge),
        SpirVModuleHelpers.Id(continueLabel),
        NoneControl);
    Branch(conditionLabel ?? body);

    if (conditionLabel is not null)
    {
        BeginBlock(conditionLabel);
        string conditionId = EmitValue(loop.Condition!, ref function);
        _module.AddLine(
            SpirVOpTable.OpBranchConditional,
            SpirVModuleHelpers.Id(conditionId),
            SpirVModuleHelpers.Id(body),
            SpirVModuleHelpers.Id(merge));
        _terminated = true;
    }

    // El loop sigue en la pila durante Continuing: el `break` de un do-while vive ahí.
    _loops.Push((merge, continueLabel));
    _breakTargets.Push(merge);
    try
    {
        BeginBlock(body);
        EmitStatement(loop.Body, function, otherFuncs);
        if (!_terminated) Branch(continueLabel);

        BeginBlock(continueLabel);
        EmitStatement(loop.Continuing, function, otherFuncs);
        if (!_terminated)
        {
            if (loop.BottomCondition is null)
            {
                Branch(header);
            }
            else
            {
                string bottomId = EmitValue(loop.BottomCondition, ref function);
                _module.AddLine(
                    SpirVOpTable.OpBranchConditional,
                    SpirVModuleHelpers.Id(bottomId),
                    SpirVModuleHelpers.Id(header),
                    SpirVModuleHelpers.Id(merge));
                _terminated = true;
            }
        }
    }
    finally
    {
        _loops.Pop();
        _breakTargets.Pop();
    }

    BeginBlock(merge);
}

private enum ValueKind { Int32, UInt32, Float32, Float64, Bool, Float16, Int16, UInt16 }

private static bool ProducesBool(BinaryOperator op) => op is
    BinaryOperator.Equal or BinaryOperator.NotEqual or
    BinaryOperator.Less or BinaryOperator.LessEqual or
    BinaryOperator.Greater or BinaryOperator.GreaterEqual or
    BinaryOperator.LogicalAnd or BinaryOperator.LogicalOr;

private string TypeIdOf(ValueKind kind) => kind switch
{
    ValueKind.Int32 => SpirVTableTypesIds.IntID,
    ValueKind.UInt32 => SpirVTableTypesIds.UIntID,
    ValueKind.Int16 => _module.DeclareType(typeof(short)),
    ValueKind.UInt16 => _module.DeclareType(typeof(ushort)),
    ValueKind.Float32 => SpirVTableTypesIds.FloatID,
    ValueKind.Float64 => SpirVTableTypesIds.FloatDinamicId(64),
    ValueKind.Float16 => SpirVTableTypesIds.FloatDinamicId(16),
    ValueKind.Bool => _module.DeclareType(typeof(bool)),
    _ => throw new NotSupportedException($"Value kind '{kind}' is not supported.")
};

private ValueKind GetValueKind(Expression expression) => expression switch
{
    FloatLiteral => ValueKind.Float32,
    DoubleLiteral => ValueKind.Float64,
    BoolLiteral => ValueKind.Bool,
    IntLiteral { Bits: 32, Signed: true } => ValueKind.Int32,
    IntLiteral { Bits: 32, Signed: false } => ValueKind.UInt32,
    IntLiteral { Bits: 16, Signed: true } => ValueKind.Int16,
    IntLiteral { Bits: 16, Signed: false } => ValueKind.UInt16,
    NumericConversion conversion => KindOf(conversion.TargetType),
    StorageReference reference => KindOf(reference.Storage),
    StorageStructPropertyReference property => KindOfProperty(property),
    StorageArrayElementReference arrayElement => KindOf(GetArrayStorage(arrayElement).ElementType),
    SequenceExpression sequence => GetValueKind(sequence.Value),
    BinaryExpression binary => ProducesBool(binary.Operator) ? ValueKind.Bool : GetValueKind(binary.Left),
    UnaryExpression unary => unary.Operator == UnaryOperator.LogicalNot ? ValueKind.Bool : GetValueKind(unary.Operand),
    FunctionCallExpression call => KindOf(ReturnStorageOf(call.Target)),
    ZeroValue zero => KindOf(zero.Type),
    RuntimeArrayLength => ValueKind.Int32,
    IntrinsicCallExpression intrinsic => KindOf(intrinsic.ResultType
        ?? throw new NotSupportedException($"La intrínseca '{intrinsic.Intrinsic}' no tiene resultado.")),
    _ => throw new NotSupportedException(
        $"Cannot infer the value kind of '{expression.GetType().Name}'.")
};

private static ValueKind KindOf(ScalarType type) => type switch
{
    ScalarType.Int32 => ValueKind.Int32,
    ScalarType.UInt32 => ValueKind.UInt32,
    ScalarType.Int16 => ValueKind.Int16,
    ScalarType.UInt16 => ValueKind.UInt16,
    ScalarType.Float32 => ValueKind.Float32,
    ScalarType.Float64 => ValueKind.Float64,
    _ => throw new NotSupportedException($"Scalar type '{type}' is not supported.")
};

private static ValueKind KindOf(Storage storage) => storage switch
{
    IRTypes.Int { Bits: 32, Signed: true } => ValueKind.Int32,
    IRTypes.Int { Bits: 32, Signed: false } => ValueKind.UInt32,
    IRTypes.Int { Bits: 16, Signed: true } => ValueKind.Int16,
    IRTypes.Int { Bits: 16, Signed: false } => ValueKind.UInt16,
    IRTypes.Float => ValueKind.Float32,
    IRTypes.Double => ValueKind.Float64,
    IRTypes.Half => ValueKind.Float16,
    IRTypes.Bool => ValueKind.Bool,
    _ => throw new NotSupportedException(
        $"Storage '{storage.HumanName}' ({storage.GetType().Name}) is not a supported scalar operand.")
};

private ValueKind KindOfProperty(StorageStructPropertyReference property)
{
    // vertex.Uv.X o v.X: el tipo es el de los componentes del vector.
    if (TryGetExpressionStorage(property.Reference) is IRTypes.Vector vector &&
        TryGetVectorComponentIndex(vector, property.PropertyName, out _))
    {
        return vector.ComponentsType switch
        {
            VectorComponentsType.Float => ValueKind.Float32,
            VectorComponentsType.Int => ValueKind.Int32,
            VectorComponentsType.Uint => ValueKind.UInt32,
            VectorComponentsType.Double => ValueKind.Float64,
            VectorComponentsType.Half => ValueKind.Float16,
            VectorComponentsType.Bool => ValueKind.Bool,
            _ => throw new NotSupportedException($"Vector component type '{vector.ComponentsType}' is not supported.")
        };
    }

    return KindOf(TryGetExpressionStorage(property)
        ?? throw new NotSupportedException(
            $"Cannot determine the type of member '{property.PropertyName}'."));
}

private Storage ReturnStorageOf(FunctionId target)
{
    if (!_functionsById.TryGetValue(target, out Function called))
        throw new InvalidOperationException($"No se encontró la función con ID {target.Value}.");

    return called.Outputs.SingleOrDefault(s => s.HumanName == "__return")
           ?? throw new InvalidOperationException($"La función '{called.HumanName}' no retorna un valor.");
}

private string EmitBinary(BinaryExpression binary, ref Function function)
{
    ValueKind kind = GetValueKind(binary.Left);
    ValueKind rightKind = GetValueKind(binary.Right);

    // El desplazamiento de un shift es un entero (int en C#) aunque el operando sea uint.
    bool isShift = binary.Operator is BinaryOperator.ShiftLeft or BinaryOperator.ShiftRight;

    if (kind != rightKind && !(isShift && rightKind is ValueKind.Int32 or ValueKind.UInt32))
    {
        throw new NotSupportedException(
            $"Operands of '{binary.Operator}' have different types ({kind} and {rightKind}).");
    }

    string leftId = EmitValue(binary.Left, ref function);
    string rightId = EmitValue(binary.Right, ref function);

    string resultTypeId = ProducesBool(binary.Operator)
        ? _module.DeclareType(typeof(bool))
        : TypeIdOf(kind);

    string resultId = $"%value_{function.HumanName}_{function.Id.Value}_{_nextTemporaryValue++}";

    _module.AddResultLine(
        resultId,
        SelectBinaryOperation(binary.Operator, kind),
        SpirVModuleHelpers.Id(resultTypeId),
        SpirVModuleHelpers.Id(leftId),
        SpirVModuleHelpers.Id(rightId));

    return resultId;
}

private static SpirVOperation SelectBinaryOperation(BinaryOperator op, ValueKind kind)
{
    if (kind == ValueKind.Bool)
    {
        return op switch
        {
            BinaryOperator.LogicalAnd => SpirVOpTable.OpLogicalAnd,
            BinaryOperator.LogicalOr => SpirVOpTable.OpLogicalOr,
            BinaryOperator.Equal => SpirVOpTable.OpLogicalEqual,
            BinaryOperator.NotEqual => SpirVOpTable.OpLogicalNotEqual,
            _ => throw new NotSupportedException($"Operator '{op}' is not valid for bool.")
        };
    }

    if (op is BinaryOperator.LogicalAnd or BinaryOperator.LogicalOr)
    {
        throw new NotSupportedException($"Operator '{op}' needs bool operands.");
    }

    bool floating = kind is ValueKind.Float32 or ValueKind.Float64;
    bool signed = kind is ValueKind.Int32 or ValueKind.Int16;

    if (floating && op is BinaryOperator.BitwiseAnd or BinaryOperator.BitwiseOr or
            BinaryOperator.BitwiseXor or BinaryOperator.ShiftLeft or BinaryOperator.ShiftRight)
    {
        throw new NotSupportedException($"Operator '{op}' needs integer operands, not {kind}.");
    }

    return op switch
    {
        BinaryOperator.BitwiseAnd => SpirVOpTable.OpBitwiseAnd,
        BinaryOperator.BitwiseOr => SpirVOpTable.OpBitwiseOr,
        BinaryOperator.BitwiseXor => SpirVOpTable.OpBitwiseXor,
        BinaryOperator.ShiftLeft => SpirVOpTable.OpShiftLeftLogical,
        // >> en C#: aritmético con signo, lógico sin signo.
        BinaryOperator.ShiftRight => signed ? SpirVOpTable.OpShiftRightArithmetic : SpirVOpTable.OpShiftRightLogical,
        BinaryOperator.Add => floating ? SpirVOpTable.OpFAdd : SpirVOpTable.OpIAdd,
        BinaryOperator.Subtract => floating ? SpirVOpTable.OpFSub : SpirVOpTable.OpISub,
        BinaryOperator.Multiply => floating ? SpirVOpTable.OpFMul : SpirVOpTable.OpIMul,
        BinaryOperator.Divide => floating ? SpirVOpTable.OpFDiv : signed ? SpirVOpTable.OpSDiv : SpirVOpTable.OpUDiv,
        // El signo del resto sigue al dividendo, igual que en C#.
        BinaryOperator.Remainder => floating ? SpirVOpTable.OpFRem : signed ? SpirVOpTable.OpSRem : SpirVOpTable.OpUMod,
        BinaryOperator.Equal => floating ? SpirVOpTable.OpFOrdEqual : SpirVOpTable.OpIEqual,
        // En C#, NaN != x es true: por eso Unord.
        BinaryOperator.NotEqual => floating ? SpirVOpTable.OpFUnordNotEqual : SpirVOpTable.OpINotEqual,
        BinaryOperator.Less => floating ? SpirVOpTable.OpFOrdLessThan : signed ? SpirVOpTable.OpSLessThan : SpirVOpTable.OpULessThan,
        BinaryOperator.LessEqual => floating ? SpirVOpTable.OpFOrdLessThanEqual : signed ? SpirVOpTable.OpSLessThanEqual : SpirVOpTable.OpULessThanEqual,
        BinaryOperator.Greater => floating ? SpirVOpTable.OpFOrdGreaterThan : signed ? SpirVOpTable.OpSGreaterThan : SpirVOpTable.OpUGreaterThan,
        BinaryOperator.GreaterEqual => floating ? SpirVOpTable.OpFOrdGreaterThanEqual : signed ? SpirVOpTable.OpSGreaterThanEqual : SpirVOpTable.OpUGreaterThanEqual,
        _ => throw new NotSupportedException($"Binary operator '{op}' is not supported.")
    };
}

private string EmitUnary(UnaryExpression unary, ref Function function)
{
    ValueKind kind = GetValueKind(unary.Operand);
    string operandId = EmitValue(unary.Operand, ref function);

    (SpirVOperation operation, string typeId) = unary.Operator switch
    {
        UnaryOperator.LogicalNot when kind == ValueKind.Bool =>
            (SpirVOpTable.OpLogicalNot, _module.DeclareType(typeof(bool))),
        UnaryOperator.BitwiseNot when kind is ValueKind.Int32 or ValueKind.UInt32 or ValueKind.Int16 or ValueKind.UInt16 =>
            (SpirVOpTable.OpNot, TypeIdOf(kind)),
        UnaryOperator.Negate when kind is ValueKind.Int32 or ValueKind.Int16 =>
            (SpirVOpTable.OpSNegate, TypeIdOf(kind)),
        UnaryOperator.Negate when kind is ValueKind.Float32 or ValueKind.Float64 =>
            (SpirVOpTable.OpFNegate, TypeIdOf(kind)),
        _ => throw new NotSupportedException(
            $"Unary '{unary.Operator}' is not supported for {kind}.")
    };

    string resultId = $"%value_{function.HumanName}_{function.Id.Value}_{_nextTemporaryValue++}";

    _module.AddResultLine(
        resultId,
        operation,
        SpirVModuleHelpers.Id(typeId),
        SpirVModuleHelpers.Id(operandId));

    return resultId;
}

    private string EmitStructPropertyValue(
        StorageStructPropertyReference property,
        ref Function function)
    {
        Storage? ownerStorage = TryGetExpressionStorage(property.Reference);

        // v.X sobre cualquier valor vectorial: local, parámetro, miembro de struct, elemento de array...
        if (ownerStorage is IRTypes.Vector vector &&
            TryGetVectorComponentIndex(vector, property.PropertyName, out uint componentIndex))
        {
            string vectorValueId = EmitValue(property.Reference, ref function);

            string componentTypeId = vector.ComponentsType switch
            {
                VectorComponentsType.Float => SpirVTableTypesIds.FloatID,
                VectorComponentsType.Int => SpirVTableTypesIds.IntID,
                VectorComponentsType.Uint => SpirVTableTypesIds.UIntID,
                VectorComponentsType.Double => SpirVTableTypesIds.FloatDinamicId(64),
                VectorComponentsType.Half => SpirVTableTypesIds.FloatDinamicId(16),
                VectorComponentsType.Bool => SpirVTableTypesIds.BoolID,

                _ => throw new NotSupportedException(
                    $"Vector component type '{vector.ComponentsType}' is not supported.")
            };

            string componentId = $"%value_{function.HumanName}_{function.Id.Value}_{_nextTemporaryValue++}";

            _module.AddResultLine(
                componentId,
                SpirVOpTable.OpCompositeExtract,
                SpirVModuleHelpers.Id(componentTypeId),
                SpirVModuleHelpers.Id(vectorValueId),
                new SpirVInstructionParameter(componentIndex.ToString(), SpirVInstructionType.UIntValue));

            return componentId;
        }

        if (ownerStorage is IRTypes.Vector notAComponentOwner)
        {
            throw NotAVectorComponent(notAComponentOwner, property.PropertyName);
        }

        if (ownerStorage is not CustomStruct structure)
        {
            throw new NotSupportedException(
                $"'{property.PropertyName}' is neither a struct member nor a vector component.");
        }

        int memberIndex = IndexOfMember(structure, property.PropertyName);
        Storage member = structure.Properties[memberIndex];
        string memberTypeId = _module.DeclareType(member, function.Stage);

        if (IsAddressable(property.Reference))
        {
            string pointerId = EmitAddress(property, ref function);

            return EmitLoad(pointerId, memberTypeId, ref function);
        }

        // Un valor sin dirección (parámetro por valor, resultado de una llamada): se extrae del compuesto.
        string structValueId = EmitValue(property.Reference, ref function);
        string extractedId = $"%value_{function.HumanName}_{function.Id.Value}_{_nextTemporaryValue++}";

        _module.AddResultLine(
            extractedId,
            SpirVOpTable.OpCompositeExtract,
            SpirVModuleHelpers.Id(memberTypeId),
            SpirVModuleHelpers.Id(structValueId),
            new SpirVInstructionParameter(memberIndex.ToString(), SpirVInstructionType.UIntValue));

        return extractedId;
    }

    /// <summary>
    /// El índice de un componente por su nombre. Se usa el índice declarado con [VectorComponent]
    /// (no el orden X, Y, Z, W) y un componente que el vector no tiene nunca resuelve a un índice.
    /// </summary>
    private static bool TryGetVectorComponentIndex(
        IRTypes.Vector vector,
        string propertyName,
        out uint index)
    {
        if (vector.ComponentNames is { } names)
        {
            for (int i = 0; i < names.Count; i++)
            {
                if (names[i] == propertyName)
                {
                    index = (uint)i;
                    return true;
                }
            }

            index = 0;
            return false;
        }

        index = propertyName switch
        {
            "X" or "x" => 0,
            "Y" or "y" => 1,
            "Z" or "z" => 2,
            "W" or "w" => 3,
            _ => uint.MaxValue
        };

        return index < (uint)vector.ComponentsLength;
    }

    private static NotSupportedException NotAVectorComponent(IRTypes.Vector vector, string propertyName) =>
        new($"'{propertyName}' is not a component of this {vector.ComponentsLength}-component vector" +
            (vector.ComponentNames is { } names ? $" (its components are {string.Join(", ", names)})." : "."));

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
            $"%value_{function.HumanName}_{function.Id.Value}_{_nextTemporaryValue++}";

        _module.AddResultLine(
            resultId,
            SpirVOpTable.OpLoad,
            SpirVModuleHelpers.Id(valueTypeId),
            SpirVModuleHelpers.Id(pointerId));

        return resultId;
    }
    
    private ScalarType GetScalarType(Expression expression)
    {
        return GetValueKind(expression) switch
        {
            ValueKind.Int32 => ScalarType.Int32,
            ValueKind.UInt32 => ScalarType.UInt32,
            ValueKind.Int16 => ScalarType.Int16,
            ValueKind.UInt16 => ScalarType.UInt16,
            ValueKind.Float32 => ScalarType.Float32,
            ValueKind.Float64 => ScalarType.Float64,

            _ => throw new NotSupportedException(
                $"Cannot convert a value of kind '{GetValueKind(expression)}'.")
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
            ScalarType.Int16 => _module.DeclareType(typeof(short)),
            ScalarType.UInt16 => _module.DeclareType(typeof(ushort)),
            ScalarType.Int32 => SpirVTableTypesIds.IntID,
            ScalarType.UInt32 => SpirVTableTypesIds.UIntID,
            ScalarType.Float32 => SpirVTableTypesIds.FloatID,
            ScalarType.Float64 => SpirVTableTypesIds.FloatDinamicId(64),

            _ => throw new NotSupportedException(
                $"Target type '{targetType}' is not supported.")
        };

        static int IntBits(ScalarType t) => t switch
        {
            ScalarType.Int16 or ScalarType.UInt16 => 16,
            ScalarType.Int32 or ScalarType.UInt32 => 32,
            _ => 0
        };

        static bool IsSigned(ScalarType t) => t is ScalarType.Int16 or ScalarType.Int32;
        static bool IsFloat(ScalarType t) => t is ScalarType.Float32 or ScalarType.Float64;

        SpirVOperation operation;
        if (IntBits(sourceType) > 0 && IntBits(targetType) > 0)
        {
            // Mismo ancho: solo cambia el signo. Distinto ancho: extiende según el signo del origen.
            operation = IntBits(sourceType) == IntBits(targetType)
                ? SpirVOpTable.OpBitcast
                : IsSigned(sourceType) ? SpirVOpTable.OpSConvert : SpirVOpTable.OpUConvert;
        }
        else if (IntBits(sourceType) > 0 && IsFloat(targetType))
        {
            operation = IsSigned(sourceType) ? SpirVOpTable.OpConvertSToF : SpirVOpTable.OpConvertUToF;
        }
        else if (IsFloat(sourceType) && IntBits(targetType) > 0)
        {
            operation = IsSigned(targetType) ? SpirVOpTable.OpConvertFToS : SpirVOpTable.OpConvertFToU;
        }
        else if (IsFloat(sourceType) && IsFloat(targetType))
        {
            operation = SpirVOpTable.OpFConvert;
        }
        else
        {
            throw new NotSupportedException($"Conversion '{sourceType}' → '{targetType}' is not supported yet.");
        }

        string resultId =
            $"%value_{function.HumanName}_{function.Id.Value}_{_nextTemporaryValue++}";

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
            $"%value_{function.HumanName}_{function.Id.Value}_{_nextTemporaryValue++}";

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
            $"%value_{function.HumanName}_{function.Id.Value}_{_nextTemporaryValue++}";

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
            $"%value_{function.HumanName}_{function.Id.Value}_{_nextTemporaryValue++}";

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

    private string EmitFunctionCall(
        FunctionId target,
        IReadOnlyList<Expression> arguments,
        Function caller,
        bool requiresValue)
    {
        if (!_functionsById.TryGetValue(
                target,
                out Function functionCalled))
        {
            throw new InvalidOperationException(
                $"No se encontró la función con ID {target.Value}.");
        }

        if (functionCalled.Stage != Stage.None)
        {
            throw new NotSupportedException(
                "Only helper functions can be called.");
        }

        if (arguments.Count != functionCalled.Inputs.Count)
        {
            throw new InvalidOperationException(
                $"Function '{functionCalled.HumanName}' expects " +
                $"{functionCalled.Inputs.Count} arguments, " +
                $"but received {arguments.Count}.");
        }

        Storage? returnStorage = functionCalled.Outputs
            .SingleOrDefault(storage =>
                storage.HumanName == "__return");

        if (requiresValue && returnStorage is null)
        {
            throw new InvalidOperationException(
                $"La función '{functionCalled.HumanName}' " +
                "no retorna un valor.");
        }

        string returnTypeId = returnStorage is null
            ? SpirVTableTypesIds.VoidID
            : _module.DeclareType(
                returnStorage,
                Stage.None);

        var parameters = new List<SpirVInstructionParameter>
        {
            new(
                returnTypeId,
                SpirVInstructionType.IdRef),

            new(
                GetFunctionId(functionCalled),
                SpirVInstructionType.IdRef)
        };

        // ref/in/out: se pasa una variable temporal (SPIR-V lógico solo admite punteros a variables completas)
        // y después de la llamada su valor se copia al argumento original.
        var copyBack = new List<(Expression Target, string Temporary, string TypeId)>();
        for (int i = 0; i < arguments.Count; i++)
        {
            Expression argument = arguments[i];
            ParameterMode mode = functionCalled.ModeOf(i);

            if (mode == ParameterMode.Value)
            {
                parameters.Add(SpirVModuleHelpers.Id(EmitValue(argument, ref caller)));
                continue;
            }

            // out var x: la declaración se emite antes de la llamada.
            while (argument is SequenceExpression sequence)
            {
                foreach (Statement statement in sequence.Prelude) EmitStatement(statement, caller, _otherFuncs);
                argument = sequence.Value;
            }

            Storage parameter = functionCalled.Inputs[i];
            string typeId = _module.DeclareType(parameter, Stage.None);
            string pointerTypeId = _module.DeclarePointer(SpirVStorageClass.Function, typeId);
            string temporary = $"%byref_{caller.HumanName}_{caller.Id.Value}_{_nextTemporaryValue++}";
            _module.AddResultLine(temporary, SpirVOpTable.OpVariable, SpirVModuleHelpers.Id(pointerTypeId),
                SpirVInstructionParameter.Enumerant(SpirVStorageClass.Function));

            if (mode is ParameterMode.Ref or ParameterMode.In)
            {
                string value = EmitValue(argument, ref caller);
                _module.AddLine(SpirVOpTable.OpStore, SpirVModuleHelpers.Id(temporary), SpirVModuleHelpers.Id(value));
            }

            // Un valor temporal (resultado de una llamada...) no tiene dónde copiarse de vuelta.
            if (mode is ParameterMode.Ref or ParameterMode.Out && IsAddressable(argument))
            {
                copyBack.Add((argument, temporary, typeId));
            }

            parameters.Add(SpirVModuleHelpers.Id(temporary));
        }

        string resultId =
            $"%call_{caller.Id.Value}_{_nextTemporaryValue++}";

        _module.AddResultLine(
            resultId,
            SpirVOpTable.OpFunctionCall,
            parameters.ToArray());

        foreach (var (copyTarget, temporary, typeId) in copyBack)
        {
            string value = $"%value_{caller.HumanName}_{caller.Id.Value}_{_nextTemporaryValue++}";
            _module.AddResultLine(value, SpirVOpTable.OpLoad, SpirVModuleHelpers.Id(typeId), SpirVModuleHelpers.Id(temporary));
            string address = EmitAddress(copyTarget, ref caller);
            _module.AddLine(SpirVOpTable.OpStore, SpirVModuleHelpers.Id(address), SpirVModuleHelpers.Id(value));
        }

        return resultId;
    }
    
    
    private static string GetFunctionId(Function function)
    {
        return $"%fn_{function.HumanName}_{function.Id.Value}";
    }
    
    
    private IRTypes.Array GetArrayStorage(StorageArrayElementReference element)
    {
        // Variable, campo de un struct (buffer.Items[i]) o elemento de otro array.
        if (TryGetExpressionStorage(element.ArrayReference) is not IRTypes.Array array)
        {
            throw new NotSupportedException("Only arrays can be indexed.");
        }

        return array;
    }

    private string EmitArrayElementAddress(StorageArrayElementReference element, ref Function function)
    {
        IRTypes.Array array = GetArrayStorage(element);

        if (GetValueKind(element.Index) is not (ValueKind.Int32 or ValueKind.UInt32))
        {
            throw new NotSupportedException("Array indices must be int or uint.");
        }

        string basePointerId = EmitAddress(element.ArrayReference, ref function);
        string indexId = EmitValue(element.Index, ref function);

        string elementTypeId = _module.DeclareType(array.ElementType, function.Stage);
        // La clase de almacenamiento es la de la variable raíz: buffer.Items[i] vive en StorageBuffer.
        string pointerTypeId = _module.DeclarePointer(
            _pointers.ContainsKey(GetRootStorage(element.ArrayReference)) && GetRootStorage(element.ArrayReference) is var root &&
            root.Kind is StorageKind.Local
                ? SpirVStorageClass.Function
                : ToSpirVStorageClass(GetRootStorage(element.ArrayReference).Kind), elementTypeId);

        string resultId = $"%address_{function.HumanName}_{function.Id.Value}_{_nextTemporaryValue++}";

        _module.AddResultLine(
            resultId,
            SpirVOpTable.OpAccessChain,
            SpirVModuleHelpers.Id(pointerTypeId),
            SpirVModuleHelpers.Id(basePointerId),
            SpirVModuleHelpers.Id(indexId));

        return resultId;
    }

    private string EmitArrayElementValue(StorageArrayElementReference element, ref Function function)
    {
        IRTypes.Array array = GetArrayStorage(element);

        string pointerId = EmitArrayElementAddress(element, ref function);
        string elementTypeId = _module.DeclareType(array.ElementType, function.Stage);

        return EmitLoad(pointerId, elementTypeId, ref function);
    }
}
