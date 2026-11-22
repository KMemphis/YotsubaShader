using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;
using YotsubaShader.Concepts.Methods;
using YotsubaShader.IR.Intermediate;
using YotsubaShader.IR.Intermediate.FunctionBody;
using YotsubaShader.IR.Intermediate.FunctionBody.Expressions;
using YotsubaShader.IR.Intermediate.FunctionBody.Expressions.AssignmentTargets;
using YotsubaShader.IR.Intermediate.FunctionBody.Expressions.FunctionCalls;
using YotsubaShader.IR.Intermediate.FunctionBody.Expressions.Literals;
using YotsubaShader.IR.Intermediate.FunctionBody.Statements;
using YotsubaShader.IR.Intermediate.ValueConvertions;
using IRTypes = YotsubaShader.IR.Intermediate.Types;

namespace YotsubaShader.Roslyn;

/// <summary>
/// Constructores y operadores de los vectores de las sintaxis (float3(x, y, z), new vec3(xy, z), a + b, a * 2...).
/// Vienen compilados en las librerías, así que no hay código fuente que traducir: los marca
/// <see cref="ConstructorMethodAttribute"/> o <see cref="ComponentwiseOperatorAttribute"/> y aquí se convierten
/// en un <see cref="VectorExpression"/> hecho con los componentes de los argumentos, o en la operación escalar
/// aplicada a cada par de componentes.
/// </summary>
public partial class StatementOperationProcessor
{
    private static readonly string ConstructorAttributeName = typeof(ConstructorMethodAttribute).FullName!;
    private static readonly string ComponentwiseAttributeName = typeof(ComponentwiseOperatorAttribute).FullName!;
    private static readonly string MatrixProductAttributeName = typeof(MatrixProductAttribute).FullName!;

    internal static bool IsConstructorMethod(IMethodSymbol? method) =>
        method is not null && HasAttribute(method.OriginalDefinition, ConstructorAttributeName);

    /// <summary>Un operador de un vector o una matriz de las sintaxis (por componente, o el producto de matrices).</summary>
    internal static bool IsComponentwiseOperator(IMethodSymbol? method) =>
        method is not null && (HasAttribute(method.OriginalDefinition, ComponentwiseAttributeName) ||
                               HasAttribute(method.OriginalDefinition, MatrixProductAttributeName));

    private static bool HasAttribute(ISymbol symbol, string attributeName) =>
        symbol.GetAttributes().Any(a => a.AttributeClass?.ToDisplayString() == attributeName);

    private static bool IsVectorType(ITypeSymbol? type) =>
        type is not null && type.GetAttributes().Any(a => a.AttributeClass?.ToDisplayString() == typeof(YotsubaShader.Language.Native.Types.Vector.VectorAttribute).FullName);

    private static IRTypes.Vector VectorStorageOf(ITypeSymbol type, Compilation compilation, Stage stage) =>
        ShaderProcessor.GetStorage(type, type.Name, null, StorageKind.Local, compilation, stage) as IRTypes.Vector
        ?? throw new NotSupportedException($"'{type.ToDisplayString()}' is not a vector type.");

    private static IRTypes.VectorComponentsType ScalarKindOf(ITypeSymbol type) => type.SpecialType switch
    {
        SpecialType.System_Single => IRTypes.VectorComponentsType.Float,
        SpecialType.System_Int32 => IRTypes.VectorComponentsType.Int,
        SpecialType.System_UInt32 => IRTypes.VectorComponentsType.Uint,
        SpecialType.System_Double => IRTypes.VectorComponentsType.Double,
        SpecialType.System_Boolean => IRTypes.VectorComponentsType.Bool,
        _ when type.ToDisplayString() == "System.Half" => IRTypes.VectorComponentsType.Half,
        _ => throw new NotSupportedException($"'{type.ToDisplayString()}' cannot be a vector component.")
    };

    /// <summary>
    /// <c>float3(x, y, z)</c>, <c>new float4(xyz, w)</c>, <c>float3(1.0f)</c>, <c>float3(someInt3)</c>: los componentes de
    /// los argumentos, en orden y convertidos al tipo del vector. Un único escalar se repite en todos.
    /// </summary>
    private Expression BuildVector(ITypeSymbol resultType, IMethodSymbol constructor, IReadOnlyList<IOperation> arguments,
        Dictionary<ISymbol, Storage> inputs, Dictionary<ISymbol, Storage> outputs, Compilation compilation, Stage stage)
    {
        if (IsMatrixType(resultType))
        {
            return BuildMatrix(resultType, IsRowMajor(constructor), arguments, inputs, outputs, compilation, stage);
        }

        IRTypes.Vector vector = VectorStorageOf(resultType, compilation, stage);
        var prelude = new List<Statement>();
        var values = new List<Expression>();

        foreach (IOperation argument in arguments)
        {
            Expression value = ProcessExpression(argument, inputs, outputs, compilation, stage)
                               ?? throw new NotSupportedException("A constructor argument did not produce an expression.");
            ITypeSymbol type = argument.Type
                               ?? throw new NotSupportedException("A constructor argument has no type.");

            foreach ((Expression component, IRTypes.VectorComponentsType kind) in Components(value, type, prelude, compilation, stage))
            {
                values.Add(ConvertComponent(component, kind, vector.ComponentsType));
            }
        }

        if (values.Count == 1 && vector.ComponentsLength > 1)
        {
            Expression single = Stable(values[0], arguments[0].Type!, prelude, compilation, stage);
            values = Enumerable.Repeat(single, vector.ComponentsLength).ToList();
        }

        if (values.Count != vector.ComponentsLength)
        {
            throw new NotSupportedException(
                $"'{resultType.ToDisplayString()}' has {vector.ComponentsLength} components, but the arguments give {values.Count}.");
        }

        Expression built = new VectorExpression(values, vector.ComponentsType);
        return prelude.Count == 0 ? built : new SequenceExpression(prelude, built);
    }

    /// <summary><c>a + b</c>, <c>v * 2.0f</c>, <c>a &lt; b</c>...: la operación escalar en cada par de componentes.</summary>
    private Expression BuildComponentwiseBinary(BinaryOperatorKind operatorKind, IMethodSymbol operatorMethod,
        Expression left, ITypeSymbol leftType, Expression right, ITypeSymbol rightType, Compilation compilation, Stage stage)
    {
        // m * v, v * m, m * n en GLSL, MSL y WGSL: el producto de álgebra lineal.
        if (HasAttribute(operatorMethod.OriginalDefinition, MatrixProductAttributeName))
        {
            return new IntrinsicCallExpression(GPUIntrinsic.MatrixMultiply, [left, right],
                [StorageOf(operatorMethod.Parameters[0].Type, compilation, stage), StorageOf(operatorMethod.Parameters[1].Type, compilation, stage)],
                StorageOf(operatorMethod.ReturnType, compilation, stage));
        }

        if (IsMatrixType(operatorMethod.ReturnType))
        {
            return BuildMatrixBinary(operatorKind, operatorMethod, left, leftType, right, rightType, compilation, stage);
        }

        IRTypes.Vector result = VectorStorageOf(operatorMethod.ReturnType, compilation, stage);
        var prelude = new List<Statement>();
        List<(Expression Value, IRTypes.VectorComponentsType Kind)> a = Spread(left, leftType, result.ComponentsLength, prelude, compilation, stage);
        List<(Expression Value, IRTypes.VectorComponentsType Kind)> b = Spread(right, rightType, result.ComponentsLength, prelude, compilation, stage);

        // Cada lado toma el tipo de componente del parámetro del operador (v * 2 multiplica por 2.0f).
        IRTypes.VectorComponentsType leftKind = KindOf(operatorMethod.Parameters[0].Type, compilation, stage);
        IRTypes.VectorComponentsType rightKind = KindOf(operatorMethod.Parameters[1].Type, compilation, stage);
        BinaryOperator op = GetBinaryOperator(operatorKind, leftKind == IRTypes.VectorComponentsType.Bool);
        var values = new List<Expression>(result.ComponentsLength);
        for (int i = 0; i < result.ComponentsLength; i++)
        {
            values.Add(new BinaryExpression(op,
                ConvertComponent(a[i].Value, a[i].Kind, leftKind),
                ConvertComponent(b[i].Value, b[i].Kind, rightKind)));
        }

        Expression built = new VectorExpression(values, result.ComponentsType);
        return prelude.Count == 0 ? built : new SequenceExpression(prelude, built);
    }

    /// <summary><c>-v</c>, <c>!b</c>, <c>~i</c> en cada componente.</summary>
    private Expression BuildComponentwiseUnary(UnaryOperatorKind operatorKind, IMethodSymbol operatorMethod,
        Expression operand, ITypeSymbol operandType, Compilation compilation, Stage stage)
    {
        if (IsMatrixType(operatorMethod.ReturnType))
        {
            return BuildMatrixUnary(operatorKind, operatorMethod, operand, operandType, compilation, stage);
        }

        IRTypes.Vector result = VectorStorageOf(operatorMethod.ReturnType, compilation, stage);
        var prelude = new List<Statement>();
        List<(Expression Value, IRTypes.VectorComponentsType Kind)> components = Spread(operand, operandType, result.ComponentsLength, prelude, compilation, stage);

        Func<Expression, Expression> apply = operatorKind switch
        {
            UnaryOperatorKind.Minus => value => new UnaryExpression(UnaryOperator.Negate, value),
            UnaryOperatorKind.Not => value => new UnaryExpression(UnaryOperator.LogicalNot, value),
            UnaryOperatorKind.BitwiseNegation => value => new UnaryExpression(UnaryOperator.BitwiseNot, value),
            UnaryOperatorKind.Plus => value => value,
            _ => throw new NotSupportedException($"The unary operator '{operatorKind}' is not supported on vectors.")
        };

        Expression built = new VectorExpression(components.Select(c => apply(c.Value)).ToList(), result.ComponentsType);
        return prelude.Count == 0 ? built : new SequenceExpression(prelude, built);
    }

    private static IRTypes.VectorComponentsType KindOf(ITypeSymbol type, Compilation compilation, Stage stage) =>
        IsVectorType(type) ? VectorStorageOf(type, compilation, stage).ComponentsType : ScalarKindOf(type);

    /// <summary>Los componentes de un operando; un escalar se repite <paramref name="count"/> veces.</summary>
    private List<(Expression Value, IRTypes.VectorComponentsType Kind)> Spread(Expression value, ITypeSymbol type, int count,
        List<Statement> prelude, Compilation compilation, Stage stage)
    {
        if (IsVectorType(type)) return Components(value, type, prelude, compilation, stage);

        Expression scalar = Stable(value, type, prelude, compilation, stage);
        IRTypes.VectorComponentsType kind = ScalarKindOf(type);
        return Enumerable.Repeat((scalar, kind), count).ToList();
    }

    /// <summary>Un escalar tal cual; un vector, una referencia a cada componente (el vector se evalúa una sola vez).</summary>
    private List<(Expression Value, IRTypes.VectorComponentsType Kind)> Components(Expression value, ITypeSymbol type,
        List<Statement> prelude, Compilation compilation, Stage stage)
    {
        if (!IsVectorType(type)) return [(value, ScalarKindOf(type))];

        IRTypes.Vector vector = VectorStorageOf(type, compilation, stage);
        Expression source = Stable(value, type, prelude, compilation, stage);
        IReadOnlyList<string> names = vector.ComponentNames ?? ShaderProcessor.GetVectorComponentNames(type);

        return names
            .Select(name => ((Expression)new StorageStructPropertyReference(source, name), vector.ComponentsType))
            .ToList();
    }

    /// <summary>Si leer la expresión varias veces repetiría trabajo o efectos, se guarda antes en una temporal.</summary>
    private Expression Stable(Expression value, ITypeSymbol type, List<Statement> prelude, Compilation compilation, Stage stage)
    {
        if (value is StorageReference or Literal) return value;

        Storage temporary = CreateTemporary(type, "part", compilation, stage);
        prelude.Add(new VariableDeclaration(temporary, value));
        return new StorageReference(temporary);
    }

    // ------------------------------------------------------------------------------------------------ matrices

    private static bool IsMatrixType(ITypeSymbol? type) =>
        type is not null && type.GetAttributes().Any(a => a.AttributeClass?.ToDisplayString() == typeof(YotsubaShader.Language.Native.Types.Matrix.MatrixAttribute).FullName);

    private static IRTypes.Matrix MatrixStorageOf(ITypeSymbol type, Compilation compilation, Stage stage) =>
        ShaderProcessor.GetStorage(type, type.Name, null, StorageKind.Local, compilation, stage) as IRTypes.Matrix
        ?? throw new NotSupportedException($"'{type.ToDisplayString()}' is not a matrix of float, double or half.");

    private static Storage StorageOf(ITypeSymbol type, Compilation compilation, Stage stage) =>
        ShaderProcessor.GetStorage(type, type.Name, null, StorageKind.Local, compilation, stage);

    private static IRTypes.VectorComponentsType ComponentKindOf(IRTypes.Matrix matrix) => matrix.ComponentsType switch
    {
        IRTypes.MatrixComponentType.Double => IRTypes.VectorComponentsType.Double,
        IRTypes.MatrixComponentType.Half => IRTypes.VectorComponentsType.Half,
        _ => IRTypes.VectorComponentsType.Float
    };

    private static bool IsRowMajor(IMethodSymbol constructor) =>
        constructor.OriginalDefinition.GetAttributes()
            .Where(a => a.AttributeClass?.ToDisplayString() == ConstructorAttributeName)
            .SelectMany(a => a.NamedArguments)
            .Any(argument => argument.Key == nameof(ConstructorMethodAttribute.RowMajor) && argument.Value.Value is true);

    private static Literal Constant(IRTypes.VectorComponentsType kind, int value) => kind switch
    {
        IRTypes.VectorComponentsType.Double => new DoubleLiteral(value),
        IRTypes.VectorComponentsType.Half => new HalfLiteral(value),
        _ => new FloatLiteral(value)
    };

    /// <summary>
    /// Cada elemento de una matriz ya evaluada (<paramref name="source"/> es una referencia estable): por columna, la
    /// lista de sus componentes, como m.C1.Y.
    /// </summary>
    private static List<List<Expression>> MatrixElements(Expression source, ITypeSymbol type, Compilation compilation, Stage stage)
    {
        List<IFieldSymbol> columns = MatrixColumnFields(type);
        var result = new List<List<Expression>>(columns.Count);
        foreach (IFieldSymbol column in columns)
        {
            IRTypes.Vector vector = VectorStorageOf(column.Type, compilation, stage);
            IReadOnlyList<string> names = vector.ComponentNames ?? ShaderProcessor.GetVectorComponentNames(column.Type);
            var columnReference = new StorageStructPropertyReference(source, column.Name);
            result.Add(names.Select(name => (Expression)new StorageStructPropertyReference(columnReference, name)).ToList());
        }

        return result;
    }

    /// <summary>Los campos [MatrixColumn] de una matriz, en orden.</summary>
    private static List<IFieldSymbol> MatrixColumnFields(ITypeSymbol type) =>
        type.GetMembers()
            .OfType<IFieldSymbol>()
            .Select(field => (Field: field, Attribute: field.GetAttributes().FirstOrDefault(a =>
                a.AttributeClass?.ToDisplayString() == typeof(YotsubaShader.Language.Native.Types.Matrix.MatrixColumnAttribute).FullName)))
            .Where(column => column.Attribute is not null && !column.Field.IsStatic)
            .OrderBy(column => (int)column.Attribute!.ConstructorArguments[0].Value!)
            .Select(column => column.Field)
            .ToList();

    private static Expression WithPrelude(List<Statement> prelude, Expression value) =>
        prelude.Count == 0 ? value : new SequenceExpression(prelude, value);

    private static MatrixExpression MatrixOf(IRTypes.Matrix matrix, Func<int, int, Expression> element)
    {
        IRTypes.VectorComponentsType kind = ComponentKindOf(matrix);
        var columns = new List<Expression>((int)matrix.Columns);
        for (int j = 0; j < matrix.Columns; j++)
        {
            int column = j;
            columns.Add(new VectorExpression(
                Enumerable.Range(0, (int)matrix.Rows).Select(i => element(column, i)).ToList(), kind));
        }

        return new MatrixExpression(matrix, columns);
    }

    /// <summary>
    /// mat3(c0, c1, c2), float2x2(m00, m01, m10, m11), mat3(1.0) (diagonal), mat3(m4) (cortada o completada con la
    /// identidad). Los componentes llenan la matriz por columnas, o por filas si el constructor es RowMajor (HLSL).
    /// </summary>
    private Expression BuildMatrix(ITypeSymbol resultType, bool rowMajor, IReadOnlyList<IOperation> arguments,
        Dictionary<ISymbol, Storage> inputs, Dictionary<ISymbol, Storage> outputs, Compilation compilation, Stage stage)
    {
        IRTypes.Matrix matrix = MatrixStorageOf(resultType, compilation, stage);
        IRTypes.VectorComponentsType kind = ComponentKindOf(matrix);
        var prelude = new List<Statement>();

        var values = new List<(Expression Value, ITypeSymbol Type)>();
        foreach (IOperation argument in arguments)
        {
            values.Add((ProcessExpression(argument, inputs, outputs, compilation, stage)
                        ?? throw new NotSupportedException("A constructor argument did not produce an expression."),
                argument.Type ?? throw new NotSupportedException("A constructor argument has no type.")));
        }

        // Desde otra matriz: lo que no cabe se corta y lo que falta es la identidad.
        if (values.Count == 1 && IsMatrixType(values[0].Type))
        {
            IRTypes.Matrix source = MatrixStorageOf(values[0].Type, compilation, stage);
            IRTypes.VectorComponentsType sourceKind = ComponentKindOf(source);
            Expression stable = Stable(values[0].Value, values[0].Type, prelude, compilation, stage);
            List<List<Expression>> elements = MatrixElements(stable, values[0].Type, compilation, stage);
            List<IFieldSymbol> sourceColumns = MatrixColumnFields(values[0].Type);

            MatrixExpression resized = MatrixOf(matrix, (j, i) =>
                j < source.Columns && i < source.Rows
                    ? ConvertComponent(elements[j][i], sourceKind, kind)
                    : Constant(kind, i == j ? 1 : 0));

            // Una columna que pasa entera y sin convertir se copia tal cual (m[0]) en lugar de componente a componente.
            if (source.Rows == matrix.Rows && sourceKind == kind)
            {
                resized = resized with
                {
                    Columns = resized.Columns
                        .Select((column, j) => j < source.Columns ? new StorageStructPropertyReference(stable, sourceColumns[j].Name) : column)
                        .ToList()
                };
            }

            return WithPrelude(prelude, resized);
        }

        // mat3(c0, c1, c2): cada argumento ya es una columna.
        if (!rowMajor && values.Count == matrix.Columns &&
            values.All(v => IsVectorType(v.Type) &&
                            VectorStorageOf(v.Type, compilation, stage) is { } column &&
                            column.ComponentsLength == matrix.Rows && column.ComponentsType == kind))
        {
            return new MatrixExpression(matrix, values.Select(v => v.Value).ToList());
        }

        // Un escalar: la diagonal, con cero en el resto (mat3(1.0) es la identidad).
        if (values.Count == 1 && !IsVectorType(values[0].Type))
        {
            Expression scalar = ConvertComponent(Stable(values[0].Value, values[0].Type, prelude, compilation, stage),
                ScalarKindOf(values[0].Type), kind);

            return WithPrelude(prelude, MatrixOf(matrix, (j, i) => i == j ? scalar : Constant(kind, 0)));
        }

        // Escalares y vectores: sus componentes en orden, por columnas o por filas.
        var components = new List<Expression>();
        foreach ((Expression value, ITypeSymbol type) in values)
        {
            foreach ((Expression component, IRTypes.VectorComponentsType componentKind) in Components(value, type, prelude, compilation, stage))
            {
                components.Add(ConvertComponent(component, componentKind, kind));
            }
        }

        int rows = (int)matrix.Rows, columns = (int)matrix.Columns;
        if (components.Count != rows * columns)
        {
            throw new NotSupportedException(
                $"'{resultType.ToDisplayString()}' has {rows * columns} components, but the arguments give {components.Count}.");
        }

        return WithPrelude(prelude, MatrixOf(matrix, (j, i) => components[rowMajor ? i * columns + j : j * rows + i]));
    }

    /// <summary>
    /// m + n, m - n, m * 2.0, m / 2.0 y, en HLSL, m * n y m / n por componente. Lo que SPIR-V tiene para matrices
    /// (OpMatrixTimesScalar) se usa directamente; el resto se hace elemento a elemento.
    /// </summary>
    private Expression BuildMatrixBinary(BinaryOperatorKind operatorKind, IMethodSymbol operatorMethod,
        Expression left, ITypeSymbol leftType, Expression right, ITypeSymbol rightType, Compilation compilation, Stage stage)
    {
        IRTypes.Matrix result = MatrixStorageOf(operatorMethod.ReturnType, compilation, stage);
        IRTypes.VectorComponentsType kind = ComponentKindOf(result);
        bool leftMatrix = IsMatrixType(leftType), rightMatrix = IsMatrixType(rightType);

        // m * s, s * m y m / s (como m * (1 / s)) son OpMatrixTimesScalar. SPIR-V no tiene constantes half a mano aquí.
        if (leftMatrix != rightMatrix && kind != IRTypes.VectorComponentsType.Half &&
            (operatorKind == BinaryOperatorKind.Multiply || operatorKind == BinaryOperatorKind.Divide && leftMatrix))
        {
            int scalarIndex = leftMatrix ? 1 : 0;
            Expression scalar = ConvertComponent(leftMatrix ? right : left, ScalarKindOf(leftMatrix ? rightType : leftType), kind);
            if (operatorKind == BinaryOperatorKind.Divide)
            {
                scalar = new BinaryExpression(BinaryOperator.Divide, Constant(kind, 1), scalar);
            }

            return new IntrinsicCallExpression(GPUIntrinsic.MatrixMultiply, [leftMatrix ? left : right, scalar],
                [result, StorageOf(operatorMethod.Parameters[scalarIndex].Type, compilation, stage)], result);
        }

        // HLSL: m * n multiplica elemento a elemento.
        if (operatorKind == BinaryOperatorKind.Multiply && leftMatrix && rightMatrix)
        {
            return new IntrinsicCallExpression(GPUIntrinsic.MatrixComponentMultiply, [left, right], [result, result], result);
        }

        var prelude = new List<Statement>();
        Func<int, int, Expression> a = Operand(left, leftType, leftMatrix, kind, prelude, compilation, stage);
        Func<int, int, Expression> b = Operand(right, rightType, rightMatrix, kind, prelude, compilation, stage);
        BinaryOperator op = GetBinaryOperator(operatorKind);

        return WithPrelude(prelude, MatrixOf(result, (j, i) => new BinaryExpression(op, a(j, i), b(j, i))));
    }

    /// <summary>Un operando de una operación elemento a elemento: un elemento de la matriz, o el escalar en todos.</summary>
    private Func<int, int, Expression> Operand(Expression value, ITypeSymbol type, bool isMatrix, IRTypes.VectorComponentsType kind,
        List<Statement> prelude, Compilation compilation, Stage stage)
    {
        if (isMatrix)
        {
            IRTypes.VectorComponentsType from = ComponentKindOf(MatrixStorageOf(type, compilation, stage));
            List<List<Expression>> elements = MatrixElements(Stable(value, type, prelude, compilation, stage), type, compilation, stage);
            return (j, i) => ConvertComponent(elements[j][i], from, kind);
        }

        Expression scalar = ConvertComponent(Stable(value, type, prelude, compilation, stage), ScalarKindOf(type), kind);
        return (_, _) => scalar;
    }

    /// <summary>-m es m * -1 (OpMatrixTimesScalar); en half, cada elemento se niega.</summary>
    private Expression BuildMatrixUnary(UnaryOperatorKind operatorKind, IMethodSymbol operatorMethod,
        Expression operand, ITypeSymbol operandType, Compilation compilation, Stage stage)
    {
        IRTypes.Matrix result = MatrixStorageOf(operatorMethod.ReturnType, compilation, stage);
        IRTypes.VectorComponentsType kind = ComponentKindOf(result);

        if (operatorKind == UnaryOperatorKind.Plus) return operand;
        if (operatorKind != UnaryOperatorKind.Minus)
        {
            throw new NotSupportedException($"The unary operator '{operatorKind}' is not supported on matrices.");
        }

        if (kind != IRTypes.VectorComponentsType.Half)
        {
            ITypeSymbol scalarType = compilation.GetSpecialType(kind == IRTypes.VectorComponentsType.Double
                ? SpecialType.System_Double
                : SpecialType.System_Single);
            return new IntrinsicCallExpression(GPUIntrinsic.MatrixMultiply, [operand, Constant(kind, -1)],
                [result, StorageOf(scalarType, compilation, stage)], result);
        }

        var prelude = new List<Statement>();
        Func<int, int, Expression> element = Operand(operand, operandType, true, kind, prelude, compilation, stage);
        return WithPrelude(prelude, MatrixOf(result, (j, i) => new UnaryExpression(UnaryOperator.Negate, element(j, i))));
    }

    private static Expression ConvertComponent(Expression value, IRTypes.VectorComponentsType from, IRTypes.VectorComponentsType to)
    {
        if (from == to) return value;

        if (from is IRTypes.VectorComponentsType.Bool or IRTypes.VectorComponentsType.Half)
        {
            throw new NotSupportedException($"A {from} component cannot be converted to {to}.");
        }

        ScalarType target = to switch
        {
            IRTypes.VectorComponentsType.Float => ScalarType.Float32,
            IRTypes.VectorComponentsType.Int => ScalarType.Int32,
            IRTypes.VectorComponentsType.Uint => ScalarType.UInt32,
            IRTypes.VectorComponentsType.Double => ScalarType.Float64,
            _ => throw new NotSupportedException($"A {from} component cannot be converted to {to}.")
        };

        return new NumericConversion(value, target);
    }
}
