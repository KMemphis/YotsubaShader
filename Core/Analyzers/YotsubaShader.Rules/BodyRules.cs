using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;

namespace YotsubaShader.Rules;

internal enum StageRequirement
{
    FragmentOnly,
    ComputeOnly
}

/// <summary>Una operación que solo existe en una etapa (derivadas, discard, barreras...).</summary>
internal sealed class StageUsage
{
    public StageUsage(StageRequirement requirement, string name, string detail, Location location)
    {
        Requirement = requirement;
        Name = name;
        Detail = detail;
        Location = location;
    }

    public StageRequirement Requirement { get; }
    public string Name { get; }
    public string Detail { get; }
    public Location Location { get; }
}

/// <summary>Lo que se sabe de un método tras recorrer su cuerpo: a quién llama y qué necesita de la etapa.</summary>
internal sealed class MethodFacts
{
    public MethodFacts(IMethodSymbol method) => Method = method;

    public IMethodSymbol Method { get; }
    public List<(IMethodSymbol Callee, Location Location)> Calls { get; } = new();
    public List<StageUsage> StageUsages { get; } = new();

    /// <summary>Diagnósticos del cuerpo pendientes de saber si el método es código de shader.</summary>
    public List<Diagnostic> Diagnostics { get; } = new();
}

/// <summary>Reglas sobre el cuerpo de los métodos: C# sin traducción, intrínsecas por etapa y portabilidad.</summary>
internal sealed class BodyRules
{
    private readonly TargetSet _set;
    private readonly ShaderTargets _targets;
    private readonly Func<IMethodSymbol, MethodFacts> _factsFor;
    private readonly IMethodSymbol _root;
    private readonly HashSet<string> _portableTypesReported = new();

    private BodyRules(IMethodSymbol root, TargetSet targets, Func<IMethodSymbol, MethodFacts> factsFor)
    {
        _root = root;
        _set = targets;
        _targets = targets.Coarse;
        _factsFor = factsFor;
    }

    /// <summary>
    /// Recorre <paramref name="body"/>. Los diagnósticos van a los <see cref="MethodFacts"/> del método (o de la función
    /// local) donde aparecen; quien llama decide cuándo publicarlos.
    /// </summary>
    public static void Analyze(IMethodSymbol method, IOperation body, TargetSet targets, Func<IMethodSymbol, MethodFacts> factsFor)
    {
        var rules = new BodyRules(method, targets, factsFor);
        MethodFacts facts = factsFor(method);
        if (!ShaderSymbols.IsEntryPoint(method)) rules.AnalyzeSignature(method, facts);
        rules.Visit(body, facts, new Context());
    }

    /// <summary>Firma de una función auxiliar: tipos de parámetros y retorno, parámetros por referencia.</summary>
    private void AnalyzeSignature(IMethodSymbol method, MethodFacts facts)
    {
        foreach (IParameterSymbol parameter in method.Parameters)
        {
            Location location = DeclarationRules.LocationOf(parameter);
            if (parameter.RefKind != RefKind.None && !SupportedFeatures.RefParameters)
            {
                Report(facts, ShaderRules.RefParameter, location, parameter.Name, method.Name, parameter.RefKind.ToString().ToLowerInvariant());
            }

            if (parameter.IsParams)
            {
                Report(facts, ShaderRules.ParamsParameter, location, parameter.Name, method.Name);
            }

            CheckType(parameter.Type, parameter.Name, location, facts);
        }

        if (!method.ReturnsVoid)
        {
            CheckType(method.ReturnType, method.Name, DeclarationRules.LocationOf(method), facts);
        }

        if (method.IsGenericMethod)
        {
            Report(facts, ShaderRules.GenericMethod, DeclarationRules.LocationOf(method), method.Name);
        }
    }

    private sealed class Context
    {
        public int NonUniformDepth;
        public string? NonUniformKind;
    }

    private void Report(MethodFacts facts, DiagnosticDescriptor descriptor, Location location, params object?[] args) =>
        facts.Diagnostics.Add(Diagnostic.Create(descriptor, location, args));

    private void Visit(IOperation? operation, MethodFacts facts, Context context)
    {
        if (operation is null) return;

        switch (operation)
        {
            // Los atributos ([ShaderStage], [TEXCOORD]...) son metadatos, no código de la GPU.
            case IAttributeOperation:
            case IParameterInitializerOperation:
                return;

            case ILocalFunctionOperation localFunction:
                if (!SupportedFeatures.LocalFunctions)
                {
                    Report(facts, ShaderRules.UnsupportedStatement, operation.Syntax.GetLocation(), Loc.T("local function"),
                        Loc.T("Local functions are not supported yet; move it to a method of the class."));
                    return;
                }

                // El cuerpo es otro método: sus llamadas y diagnósticos son suyos.
                MethodFacts local = _factsFor(localFunction.Symbol);
                AnalyzeSignature(localFunction.Symbol, local);
                Visit(localFunction.Body, local, new Context());
                return;

            case ITryOperation:
                Unsupported(facts, operation, "try/catch/finally", Loc.T("The GPU has no exceptions."));
                return;
            case IThrowOperation:
                Unsupported(facts, operation, "throw", Loc.T("The GPU has no exceptions; use a default value or discard."));
                return;
            case IBranchOperation { BranchKind: BranchKind.GoTo }:
            case ILabeledOperation:
                Unsupported(facts, operation, "goto", Loc.T("Use loops and break/continue."));
                return;
            case ILockOperation:
                Unsupported(facts, operation, "lock", Loc.T("To synchronize threads in a compute shader use a barrier."));
                return;
            case IUsingOperation:
            case IUsingDeclarationOperation:
                Unsupported(facts, operation, "using", Loc.T("The GPU has no disposable resources."));
                return;
            case IReturnOperation { Kind: OperationKind.YieldReturn or OperationKind.YieldBreak }:
                Unsupported(facts, operation, "yield", Loc.T("Shaders have no iterators."));
                return;
            case IAwaitOperation:
                Unsupported(facts, operation, "await", Loc.T("Shaders have no asynchronous code."), expression: true);
                return;
            case IAnonymousFunctionOperation:
            case IDelegateCreationOperation:
                Unsupported(facts, operation, "lambda", Loc.T("Shaders have no delegates; write a method."), expression: true);
                return;
            case IDynamicInvocationOperation:
            case IDynamicMemberReferenceOperation:
            case IDynamicObjectCreationOperation:
            case IDynamicIndexerAccessOperation:
                Unsupported(facts, operation, "dynamic", Loc.T("Types are resolved when the shader is compiled."), expression: true);
                return;
            case IFunctionPointerInvocationOperation:
                Unsupported(facts, operation, Loc.T("function pointer"), Loc.T("The GPU has no indirect calls."), expression: true);
                return;
            case IConditionalAccessOperation:
                Unsupported(facts, operation, "?.", Loc.T("There are no null values on the GPU."), expression: true);
                return;
            case ICoalesceOperation:
            case ICoalesceAssignmentOperation:
                Unsupported(facts, operation, "??", Loc.T("There are no null values on the GPU."), expression: true);
                return;
            case IEventReferenceOperation:
            case IEventAssignmentOperation:
                Unsupported(facts, operation, Loc.T("event"), Loc.T("Shaders have no events."), expression: true);
                return;
            case ITypeOfOperation:
                Unsupported(facts, operation, "typeof", Loc.T("Shaders have no reflection."), expression: true);
                return;
            case IInterpolatedStringOperation:
                Unsupported(facts, operation, Loc.T("interpolated string"), Loc.T("The GPU has no strings."), expression: true);
                return;
            case IAnonymousObjectCreationOperation:
                Unsupported(facts, operation, Loc.T("anonymous type"), Loc.T("Declare a struct."), expression: true);
                return;
            case ITranslatedQueryOperation:
                Unsupported(facts, operation, Loc.T("LINQ query"), Loc.T("Use a loop."), expression: true);
                return;
            case IIsTypeOperation:
            case IDeclarationPatternOperation:
            case ITypePatternOperation:
                Unsupported(facts, operation, Loc.T("type pattern"), Loc.T("The types of a shader are known at compile time."), expression: true);
                return;
            case ISwitchOperation when !SupportedFeatures.Switch:
                Unsupported(facts, operation, "switch", Loc.T("Not supported yet; use if/else."));
                return;
            case ISwitchExpressionOperation switchExpression:
                if (!SupportedFeatures.Switch)
                {
                    Unsupported(facts, operation, Loc.T("switch expression"), Loc.T("Not supported yet; use the ?: operator."), expression: true);
                    return;
                }

                if (!switchExpression.Arms.Any(arm => arm.Pattern is IDiscardPatternOperation && arm.Guard is null))
                {
                    Report(facts, ShaderRules.SwitchNotExhaustive, operation.Syntax.GetLocation());
                }

                break;
            case IDefaultValueOperation when !SupportedFeatures.DefaultValue:
                Unsupported(facts, operation, "default", Loc.T("Not supported yet; initialize with an explicit value."), expression: true);
                return;
            case IDeconstructionAssignmentOperation when !SupportedFeatures.Deconstruction:
            case ITupleOperation when !SupportedFeatures.Deconstruction:
                Unsupported(facts, operation, Loc.T("tuple"), Loc.T("Tuples and deconstruction are not supported yet."), expression: true);
                return;
            case IForEachLoopOperation forEach:
                CheckForEach(forEach, facts);
                break;
            case IInvocationOperation invocation:
                CheckInvocation(invocation, facts, context);
                break;
            case IObjectCreationOperation creation:
                CheckObjectCreation(creation, facts);
                break;
            case IArrayCreationOperation arrayCreation:
                CheckArrayCreation(arrayCreation, facts);
                break;
            case IPropertyReferenceOperation { Property.Name: "Length", Instance.Type: IArrayTypeSymbol } length:
                CheckArrayLength(length, facts);
                break;
            case IFieldReferenceOperation { Field: { IsStatic: true, IsConst: false, IsReadOnly: false } field }:
                Report(facts, ShaderRules.MutableStaticField, operation.Syntax.GetLocation(), field.Name);
                return;
            case IVariableDeclaratorOperation declarator:
                CheckType(declarator.Symbol.Type, declarator.Symbol.Name, declarator.Syntax.GetLocation(), facts);
                break;
            case ILiteralOperation { Type: { } literalType } literal:
                if (literalType.SpecialType is SpecialType.System_String or SpecialType.System_Char or SpecialType.System_Decimal ||
                    literal.ConstantValue is { HasValue: true, Value: null })
                {
                    Report(facts, ShaderRules.UnsupportedTypeUse, operation.Syntax.GetLocation(), operation.Syntax.ToString(),
                        literal.ConstantValue.Value is null ? "null" : literalType.ToDisplayString());
                    return;
                }

                break;
            case IBinaryOperation binary:
                CheckBinary(binary, facts);
                break;
            // long/ulong: C# los produce en silencio (uint + int es long) y la GPU no los tiene en el compilador.
            case IConversionOperation { Type.SpecialType: SpecialType.System_Int64 or SpecialType.System_UInt64 } longConversion
                when !SupportedFeatures.Int64:
                Report(facts, ShaderRules.UnsupportedTypeUse, operation.Syntax.GetLocation(), operation.Syntax.ToString(),
                    longConversion.Type!.ToDisplayString() + Loc.T(" (in C#, mixing uint with int gives long: convert one of them)"));
                return;
            case IAddressOfOperation:
            case { Syntax: var pointerSyntax } when pointerSyntax.GetType().Name == "PrefixUnaryExpressionSyntax" && pointerSyntax.ToString().StartsWith("*"):
                Unsupported(facts, operation, Loc.T("pointer"), Loc.T("The GPU has no pointers in shader code."), expression: true);
                return;
            case { Syntax: var unsafeSyntax } when unsafeSyntax.GetType().Name is "StackAllocArrayCreationExpressionSyntax" or "FixedStatementSyntax" or "UnsafeStatementSyntax":
                Unsupported(facts, operation, unsafeSyntax.GetType().Name.StartsWith("StackAlloc") ? "stackalloc" : unsafeSyntax.GetType().Name.StartsWith("Fixed") ? "fixed" : "unsafe",
                    Loc.T("The GPU has no unmanaged memory."));
                return;
            case IConversionOperation { IsChecked: true }:
                Unsupported(facts, operation, "checked", Loc.T("The GPU does not detect overflows."), expression: true);
                return;
            case ISimpleAssignmentOperation or ICompoundAssignmentOperation or IIncrementOrDecrementOperation:
                CheckWriteToInput(operation, facts);
                break;
            case IConditionalOperation conditional:
            {
                Visit(conditional.Condition, facts, context);
                bool varying = DependsOnEntryInput(conditional.Condition);
                if (varying) EnterNonUniform(context, "if");
                Visit(conditional.WhenTrue, facts, context);
                Visit(conditional.WhenFalse, facts, context);
                if (varying) context.NonUniformDepth--;
                return;
            }
            case ILoopOperation loop:
            {
                bool varying = loop switch
                {
                    IWhileLoopOperation w => DependsOnEntryInput(w.Condition),
                    IForLoopOperation f => DependsOnEntryInput(f.Condition),
                    _ => false
                };
                if (varying) EnterNonUniform(context, "bucle");
                foreach (IOperation child in operation.ChildOperations) Visit(child, facts, context);
                if (varying) context.NonUniformDepth--;
                return;
            }
        }

        foreach (IOperation child in operation.ChildOperations)
        {
            Visit(child, facts, context);
        }
    }

    private static void EnterNonUniform(Context context, string kind)
    {
        if (context.NonUniformDepth == 0) context.NonUniformKind = kind;
        context.NonUniformDepth++;
    }

    private void Unsupported(MethodFacts facts, IOperation operation, string what, string hint, bool expression = false) =>
        Report(facts, expression ? ShaderRules.UnsupportedExpression : ShaderRules.UnsupportedStatement,
            operation.Syntax.GetLocation(), what, hint);

    private void CheckType(ITypeSymbol type, string name, Location location, MethodFacts facts)
    {
        GpuType gpuType = GpuType.Of(type);
        if (gpuType.Kind == GpuTypeKind.Unsupported || IsForbiddenStruct(type))
        {
            Report(facts, ShaderRules.UnsupportedTypeUse, location, name, type.ToDisplayString());
            return;
        }

        GpuScalar scalar = gpuType.Kind == GpuTypeKind.Array ? GpuType.ScalarOf(((IArrayTypeSymbol)type).ElementType) : gpuType.Scalar;
        if (scalar is GpuScalar.Int64 or GpuScalar.UInt64 && !SupportedFeatures.Int64)
        {
            Report(facts, ShaderRules.UnsupportedTypeUse, location, name, type.ToDisplayString() + Loc.T(" (64-bit integers are not supported yet)"));
            return;
        }

        ReportPortableScalar(scalar, type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat), location, facts);
    }

    /// <summary>Structs de .NET que GpuType acepta como "struct" pero que no son datos de GPU.</summary>
    public static bool IsForbiddenStruct(ITypeSymbol type) =>
        GpuType.Of(type).Kind == GpuTypeKind.Struct && (type.IsRefLikeType ||
        type.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T ||
        (!SupportedFeatures.Deconstruction && type.IsTupleType) ||
        type.ContainingNamespace?.ToDisplayString() is { } ns && (ns == "System" || ns.StartsWith("System.")) &&
        !type.IsTupleType && ShaderSymbols.FullName(type) != "System.Half");

    private void ReportPortableScalar(GpuScalar scalar, string typeName, Location location, MethodFacts facts)
    {
        ApiFeature? feature = scalar switch
        {
            GpuScalar.Double => ApiFeature.Float64,
            GpuScalar.Half => ApiFeature.Float16,
            GpuScalar.Int16 or GpuScalar.UInt16 => ApiFeature.Int16,
            GpuScalar.Int64 or GpuScalar.UInt64 => ApiFeature.Int64,
            _ => null
        };

        if (feature is null || !_portableTypesReported.Add(scalar.ToString())) return;
        if (_set.Check(feature.Value) is not { } issue) return;

        DiagnosticDescriptor descriptor = issue.Unavailable ? ShaderRules.AsError(ShaderRules.PortableType) : ShaderRules.PortableType;
        Report(facts, descriptor, location, typeName, issue.Reasons, issue.Targets);
    }

    private void CheckBinary(IBinaryOperation binary, MethodFacts facts)
    {
        if (binary.IsChecked)
        {
            Unsupported(facts, binary, "checked", Loc.T("The GPU does not detect overflows."), expression: true);
        }

        if (binary.OperatorKind is BinaryOperatorKind.Divide or BinaryOperatorKind.Remainder &&
            binary.Type is { } type && GpuType.Of(type).IsInteger &&
            binary.RightOperand.ConstantValue is { HasValue: true, Value: { } divisor } && Convert.ToInt64(divisor) == 0)
        {
            Report(facts, ShaderRules.IntegerDivisionByZero, binary.Syntax.GetLocation());
        }

        if (binary.OperatorMethod is { } method && !IsGpuMethod(method))
        {
            Report(facts, ShaderRules.NoGpuTranslation, binary.Syntax.GetLocation(), method.ToDisplayString());
        }
    }

    private void CheckObjectCreation(IObjectCreationOperation creation, MethodFacts facts)
    {
        if (creation.Type is { } type && (GpuType.Of(type).Kind == GpuTypeKind.Unsupported || IsForbiddenStruct(type)) &&
            !ShaderSymbols.HasAttribute(type, ShaderNames.IntrinsicOption))
        {
            Report(facts, ShaderRules.UnsupportedTypeUse, creation.Syntax.GetLocation(), "new " + type.Name, type.ToDisplayString());
        }

        if (creation.Parent is IExpressionStatementOperation)
        {
            Report(facts, ShaderRules.UnusedObjectCreation, creation.Syntax.GetLocation(), creation.Type?.Name ?? "?");
        }
    }

    private void CheckArrayCreation(IArrayCreationOperation creation, MethodFacts facts)
    {
        if (creation.Type is IArrayTypeSymbol { Rank: > 1 })
        {
            Report(facts, ShaderRules.UnsupportedArray, creation.Syntax.GetLocation(), Loc.T("Multidimensional arrays do not exist on the GPU"));
        }
        else if (creation.Type is IArrayTypeSymbol { ElementType: IArrayTypeSymbol })
        {
            Report(facts, ShaderRules.UnsupportedArray, creation.Syntax.GetLocation(), Loc.T("Arrays of arrays do not exist on the GPU"));
        }
        else if (creation.DimensionSizes.Length == 1 && !creation.DimensionSizes[0].ConstantValue.HasValue)
        {
            Report(facts, ShaderRules.UnsupportedArray, creation.Syntax.GetLocation(), Loc.T("The array length is not constant"));
        }
    }

    private void CheckForEach(IForEachLoopOperation forEach, MethodFacts facts)
    {
        IOperation collection = forEach.Collection;
        while (collection is IConversionOperation { IsImplicit: true } conversion) collection = conversion.Operand;

        // Un campo List<T>/ImmutableArray<T> del shader es un buffer: se recorre como su array.
        if (collection is IFieldReferenceOperation { Field: { } bufferField } &&
            ResourceRules.CollectionElement(bufferField.Type, out _) is not null)
        {
            return;
        }

        if (collection.Type is not IArrayTypeSymbol)
        {
            if (!(SupportedFeatures.ForEachOverVectors && GpuType.Of(collection.Type).Kind == GpuTypeKind.Vector))
            {
                Report(facts, ShaderRules.UnsupportedStatement, forEach.Collection.Syntax.GetLocation(), Loc.F("foreach over {0}", collection.Type?.ToDisplayString() ?? "?"), Loc.T("Only an array can be iterated."));
            }

            return;
        }

        if (!HasKnownLength(collection))
        {
            Report(facts, ShaderRules.UnsupportedStatement, forEach.Collection.Syntax.GetLocation(), Loc.T("foreach over this array"),
                Loc.T("Its length is not known at compile time: use a local array new T[N], a parameter or a field with [ArrayLength(n)]."));
        }
    }

    private void CheckArrayLength(IPropertyReferenceOperation length, MethodFacts facts)
    {
        if (!HasKnownLength(length.Instance!))
        {
            Report(facts, ShaderRules.UnsupportedExpression, length.Syntax.GetLocation(), "Length",
                Loc.T("The length is not known at compile time: use a local array new T[N], a parameter or a field with [ArrayLength(n)]."));
        }
    }

    /// <summary>
    /// ¿Se conoce la longitud del array al compilar? Un local creado con <c>new T[constante]</c>, un parámetro de
    /// una función auxiliar (toma la del argumento) o un campo con [ArrayLength(n)].
    /// </summary>
    private static bool HasKnownLength(IOperation array)
    {
        while (array is IConversionOperation { IsImplicit: true } conversion) array = conversion.Operand;

        switch (array)
        {
            case IParameterReferenceOperation { Parameter: { } parameter }:
                return parameter.ContainingSymbol is IMethodSymbol owner && !ShaderSymbols.IsEntryPoint(owner);
            case IFieldReferenceOperation { Field: { } field } fieldReference:
                return ShaderSymbols.HasAttribute(field, ShaderNames.ArrayLength) || IsInsideStorageBuffer(fieldReference) ||
                       ResourceRules.KindOf(field, field.Type) == ResourceKind.StorageBuffer;
            case IPropertyReferenceOperation { Property: { } property }:
                return ShaderSymbols.HasAttribute(property, ShaderNames.ArrayLength);
        }

        if (array is not ILocalReferenceOperation { Local: { } local }) return false;

        IOperation? root = array;
        while (root.Parent is not null) root = root.Parent;

        foreach (IOperation operation in root.DescendantsAndSelf())
        {
            if (operation is IVariableDeclaratorOperation declarator &&
                SymbolEqualityComparer.Default.Equals(declarator.Symbol, local))
            {
                IOperation? value = declarator.Initializer?.Value;
                while (value is IConversionOperation { IsImplicit: true } conversion) value = conversion.Operand;
                return value is IArrayCreationOperation creation &&
                       creation.DimensionSizes.Length == 1 && creation.DimensionSizes[0].ConstantValue.HasValue;
            }
        }

        return false;
    }

    /// <summary>¿El campo se lee a través de un storage buffer? Ahí un array sin tamaño tiene longitud en la GPU.</summary>
    private static bool IsInsideStorageBuffer(IFieldReferenceOperation reference)
    {
        IOperation? current = reference.Instance;
        while (current is not null)
        {
            switch (current)
            {
                case IFieldReferenceOperation { Field: { } owner } ownerReference:
                    if (ResourceRules.KindOf(owner, owner.Type) == ResourceKind.StorageBuffer) return true;
                    current = ownerReference.Instance;
                    break;
                case IArrayElementReferenceOperation element:
                    current = element.ArrayReference;
                    break;
                case IConversionOperation conversion:
                    current = conversion.Operand;
                    break;
                default:
                    return false;
            }
        }

        return false;
    }

    private void CheckWriteToInput(IOperation assignment, MethodFacts facts)
    {
        IOperation? target = assignment switch
        {
            ISimpleAssignmentOperation simple => simple.Target,
            ICompoundAssignmentOperation compound => compound.Target,
            IIncrementOrDecrementOperation increment => increment.Target,
            _ => null
        };

        // ¿Se escribe dentro de un storage buffer de solo lectura? (buffer.Items[i].X = ...)
        for (IOperation? current = target; current is not null;)
        {
            if (current is IFieldReferenceOperation { Field: { } field } fieldReference)
            {
                if (ResourceRules.KindOf(field, field.Type) == ResourceKind.StorageBuffer && ResourceRules.IsReadOnlyBuffer(field))
                {
                    Report(facts, ShaderRules.ReadOnlyBufferWrite, assignment.Syntax.GetLocation(), field.Name);
                    return;
                }

                current = fieldReference.Instance;
            }
            else if (current is IArrayElementReferenceOperation element) current = element.ArrayReference;
            else if (current is IPropertyReferenceOperation property) current = property.Instance;
            else break;
        }

        while (target is IFieldReferenceOperation { Instance: { } instance }) target = instance;
        while (target is IPropertyReferenceOperation { Instance: { } instance }) target = instance;

        if (target is IParameterReferenceOperation { Parameter: { RefKind: RefKind.None } parameter } &&
            parameter.ContainingSymbol is IMethodSymbol owner && ShaderSymbols.IsEntryPoint(owner))
        {
            Report(facts, ShaderRules.WriteToInput, assignment.Syntax.GetLocation(), parameter.Name);
        }
    }

    // ── Llamadas ──────────────────────────────────────────────────────────────────────────────────

    private void CheckInvocation(IInvocationOperation invocation, MethodFacts facts, Context context)
    {
        IMethodSymbol method = invocation.TargetMethod;
        Location location = invocation.Syntax.GetLocation();

        if (ShaderSymbols.IsEntryPoint(method.OriginalDefinition))
        {
            Report(facts, ShaderRules.EntryPointCalled, location, method.Name);
            return;
        }

        if (IntrinsicName(method) is { } intrinsic)
        {
            CheckIntrinsic(intrinsic, invocation, facts, context);
            return;
        }

        // float3(x, y, z), vec3<T>(...): los constructores de las sintaxis se compilan como un vector.
        if (ShaderSymbols.HasAttribute(method.OriginalDefinition, ShaderNames.ConstructorMethod))
        {
            return;
        }

        if (MathSupport.IsMath(method))
        {
            if (method.Parameters.Any(p => p.Type.SpecialType == SpecialType.System_Decimal) ||
                method.ReturnType.SpecialType == SpecialType.System_Decimal)
            {
                Report(facts, ShaderRules.UnsupportedTypeUse, location, method.Name, "decimal");
            }
            else if (!MathSupport.IsSupported(method))
            {
                Report(facts, ShaderRules.NoGpuTranslation, location, method.ContainingType.Name + "." + method.Name);
            }
            else if (MathSupport.SpecialValueTest(method) is { } test)
            {
                ReportFloatSpecialValues(test, location, facts);
            }

            return;
        }

        if (method.MethodKind == MethodKind.LocalFunction)
        {
            facts.Calls.Add((method.OriginalDefinition, location));
            return;
        }

        if (method.IsGenericMethod)
        {
            Report(facts, ShaderRules.GenericMethod, location, method.Name);
        }

        if (method.IsVirtual || method.IsAbstract || method.IsOverride || method.ContainingType?.TypeKind == TypeKind.Interface)
        {
            if (!method.ContainingType!.IsValueType || method.IsAbstract || method.ContainingType.TypeKind == TypeKind.Interface)
            {
                Report(facts, ShaderRules.VirtualCall, location, method.Name,
                    method.ContainingType.TypeKind == TypeKind.Interface ? Loc.T("an interface method") : "virtual");
            }
        }

        if (method.IsAsync)
        {
            Report(facts, ShaderRules.AsyncOrIterator, location, method.Name, "async");
        }

        foreach (IParameterSymbol parameter in method.Parameters)
        {
            if (parameter.IsParams)
            {
                Report(facts, ShaderRules.ParamsParameter, location, parameter.Name, method.Name);
            }
        }

        if (!IsGpuMethod(method))
        {
            Report(facts, ShaderRules.NoGpuTranslation, location, method.ToDisplayString(SymbolDisplayFormat.CSharpShortErrorMessageFormat));
            return;
        }

        // Cada array que se pasa a una función auxiliar debe tener una longitud conocida al compilar.
        foreach (IArgumentOperation argument in invocation.Arguments)
        {
            if (argument.Parameter?.Type is IArrayTypeSymbol && !HasKnownLength(argument.Value))
            {
                Report(facts, ShaderRules.UnsupportedArray, argument.Syntax.GetLocation(),
                    Loc.F("The length of the array passed to '{0}' is not known at compile time", method.Name));
            }
        }

        if (method.DeclaringSyntaxReferences.Length > 0)
        {
            facts.Calls.Add((method.OriginalDefinition, location));
        }
    }

    /// <summary>¿Se puede compilar a la GPU? No los métodos de .NET (salvo Math), sí los del proyecto y los de las sintaxis.</summary>
    private static bool IsGpuMethod(IMethodSymbol method)
    {
        if (IntrinsicName(method) is not null || MathSupport.IsMath(method)) return true;
        string? assembly = method.ContainingAssembly?.Name;
        if (assembly is null) return true;
        return !(assembly == "mscorlib" || assembly == "netstandard" || assembly.StartsWith("System.") || assembly == "System" ||
                 assembly.StartsWith("Microsoft."));
    }

    public static string? IntrinsicName(IMethodSymbol method)
    {
        foreach (AttributeData attribute in method.OriginalDefinition.GetAttributes())
        {
            if (ShaderSymbols.FullName(attribute.AttributeClass) != ShaderNames.IntrinsicMethod) continue;
            if (attribute.ConstructorArguments.Length != 1) return null;
            TypedConstant argument = attribute.ConstructorArguments[0];
            if (argument.Type is INamedTypeSymbol { TypeKind: TypeKind.Enum } enumType)
            {
                foreach (IFieldSymbol field in enumType.GetMembers().OfType<IFieldSymbol>())
                {
                    if (field.HasConstantValue && Equals(field.ConstantValue, argument.Value)) return field.Name;
                }
            }

            return argument.Value?.ToString();
        }

        return null;
    }

    private void CheckIntrinsic(string intrinsic, IInvocationOperation invocation, MethodFacts facts, Context context)
    {
        Location location = invocation.Syntax.GetLocation();
        string name = invocation.TargetMethod.Name;

        bool implicitLod = intrinsic.StartsWith("Sample") && !intrinsic.StartsWith("SampleLevel") &&
                           !intrinsic.StartsWith("SampleGrad") && !intrinsic.StartsWith("SampleCompareLevel") ||
                           intrinsic.StartsWith("QueryLod");
        bool derivative = intrinsic.StartsWith("Derivative") || intrinsic.StartsWith("Fwidth");

        if (implicitLod)
        {
            facts.StageUsages.Add(new StageUsage(StageRequirement.FragmentOnly, name, Loc.T("(sampling with implicit LOD, uses derivatives)"), location));
        }
        else if (derivative)
        {
            facts.StageUsages.Add(new StageUsage(StageRequirement.FragmentOnly, name, Loc.T("(screen-space derivative)"), location));
        }
        else if (intrinsic is "Discard" or "DiscardIfNegative")
        {
            facts.StageUsages.Add(new StageUsage(StageRequirement.FragmentOnly, name, Loc.T("(discards the fragment)"), location));
        }
        else if (intrinsic.StartsWith("InterpolateAt"))
        {
            facts.StageUsages.Add(new StageUsage(StageRequirement.FragmentOnly, name, Loc.T("(interpolates a fragment input)"), location));
            ReportPortableIntrinsic(name, Loc.T("does not exist in WGSL"), ShaderTargets.WebGPU, location, facts);
        }
        else if (intrinsic is "ThreadGroupBarrier" or "MemoryBarrierShared" or "MemoryBarrierGroup")
        {
            facts.StageUsages.Add(new StageUsage(StageRequirement.ComputeOnly, name, "", location));
        }

        if ((implicitLod || derivative || intrinsic == "ThreadGroupBarrier") && context.NonUniformDepth > 0)
        {
            ShaderTargets affected = _targets;
            if (affected != ShaderTargets.None)
            {
                Report(facts, ShaderRules.NonUniformDerivative, location, name, context.NonUniformKind, ShaderTargetsParser.Describe(affected));
            }
        }

        if (intrinsic.StartsWith("Atomic"))
        {
            IOperation? target = invocation.Arguments.OrderBy(a => a.Parameter?.Ordinal ?? 0).FirstOrDefault()?.Value;
            if (!IsBufferMember(target))
            {
                Report(facts, ShaderRules.AtomicTarget, location, name);
            }
        }

        if (intrinsic is "IsNaN" or "IsInfinity" or "IsFinite")
        {
            ReportFloatSpecialValues(name, location, facts);
        }

        if (intrinsic.StartsWith("Sample") || intrinsic.StartsWith("Gather") || intrinsic.StartsWith("TexelFetch"))
        {
            CheckTextureArguments(intrinsic, invocation, facts);
        }

        if (intrinsic == "TexelFetchCube")
        {
            Report(facts, ShaderRules.UnsupportedIntrinsic, location, name, Loc.T("reads a texel from a cube texture, and SPIR-V does not allow it (OpImageFetch does not support cube)"));
        }
    }

    private void CheckTextureArguments(string intrinsic, IInvocationOperation invocation, MethodFacts facts)
    {
        var arguments = invocation.Arguments.OrderBy(a => a.Parameter?.Ordinal ?? 0).ToList();
        int offsets = 0;
        foreach (IArgumentOperation argument in arguments)
        {
            ITypeSymbol? type = argument.Parameter?.Type;
            if (type is null) continue;
            GpuType gpuType = GpuType.Of(type);
            bool isOffset = gpuType.Kind == GpuTypeKind.Vector && gpuType.Scalar == GpuScalar.Int32 ||
                            gpuType.IsScalar(GpuScalar.Int32) && argument.Parameter!.Name.IndexOf("offset", StringComparison.OrdinalIgnoreCase) >= 0;
            if (!isOffset) continue;
            offsets++;

            if (!IsConstant(argument.Value))
            {
                ShaderTargets affected = _targets & (ShaderTargets.D3D12 | ShaderTargets.WebGPU | ShaderTargets.OpenGL);
                if (affected != ShaderTargets.None)
                {
                    Report(facts, ShaderRules.PortableIntrinsic, argument.Syntax.GetLocation(), invocation.TargetMethod.Name,
                        Loc.T("uses a texel offset that is not constant: HLSL, WGSL and GLSL require a value known at compile time"),
                        ShaderTargetsParser.Describe(affected));
                }
            }
        }

        if (offsets >= 4)
        {
            Report(facts, ShaderRules.UnsupportedIntrinsic, invocation.Syntax.GetLocation(), invocation.TargetMethod.Name,
                Loc.T("with four different offsets is not supported yet"));
        }
        else if (offsets > 0 && intrinsic.Contains("Cube"))
        {
            Report(facts, ShaderRules.UnsupportedIntrinsic, invocation.Syntax.GetLocation(), invocation.TargetMethod.Name,
                Loc.T("does not support offsets on a cube texture"));
        }
    }

    private static bool IsConstant(IOperation operation)
    {
        if (operation.ConstantValue.HasValue) return true;
        while (operation is IConversionOperation { IsImplicit: true } conversion) operation = conversion.Operand;
        return operation is IObjectCreationOperation creation && creation.Arguments.All(a => IsConstant(a.Value));
    }

    /// <summary>¿El destino de una atómica está dentro de un storage buffer? (Data.Items[i].Count, Items[i].X...)</summary>
    private static bool IsBufferMember(IOperation? target)
    {
        while (target is not null)
        {
            switch (target)
            {
                case IFieldReferenceOperation { Field: { } field } fieldReference:
                    if (ResourceRules.KindOf(field, field.Type) == ResourceKind.StorageBuffer) return true;
                    target = fieldReference.Instance;
                    break;
                case IPropertyReferenceOperation property:
                    target = property.Instance;
                    break;
                case IArrayElementReferenceOperation element:
                    target = element.ArrayReference;
                    break;
                case IConversionOperation conversion:
                    target = conversion.Operand;
                    break;
                default:
                    return false;
            }
        }

        return false;
    }

    private void ReportPortableIntrinsic(string name, string why, ShaderTargets affected, Location location, MethodFacts facts)
    {
        affected &= _targets;
        if (affected == ShaderTargets.None) return;
        Report(facts, ShaderRules.PortableIntrinsic, location, name, why, ShaderTargetsParser.Describe(affected));
    }

    private void ReportFloatSpecialValues(string name, Location location, MethodFacts facts)
    {
        ShaderTargets affected = _targets & (ShaderTargets.WebGPU | ShaderTargets.Metal);
        if (affected == ShaderTargets.None) return;
        Report(facts, ShaderRules.FloatSpecialValues, location, name, ShaderTargetsParser.Describe(affected));
    }

    /// <summary>¿La condición lee una entrada del punto de entrada (un valor distinto por invocación)?</summary>
    private bool DependsOnEntryInput(IOperation? condition)
    {
        if (condition is null || !ShaderSymbols.IsEntryPoint(_root)) return false;
        return condition.DescendantsAndSelf().Any(o =>
            o is IParameterReferenceOperation { Parameter: { RefKind: RefKind.None } parameter } &&
            SymbolEqualityComparer.Default.Equals(parameter.ContainingSymbol, _root));
    }
}
