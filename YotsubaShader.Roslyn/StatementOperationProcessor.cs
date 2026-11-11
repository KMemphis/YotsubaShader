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
using Microsoft.CodeAnalysis.Operations;
using YotsubaShader.IR.Intermediate;
using YotsubaShader.IR.Intermediate.FunctionBody;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using YotsubaShader.IR.Intermediate.FunctionBody.Expressions;
using YotsubaShader.IR.Intermediate.FunctionBody.Expressions.AssignmentTargets;
using YotsubaShader.IR.Intermediate.FunctionBody.Expressions.FunctionCalls;
using YotsubaShader.IR.Intermediate.FunctionBody.Expressions.Literals;
using YotsubaShader.IR.Intermediate.FunctionBody.Statements;
using IRTypes = YotsubaShader.IR.Intermediate.Types;
namespace YotsubaShader.Roslyn;
public partial class StatementOperationProcessor : IStatementOperationProcessor
{
    internal Storage? ThisStorage { get; set; }
    private int _loopDepth;
    private int _foreachCounter;
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
            case ICompoundAssignmentOperation compoundAssignmentOperation:
                ProcessCompoundAssignmentOperation(compoundAssignmentOperation, inputs, outputs, compilation, stage,
                    out statement);
                break;
            case IDeconstructionAssignmentOperation:
                throw new NotImplementedException(
                    nameof(ProcessDeconstructionAssignmentOperation));

            case ISimpleAssignmentOperation simpleAssignmentOperation:
                 ProcessSimpleAssignmentOperation(simpleAssignmentOperation, inputs, outputs, compilation, stage,
                    out statement);
                 break;
            case IIncrementOrDecrementOperation incrementOperation:
                ProcessIncrementOrDecrementOperation(incrementOperation, inputs, outputs, compilation, stage, out statement);
                break;
            case IInvocationOperation invocationOperation:
                ProcessInvocationOperation(invocationOperation, inputs, outputs, compilation, stage, out statement);
            break;
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

            case IReturnOperation returnOperation:
                ProcessReturnOperation(returnOperation, inputs, outputs, compilation, stage, out statement);
                break;
            case IConditionalOperation conditionalOperation:
                ProcessConditionalOperation(conditionalOperation, inputs, outputs, compilation, stage, out statement);
                break;
            case IForLoopOperation forLoopOperation:
                ProcessForLoopOperation(forLoopOperation, inputs, outputs, compilation, stage, out statement);
                break;
            case IForEachLoopOperation forEachLoopOperation:
                ProcessForEachLoopOperation(forEachLoopOperation, inputs, outputs, compilation, stage, out statement);
                break;
            case IWhileLoopOperation whileLoopOperation:
                ProcessWhileLoopOperation(whileLoopOperation, inputs, outputs, compilation, stage, out statement);
                break;
            case ISwitchOperation:
                throw new NotImplementedException(
                    nameof(ProcessSwitchOperation));

            case IBranchOperation branchOperation:
                ProcessBranchOperation(branchOperation, inputs, outputs, compilation, stage, out statement);
                break;
            
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

            case IEmptyOperation emptyOperation:
                ProcessEmptyOperation(emptyOperation, inputs, outputs, compilation, stage, out statement);
                break;
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
                storage = storage with { HumanName = UniqueLocalName(symbol.Name) };
                
                var initializerOperation =
                    variableDeclaratorOperation.Initializer ?? variableDeclarationOperation.Initializer;

                if (storage is IRTypes.Array arrayStorage)
                {
                    ProcessLocalArrayDeclaration(symbol, arrayStorage, initializerOperation,
                        inputs, outputs, compilation, stage, statements);
                    continue;
                }
                
                _locals.Add(symbol, storage);

               
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
        var target = ProcessExpression(operation.Target, inputs, outputs, compilation, stage) ?? throw new NotSupportedException(
            "The assignment target did not produce an expression.");

        var value = ProcessExpression(operation.Value, inputs, outputs, compilation, stage) ?? throw new NotSupportedException(
            "The assignment value did not produce an expression.");

        statement = new Assignment(target, value);
    }

    public void ProcessCompoundAssignmentOperation(ICompoundAssignmentOperation operation, Dictionary<ISymbol, Storage> inputs, Dictionary<ISymbol, Storage> outputs,
        Compilation compilation, Stage stage, out Statement? statement)
    {
        var prelude = new List<Statement>();

        // El destino se evalúa una sola vez aunque se lea y se escriba.
        var target = ProcessAssignableTarget(operation.Target, prelude, inputs, outputs, compilation, stage);

        if (target is not StorageReference && target is not StorageStructPropertyReference &&
            target is not StorageArrayElementReference)
        {
            throw new NotSupportedException(
                "The compound-assignment target is not assignable.");
        }

        var value = ProcessExpression(operation.Value, inputs, outputs, compilation, stage)  ?? throw new NotSupportedException(
            "The compound-assignment value did not produce an expression.");

        Expression result;

        if (operation.OperatorMethod is { MethodKind: MethodKind.UserDefinedOperator } userOperator)
        {
            // x op= y  =>  x = op(x, y), con el operador que Roslyn resolvió.
            if (userOperator.Parameters.Length != 2)
            {
                throw new NotSupportedException(
                    $"The operator '{userOperator.Name}' is not supported as a compound assignment.");
            }

            var (functionId, arguments) = ProcessMethodCall(
                userOperator, [], inputs, outputs, compilation, stage,
                preparedArguments: [target, value]);

            result = new FunctionCallExpression(functionId, arguments);
        }
        else
        {
            BinaryOperator binaryOperator = GetBinaryOperator(
                operation.OperatorKind,
                operation.Target.Type?.SpecialType == SpecialType.System_Boolean);

            bool isShift = binaryOperator is BinaryOperator.ShiftLeft or BinaryOperator.ShiftRight;

            // `int x; x += 1.5f;`: el destino se convierte al tipo del operador y el resultado vuelve al del destino.
            Expression left = isShift ? target : ConvertScalar(target, operation.Target.Type, operation.Value.Type);
            Expression computed = new BinaryExpression(binaryOperator, left, value);

            result = isShift ? computed : ConvertScalar(computed, operation.Value.Type, operation.Target.Type);
        }

        var assignment = new Assignment(target, result);

        statement = prelude.Count == 0
            ? assignment
            : new Block([.. prelude, assignment]);
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
        var prelude = new List<Statement>();
        var target = ProcessAssignableTarget(operation.Target, prelude, inputs, outputs, compilation, stage);

        var assignment = new Assignment(
            target,
            BuildIncrementedValue(operation, target, inputs, outputs, compilation, stage));

        statement = prelude.Count == 0
            ? assignment
            : new Block([.. prelude, assignment]);
    }

    public void ProcessInvocationOperation(
        IInvocationOperation operation,
        Dictionary<ISymbol, Storage> inputs,
        Dictionary<ISymbol, Storage> outputs,
        Compilation compilation,
        Stage stage,
        out Statement? statement)
    {
        (FunctionId functionId, IReadOnlyList<Expression> arguments) = ProcessInvocation(
                                                                        operation,
                                                                        inputs,
                                                                        outputs,
                                                                        compilation,
                                                                        stage);
        statement = new FunctionCallStatement(
            functionId,
            arguments);
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
            "Standalone object-creation statements are not supported.");
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
        Expression? value = operation.ReturnedValue is null
            ? (ThisStorage is null ? null : new StorageReference(ThisStorage))
            : ProcessExpression(operation.ReturnedValue, inputs, outputs, compilation, stage);
        
        statement = new Return(value);
    }

    public void ProcessConditionalOperation(IConditionalOperation operation,
        Dictionary<ISymbol, Storage> inputs, Dictionary<ISymbol, Storage> outputs,
        Compilation compilation, Stage stage, out Statement? statement)
    {
        if (operation.IsRef)
        {
            throw new NotSupportedException("Ref conditionals are not supported.");
        }

        var condition = ProcessExpression(operation.Condition, inputs, outputs, compilation, stage)
                        ?? throw new NotSupportedException("The if condition did not produce an expression.");

        // `if (x) ;` o cuerpo vacío: ProcessStatement devuelve null.
        ProcessStatement(operation.WhenTrue, inputs, outputs, compilation, stage, out var whenTrue);

        Statement? whenFalse = null;
        if (operation.WhenFalse is not null)
        {
            // `else if` llega como otro IConditionalOperation y se procesa recursivamente.
            ProcessStatement(operation.WhenFalse, inputs, outputs, compilation, stage, out whenFalse);
        }

        statement = new If(condition, whenTrue ?? new Block([]), whenFalse);
    }

    public void ProcessForLoopOperation(IForLoopOperation operation, Dictionary<ISymbol, Storage> inputs, Dictionary<ISymbol, Storage> outputs,
        Compilation compilation, Stage stage, out Statement? statement)
    {
        if (!operation.ConditionLocals.IsDefaultOrEmpty)
        {
            throw new NotSupportedException(
                "Variables declared inside a for condition are not supported.");
        }
        
        var statements = new List<Statement>();
        
        foreach (var before in operation.Before)
        {
            ProcessStatement(before, inputs, outputs, compilation, stage, out var beforeStatement);
            if (beforeStatement is not null) statements.Add(beforeStatement!);
        }

        Expression? condition = null;

        if (operation.Condition is not null)
        {
            condition = ProcessExpression(operation.Condition, inputs, outputs, compilation, stage)
                        ?? throw new NotSupportedException("The for condition did not produce an expression.");
        }
        
        Statement? body;
        _loopDepth++;
        try
        {
            ProcessStatement(operation.Body, inputs, outputs, compilation, stage, out body);
        }
        finally
        {
            _loopDepth--;
        }
        var continuing = new List<Statement>();
        
        foreach (var after in operation.AtLoopBottom)
        {
            ProcessStatement(after, inputs, outputs, compilation, stage, out var afterStatement);
            if (afterStatement is not null) continuing.Add(afterStatement);
        }
        
        statements.Add(new Loop(condition, body ?? new Block([]), new Block(continuing)));
        statement = new Block(statements);
    }

    public void ProcessForEachLoopOperation(IForEachLoopOperation operation,
    Dictionary<ISymbol, Storage> inputs, Dictionary<ISymbol, Storage> outputs,
    Compilation compilation, Stage stage, out Statement? statement)
{
    if (operation.IsAsynchronous)
    {
        throw new NotSupportedException("await foreach is not supported.");
    }

    if (operation.LoopControlVariable is not IVariableDeclaratorOperation declarator)
    {
        throw new NotSupportedException(
            "Only 'foreach (T x in collection)' with a simple variable is supported.");
    }

    IOperation collectionOperation = operation.Collection;
    while (collectionOperation is IConversionOperation { IsImplicit: true } implicitConversion)
    {
        collectionOperation = implicitConversion.Operand;
    }

    var collection = ProcessExpression(collectionOperation, inputs, outputs, compilation, stage)
                     ?? throw new NotSupportedException("The foreach collection did not produce an expression.");
    
    if (collection is not StorageReference { Storage: IRTypes.Array array })
    {
        throw new NotSupportedException(
            "foreach is only supported over arrays referenced by a variable or parameter.");
    }

    if (array.Length is not uint length)
    {
        throw new NotSupportedException(
            "foreach over a runtime-sized array is not supported; use a for loop with an explicit length.");
    }

    // 2. Índice oculto (nombre único, igual que cualquier otro local).
    var indexStorage = new IRTypes.Int(
        UniqueLocalName($"__foreach_index_{_foreachCounter++}"), StorageKind.Local, null)
    {
        Bits = 32,
        Signed = true
    };
    var indexRef = new StorageReference(indexStorage);

    // 3. Variable del foreach (`x`), registrada igual que en una declaración normal.
    var elementStorage = CreateLocalStorage(declarator.Symbol, compilation, stage);
    elementStorage = elementStorage with { HumanName = UniqueLocalName(declarator.Symbol.Name) };
    _locals.Add(declarator.Symbol, elementStorage);

    // 4. Cuerpo.
    Statement? body;
    _loopDepth++;
    try
    {
        ProcessStatement(operation.Body, inputs, outputs, compilation, stage, out body);
    }
    finally
    {
        _loopDepth--;
    }

    var bodyStatements = new List<Statement>
    {
        new VariableDeclaration(
            elementStorage,
            new StorageArrayElementReference(collection, indexRef))
    };
    if (body is not null) bodyStatements.Add(body);

    // 5. Condición e incremento.
    var condition = new BinaryExpression(
        BinaryOperator.Less,
        indexRef,
        new IntLiteral(length.ToString(CultureInfo.InvariantCulture), true));

    var increment = new Assignment(
        indexRef,
        new BinaryExpression(BinaryOperator.Add, indexRef, new IntLiteral("1", true)));

    statement = new Block(
    [
        new VariableDeclaration(indexStorage, new IntLiteral("0", true)),
        new Loop(condition, new Block(bodyStatements), new Block([increment]))
    ]);
}

    public void ProcessWhileLoopOperation(IWhileLoopOperation operation, Dictionary<ISymbol, Storage> inputs, Dictionary<ISymbol, Storage> outputs,
        Compilation compilation, Stage stage, out Statement? statement)
    {
        if (!operation.Locals.IsDefaultOrEmpty)
        {
            throw new NotSupportedException(
                "Variables declared inside a while condition are not supported.");
        }

        var condition = ProcessExpression(operation.Condition!, inputs, outputs, compilation, stage)
                        ?? throw new NotSupportedException("The while condition did not produce an expression.");
        

        Statement? body;
        _loopDepth++;
        try
        {
            ProcessStatement(operation.Body, inputs, outputs, compilation, stage, out body);
        }
        finally
        {
            _loopDepth--;
        }

        body ??= new Block([]);

        if (operation.ConditionIsTop)
        {
            statement = new Loop(condition, body, new Block([]));
        }
        else
        {
            statement = new Loop(null, body, new Block([]), condition);
        }
    }

    public void ProcessSwitchOperation(ISwitchOperation operation, Dictionary<ISymbol, Storage> inputs, Dictionary<ISymbol, Storage> outputs, Compilation compilation,
        Stage stage, out Statement? statement)
    {
        throw new NotImplementedException();
    }

    public void ProcessBranchOperation(IBranchOperation operation,
        Dictionary<ISymbol, Storage> inputs, Dictionary<ISymbol, Storage> outputs,
        Compilation compilation, Stage stage, out Statement? statement)
    {
        if (_loopDepth == 0)
        {
            throw new NotSupportedException("break/continue outside of a loop is not supported.");
        }

        statement = operation.BranchKind switch
        {
            BranchKind.Break => new Break(),
            BranchKind.Continue => new Continue(),
            _ => throw new NotSupportedException(
                $"Branch kind '{operation.BranchKind}' is not supported.")
        };
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
        statement = null;
    }

    public void ProcessInvalidOperation(IInvalidOperation operation, Dictionary<ISymbol, Storage> inputs, Dictionary<ISymbol, Storage> outputs,
        Compilation compilation, Stage stage, out Statement? statement)
    {
        throw new NotImplementedException();
    }
}