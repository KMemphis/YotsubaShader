using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using YotsubaShader.IR.Intermediate;
using YotsubaShader.IR.Intermediate.FunctionBody;
using YotsubaShader.IR.Intermediate.FunctionBody.Expressions;
using YotsubaShader.IR.Intermediate.FunctionBody.Expressions.AssignmentTargets;
using YotsubaShader.IR.Intermediate.FunctionBody.Expressions.FunctionCalls;
using YotsubaShader.IR.Intermediate.FunctionBody.Expressions.Literals;
using YotsubaShader.IR.Intermediate.FunctionBody.Statements;
using IRTypes = YotsubaShader.IR.Intermediate.Types;

namespace YotsubaShader.Roslyn;

/// <summary>
/// Tuplas y deconstrucción. Una tupla es un struct (<c>ValueTuple</c>, campos Item1..ItemN). Al deconstruir,
/// primero se evalúan todos los valores en temporales y después se asignan: <c>(a, b) = (b, a)</c> intercambia
/// correctamente, como en C#.
/// </summary>
public partial class StatementOperationProcessor
{
    /// <summary>El nombre real del campo: en una tupla, <c>t.Seno</c> es <c>Item1</c>.</summary>
    internal static string FieldName(IFieldSymbol field) => field.CorrespondingTupleField?.Name ?? field.Name;

    private Expression ProcessTupleExpression(ITupleOperation tuple, Dictionary<ISymbol, Storage> inputs,
        Dictionary<ISymbol, Storage> outputs, Compilation compilation, Stage stage)
    {
        ITypeSymbol type = tuple.Type ?? throw new NotSupportedException("The tuple has no type.");
        var structure = ShaderProcessor.GetStorage(type, type.Name, null, StorageKind.Local, compilation, stage) as IRTypes.CustomStruct
                        ?? throw new NotSupportedException($"The tuple type '{type}' is not supported.");

        var values = new List<Expression>(tuple.Elements.Length);
        foreach (IOperation element in tuple.Elements)
        {
            values.Add(ProcessExpression(element, inputs, outputs, compilation, stage)
                       ?? throw new NotSupportedException("A tuple element did not produce an expression."));
        }

        return new CustomStructExpression(structure, values);
    }

    public void ProcessDeconstructionAssignmentOperation(IDeconstructionAssignmentOperation operation, Dictionary<ISymbol, Storage> inputs,
        Dictionary<ISymbol, Storage> outputs, Compilation compilation, Stage stage, out Statement? statement)
    {
        var statements = new List<Statement>();
        DeconstructionInfo info = ShaderProcessor.Sources
            .GetSemanticModel(operation.Syntax.SyntaxTree)
            .GetDeconstructionInfo((AssignmentExpressionSyntax)operation.Syntax);

        Deconstruct(operation.Target, operation.Value, info, statements, inputs, outputs, compilation, stage);
        statement = new Block(statements);
    }

    private void Deconstruct(IOperation target, IOperation value, DeconstructionInfo info, List<Statement> statements,
        Dictionary<ISymbol, Storage> inputs, Dictionary<ISymbol, Storage> outputs, Compilation compilation, Stage stage)
    {
        // var (a, b) = ...: la declaración envuelve la tupla de destinos.
        if (target is IDeclarationExpressionOperation declaration) target = declaration.Expression;
        if (target is not ITupleOperation targets)
        {
            throw new NotSupportedException("The deconstruction target is not a tuple.");
        }

        while (value is IConversionOperation { IsImplicit: true } conversion) value = conversion.Operand;

        // 1. Cada componente del valor, ya evaluado en una temporal.
        var elements = new List<(Expression Value, IOperation? Source)>();
        if (value is ITupleOperation tupleValue)
        {
            foreach (IOperation element in tupleValue.Elements)
            {
                ITypeSymbol elementType = element.Type ?? throw new NotSupportedException("A tuple element has no type.");
                Storage temp = CreateTemporary(elementType, "__deconstruct", compilation, stage);
                Expression elementValue = element is ITupleOperation nested
                    ? ProcessTupleExpression(nested, inputs, outputs, compilation, stage)
                    : ProcessExpression(element, inputs, outputs, compilation, stage)
                      ?? throw new NotSupportedException("A tuple element did not produce an expression.");
                statements.Add(new VariableDeclaration(temp, elementValue));
                elements.Add((new StorageReference(temp), element));
            }
        }
        else
        {
            ITypeSymbol valueType = value.Type ?? throw new NotSupportedException("The deconstructed value has no type.");
            Storage whole = CreateTemporary(valueType, "__deconstruct", compilation, stage);
            statements.Add(new VariableDeclaration(whole,
                ProcessExpression(value, inputs, outputs, compilation, stage)
                ?? throw new NotSupportedException("The deconstructed value did not produce an expression.")));

            if (valueType.IsTupleType && valueType is INamedTypeSymbol tupleType)
            {
                for (int i = 0; i < tupleType.TupleElements.Length; i++)
                {
                    elements.Add((new StorageStructPropertyReference(new StorageReference(whole), "Item" + (i + 1)), null));
                }
            }
            else if (info.Method is { } deconstruct)
            {
                // obj.Deconstruct(out a, out b): cada salida en su temporal.
                var outTemps = new List<Storage>();
                var arguments = new List<Expression>();
                foreach (IParameterSymbol parameter in deconstruct.Parameters)
                {
                    Storage temp = CreateTemporary(parameter.Type, "__deconstruct", compilation, stage);
                    statements.Add(new VariableDeclaration(temp, null));
                    outTemps.Add(temp);
                    arguments.Add(new StorageReference(temp));
                }

                if (deconstruct.IsExtensionMethod)
                {
                    throw new NotSupportedException("Deconstruct as an extension method is not supported.");
                }

                var (functionId, processed) = ProcessMethodCall(deconstruct, [], inputs, outputs, compilation, stage,
                    preparedArguments: arguments, instance: new StorageReference(whole));
                statements.Add(new FunctionCallStatement(functionId, processed));
                elements.AddRange(outTemps.Select(t => ((Expression)new StorageReference(t), (IOperation?)null)));
            }
            else
            {
                throw new NotSupportedException($"'{valueType}' cannot be deconstructed.");
            }
        }

        // 2. Las asignaciones, en orden.
        for (int i = 0; i < targets.Elements.Length; i++)
        {
            IOperation element = targets.Elements[i];
            if (element is IDiscardOperation) continue;

            Expression elementValue = elements[i].Value;
            if (element is IDeclarationExpressionOperation { Expression: ITupleOperation } or ITupleOperation)
            {
                // (a, (b, c)) = ...: se deconstruye la parte anidada desde su temporal.
                Storage nestedTemp = CreateTemporary(element.Type!, "__deconstruct", compilation, stage);
                statements.Add(new VariableDeclaration(nestedTemp, elementValue));
                DeconstructFromStorage(element, nestedTemp, info.Nested.Length > i ? info.Nested[i] : default,
                    statements, inputs, outputs, compilation, stage);
                continue;
            }

            statements.Add(AssignTarget(element, elementValue, inputs, outputs, compilation, stage));
        }
    }

    private void DeconstructFromStorage(IOperation target, Storage source, DeconstructionInfo info, List<Statement> statements,
        Dictionary<ISymbol, Storage> inputs, Dictionary<ISymbol, Storage> outputs, Compilation compilation, Stage stage)
    {
        if (target is IDeclarationExpressionOperation declaration) target = declaration.Expression;
        var targets = (ITupleOperation)target;
        for (int i = 0; i < targets.Elements.Length; i++)
        {
            if (targets.Elements[i] is IDiscardOperation) continue;
            Expression value = new StorageStructPropertyReference(new StorageReference(source), "Item" + (i + 1));
            statements.Add(AssignTarget(targets.Elements[i], value, inputs, outputs, compilation, stage));
        }
    }

    /// <summary>Un destino de la deconstrucción: variable nueva (var x), variable existente, campo o elemento.</summary>
    private Statement AssignTarget(IOperation target, Expression value, Dictionary<ISymbol, Storage> inputs,
        Dictionary<ISymbol, Storage> outputs, Compilation compilation, Stage stage)
    {
        if (target is IDeclarationExpressionOperation { Expression: ILocalReferenceOperation declared }) target = declared;

        if (target is ILocalReferenceOperation { IsDeclaration: true, Local: { } local } && !_locals.ContainsKey(local))
        {
            Storage storage = CreateLocalStorage(local, compilation, stage);
            storage = storage with { HumanName = UniqueLocalName(local.Name) };
            _locals.Add(local, storage);
            return new VariableDeclaration(storage, value);
        }

        Expression destination = ProcessExpression(target, inputs, outputs, compilation, stage)
                                 ?? throw new NotSupportedException("A deconstruction target did not produce an expression.");
        return new Assignment(destination, value);
    }
}
