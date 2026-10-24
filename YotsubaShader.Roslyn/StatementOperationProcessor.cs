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
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Globalization;
using YotsubaShader.IR.Intermediate.FunctionBody.Expressions.AssignmentTargets;
using YotsubaShader.IR.Intermediate.FunctionBody.Expressions.Literals;
using YotsubaShader.IR.Intermediate.Slot;
using YotsubaShader.Language.Native.Types.Vector;
using YotsubaShader.Language.Native.Types.Vector.Exceptions;
using IRTypes = YotsubaShader.IR.Intermediate.Types;
namespace YotsubaShader.Roslyn;

public class StatementOperationProcessor : IStatementOperationProcessor
{
    
    private readonly Dictionary<ISymbol, Storage> locals =
        new(SymbolEqualityComparer.Default);
    
    public void ProcessStatement(
        IOperation op,
        Dictionary<ISymbol, Storage> inputs,
        Dictionary<ISymbol, Storage> outputs,
        Compilation compilation,
        Stage stage,
        out Statement? statement)
    {
        statement = null;

        if (op is IExpressionStatementOperation expressionStatement)
        {
            op = expressionStatement.Operation;
        }

        switch (op)
        {
            case IBlockOperation blockOperation:
                ProcessBlockOperation(blockOperation, inputs, outputs, compilation, stage, out statement);
                break;
            case IVariableDeclarationGroupOperation variableDeclarationGroupOperation:
                ProcessVariableDeclarationGroupOperation(variableDeclarationGroupOperation, inputs, outputs, compilation,stage, out statement);
                break;
            case ICoalesceAssignmentOperation:
                throw new NotImplementedException(
                    nameof(ProcessCoalesceAssignmentOperation));

            case ICompoundAssignmentOperation:
                throw new NotImplementedException(
                    nameof(ProcessCompoundAssignmentOperation));

            case IDeconstructionAssignmentOperation:
                throw new NotImplementedException(
                    nameof(ProcessDeconstructionAssignmentOperation));

            case ISimpleAssignmentOperation:
                throw new NotImplementedException(
                    nameof(ProcessSimpleAssignmentOperation));

            case IIncrementOrDecrementOperation:
                throw new NotImplementedException(
                    nameof(ProcessIncrementOrDecrementOperation));

            case IInvocationOperation:
                throw new NotImplementedException(
                    nameof(ProcessInvocationOperation));

            case IDynamicInvocationOperation:
                throw new NotImplementedException(
                    nameof(ProcessDynamicInvocationOperation));

            case IFunctionPointerInvocationOperation:
                throw new NotImplementedException(
                    nameof(ProcessFunctionPointerInvocationOperation));

            case IObjectCreationOperation:
                throw new NotImplementedException(
                    nameof(ProcessObjectCreationOperation));

            case IDynamicObjectCreationOperation:
                throw new NotImplementedException(
                    nameof(ProcessDynamicObjectCreationOperation));

            case IConditionalAccessOperation:
                throw new NotImplementedException(
                    nameof(ProcessConditionalAccessOperation));

            case IAwaitOperation:
                throw new NotImplementedException(
                    nameof(ProcessAwaitOperation));

            case IReturnOperation:
                throw new NotImplementedException(
                    nameof(ProcessReturnOperation));

            case IConditionalOperation:
                throw new NotImplementedException(
                    nameof(ProcessConditionalOperation));

            case IForLoopOperation:
                throw new NotImplementedException(
                    nameof(ProcessForLoopOperation));

            case IForEachLoopOperation:
                throw new NotImplementedException(
                    nameof(ProcessForEachLoopOperation));

            case IWhileLoopOperation:
                throw new NotImplementedException(
                    nameof(ProcessWhileLoopOperation));

            case ISwitchOperation:
                throw new NotImplementedException(
                    nameof(ProcessSwitchOperation));

            case IBranchOperation:
                throw new NotImplementedException(
                    nameof(ProcessBranchOperation));

            case ILabeledOperation:
                throw new NotImplementedException(
                    nameof(ProcessLabeledOperation));

            case ITryOperation:
                throw new NotImplementedException(
                    nameof(ProcessTryOperation));

            case IThrowOperation:
                throw new NotImplementedException(
                    nameof(ProcessThrowOperation));

            case IUsingOperation:
                throw new NotImplementedException(
                    nameof(ProcessUsingOperation));

            case IUsingDeclarationOperation:
                throw new NotImplementedException(
                    nameof(ProcessUsingDeclarationOperation));

            case ILockOperation:
                throw new NotImplementedException(
                    nameof(ProcessLockOperation));

            case { Syntax: FixedStatementSyntax }:
                throw new NotImplementedException(
                    nameof(ProcessFixedOperation));

            case ILocalFunctionOperation:
                throw new NotImplementedException(
                    nameof(ProcessLocalFunctionOperation));

            case IEventAssignmentOperation:
                throw new NotImplementedException(
                    nameof(ProcessEventAssignmentOperation));

            case IEmptyOperation:
                throw new NotImplementedException(
                    nameof(ProcessEmptyOperation));

            case IInvalidOperation:
                throw new NotImplementedException(
                    nameof(ProcessInvalidOperation));

            default:
                throw new NotImplementedException(
                    $"Statement operation '{op.Kind}' has no processor.");
        }
    }
        
    public void ProcessBlockOperation(IBlockOperation operation, Dictionary<ISymbol, Storage> inputs, Dictionary<ISymbol, Storage> outputs, Compilation compilation, Stage stage, out Statement? statement)
    {
        var statements = new List<Statement>();

        foreach (var op in operation.ChildOperations)
        {
            ProcessStatement(op, inputs, outputs, compilation,stage, out var childStatement);

            if (childStatement is not null)
            {
                statements.Add(childStatement);
            }
        }

        statement = new Block(statements);
    }

    public void ProcessVariableDeclarationGroupOperation(IVariableDeclarationGroupOperation operation, Dictionary<ISymbol, Storage> inputs,
        Dictionary<ISymbol, Storage> outputs, Compilation compilation, Stage stage, out Statement? statement)
    {
        var statements = new List<Statement>();
        foreach (var variableDeclarationOperation in operation.Declarations)
        {
            foreach (var variableDeclaratorOperation in variableDeclarationOperation.Declarators)
            {
                var symbol = variableDeclaratorOperation.Symbol;
                var storage = CreateLocalStorage(symbol, compilation, stage);
                
                locals.Add(symbol, storage);

                var initializerOperation =
                    variableDeclaratorOperation.Initializer ?? variableDeclarationOperation.Initializer;

                Expression? initializer = null;
                if (initializerOperation is not null)
                {
                    initializer = ProcessExpression(initializerOperation.Value, inputs, outputs);
                }
                
                statements.Add(new VariableDeclaration(storage, initializer));
            }
        }

        throw new NotImplementedException();
    }

    private Expression? ProcessExpression(IOperation initializerOperationValue, Dictionary<ISymbol, Storage> inputs, Dictionary<ISymbol, Storage> outputs)
    {
        throw new NotImplementedException();
    }

    private Storage CreateLocalStorage(ILocalSymbol symbol, Compilation compilation, Stage stage)
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
                        
                Storage vector = new IRTypes.Vector(symbol.Name, storageKind, null)
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

    public void ProcessSimpleAssignmentOperation(ISimpleAssignmentOperation operation, Dictionary<ISymbol, Storage> inputs, Dictionary<ISymbol, Storage> outputs,
        out Statement? statement)
    {
        throw new NotImplementedException();
    }

    public void ProcessCompoundAssignmentOperation(ICompoundAssignmentOperation operation, Dictionary<ISymbol, Storage> inputs, Dictionary<ISymbol, Storage> outputs,
        out Statement? statement)
    {
        throw new NotImplementedException();
    }

    public void ProcessCoalesceAssignmentOperation(ICoalesceAssignmentOperation operation, Dictionary<ISymbol, Storage> inputs, Dictionary<ISymbol, Storage> outputs,
        out Statement? statement)
    {
        throw new NotImplementedException();
    }

    public void ProcessDeconstructionAssignmentOperation(IDeconstructionAssignmentOperation operation, Dictionary<ISymbol, Storage> inputs,
        Dictionary<ISymbol, Storage> outputs, out Statement? statement)
    {
        throw new NotImplementedException();
    }

    public void ProcessIncrementOrDecrementOperation(IIncrementOrDecrementOperation operation, Dictionary<ISymbol, Storage> inputs,
        Dictionary<ISymbol, Storage> outputs, out Statement? statement)
    {
        throw new NotImplementedException();
    }

    public void ProcessInvocationOperation(IInvocationOperation operation, Dictionary<ISymbol, Storage> inputs, Dictionary<ISymbol, Storage> outputs,
        out Statement? statement)
    {
        throw new NotImplementedException();
    }

    public void ProcessDynamicInvocationOperation(IDynamicInvocationOperation operation, Dictionary<ISymbol, Storage> inputs, Dictionary<ISymbol, Storage> outputs,
        out Statement? statement)
    {
        throw new NotImplementedException();
    }

    public void ProcessFunctionPointerInvocationOperation(IFunctionPointerInvocationOperation operation, Dictionary<ISymbol, Storage> inputs,
        Dictionary<ISymbol, Storage> outputs, out Statement? statement)
    {
        throw new NotImplementedException();
    }

    public void ProcessObjectCreationOperation(IObjectCreationOperation operation, Dictionary<ISymbol, Storage> inputs, Dictionary<ISymbol, Storage> outputs,
        out Statement? statement)
    {
        throw new NotImplementedException();
    }

    public void ProcessDynamicObjectCreationOperation(IDynamicObjectCreationOperation operation, Dictionary<ISymbol, Storage> inputs,
        Dictionary<ISymbol, Storage> outputs, out Statement? statement)
    {
        throw new NotImplementedException();
    }

    public void ProcessConditionalAccessOperation(IConditionalAccessOperation operation, Dictionary<ISymbol, Storage> inputs, Dictionary<ISymbol, Storage> outputs,
        out Statement? statement)
    {
        throw new NotImplementedException();
    }

    public void ProcessAwaitOperation(IAwaitOperation operation, Dictionary<ISymbol, Storage> inputs, Dictionary<ISymbol, Storage> outputs, out Statement? statement)
    {
        throw new NotImplementedException();
    }

    public void ProcessReturnOperation(IReturnOperation operation, Dictionary<ISymbol, Storage> inputs, Dictionary<ISymbol, Storage> outputs,
        out Statement? statement)
    {
        throw new NotImplementedException();
    }

    public void ProcessConditionalOperation(IConditionalOperation operation, Dictionary<ISymbol, Storage> inputs, Dictionary<ISymbol, Storage> outputs,
        out Statement? statement)
    {
        throw new NotImplementedException();
    }

    public void ProcessForLoopOperation(IForLoopOperation operation, Dictionary<ISymbol, Storage> inputs, Dictionary<ISymbol, Storage> outputs,
        out Statement? statement)
    {
        throw new NotImplementedException();
    }

    public void ProcessForEachLoopOperation(IForEachLoopOperation operation, Dictionary<ISymbol, Storage> inputs, Dictionary<ISymbol, Storage> outputs,
        out Statement? statement)
    {
        throw new NotImplementedException();
    }

    public void ProcessWhileLoopOperation(IWhileLoopOperation operation, Dictionary<ISymbol, Storage> inputs, Dictionary<ISymbol, Storage> outputs,
        out Statement? statement)
    {
        throw new NotImplementedException();
    }

    public void ProcessSwitchOperation(ISwitchOperation operation, Dictionary<ISymbol, Storage> inputs, Dictionary<ISymbol, Storage> outputs,
        out Statement? statement)
    {
        throw new NotImplementedException();
    }

    public void ProcessBranchOperation(IBranchOperation operation, Dictionary<ISymbol, Storage> inputs, Dictionary<ISymbol, Storage> outputs,
        out Statement? statement)
    {
        throw new NotImplementedException();
    }

    public void ProcessLabeledOperation(ILabeledOperation operation, Dictionary<ISymbol, Storage> inputs, Dictionary<ISymbol, Storage> outputs,
        out Statement? statement)
    {
        throw new NotImplementedException();
    }

    public void ProcessTryOperation(ITryOperation operation, Dictionary<ISymbol, Storage> inputs, Dictionary<ISymbol, Storage> outputs, out Statement? statement)
    {
        throw new NotImplementedException();
    }

    public void ProcessThrowOperation(IThrowOperation operation, Dictionary<ISymbol, Storage> inputs, Dictionary<ISymbol, Storage> outputs, out Statement? statement)
    {
        throw new NotImplementedException();
    }

    public void ProcessUsingOperation(IUsingOperation operation, Dictionary<ISymbol, Storage> inputs, Dictionary<ISymbol, Storage> outputs, out Statement? statement)
    {
        throw new NotImplementedException();
    }

    public void ProcessUsingDeclarationOperation(IUsingDeclarationOperation operation, Dictionary<ISymbol, Storage> inputs, Dictionary<ISymbol, Storage> outputs,
        out Statement? statement)
    {
        throw new NotImplementedException();
    }

    public void ProcessLockOperation(ILockOperation operation, Dictionary<ISymbol, Storage> inputs, Dictionary<ISymbol, Storage> outputs, out Statement? statement)
    {
        throw new NotImplementedException();
    }

    public void ProcessFixedOperation(IOperation operation, Dictionary<ISymbol, Storage> inputs, Dictionary<ISymbol, Storage> outputs, out Statement? statement)
    {
        throw new NotImplementedException();
    }

    public void ProcessLocalFunctionOperation(ILocalFunctionOperation operation, Dictionary<ISymbol, Storage> inputs, Dictionary<ISymbol, Storage> outputs,
        out Statement? statement)
    {
        throw new NotImplementedException();
    }

    public void ProcessEventAssignmentOperation(IEventAssignmentOperation operation, Dictionary<ISymbol, Storage> inputs, Dictionary<ISymbol, Storage> outputs,
        out Statement? statement)
    {
        throw new NotImplementedException();
    }

    public void ProcessEmptyOperation(IEmptyOperation operation, Dictionary<ISymbol, Storage> inputs, Dictionary<ISymbol, Storage> outputs, out Statement? statement)
    {
        throw new NotImplementedException();
    }

    public void ProcessInvalidOperation(IInvalidOperation operation, Dictionary<ISymbol, Storage> inputs, Dictionary<ISymbol, Storage> outputs,
        out Statement? statement)
    {
        throw new NotImplementedException();
    }
}