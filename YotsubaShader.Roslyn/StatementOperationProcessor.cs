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
namespace YotsubaShader.Roslyn;

/*
 *| Prioridad | Operación Roslyn | Casos que desbloquea |
   |---|---|---|
   | 1 | `IVariableDeclarationGroupOperation` | `VertexOutput output`, `float numero` — ya está iniciado |
   | 2 | `IObjectCreationOperation` | `new VertexOutput()`, `new Vector2(...)`, `new Vector4(...)` |
   | 3 | `ISimpleAssignmentOperation` | `output.Position = ...`, `output.Uv = ...` |
   | 4 | `IReturnOperation` | Todos los `return output` y `return new Vector4(...)` |
   | 5 | `IBinaryOperation` | `33 + 4`, `id * 3`, `id + 8`, `id * 7` |
   | 6 | `IConversionOperation` | `0`, `1`, `33 + 4`, etc., al ser convertidos implícitamente a `float`; y `uint` a `float` |
   | 7 | `ICompoundAssignmentOperation` | `output.Position *= numero` |
   | 8 | `IEmptyOperation` | El cuerpo vacío de `Compute`; debe producir `statement = null`, no un error |
 */

public partial class StatementOperationProcessor : IStatementOperationProcessor
{
    
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
                ProcessVariableDeclarationGroupOperation(variableDeclarationGroupOperation, inputs, outputs, compilation, stage, out statement);
                break;
            case ICoalesceAssignmentOperation coalesceAssignmentOperation:
                //ProcessCoalesceAssignmentOperation(coalesceAssignmentOperation, inputs, outputs, compilation, stage, out statement);
                throw new NotImplementedException(nameof(ProcessCoalesceAssignmentOperation));
                break;
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

            case IObjectCreationOperation objectCreationOperation:
                ProcessObjectCreationOperation(objectCreationOperation, inputs, outputs, compilation, stage, out statement);
             break;
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
        var variableDeclarationOperationsSpan = operation.Declarations.AsSpan();
        foreach (var variableDeclarationOperation in variableDeclarationOperationsSpan)
        {
            var variableDeclaratorOperations = variableDeclarationOperation.Declarators.AsSpan();
            foreach (var variableDeclaratorOperation in variableDeclaratorOperations)
            {
                var symbol = variableDeclaratorOperation.Symbol;
                var storage = CreateLocalStorage(symbol, compilation, stage);
                
                _locals.Add(symbol, storage);

                var initializerOperation =
                    variableDeclaratorOperation.Initializer ?? variableDeclarationOperation.Initializer;

                Expression? initializer = null;
                if (initializerOperation is not null)
                {
                    initializer = ProcessExpression(initializerOperation.Value, inputs, outputs, compilation, stage);
                }
                
                statements.Add(new VariableDeclaration(storage, initializer));
            }
        }

        statement = new Block(statements);
    }

    public void ProcessSimpleAssignmentOperation(ISimpleAssignmentOperation operation, Dictionary<ISymbol, Storage> inputs, Dictionary<ISymbol, Storage> outputs,
        Compilation compilation, Stage stage, out Statement? statement)
    {
        throw new NotImplementedException();
    }

    public void ProcessCompoundAssignmentOperation(ICompoundAssignmentOperation operation, Dictionary<ISymbol, Storage> inputs, Dictionary<ISymbol, Storage> outputs,
        Compilation compilation, Stage stage, out Statement? statement)
    {
        throw new NotImplementedException();
    }

    public void ProcessCoalesceAssignmentOperation(ICoalesceAssignmentOperation operation, Dictionary<ISymbol, Storage> inputs, Dictionary<ISymbol, Storage> outputs,
        Compilation compilation, Stage stage, out Statement? statement)
    {
        throw new NotImplementedException();
    }

    public void ProcessDeconstructionAssignmentOperation(IDeconstructionAssignmentOperation operation, Dictionary<ISymbol, Storage> inputs,
        Dictionary<ISymbol, Storage> outputs, Compilation compilation, Stage stage, out Statement? statement)
    {
        throw new NotImplementedException();
    }

    public void ProcessIncrementOrDecrementOperation(IIncrementOrDecrementOperation operation, Dictionary<ISymbol, Storage> inputs,
        Dictionary<ISymbol, Storage> outputs, Compilation compilation, Stage stage, out Statement? statement)
    {
        throw new NotImplementedException();
    }

    public void ProcessInvocationOperation(IInvocationOperation operation, Dictionary<ISymbol, Storage> inputs, Dictionary<ISymbol, Storage> outputs,
        Compilation compilation, Stage stage, out Statement? statement)
    {
        throw new NotImplementedException();
    }

    public void ProcessDynamicInvocationOperation(IDynamicInvocationOperation operation, Dictionary<ISymbol, Storage> inputs, Dictionary<ISymbol, Storage> outputs,
        Compilation compilation, Stage stage, out Statement? statement)
    {
        throw new NotImplementedException();
    }

    public void ProcessFunctionPointerInvocationOperation(IFunctionPointerInvocationOperation operation, Dictionary<ISymbol, Storage> inputs,
        Dictionary<ISymbol, Storage> outputs, Compilation compilation, Stage stage, out Statement? statement)
    {
        throw new NotImplementedException();
    }

    public void ProcessObjectCreationOperation(IObjectCreationOperation operation, Dictionary<ISymbol, Storage> inputs, Dictionary<ISymbol, Storage> outputs,
        Compilation compilation, Stage stage, out Statement? statement)
    {
        //!TODO
        throw new NotSupportedException(
            "Una creación de objeto usada como sentencia no está soportada.");
    }

    public void ProcessDynamicObjectCreationOperation(IDynamicObjectCreationOperation operation, Dictionary<ISymbol, Storage> inputs,
        Dictionary<ISymbol, Storage> outputs, Compilation compilation, Stage stage, out Statement? statement)
    {
        throw new NotImplementedException();
    }

    public void ProcessConditionalAccessOperation(IConditionalAccessOperation operation, Dictionary<ISymbol, Storage> inputs, Dictionary<ISymbol, Storage> outputs,
        Compilation compilation, Stage stage, out Statement? statement)
    {
        throw new NotImplementedException();
    }

    public void ProcessAwaitOperation(IAwaitOperation operation, Dictionary<ISymbol, Storage> inputs, Dictionary<ISymbol, Storage> outputs, Compilation compilation,
        Stage stage, out Statement? statement)
    {
        throw new NotImplementedException();
    }

    public void ProcessReturnOperation(IReturnOperation operation, Dictionary<ISymbol, Storage> inputs, Dictionary<ISymbol, Storage> outputs, Compilation compilation,
        Stage stage, out Statement? statement)
    {
        throw new NotImplementedException();
    }

    public void ProcessConditionalOperation(IConditionalOperation operation, Dictionary<ISymbol, Storage> inputs, Dictionary<ISymbol, Storage> outputs,
        Compilation compilation, Stage stage, out Statement? statement)
    {
        throw new NotImplementedException();
    }

    public void ProcessForLoopOperation(IForLoopOperation operation, Dictionary<ISymbol, Storage> inputs, Dictionary<ISymbol, Storage> outputs,
        Compilation compilation, Stage stage, out Statement? statement)
    {
        throw new NotImplementedException();
    }

    public void ProcessForEachLoopOperation(IForEachLoopOperation operation, Dictionary<ISymbol, Storage> inputs, Dictionary<ISymbol, Storage> outputs,
        Compilation compilation, Stage stage, out Statement? statement)
    {
        throw new NotImplementedException();
    }

    public void ProcessWhileLoopOperation(IWhileLoopOperation operation, Dictionary<ISymbol, Storage> inputs, Dictionary<ISymbol, Storage> outputs,
        Compilation compilation, Stage stage, out Statement? statement)
    {
        throw new NotImplementedException();
    }

    public void ProcessSwitchOperation(ISwitchOperation operation, Dictionary<ISymbol, Storage> inputs, Dictionary<ISymbol, Storage> outputs, Compilation compilation,
        Stage stage, out Statement? statement)
    {
        throw new NotImplementedException();
    }

    public void ProcessBranchOperation(IBranchOperation operation, Dictionary<ISymbol, Storage> inputs, Dictionary<ISymbol, Storage> outputs, Compilation compilation,
        Stage stage, out Statement? statement)
    {
        throw new NotImplementedException();
    }

    public void ProcessLabeledOperation(ILabeledOperation operation, Dictionary<ISymbol, Storage> inputs, Dictionary<ISymbol, Storage> outputs,
        Compilation compilation, Stage stage, out Statement? statement)
    {
        throw new NotImplementedException();
    }

    public void ProcessTryOperation(ITryOperation operation, Dictionary<ISymbol, Storage> inputs, Dictionary<ISymbol, Storage> outputs, Compilation compilation,
        Stage stage, out Statement? statement)
    {
        throw new NotImplementedException();
    }

    public void ProcessThrowOperation(IThrowOperation operation, Dictionary<ISymbol, Storage> inputs, Dictionary<ISymbol, Storage> outputs, Compilation compilation,
        Stage stage, out Statement? statement)
    {
        throw new NotImplementedException();
    }

    public void ProcessUsingOperation(IUsingOperation operation, Dictionary<ISymbol, Storage> inputs, Dictionary<ISymbol, Storage> outputs, Compilation compilation,
        Stage stage, out Statement? statement)
    {
        throw new NotImplementedException();
    }

    public void ProcessUsingDeclarationOperation(IUsingDeclarationOperation operation, Dictionary<ISymbol, Storage> inputs, Dictionary<ISymbol, Storage> outputs,
        Compilation compilation, Stage stage, out Statement? statement)
    {
        throw new NotImplementedException();
    }

    public void ProcessLockOperation(ILockOperation operation, Dictionary<ISymbol, Storage> inputs, Dictionary<ISymbol, Storage> outputs, Compilation compilation,
        Stage stage, out Statement? statement)
    {
        throw new NotImplementedException();
    }

    public void ProcessFixedOperation(IOperation operation, Dictionary<ISymbol, Storage> inputs, Dictionary<ISymbol, Storage> outputs, Compilation compilation,
        Stage stage, out Statement? statement)
    {
        throw new NotImplementedException();
    }

    public void ProcessLocalFunctionOperation(ILocalFunctionOperation operation, Dictionary<ISymbol, Storage> inputs, Dictionary<ISymbol, Storage> outputs,
        Compilation compilation, Stage stage, out Statement? statement)
    {
        throw new NotImplementedException();
    }

    public void ProcessEventAssignmentOperation(IEventAssignmentOperation operation, Dictionary<ISymbol, Storage> inputs, Dictionary<ISymbol, Storage> outputs,
        Compilation compilation, Stage stage, out Statement? statement)
    {
        throw new NotImplementedException();
    }

    public void ProcessEmptyOperation(IEmptyOperation operation, Dictionary<ISymbol, Storage> inputs, Dictionary<ISymbol, Storage> outputs, Compilation compilation,
        Stage stage, out Statement? statement)
    {
        throw new NotImplementedException();
    }

    public void ProcessInvalidOperation(IInvalidOperation operation, Dictionary<ISymbol, Storage> inputs, Dictionary<ISymbol, Storage> outputs,
        Compilation compilation, Stage stage, out Statement? statement)
    {
        throw new NotImplementedException();
    }
}