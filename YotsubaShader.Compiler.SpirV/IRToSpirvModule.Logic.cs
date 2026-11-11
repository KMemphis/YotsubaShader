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

using YotsubaShader.Compiler.SpirV.Builder;
using YotsubaShader.Compiler.SpirV.YS_Types;
using YotsubaShader.IR.Intermediate;
using YotsubaShader.IR.Intermediate.FunctionBody;
using YotsubaShader.IR.Intermediate.FunctionBody.Expressions;
using YotsubaShader.IR.Intermediate.FunctionBody.Expressions.AssignmentTargets;
using YotsubaShader.IR.Intermediate.FunctionBody.Expressions.FunctionCalls;
using YotsubaShader.IR.Intermediate.FunctionBody.Expressions.Literals;
using YotsubaShader.IR.Intermediate.FunctionBody.Statements;
using YotsubaShader.IR.Intermediate.Slot;
using YotsubaShader.IR.Intermediate.Slot.Types;
using YotsubaShader.IR.Intermediate.Types;
using YotsubaShader.IR.Intermediate.ValueConvertions;
using Array = YotsubaShader.IR.Intermediate.Types.Array;
using Double = YotsubaShader.IR.Intermediate.Types.Double;

namespace YotsubaShader.Compiler.SpirV;

public partial class IRToSpirvModule
{

    private void BuildHeader(SpirVModule module, Shader shader)
    {
        CheckCapabilities(module, shader);
        
        module.AddLine(
            SpirVOpTable.OpExtension,
            new SpirVInstructionParameter(
                "SPV_GOOGLE_decorate_string",
                SpirVInstructionType.StringLiteral));

        module.AddLine(
            SpirVOpTable.OpExtension,
            new SpirVInstructionParameter(
                "SPV_GOOGLE_hlsl_functionality1",
                SpirVInstructionType.StringLiteral));
        
        module.AddLine(SpirVOpTable.OpMemoryModel, new []
        {
            SpirVInstructionParameter.Enumerant(SpirVAddressingModel.Logical),
            SpirVInstructionParameter.Enumerant(SpirVMemoryModel.GLSL450)
        });
    }

    private void CheckCapabilities(SpirVModule module, Shader shader)
    {
        HashSet<SpirVCapability> emittedCapabilities = new HashSet<SpirVCapability>();
        
        foreach (var shaderGlobalResource in shader.GlobalResources)
        {
            AddCapabilityForStorage(shaderGlobalResource, emittedCapabilities);
        }
        
        foreach (var shaderFunction in shader.Functions)
        {
            foreach (var shaderFunctionInput in shaderFunction.Inputs)
            {
                AddCapabilityForStorage(shaderFunctionInput, emittedCapabilities);
            }

            foreach (var shaderFunctionOutput in shaderFunction.Outputs)
            {
                AddCapabilityForStorage(shaderFunctionOutput, emittedCapabilities);
            }
            
            
            foreach (var statement in shaderFunction.Body)
            {
                CheckStatementCapabilities(statement, emittedCapabilities);
            }
        }
        
        module.AddLine(SpirVOpTable.OpCapability, 
            SpirVInstructionParameter.Enumerant(SpirVCapability.Shader));
        
        foreach (SpirVCapability spirVCapability in emittedCapabilities)
        {
            module.AddLine(SpirVOpTable.OpCapability, 
                SpirVInstructionParameter.Enumerant(spirVCapability));
        }
    }

    private static void CheckStatementCapabilities(
        Statement childStatement, 
        HashSet<SpirVCapability> emittedCapabilities)
    {
        switch (childStatement)
        {
            case Block block:
                foreach (var blockStatement in block.Statements)
                {
                    CheckStatementCapabilities(blockStatement, emittedCapabilities);
                }
                break;
            case VariableDeclaration declaration:
                AddCapabilityForStorage(declaration.Storage, emittedCapabilities);
                AddCapabilityForExpression(declaration.Initializer, emittedCapabilities);
                break;
            case Return returnCase:
                AddCapabilityForExpression(returnCase.Value, emittedCapabilities);
                break;
            case Assignment assignment:
                AddCapabilityForExpression(assignment.Target, emittedCapabilities);
                AddCapabilityForExpression(assignment.Value, emittedCapabilities);
                break;
            case If ifStatement:
                AddCapabilityForExpression(ifStatement.Condition, emittedCapabilities);
                CheckStatementCapabilities(ifStatement.Then, emittedCapabilities);
                if (ifStatement.Else is not null)
                {
                    CheckStatementCapabilities(ifStatement.Else, emittedCapabilities);
                }
                break;
            case Loop loop:
                AddCapabilityForExpression(loop.Condition, emittedCapabilities);
                AddCapabilityForExpression(loop.BottomCondition, emittedCapabilities);
                CheckStatementCapabilities(loop.Body, emittedCapabilities);
                CheckStatementCapabilities(loop.Continuing, emittedCapabilities);
                break;
        }
    }

    private static void AddCapabilityForExpression(
        Expression? expression,
        HashSet<SpirVCapability> emittedCapabilities)
    {

        if (expression is null) return;
        switch (expression)
        {
            case DoubleLiteral:
                AddCapability(SpirVCapability.Float64, emittedCapabilities);
                break;
            
            case IntLiteral { Bits: 16 }:
                AddCapability(SpirVCapability.Int16, emittedCapabilities);
                break;

            case IntLiteral { Bits: 64 }:
                AddCapability(SpirVCapability.Int64, emittedCapabilities);
                break;

            case BinaryExpression binary:
                AddCapabilityForExpression(binary.Left, emittedCapabilities);
                AddCapabilityForExpression(binary.Right, emittedCapabilities);
                break;
            case UnaryExpression unary:
                AddCapabilityForExpression(unary.Operand, emittedCapabilities);
                break;
            case SequenceExpression sequence:
                foreach (Statement statement in sequence.Prelude)
                {
                    CheckStatementCapabilities(statement, emittedCapabilities);
                }

                AddCapabilityForExpression(sequence.Value, emittedCapabilities);
                break;
            case VectorExpression vector:
                foreach (Expression value in vector.Values)
                {
                    AddCapabilityForExpression(value, emittedCapabilities);
                }
                break;
            case MatrixLiteral matrixLiteral:
                AddCapability(SpirVCapability.Matrix, emittedCapabilities);
                if (matrixLiteral.MatrixType.ComponentsType == MatrixComponentType.Double)
                {
                    AddCapability(SpirVCapability.Float64, emittedCapabilities);
                }
                break;
            case CustomStructExpression customStructExpression:
                foreach (var property in customStructExpression.Properties)
                {
                    AddCapabilityForExpression(property, emittedCapabilities);
                }
                foreach (var structTypeProperty in customStructExpression.StructType.Properties)
                {
                    AddCapabilityForStorage(structTypeProperty, emittedCapabilities);
                }
                break;
            case ArrayLiteral arr:
                foreach (var arrElement in arr.Elements)
                {
                    AddCapabilityForExpression(arrElement, emittedCapabilities);
                }

                AddCapabilityForStorage(arr.ArrayType.ElementType, emittedCapabilities);
                break;
        }
    }

    private static void AddCapability(SpirVCapability capability, HashSet<SpirVCapability> emittedCapabilities)
    {
        emittedCapabilities.Add(capability);
    }

    private static void AddCapabilityForStorage(Storage shaderGlobalResource, HashSet<SpirVCapability> emittedCapabilities)
    {
        switch (shaderGlobalResource)
        {
            case Double d:
                AddCapability(SpirVCapability.Float64, emittedCapabilities);
                break; 
            case Int { Bits: 16 } int16:
                AddCapability(SpirVCapability.Int16, emittedCapabilities);
                break;
            case Int { Bits: 64 } int16:
                AddCapability(SpirVCapability.Int64, emittedCapabilities);
                break;
            case Matrix matrix:
                AddCapability(SpirVCapability.Matrix, emittedCapabilities);
                switch (matrix.ComponentsType)
                {
                    case MatrixComponentType.Double:
                        AddCapability(SpirVCapability.Float64, emittedCapabilities);
                        break;
                }
                break;
            case Vector vector:
                switch (vector.ComponentsType)
                {
                    case VectorComponentsType.Double:
                        AddCapability(SpirVCapability.Float64, emittedCapabilities);
                        break;
                }
                if(vector.ComponentsDefaultValues is not null)
                foreach (var vectorComponentsDefaultValue in vector.ComponentsDefaultValues)
                {
                    AddCapabilityForExpression(vectorComponentsDefaultValue, emittedCapabilities);
                }
                break;
            case Array array:
                AddCapabilityForStorage(array.ElementType, emittedCapabilities);
                break;
            case CustomStruct customStruct:
                foreach (var customStructProperty in customStruct.Properties)
                {
                    AddCapabilityForStorage(customStructProperty, emittedCapabilities);
                }
                break;
        }
    }

    private void BuildEntryPoints(SpirVModule module, Shader shader)
    {
        foreach (var shaderFunction in shader.Functions)
        {
            string functionId = GetFunctionId(shaderFunction);
            SpirVExecutionModel executionModel;
            switch (shaderFunction.Stage)
            {
                case Stage.Vertex:
                    executionModel = SpirVExecutionModel.Vertex;
                    break;
                case Stage.Fragment:
                    executionModel = SpirVExecutionModel.Fragment;
                    break;
                case Stage.Compute:
                    executionModel = SpirVExecutionModel.GLCompute;
                    break;
                case Stage.None:
                    continue;
                default:
                    throw new NotSupportedException(
                        $"La etapa '{shaderFunction.Stage}' no esta soportada");
            }
            
            var parameters = new List<SpirVInstructionParameter>
            {
                SpirVInstructionParameter.Enumerant(executionModel),
                SpirVModuleHelpers.Id(functionId),
                new(shaderFunction.HumanName, SpirVInstructionType.StringLiteral)
            };

            parameters.AddRange(
                shaderFunction.Inputs
                    .Concat(shaderFunction.Outputs)
                    .Where(storage =>
                        storage.Kind is StorageKind.Input or StorageKind.Output)
                    .SelectMany(storage =>
                        BuildIrTypes.InterfaceVariables(shaderFunction, storage))
                    .Select(leaf => SpirVModuleHelpers.Id(leaf.VariableId)));
            
            module.AddLine(SpirVOpTable.OpEntryPoint, [.. parameters]);

        }
        
        
        foreach (var shaderFunction in shader.Functions)
        {
            
            string functionId = GetFunctionId(shaderFunction);

            if (shaderFunction.Stage == Stage.Fragment)
            {
                module.AddLine(SpirVOpTable.OpExecutionMode,
                    new SpirVInstructionParameter(functionId, SpirVInstructionType.IdRef),
                    SpirVInstructionParameter.Enumerant(SpirVExecutionMode.OriginUpperLeft));
            }

            if (shaderFunction.Stage is Stage.Compute)
            {
                if (shaderFunction.ExtraParams is not ComputeParams compute)
                {
                    throw new InvalidOperationException(
                        $"El compute shader '{shaderFunction.HumanName}' no tiene ComputeParams.");
                }

                module.AddLine(SpirVOpTable.OpExecutionMode,
                    SpirVModuleHelpers.Id(functionId),
                    SpirVInstructionParameter.Enumerant(SpirVExecutionMode.LocalSize),
                    new SpirVInstructionParameter(compute.X.ToString(), SpirVInstructionType.UIntValue),
                    new SpirVInstructionParameter(compute.Y.ToString(), SpirVInstructionType.UIntValue),
                    new SpirVInstructionParameter(compute.Z.ToString(), SpirVInstructionType.UIntValue));
            }

        }
    }


    private void BuildFunctionTypes(SpirVModule module)
    {
       module.DeclareType(typeof(void));
    }

    private static void BuildDebugNames(
        SpirVModule module,
        Shader shader)
    {
        foreach (Storage resource in shader.GlobalResources)
        {
            module.Name(
                BuildIrTypes.GlobalVariableId(resource),
                resource.HumanName);
        }

        foreach (Function function in shader.Functions)
        {
            module.Name(
                GetFunctionId(function),
                function.HumanName);

            if (function.Stage == Stage.None)
                continue;
            
            foreach (Storage storage in function.Inputs.Concat(function.Outputs))
            {
                bool flattened = BuildIrTypes.IsInterfaceStruct(function, storage);

                foreach (var (leaf, variableId) in BuildIrTypes.InterfaceVariables(function, storage))
                {
                    module.Name(
                        variableId,
                        flattened ? $"{storage.HumanName}_{leaf.HumanName}" : storage.HumanName);
                }
            }
        }
    }
    
    private void BuildRequiredConstants(SpirVModule module, Shader shader)
    {
        foreach (var shaderFunction in shader.Functions)
        {
            foreach (var statement in shaderFunction.Body)
            {
                DeclareStatementConstants(module, statement, shaderFunction.Stage);
            }
        }
    }

    private void DeclareStatementConstants(SpirVModule module, Statement statement, Stage stage)
    {
        switch (statement)
        {
            case Block block:
                foreach (var blockStatement in block.Statements)
                {
                    DeclareStatementConstants(module, blockStatement, stage);
                }
                break;
            case VariableDeclaration {Initializer: not null } declaration :
                DeclareExpressionConstants(module, declaration.Initializer, stage);
                break;
            case Assignment assignment:
                DeclareExpressionConstants(module, assignment.Target, stage);
                DeclareExpressionConstants(module, assignment.Value, stage);
                break;
            case Return { Value:not null} @return:
                DeclareExpressionConstants(module, @return.Value, stage);
                break;
            case If ifStatement:
                DeclareExpressionConstants(module, ifStatement.Condition, stage);
                DeclareStatementConstants(module, ifStatement.Then, stage);
                if (ifStatement.Else is not null)
                {
                    DeclareStatementConstants(module, ifStatement.Else, stage);
                }
                break;
            case Loop loop:
                if (loop.Condition is not null)
                {
                    DeclareExpressionConstants(module, loop.Condition, stage);
                }
                if (loop.BottomCondition is not null)
                {
                    DeclareExpressionConstants(module, loop.BottomCondition, stage);
                }
                DeclareStatementConstants(module, loop.Body, stage);
                DeclareStatementConstants(module, loop.Continuing, stage);
                break;
            
        }
    }

    private void DeclareExpressionConstants(SpirVModule module, Expression expression, Stage stage)
    {
        switch (expression)
        {
            case FloatLiteral literal:
                module.DeclareFloatConstant(literal.Value);
                break;
            case BoolLiteral literal:
                module.DeclareBoolConstant(literal.Value);
                break;

            case IntLiteral literal:
                module.DeclareIntConstant(literal.Value, literal.Bits, literal.Signed);
                break;
            
            case DoubleLiteral literal:
                module.DeclareDoubleConstant(literal.Value);
                break;
            case BinaryExpression binaryExpression:
                DeclareExpressionConstants(module, binaryExpression.Left, stage);
                DeclareExpressionConstants(module, binaryExpression.Right, stage);
                break;
            case NumericConversion conversion:
                DeclareExpressionConstants(module, conversion.Operand, stage);
                break;
            case VectorExpression vectorExpression:
                foreach (var vectorExpressionValue in vectorExpression.Values)
                {
                    DeclareExpressionConstants(module, vectorExpressionValue, stage);
                }
                break;  
            case CustomStructExpression structure:
            {
                // new VertexOutput() significa el valor cero/default del struct.
                if (structure.Properties.Count == 0)
                {
                    string structTypeId = module.DeclareType(structure.StructType, stage);
                    string nullId = $"%null_{structTypeId.TrimStart('%')}";

                    if (!module.IsDefined(nullId))
                    {
                        module.AddResultLine(
                            nullId,
                            SpirVOpTable.OpConstantNull,
                            SpirVModuleHelpers.Id(structTypeId));

                        module.Name(nullId, $"{structure.StructType.TypeName}_Null");
                    }

                    break;
                }

                // Para new MiStruct(a, b, c): las partes pueden necesitar
                // constantes escalares, aunque el composite se construya en la función.
                foreach (Expression property in structure.Properties)
                {
                    DeclareExpressionConstants(module, property, stage);
                }

                break;
                
            }
            case FunctionCallExpression call:
                foreach (Expression argument in call.Arguments)
                {
                    DeclareExpressionConstants(module, argument, stage);
                }
                break;
            case StorageArrayElementReference arrayElement:
                DeclareExpressionConstants(module, arrayElement.ArrayReference, stage);
                DeclareExpressionConstants(module, arrayElement.Index, stage);
                break;
            case StorageStructPropertyReference property:
                // a[1].V: el índice de `a[1]` también necesita su constante.
                DeclareExpressionConstants(module, property.Reference, stage);
                break;
            case SequenceExpression sequence:
                foreach (Statement statement in sequence.Prelude)
                {
                    DeclareStatementConstants(module, statement, stage);
                }

                DeclareExpressionConstants(module, sequence.Value, stage);
                break;
            case UnaryExpression unary:
                DeclareExpressionConstants(module, unary.Operand, stage);
                break;
        }
    }

    private static string GetFunctionReturnType(
        SpirVModule module,
        Function function)
    {
        // Los entry points SPIR-V siempre retornan void.
        if (function.Stage != Stage.None)
            return SpirVTableTypesIds.VoidID;

        Storage? returnStorage = function.Outputs
            .SingleOrDefault(storage => storage.HumanName == "__return");

        return returnStorage is null
            ? SpirVTableTypesIds.VoidID
            : module.DeclareType(returnStorage, Stage.None);
    }
    private static string DeclareFunctionType(SpirVModule module, Function function)
    {
        string returnTypeId = GetFunctionReturnType(module, function);

        var parameters = new List<SpirVInstructionParameter>
        {
            new(returnTypeId, SpirVInstructionType.IdRef)
        };

        // Solo las funciones auxiliares tienen parámetros SPIR-V.
        if (function.Stage == Stage.None)
        {
            foreach (Storage input in function.Inputs)
            {
                string inputTypeId = module.DeclareType(input, Stage.None);

                parameters.Add(new SpirVInstructionParameter(inputTypeId, SpirVInstructionType.IdRef));
            }
        }

        // El ID depende de la firma: dos funciones void() comparten el mismo tipo.
        string functionTypeId = "%fn_type_" +
                                string.Join("_", parameters.Select(p => p.Instruction.TrimStart('%')));

        if (module.IsDefined(functionTypeId))
            return functionTypeId;

        module.AddResultLine(functionTypeId, SpirVOpTable.OpTypeFunction, parameters.ToArray());

        return functionTypeId;
    }
    
    private static void BuildFunctionSkeletons(
        SpirVModule module,
        Shader shader)
    {
        foreach (var shaderFunction in shader.Functions)
        {
            string functionId = GetFunctionId(shaderFunction);
            string labelId = $"%label_{shaderFunction.HumanName}_{shaderFunction.Id.Value}";

            var declareFunctionType = DeclareFunctionType(module, shaderFunction);
            var functionReturnType = GetFunctionReturnType(module, shaderFunction);
            
            module.BeginFunction();
            module.AddResultLine(
                functionId,
                SpirVOpTable.OpFunction,
                new SpirVInstructionParameter(
                    functionReturnType,
                    SpirVInstructionType.IdRef),
                SpirVInstructionParameter.Enumerant(SpirVFunctionControl.None),
                new SpirVInstructionParameter(
                    declareFunctionType,
                    SpirVInstructionType.IdRef));

            var pointers = new Dictionary<Storage, string>();
            var values = new Dictionary<Storage, string>();
            
            if (shaderFunction.Stage == Stage.None)
            {
                foreach (Storage input in shaderFunction.Inputs)
                {
                    string inputTypeId =
                        module.DeclareType(input, Stage.None);
                    string parameterId =
                        FunctionParameterId(shaderFunction, input);

                    module.AddResultLine(
                        parameterId,
                        SpirVOpTable.OpFunctionParameter,
                        new SpirVInstructionParameter(
                            inputTypeId,
                            SpirVInstructionType.IdRef));

                    module.Name(
                        parameterId,
                        input.HumanName);
                    
                    values[input] = parameterId;
                }
            }
            
            module.AddResultLine(labelId, SpirVOpTable.OpLabel);

           
            FunctionBodyEmitter emitter = new FunctionBodyEmitter(module, pointers, values);
            emitter.EmitStatements(shaderFunction.Body, shaderFunction, shader.Functions);
            
            emitter.FinishFunction(shaderFunction);

            module.AddLine(SpirVOpTable.OpFunctionEnd);
            
            module.EndFunction();
        }
    }

    
    internal static string FunctionParameterId(
        Function function,
        Storage input)
    {
        return $"%param_{function.Id.Value}_{input.HumanName}";
    }
    private static bool EndsInReturn(IReadOnlyList<Statement> statements)
    {
        return statements.Count > 0 &&
               EndsInReturn(statements[^1]);
    }

    private static bool EndsInReturn(Statement statement)
    {
        return statement switch
        {
            Return => true,
            Block block => EndsInReturn(block.Statements),
            _ => false
        };
    }
    
    private static string GetFunctionId(Function function)
    {
        return $"%fn_{function.HumanName}_{function.Id.Value}";
    }
    private void BuildDecorations(SpirVModule module, Shader shader)
    {
        foreach (var shaderGlobalResource in shader.GlobalResources)
        {
            if(shaderGlobalResource.Slot is not DescriptorSlot descriptor) continue;

            DecorateDescriptor(module, BuildIrTypes.GlobalVariableId(shaderGlobalResource), descriptor);
        }

        foreach (var shaderFunction in shader.Functions)
        {
            if(shaderFunction.Stage == Stage.None) continue;

            foreach (var storage in shaderFunction.Inputs.Concat(shaderFunction.Outputs))
            {
                if (storage.Kind is not (StorageKind.Input or StorageKind.Output))
                {
                    continue;
                }

                // Cada miembro de un struct de interfaz es su propia variable, con su propia decoración.
                foreach (var (leaf, variableId) in BuildIrTypes.InterfaceVariables(shaderFunction, storage))
                {
                    if (leaf.Slot is null)
                    {
                        // Un fragment shader que devuelve un valor sin anotar escribe en el render target 0.
                        if (shaderFunction.Stage == Stage.Fragment &&
                            storage.Kind == StorageKind.Output &&
                            storage is not 
                                CustomStruct)
                        {
                            DecorateVariable(module, variableId, new LocationSlot(0));
                        }

                        continue;
                    }

                    DecorateVariable(module, variableId, leaf.Slot);
                }
            }
        }
    }
    
    private void DecorateDescriptor(SpirVModule module, string globalVariableId, DescriptorSlot descriptor)
    {
        module.AddLine(
            SpirVOpTable.OpDecorate,
            SpirVModuleHelpers.Id(globalVariableId),
            SpirVInstructionParameter.Enumerant(
                SpirVDecoration.DescriptorSet),
            new(
                descriptor.Set.ToString(
                    System.Globalization.CultureInfo.InvariantCulture),
                SpirVInstructionType.UIntValue));
        
        module.AddLine(SpirVOpTable.OpDecorate,
            SpirVModuleHelpers.Id(globalVariableId),
            SpirVInstructionParameter.Enumerant(
                SpirVDecoration.Binding),
            new(descriptor.Binding.ToString(System.Globalization.CultureInfo.InvariantCulture), 
                SpirVInstructionType.UIntValue));
    }

    private void DecorateVariable(SpirVModule module, string variableId, InterfaceSlot storageSlot)
    {
        switch (storageSlot)
        {
            case BuiltInSlot builtInSlot:
                module.AddLine(SpirVOpTable.OpDecorate,
                    SpirVModuleHelpers.Id(variableId),
                    SpirVInstructionParameter.Enumerant(SpirVDecoration.BuiltIn),
                    SpirVInstructionParameter.Enumerant(ToSpirVBuiltIn(builtInSlot.BuiltIn)));
                break;
            case LocationSlot locationSlot:        
                module.AddLine(SpirVOpTable.OpDecorate,
                    SpirVModuleHelpers.Id(variableId),
                    SpirVInstructionParameter.Enumerant(SpirVDecoration.Location),
                    new(locationSlot.Location.ToString(), SpirVInstructionType.UIntValue));
                break;

            case VertexSlot vertexSlot:          
                module.AddLine(SpirVOpTable.OpDecorate,
                    SpirVModuleHelpers.Id(variableId),
                    SpirVInstructionParameter.Enumerant(SpirVDecoration.Location),
                    new(_interfaceLocations[variableId].ToString(), SpirVInstructionType.UIntValue));

                module.AddLine(SpirVOpTable.OpDecorateString,
                    SpirVModuleHelpers.Id(variableId),
                    SpirVInstructionParameter.Enumerant(SpirVDecoration.UserSemantic),
                    new(ToHlslSemantic(vertexSlot.Semantic), SpirVInstructionType.StringLiteral));
                break;
        }
    }
    
    private static SpirVBuiltIn ToSpirVBuiltIn(BuiltIn builtIn) =>
        builtIn switch
        {
            BuiltIn.Position => SpirVBuiltIn.Position,
            BuiltIn.PointSize => SpirVBuiltIn.PointSize,
            BuiltIn.VertexIndex => SpirVBuiltIn.VertexIndex,
            BuiltIn.InstanceIndex => SpirVBuiltIn.InstanceIndex,
            BuiltIn.FragCoord => SpirVBuiltIn.FragCoord,
            BuiltIn.FrontFacing => SpirVBuiltIn.FrontFacing,
            BuiltIn.FragDepth => SpirVBuiltIn.FragDepth,
            BuiltIn.GlobalInvocationId => SpirVBuiltIn.GlobalInvocationId,
            BuiltIn.LocalInvocationId => SpirVBuiltIn.LocalInvocationId,
            BuiltIn.LocalInvocationIndex => SpirVBuiltIn.LocalInvocationIndex,
            BuiltIn.WorkgroupId => SpirVBuiltIn.WorkgroupId,
            BuiltIn.NumWorkgroups => SpirVBuiltIn.NumWorkgroups,
            _ => throw new NotSupportedException(
                $"El built-in '{builtIn}' aún no está soportado por SPIR-V."),
        };
}
