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
using IRTypes = YotsubaShader.IR.Intermediate.Types;

namespace YotsubaShader.Roslyn;

/// <summary>
/// Llamadas a intrínsecas de GPU: los métodos marcados con <see cref="IntrinsicMethodAttribute"/> (las sintaxis
/// GLSL, HLSL, MSL, WGSL y SPIR-V) y los de <c>System.Math</c> / <c>System.MathF</c>. No se compilan como
/// funciones auxiliares (sus cuerpos solo lanzan una excepción): se convierten en <see cref="IntrinsicCallExpression"/>.
/// </summary>
public partial class StatementOperationProcessor
{
    private static readonly string IntrinsicAttributeName = typeof(IntrinsicMethodAttribute).FullName!;

    /// <summary>La intrínseca que implementa <paramref name="method"/>, si es una.</summary>
    internal static bool TryGetIntrinsic(IMethodSymbol method, out GPUIntrinsic intrinsic)
    {
        intrinsic = default;
        method = method.OriginalDefinition;

        foreach (AttributeData attribute in method.GetAttributes())
        {
            if (attribute.AttributeClass?.ToDisplayString() != IntrinsicAttributeName) continue;

            string name = ((Intrinsic)(int)attribute.ConstructorArguments[0].Value!).ToString();
            return Enum.TryParse(name, out intrinsic);
        }

        string key = $"{method.ContainingType.ToDisplayString()}.{method.Name}";
        if (!CSharpIntrinsicToIntrinsicGPU.Map.TryGetValue(key, out Intrinsic mapped)) return false;

        // Math.Round redondea al par (banker's rounding): es RoundEven, no el Round "sin especificar" de GLSL.
        // Math.Round(x, digits) o con MidpointRounding no tienen equivalente en la GPU.
        if (mapped == Intrinsic.Round && method.Parameters.Length != 1) return false;
        if (mapped == Intrinsic.Round) mapped = Intrinsic.RoundEven;

        return Enum.TryParse(mapped.ToString(), out intrinsic);
    }

    private static bool IsSystemMath(IMethodSymbol method) =>
        method.ContainingType.ToDisplayString() is "System.Math" or "System.MathF";

    private static Storage? StorageOfType(ITypeSymbol type, string name, Compilation compilation, Stage stage)
    {
        StorageKind kind = StorageKind.Local;
        foreach (AttributeData attribute in type.GetAttributes())
        {
            string? attributeName = attribute.AttributeClass?.Name;
            if (attributeName is "TextureAttribute") { kind = StorageKind.Texture; break; }
            if (attributeName is "SamplerAttribute") kind = StorageKind.Sampler;
        }

        try
        {
            return ShaderProcessor.GetStorage(type, name, null, kind, compilation, stage);
        }
        catch (NotSupportedException)
        {
            return null;
        }
    }

    private IOperation[] OrderedArguments(IInvocationOperation operation) =>
        operation.Arguments
            .OrderBy(argument => argument.Parameter?.Ordinal
                                 ?? throw new NotSupportedException(
                                     $"No se pudo resolver un parámetro de '{operation.TargetMethod.Name}'."))
            .Select(argument => argument.Value)
            .ToArray();

    /// <summary>
    /// Convierte la llamada en una intrínseca si lo es. <paramref name="asFloat"/> reduce una llamada de
    /// <c>Math</c> (que trabaja en double) a float cuando sus argumentos son floats ensanchados: el idioma
    /// <c>(float)Math.Sin(x)</c>.
    /// </summary>
    private bool TryProcessIntrinsicCall(
        IInvocationOperation operation,
        Dictionary<ISymbol, Storage> inputs,
        Dictionary<ISymbol, Storage> outputs,
        Compilation compilation,
        Stage stage,
        bool asFloat,
        out GPUIntrinsic intrinsic,
        out List<Expression> arguments,
        out List<Storage?> argumentTypes,
        out Storage? resultType)
    {
        arguments = [];
        argumentTypes = [];
        resultType = null;

        IMethodSymbol method = operation.TargetMethod;
        if (!TryGetIntrinsic(method, out intrinsic)) return false;

        IOperation[] orderedArguments = OrderedArguments(operation);
        Storage? floatType = null;

        if (asFloat)
        {
            floatType = new IRTypes.Float("float", StorageKind.Local, null);
        }

        // new level(x), new bias(x), new gradient2d(dx, dy)...: se sustituyen por sus argumentos.
        var ordered = new List<(IOperation Value, ITypeSymbol Type)>();
        for (int i = 0; i < orderedArguments.Length; i++)
        {
            IOperation candidate = orderedArguments[i];
            while (candidate is IConversionOperation { IsImplicit: true } implicitConversion)
            {
                candidate = implicitConversion.Operand;
            }

            if (candidate is IObjectCreationOperation creation &&
                creation.Type!.GetAttributes().Any(a => a.AttributeClass?.ToDisplayString() == typeof(IntrinsicOptionAttribute).FullName))
            {
                foreach (IArgumentOperation inner in creation.Arguments.OrderBy(a => a.Parameter!.Ordinal))
                {
                    ordered.Add((inner.Value, inner.Parameter!.Type));
                }
            }
            else
            {
                ordered.Add((orderedArguments[i], method.Parameters[i].Type));
            }
        }

        for (int i = 0; i < ordered.Count; i++)
        {
            IOperation value = ordered[i].Value;
            Expression argument;

            if (asFloat)
            {
                // Quita el ensanchamiento float -> double o baja la constante a float.
                if (value is IConversionOperation { Operand.Type.SpecialType: SpecialType.System_Single } widening)
                {
                    value = widening.Operand;
                }

                argument = value.ConstantValue is { HasValue: true, Value: IConvertible constant } &&
                           value.Type?.SpecialType != SpecialType.System_Single
                    ? new FloatLiteral(Convert.ToSingle(constant, System.Globalization.CultureInfo.InvariantCulture))
                    : ProcessExpression(value, inputs, outputs, compilation, stage)
                      ?? throw new NotSupportedException($"El argumento de '{method.Name}' no produjo una expresión.");
                argumentTypes.Add(floatType);
            }
            else
            {
                argument = ProcessExpression(value, inputs, outputs, compilation, stage)
                           ?? throw new NotSupportedException($"El argumento de '{method.Name}' no produjo una expresión.");
                argumentTypes.Add(StorageOfType(ordered[i].Type, "arg" + i, compilation, stage));
            }

            arguments.Add(argument);
        }

        // CalculateLevelOfDetailUnclamped comparte intrínseca con la versión con clamp: el LOD sin clamp es la
        // segunda componente de OpImageQueryLod, que se indica con un literal final.
        if (method.Name.EndsWith("Unclamped") && intrinsic.ToString().StartsWith("QueryLod"))
        {
            arguments.Add(new IntLiteral("1", true));
            argumentTypes.Add(new IRTypes.Int("component", StorageKind.Local, null) { Bits = 32, Signed = true });
        }

        // MSL no distingue el acceso en el tipo del stub (texture2d<T>): si la textura se lee o escribe como
        // imagen, pasa a ser de almacenamiento.
        string intrinsicName = intrinsic.ToString();
        if ((intrinsicName.StartsWith("ImageLoad") || intrinsicName.StartsWith("ImageStore")) &&
            arguments.Count > 0 && arguments[0] is StorageReference { Storage: IRTypes.Texture { Usage: IRTypes.TextureUsage.Sampled } sampledTexture })
        {
            sampledTexture.Usage = IRTypes.TextureUsage.Storage;
        }

        if (!method.ReturnsVoid)
        {
            resultType = asFloat
                ? floatType
                : StorageOfType(method.ReturnType, "__result", compilation, stage);
        }

        return true;
    }

    /// <summary>El caso de `(float)Math.X(...)` con argumentos float: se resuelve en float y sin ida y vuelta por double.</summary>
    private Expression? TryProcessFloatMathConversion(
        IConversionOperation conversion,
        Dictionary<ISymbol, Storage> inputs,
        Dictionary<ISymbol, Storage> outputs,
        Compilation compilation,
        Stage stage)
    {
        if (conversion.Type?.SpecialType != SpecialType.System_Single) return null;
        if (conversion.Operand is not IInvocationOperation invocation) return null;
        if (!IsSystemMath(invocation.TargetMethod) ||
            invocation.TargetMethod.ReturnType.SpecialType != SpecialType.System_Double) return null;
        if (!invocation.Arguments.All(argument => argument.Parameter?.Type.SpecialType == SpecialType.System_Double))
            return null;

        bool allFloat = invocation.Arguments.All(argument =>
            argument.Value is IConversionOperation { Operand.Type.SpecialType: SpecialType.System_Single } ||
            argument.Value.ConstantValue.HasValue);
        if (!allFloat) return null;

        if (!TryProcessIntrinsicCall(invocation, inputs, outputs, compilation, stage, asFloat: true,
                out GPUIntrinsic intrinsic, out var arguments, out var argumentTypes, out var resultType))
        {
            return null;
        }

        return BuildIntrinsicExpression(invocation.TargetMethod, intrinsic, arguments, argumentTypes, resultType);
    }

    private static Expression BuildIntrinsicExpression(
        IMethodSymbol method,
        GPUIntrinsic intrinsic,
        List<Expression> arguments,
        List<Storage?> argumentTypes,
        Storage? resultType)
    {
        // Math.Log(x, newBase) = ln(x) / ln(newBase)
        if (intrinsic == GPUIntrinsic.Log && arguments.Count == 2 && IsSystemMath(method))
        {
            var left = new IntrinsicCallExpression(GPUIntrinsic.Log, [arguments[0]], [argumentTypes[0]], resultType);
            var right = new IntrinsicCallExpression(GPUIntrinsic.Log, [arguments[1]], [argumentTypes[1]], resultType);
            return new BinaryExpression(BinaryOperator.Divide, left, right);
        }

        return new IntrinsicCallExpression(intrinsic, arguments, argumentTypes, resultType);
    }
}
