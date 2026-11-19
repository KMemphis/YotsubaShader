using System.Globalization;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;
using YotsubaShader.IR.Intermediate;
using YotsubaShader.IR.Intermediate.FunctionBody;
using YotsubaShader.IR.Intermediate.FunctionBody.Expressions;
using YotsubaShader.IR.Intermediate.FunctionBody.Expressions.AssignmentTargets;
using YotsubaShader.IR.Intermediate.FunctionBody.Expressions.Literals;
using YotsubaShader.IR.Intermediate.FunctionBody.Statements;
using YotsubaShader.IR.Intermediate.ValueConvertions;
using IRTypes = YotsubaShader.IR.Intermediate.Types;

namespace YotsubaShader.Roslyn;

/// <summary>
/// <c>switch</c> de C# (sentencia y expresión). Un switch sobre un entero con casos constantes va directo a
/// <see cref="Switch"/> (OpSwitch). Con patrones (&lt; &gt;, and/or/not, when, selector float o bool) primero se
/// calcula qué sección gana en una variable entera y después se hace el switch sobre ella: así <c>break</c> y
/// <c>continue</c> mantienen su significado de C#.
/// </summary>
public partial class StatementOperationProcessor
{
    private int _switchDepth;
    private int _switchCounter;

    public void ProcessSwitchOperation(ISwitchOperation operation, Dictionary<ISymbol, Storage> inputs, Dictionary<ISymbol, Storage> outputs, Compilation compilation,
        Stage stage, out Statement? statement)
    {
        ITypeSymbol selectorType = operation.Value.Type
                                   ?? throw new NotSupportedException("The switch value has no type.");
        Expression selector = ProcessExpression(operation.Value, inputs, outputs, compilation, stage)
                              ?? throw new NotSupportedException("The switch value did not produce an expression.");

        var prelude = new List<Statement>();
        var cases = operation.Cases.Select(section => section.Clauses).ToList();

        Expression switchOn;
        var labels = new List<(List<long> Values, bool IsDefault)>();

        if (TryDirectLabels(selectorType, cases.Select(c => c.Cast<IOperation>().ToList()).ToList(), out var direct))
        {
            switchOn = ToSwitchInt(selector, selectorType);
            labels = direct;
        }
        else
        {
            Storage value = DeclareSwitchValue(selectorType, selector, prelude, compilation, stage);
            Storage caseIndex = DeclareCaseIndex(prelude);

            var conditions = new List<(int Section, Expression Condition)>();
            for (int i = 0; i < cases.Count; i++)
            {
                bool isDefault = false;
                Expression? condition = null;
                foreach (ICaseClauseOperation clause in cases[i])
                {
                    if (clause is IDefaultCaseClauseOperation)
                    {
                        isDefault = true;
                        continue;
                    }

                    Expression clauseCondition = ClauseCondition(clause, new StorageReference(value), inputs, outputs, compilation, stage);
                    condition = condition is null ? clauseCondition : new BinaryExpression(BinaryOperator.LogicalOr, condition, clauseCondition);
                }

                if (condition is not null) conditions.Add((i, condition));
                labels.Add((condition is null ? [] : [i], isDefault));
            }

            prelude.Add(BuildCaseSelection(caseIndex, conditions));
            switchOn = new StorageReference(caseIndex);
        }

        var sections = new List<SwitchSection>();
        _switchDepth++;
        try
        {
            for (int i = 0; i < operation.Cases.Length; i++)
            {
                var body = new List<Statement>();
                foreach (IOperation inner in operation.Cases[i].Body)
                {
                    ProcessStatement(inner, inputs, outputs, compilation, stage, out Statement? processed);
                    if (processed is not null) body.Add(processed);
                }

                sections.Add(new SwitchSection(labels[i].Values, labels[i].IsDefault, new Block(body)));
            }
        }
        finally
        {
            _switchDepth--;
        }

        prelude.Add(new Switch(switchOn, sections));
        statement = new Block(prelude);
    }

    /// <summary><c>x switch { 1 => a, &gt; 5 => b, _ => c }</c>: una variable temporal recibe el valor del brazo elegido.</summary>
    private Expression ProcessSwitchExpression(
        ISwitchExpressionOperation operation,
        Dictionary<ISymbol, Storage> inputs,
        Dictionary<ISymbol, Storage> outputs,
        Compilation compilation,
        Stage stage)
    {
        ITypeSymbol selectorType = operation.Value.Type
                                   ?? throw new NotSupportedException("The switch value has no type.");
        ITypeSymbol resultType = operation.Type
                                 ?? throw new NotSupportedException("The switch expression has no type.");
        Expression selector = ProcessExpression(operation.Value, inputs, outputs, compilation, stage)
                              ?? throw new NotSupportedException("The switch value did not produce an expression.");

        var prelude = new List<Statement>();
        Storage result = CreateTemporary(resultType, "__switch_result", compilation, stage);
        prelude.Add(new VariableDeclaration(result, null));
        var resultReference = new StorageReference(result);

        Storage value = DeclareSwitchValue(selectorType, selector, prelude, compilation, stage);
        Storage caseIndex = DeclareCaseIndex(prelude);

        var conditions = new List<(int Section, Expression Condition)>();
        var sections = new List<SwitchSection>();
        for (int i = 0; i < operation.Arms.Length; i++)
        {
            ISwitchExpressionArmOperation arm = operation.Arms[i];
            bool isDefault = arm.Pattern is IDiscardPatternOperation && arm.Guard is null;

            if (!isDefault)
            {
                Expression condition = PatternCondition(arm.Pattern, new StorageReference(value), inputs, outputs, compilation, stage);
                if (arm.Guard is not null)
                {
                    Expression guard = ProcessExpression(arm.Guard, inputs, outputs, compilation, stage)
                                       ?? throw new NotSupportedException("The 'when' guard did not produce an expression.");
                    condition = new BinaryExpression(BinaryOperator.LogicalAnd, condition, guard);
                }

                conditions.Add((i, condition));
            }

            Expression armValue = ProcessExpression(arm.Value, inputs, outputs, compilation, stage)
                                  ?? throw new NotSupportedException("A switch arm did not produce an expression.");
            sections.Add(new SwitchSection(isDefault ? [] : [i], isDefault,
                new Block([new Assignment(resultReference, armValue), new Break()])));

            if (isDefault) break; // Los brazos después de '_' no se alcanzan nunca.
        }

        prelude.Add(BuildCaseSelection(caseIndex, conditions));
        prelude.Add(new Switch(new StorageReference(caseIndex), sections));
        return new SequenceExpression(prelude, resultReference);
    }

    private Storage DeclareSwitchValue(ITypeSymbol type, Expression selector, List<Statement> prelude, Compilation compilation, Stage stage)
    {
        Storage value = CreateTemporary(type, "__switch_value", compilation, stage);
        prelude.Add(new VariableDeclaration(value, selector));
        return value;
    }

    private Storage DeclareCaseIndex(List<Statement> prelude)
    {
        Storage caseIndex = new IRTypes.Int(UniqueLocalName($"__switch_case_{_switchCounter++}"), StorageKind.Local, null)
        {
            Bits = 32,
            Signed = true
        };
        prelude.Add(new VariableDeclaration(caseIndex, new IntLiteral("-1", true)));
        return caseIndex;
    }

    /// <summary>if (c0) case = 0; else if (c1) case = 1; ... — el primer caso que se cumple gana, como en C#.</summary>
    private static Statement BuildCaseSelection(Storage caseIndex, List<(int Section, Expression Condition)> conditions)
    {
        Statement? chain = null;
        for (int i = conditions.Count - 1; i >= 0; i--)
        {
            var assign = new Assignment(new StorageReference(caseIndex),
                new IntLiteral(conditions[i].Section.ToString(CultureInfo.InvariantCulture), true));
            chain = new If(conditions[i].Condition, assign, chain);
        }

        return chain ?? new Block([]);
    }

    private Expression ClauseCondition(ICaseClauseOperation clause, Expression value,
        Dictionary<ISymbol, Storage> inputs, Dictionary<ISymbol, Storage> outputs, Compilation compilation, Stage stage)
    {
        switch (clause)
        {
            case ISingleValueCaseClauseOperation single:
            {
                Expression constant = ProcessExpression(single.Value, inputs, outputs, compilation, stage)
                                      ?? throw new NotSupportedException("A case value did not produce an expression.");
                return new BinaryExpression(BinaryOperator.Equal, value, constant);
            }
            case IPatternCaseClauseOperation pattern:
            {
                Expression condition = PatternCondition(pattern.Pattern, value, inputs, outputs, compilation, stage);
                if (pattern.Guard is null) return condition;
                Expression guard = ProcessExpression(pattern.Guard, inputs, outputs, compilation, stage)
                                   ?? throw new NotSupportedException("The 'when' guard did not produce an expression.");
                return new BinaryExpression(BinaryOperator.LogicalAnd, condition, guard);
            }
            default:
                throw new NotSupportedException($"The case clause '{clause.CaseKind}' is not supported.");
        }
    }

    /// <summary>La condición de un patrón: constantes, relacionales, and/or/not y descarte.</summary>
    private Expression PatternCondition(IPatternOperation pattern, Expression value,
        Dictionary<ISymbol, Storage> inputs, Dictionary<ISymbol, Storage> outputs, Compilation compilation, Stage stage)
    {
        switch (pattern)
        {
            case IDiscardPatternOperation:
                return new BoolLiteral(true);
            case IConstantPatternOperation constant:
                return new BinaryExpression(BinaryOperator.Equal, value,
                    ProcessExpression(constant.Value, inputs, outputs, compilation, stage)
                    ?? throw new NotSupportedException("A pattern constant did not produce an expression."));
            case IRelationalPatternOperation relational:
            {
                BinaryOperator op = relational.OperatorKind switch
                {
                    BinaryOperatorKind.LessThan => BinaryOperator.Less,
                    BinaryOperatorKind.LessThanOrEqual => BinaryOperator.LessEqual,
                    BinaryOperatorKind.GreaterThan => BinaryOperator.Greater,
                    BinaryOperatorKind.GreaterThanOrEqual => BinaryOperator.GreaterEqual,
                    _ => throw new NotSupportedException($"The relational pattern '{relational.OperatorKind}' is not supported.")
                };
                return new BinaryExpression(op, value,
                    ProcessExpression(relational.Value, inputs, outputs, compilation, stage)
                    ?? throw new NotSupportedException("A pattern value did not produce an expression."));
            }
            case IBinaryPatternOperation binary:
                return new BinaryExpression(
                    binary.OperatorKind == BinaryOperatorKind.And ? BinaryOperator.LogicalAnd : BinaryOperator.LogicalOr,
                    PatternCondition(binary.LeftPattern, value, inputs, outputs, compilation, stage),
                    PatternCondition(binary.RightPattern, value, inputs, outputs, compilation, stage));
            case INegatedPatternOperation negated:
                return new UnaryExpression(UnaryOperator.LogicalNot,
                    PatternCondition(negated.Pattern, value, inputs, outputs, compilation, stage));
            default:
                throw new NotSupportedException($"The pattern '{pattern.Kind}' is not supported in a shader.");
        }
    }

    /// <summary>Switch directo: selector entero y todos los casos son constantes (o default).</summary>
    private static bool TryDirectLabels(ITypeSymbol selectorType, List<List<IOperation>> sections,
        out List<(List<long> Values, bool IsDefault)> labels)
    {
        labels = [];
        if (selectorType.SpecialType is not (SpecialType.System_Int32 or SpecialType.System_UInt32 or
            SpecialType.System_Int16 or SpecialType.System_UInt16)) return false;

        foreach (List<IOperation> clauses in sections)
        {
            var values = new List<long>();
            bool isDefault = false;
            foreach (IOperation clause in clauses)
            {
                switch (clause)
                {
                    case IDefaultCaseClauseOperation:
                        isDefault = true;
                        break;
                    case ISingleValueCaseClauseOperation { Value.ConstantValue: { HasValue: true, Value: { } constant } }:
                        values.Add(Convert.ToInt64(constant, CultureInfo.InvariantCulture));
                        break;
                    case IPatternCaseClauseOperation { Guard: null, Pattern: IConstantPatternOperation { Value.ConstantValue: { HasValue: true, Value: { } patternConstant } } }:
                        values.Add(Convert.ToInt64(patternConstant, CultureInfo.InvariantCulture));
                        break;
                    default:
                        return false;
                }
            }

            labels.Add((values, isDefault));
        }

        return true;
    }

    /// <summary>OpSwitch trabaja sobre el entero de 32 bits: short y ushort se amplían.</summary>
    private static Expression ToSwitchInt(Expression selector, ITypeSymbol type) => type.SpecialType switch
    {
        SpecialType.System_Int16 => new NumericConversion(selector, ScalarType.Int32),
        SpecialType.System_UInt16 => new NumericConversion(selector, ScalarType.UInt32),
        _ => selector
    };
}
