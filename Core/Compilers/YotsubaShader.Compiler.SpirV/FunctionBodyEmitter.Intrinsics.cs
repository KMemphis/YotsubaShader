using YotsubaShader.Compiler.SpirV.Builder;
using YotsubaShader.Compiler.SpirV.YS_Types;
using YotsubaShader.IR.Intermediate;
using YotsubaShader.IR.Intermediate.FunctionBody;
using YotsubaShader.IR.Intermediate.FunctionBody.Expressions.FunctionCalls;
using YotsubaShader.IR.Intermediate.FunctionBody.Statements;
using IRTypes = YotsubaShader.IR.Intermediate.Types;

namespace YotsubaShader.Compiler.SpirV;

/// <summary>
/// Intrínsecas de GPU (<see cref="GPUIntrinsic"/>): matemática por GLSL.std.450 y operaciones nativas de SPIR-V.
/// Las de texturas están en <c>FunctionBodyEmitter.Textures.cs</c>.
/// </summary>
internal sealed partial class FunctionBodyEmitter
{
    private const string GlslStd = "%glsl_std_450";

    /// <summary>Números de instrucción del conjunto GLSL.std.450.</summary>
    private enum Glsl
    {
        Round = 1, RoundEven = 2, Trunc = 3, FAbs = 4, SAbs = 5, FSign = 6, SSign = 7, Floor = 8, Ceil = 9, Fract = 10,
        Radians = 11, Degrees = 12, Sin = 13, Cos = 14, Tan = 15, Asin = 16, Acos = 17, Atan = 18, Sinh = 19,
        Cosh = 20, Tanh = 21, Asinh = 22, Acosh = 23, Atanh = 24, Atan2 = 25, Pow = 26, Exp = 27, Log = 28,
        Exp2 = 29, Log2 = 30, Sqrt = 31, InverseSqrt = 32, Determinant = 33, MatrixInverse = 34, Modf = 35,
        ModfStruct = 36, FMin = 37, UMin = 38, SMin = 39, FMax = 40, UMax = 41, SMax = 42, FClamp = 43,
        UClamp = 44, SClamp = 45, FMix = 46, Step = 48, SmoothStep = 49, Fma = 50, Frexp = 51, FrexpStruct = 52,
        Ldexp = 53, PackSnorm4x8 = 54, PackUnorm4x8 = 55, PackSnorm2x16 = 56, PackUnorm2x16 = 57,
        PackHalf2x16 = 58, UnpackSnorm2x16 = 60, UnpackUnorm2x16 = 61, UnpackHalf2x16 = 62,
        UnpackSnorm4x8 = 63, UnpackUnorm4x8 = 64, Length = 66, Distance = 67, Cross = 68, Normalize = 69,
        FaceForward = 70, Reflect = 71, Refract = 72, FindILsb = 73, FindSMsb = 74, FindUMsb = 75,
        InterpolateAtCentroid = 76, InterpolateAtSample = 77, InterpolateAtOffset = 78
    }

    private enum Num { Float, Double, Int, Uint, Bool, Half, Other }

    /// <summary>Una llamada intrínseca ya desempaquetada.</summary>
    private sealed record IntrinsicCall(
        string Name,
        IReadOnlyList<Expression> Args,
        IReadOnlyList<Storage?> Types,
        Storage? Result);

    // ---------------------------------------------------------------------------------------- entradas

    private string EmitIntrinsicValue(IntrinsicCallExpression call, ref Function function)
    {
        var data = new IntrinsicCall(call.Intrinsic.ToString(), call.Arguments, call.ArgumentTypes, call.ResultType);
        return EmitIntrinsic(data, ref function)
               ?? throw new NotSupportedException($"La intrínseca '{data.Name}' no produce un valor.");
    }

    private void EmitIntrinsicStatement(IntrinsicCallStatement statement, ref Function function)
    {
        var data = new IntrinsicCall(statement.Intrinsic.ToString(), statement.Arguments, statement.ArgumentTypes, null);
        EmitIntrinsic(data, ref function);
    }

    private string? EmitIntrinsic(IntrinsicCall c, ref Function function)
    {
        string name = c.Name;

        if (name.StartsWith("Atomic")) return EmitAtomic(c, ref function);

        if (name.StartsWith("Sample") || name.StartsWith("Gather") || name.StartsWith("TexelFetch") ||
            name.StartsWith("Image") || name.StartsWith("TextureSize") || name.StartsWith("Query"))
        {
            return EmitTextureIntrinsic(c, ref function);
        }

        if (name == "DiscardIfNegative" && c.Args.Count > 0 && ArgType(c, 0) is IRTypes.Matrix)
        {
            throw new NotSupportedException("La intrínseca 'DiscardIfNegative' no está soportada sobre matrices.");
        }

        switch (name)
        {
            case "Discard":
                _module.AddLine(SpirVOpTable.OpKill);
                _terminated = true;
                return null;

            case "DiscardIfNegative":
                return EmitDiscardIfNegative(c, ref function);

            case "ThreadGroupBarrier":
                return EmitBarrier(c, ref function);
        }

        if (name.StartsWith("MemoryBarrier")) return EmitBarrier(c, ref function);

        if (name.StartsWith("InterpolateAt")) return EmitInterpolate(c, ref function);

        if (name.StartsWith("Derivative") || name.StartsWith("Fwidth"))
        {
            if (c.Args.Count > 0 && ArgType(c, 0) is IRTypes.Matrix)
            {
                string[] matrixIds = [EmitValue(c.Args[0], ref function)];
                return EmitPerColumn(c, matrixIds, ref function);
            }

            return EmitDerivative(c, ref function);
        }

        if (name is "SinCos" or "Frexp" or "Modf" or "AddCarry" or "SubtractBorrow" or "MultiplyExtended" or "DiscardIfNegative"
            && c.Args.Count > 0 && ArgType(c, 0) is IRTypes.Matrix)
        {
            throw new NotSupportedException($"La intrínseca '{name}' no está soportada sobre matrices.");
        }

        if (name is "SinCos" or "Frexp" or "Modf" or "AddCarry" or "SubtractBorrow" or "MultiplyExtended")
        {
            return EmitWithOutputs(c, ref function);
        }

        var ids = new string[c.Args.Count];
        for (int i = 0; i < ids.Length; i++) ids[i] = EmitValue(c.Args[i], ref function);

        return EmitValueIntrinsic(c, ids, ref function);
    }

    // ---------------------------------------------------------------------------------------- utilidades

    private string Tmp(Function f) => $"%value_{f.HumanName}_{f.Id.Value}_{_nextTemporaryValue++}";

    private static SpirVInstructionParameter P(string id) => SpirVModuleHelpers.Id(id);

    private static SpirVInstructionParameter Lit(uint value) =>
        new(value.ToString(), SpirVInstructionType.UIntValue);

    private string Emit(Function f, SpirVOperation op, string typeId, params string[] operands)
    {
        string id = Tmp(f);
        var parameters = new SpirVInstructionParameter[operands.Length + 1];
        parameters[0] = P(typeId);
        for (int i = 0; i < operands.Length; i++) parameters[i + 1] = P(operands[i]);
        _module.AddResultLine(id, op, parameters);
        return id;
    }

    private string EmitRaw(Function f, SpirVOperation op, string typeId, params SpirVInstructionParameter[] operands)
    {
        string id = Tmp(f);
        var parameters = new SpirVInstructionParameter[operands.Length + 1];
        parameters[0] = P(typeId);
        operands.CopyTo(parameters, 1);
        _module.AddResultLine(id, op, parameters);
        return id;
    }

    private string Ext(Function f, Glsl instruction, string typeId, params string[] operands)
    {
        var parameters = new SpirVInstructionParameter[operands.Length + 2];
        parameters[0] = P(GlslStd);
        parameters[1] = Lit((uint)instruction);
        for (int i = 0; i < operands.Length; i++) parameters[i + 2] = P(operands[i]);
        return EmitRaw(f, SpirVOpTable.OpExtInst, typeId, parameters);
    }

    private string TypeIdOf(Storage type, Function f) => _module.DeclareType(type, f.Stage);

    private static Num NumOf(Storage? type) => type switch
    {
        IRTypes.Float => Num.Float,
        IRTypes.Double => Num.Double,
        IRTypes.Half => Num.Half,
        IRTypes.Int { Signed: true } => Num.Int,
        IRTypes.Int => Num.Uint,
        IRTypes.Bool => Num.Bool,
        IRTypes.Vector v => v.ComponentsType switch
        {
            IRTypes.VectorComponentsType.Float => Num.Float,
            IRTypes.VectorComponentsType.Double => Num.Double,
            IRTypes.VectorComponentsType.Half => Num.Half,
            IRTypes.VectorComponentsType.Int => Num.Int,
            IRTypes.VectorComponentsType.Uint => Num.Uint,
            IRTypes.VectorComponentsType.Bool => Num.Bool,
            _ => Num.Other
        },
        IRTypes.Matrix m => m.ComponentsType == IRTypes.MatrixComponentType.Double ? Num.Double
            : m.ComponentsType == IRTypes.MatrixComponentType.Half ? Num.Half : Num.Float,
        _ => Num.Other
    };

    private static int WidthOf(Storage? type) => type is IRTypes.Vector v ? v.ComponentsLength : 1;

    private static Storage Scalar(Num n) => n switch
    {
        Num.Float => new IRTypes.Float("f", StorageKind.Local, null) { InitialValue = null },
        Num.Double => new IRTypes.Double("d", StorageKind.Local, null) { InitialValue = null },
        Num.Half => new IRTypes.Half("h", StorageKind.Local, null) { InitialValue = null },
        Num.Int => new IRTypes.Int("i", StorageKind.Local, null) { Bits = 32, Signed = true, InitialValue = null },
        Num.Uint => new IRTypes.Int("u", StorageKind.Local, null) { Bits = 32, Signed = false, InitialValue = null },
        Num.Bool => new IRTypes.Bool("b", StorageKind.Local, null) { InitialValue = null },
        _ => throw new NotSupportedException("Tipo escalar no soportado por la intrínseca.")
    };

    private static Storage TypeOfNum(Num n, int width) =>
        width == 1
            ? Scalar(n)
            : new IRTypes.Vector("v", StorageKind.Local, null)
            {
                ComponentsLength = width,
                ComponentsType = n switch
                {
                    Num.Float => IRTypes.VectorComponentsType.Float,
                    Num.Double => IRTypes.VectorComponentsType.Double,
                    Num.Half => IRTypes.VectorComponentsType.Half,
                    Num.Int => IRTypes.VectorComponentsType.Int,
                    Num.Uint => IRTypes.VectorComponentsType.Uint,
                    Num.Bool => IRTypes.VectorComponentsType.Bool,
                    _ => throw new NotSupportedException("Tipo de vector no soportado por la intrínseca.")
                }
            };

    private string ScalarTypeId(Num n, Function f) => TypeIdOf(Scalar(n), f);

    private string VectorTypeId(Num n, int width, Function f) => TypeIdOf(TypeOfNum(n, width), f);

    private string Splat(string scalarId, Num n, int width, Function f)
    {
        if (width == 1) return scalarId;
        return Emit(f, SpirVOpTable.OpCompositeConstruct, VectorTypeId(n, width, f),
            Enumerable.Repeat(scalarId, width).ToArray());
    }

    private string ConstOf(Num n, double value, int width, Function f)
    {
        string scalar = n switch
        {
            Num.Float => _module.DeclareFloatConstant((float)value),
            Num.Double => _module.DeclareDoubleConstant(value),
            Num.Half => _module.DeclareHalfConstant((float)value),
            Num.Int => _module.DeclareIntConstant(((long)value).ToString(), 32, true),
            Num.Uint => _module.DeclareIntConstant(((long)value).ToString(), 32, false),
            Num.Bool => _module.DeclareBoolConstant(value != 0),
            _ => throw new NotSupportedException("Constante no soportada por la intrínseca.")
        };

        return Splat(scalar, n, width, f);
    }

    /// <summary>Un escalar usado donde se espera un vector (mix(vec, vec, float), clamp(vec, float, float)) se replica.</summary>
    private string Broadcast(string id, Storage? type, Num n, int width, Function f) =>
        WidthOf(type) == 1 && width > 1 ? Splat(id, n, width, f) : id;

    private Storage ArgType(IntrinsicCall c, int index)
    {
        Storage? type = index < c.Types.Count ? c.Types[index] : null;
        type ??= TryGetExpressionStorage(c.Args[index]);
        return type ?? throw new NotSupportedException(
            $"No se pudo determinar el tipo del argumento {index} de la intrínseca '{c.Name}'.");
    }

    /// <summary>Convierte entre tipos numéricos del mismo ancho (int/uint por bitcast, float/int por conversión).</summary>
    private string ConvertNum(string id, Num from, Num to, int width, Function f)
    {
        if (from == to) return id;
        string target = VectorTypeId(to, width, f);

        return (from, to) switch
        {
            (Num.Int, Num.Uint) or (Num.Uint, Num.Int) => Emit(f, SpirVOpTable.OpBitcast, target, id),
            (Num.Float or Num.Double or Num.Half, Num.Int) => Emit(f, SpirVOpTable.OpConvertFToS, target, id),
            (Num.Float or Num.Double or Num.Half, Num.Uint) => Emit(f, SpirVOpTable.OpConvertFToU, target, id),
            (Num.Int, Num.Float or Num.Double or Num.Half) => Emit(f, SpirVOpTable.OpConvertSToF, target, id),
            (Num.Uint, Num.Float or Num.Double or Num.Half) => Emit(f, SpirVOpTable.OpConvertUToF, target, id),
            (Num.Float or Num.Double or Num.Half, Num.Float or Num.Double or Num.Half) => Emit(f, SpirVOpTable.OpFConvert, target, id),
            _ => throw new NotSupportedException($"Conversión {from} -> {to} no soportada en una intrínseca.")
        };
    }

    /// <summary>El resultado calculado se ajusta al tipo que declara el método C# (p. ej. Math.Sign devuelve int).</summary>
    private string FitResult(string id, Storage computed, Storage? declared, Function f)
    {
        if (declared is null) return id;
        Num from = NumOf(computed), to = NumOf(declared);
        int fromWidth = WidthOf(computed), toWidth = WidthOf(declared);
        if (from == to && fromWidth == toWidth) return id;
        if (fromWidth != toWidth || from is Num.Other || to is Num.Other) return id;
        return ConvertNum(id, from, to, fromWidth, f);
    }

    // ---------------------------------------------------------------------------------------- matemática

    private static readonly Dictionary<string, Glsl> UnaryMath = new()
    {
        ["Sin"] = Glsl.Sin, ["Cos"] = Glsl.Cos, ["Tan"] = Glsl.Tan, ["Asin"] = Glsl.Asin, ["Acos"] = Glsl.Acos,
        ["Atan"] = Glsl.Atan, ["Sinh"] = Glsl.Sinh, ["Cosh"] = Glsl.Cosh, ["Tanh"] = Glsl.Tanh,
        ["Asinh"] = Glsl.Asinh, ["Acosh"] = Glsl.Acosh, ["Atanh"] = Glsl.Atanh, ["Exp"] = Glsl.Exp,
        ["Log"] = Glsl.Log, ["Exp2"] = Glsl.Exp2, ["Log2"] = Glsl.Log2, ["Sqrt"] = Glsl.Sqrt,
        ["InverseSqrt"] = Glsl.InverseSqrt, ["Fract"] = Glsl.Fract, ["Radians"] = Glsl.Radians,
        ["Degrees"] = Glsl.Degrees, ["Floor"] = Glsl.Floor, ["Ceiling"] = Glsl.Ceil, ["Truncate"] = Glsl.Trunc,
        ["Round"] = Glsl.Round, ["RoundEven"] = Glsl.RoundEven, ["Normalize"] = Glsl.Normalize,
        ["Determinant"] = Glsl.Determinant, ["MatrixInverse"] = Glsl.MatrixInverse,
        ["PackSnorm4x8"] = Glsl.PackSnorm4x8, ["PackUnorm4x8"] = Glsl.PackUnorm4x8,
        ["PackSnorm2x16"] = Glsl.PackSnorm2x16, ["PackUnorm2x16"] = Glsl.PackUnorm2x16,
        ["PackHalf2x16"] = Glsl.PackHalf2x16, ["UnpackSnorm2x16"] = Glsl.UnpackSnorm2x16,
        ["UnpackUnorm2x16"] = Glsl.UnpackUnorm2x16, ["UnpackHalf2x16"] = Glsl.UnpackHalf2x16,
        ["UnpackSnorm4x8"] = Glsl.UnpackSnorm4x8, ["UnpackUnorm4x8"] = Glsl.UnpackUnorm4x8,
        ["Length"] = Glsl.Length
    };

    private static readonly Dictionary<string, Glsl> BinaryMath = new()
    {
        ["Pow"] = Glsl.Pow, ["Atan2"] = Glsl.Atan2, ["Distance"] = Glsl.Distance, ["Cross"] = Glsl.Cross,
        ["Reflect"] = Glsl.Reflect, ["Ldexp"] = Glsl.Ldexp
    };

    private string? EmitValueIntrinsic(IntrinsicCall c, string[] ids, ref Function function)
    {
        Function f = function;
        string name = c.Name;

        if (!MatrixSpecific.Contains(name) && c.Args.Count > 0 &&
            Enumerable.Range(0, c.Args.Count).Any(i => ArgType(c, i) is IRTypes.Matrix))
        {
            return EmitPerColumn(c, ids, ref function);
        }

        if (c.Args.Count > 0 && NumOf(ArgType(c, 0)) == Num.Double && FloatOnly.Contains(name))
        {
            return EmitViaFloat(c, ids, ref function);
        }

        Storage first = c.Args.Count > 0 ? ArgType(c, 0) : c.Result
            ?? throw new NotSupportedException($"La intrínseca '{name}' no tiene argumentos ni resultado.");
        Storage result = c.Result ?? first;
        Num n = NumOf(first);
        int width = WidthOf(first);
        string resultTypeId = TypeIdOf(result, f);

        // --- unarias con una instrucción GLSL.std.450 ---
        if (UnaryMath.TryGetValue(name, out Glsl unary))
        {
            // Los empaquetados convierten entre tipos distintos: el tipo del resultado es el del método.
            string typeId = name == "Length" || name == "Determinant" ? ScalarTypeId(n, f) : resultTypeId;
            if (name is "Normalize" or "Floor" or "Ceiling" or "Truncate" or "Round" or "RoundEven" or "Fract")
            {
                typeId = TypeIdOf(first, f);
            }

            return Ext(f, unary, typeId, ids);
        }

        if (BinaryMath.TryGetValue(name, out Glsl binary))
        {
            string a = ids[0];
            string b = ids.Length > 1 ? ids[1] : ids[0];
            string typeId = name == "Distance" ? ScalarTypeId(n, f) : TypeIdOf(first, f);
            if (name is "Pow" or "Atan2") b = Broadcast(b, ArgType(c, 1), n, width, f);
            if (name == "Ldexp" && NumOf(ArgType(c, 1)) is Num.Float or Num.Double or Num.Half)
            {
                b = ConvertNum(b, NumOf(ArgType(c, 1)), Num.Int, WidthOf(ArgType(c, 1)), f);
            }
            return Ext(f, binary, typeId, a, b);
        }

        switch (name)
        {
            case "Abs":
                return n switch
                {
                    Num.Float or Num.Double or Num.Half => Ext(f, Glsl.FAbs, TypeIdOf(first, f), ids[0]),
                    Num.Int => Ext(f, Glsl.SAbs, TypeIdOf(first, f), ids[0]),
                    Num.Uint => ids[0],
                    _ => throw new NotSupportedException($"Abs no admite {n}.")
                };

            case "Sign":
            {
                // Math.Sign devuelve int aunque el argumento sea float.
                string sign = n switch
                {
                    Num.Float or Num.Double or Num.Half => Ext(f, Glsl.FSign, TypeIdOf(first, f), ids[0]),
                    Num.Int => Ext(f, Glsl.SSign, TypeIdOf(first, f), ids[0]),
                    _ => throw new NotSupportedException($"Sign no admite {n}.")
                };
                return FitResult(sign, first, c.Result, f);
            }

            case "Min":
            case "Max":
            {
                bool min = name == "Min";
                Glsl op = n switch
                {
                    Num.Float or Num.Double or Num.Half => min ? Glsl.FMin : Glsl.FMax,
                    Num.Int => min ? Glsl.SMin : Glsl.SMax,
                    Num.Uint => min ? Glsl.UMin : Glsl.UMax,
                    _ => throw new NotSupportedException($"{name} no admite {n}.")
                };
                string b = Broadcast(ids[1], ArgType(c, 1), n, width, f);
                return Ext(f, op, TypeIdOf(first, f), ids[0], b);
            }

            case "Clamp":
            {
                Glsl op = n switch
                {
                    Num.Float or Num.Double or Num.Half => Glsl.FClamp,
                    Num.Int => Glsl.SClamp,
                    Num.Uint => Glsl.UClamp,
                    _ => throw new NotSupportedException($"Clamp no admite {n}.")
                };
                string lo = Broadcast(ids[1], ArgType(c, 1), n, width, f);
                string hi = Broadcast(ids[2], ArgType(c, 2), n, width, f);
                return Ext(f, op, TypeIdOf(first, f), ids[0], lo, hi);
            }

            case "Saturate":
                return Ext(f, Glsl.FClamp, TypeIdOf(first, f), ids[0], ConstOf(n, 0, width, f), ConstOf(n, 1, width, f));

            case "Reciprocal":
                return Emit(f, SpirVOpTable.OpFDiv, TypeIdOf(first, f), ConstOf(n, 1, width, f), ids[0]);

            case "FusedMultiplyAdd" when n is Num.Int or Num.Uint:
            {
                string t = TypeIdOf(first, f);
                string product = Emit(f, SpirVOpTable.OpIMul, t, ids[0], ids[1]);
                return Emit(f, SpirVOpTable.OpIAdd, t, product, ids[2]);
            }

            case "FusedMultiplyAdd":
                return Ext(f, Glsl.Fma, TypeIdOf(first, f), ids[0], ids[1], ids[2]);

            case "Lerp":
            {
                string y = ids[1];
                string t = Broadcast(ids[2], ArgType(c, 2), n, width, f);
                return Ext(f, Glsl.FMix, TypeIdOf(first, f), ids[0], y, t);
            }

            case "Step":
            {
                // step(edge, x): edge puede ser escalar con x vector.
                Storage xType = ArgType(c, 1);
                string edge = Broadcast(ids[0], first, NumOf(xType), WidthOf(xType), f);
                return Ext(f, Glsl.Step, TypeIdOf(xType, f), edge, ids[1]);
            }

            case "SmoothStep":
            {
                Storage xType = ArgType(c, 2);
                int w = WidthOf(xType);
                Num xn = NumOf(xType);
                string e0 = Broadcast(ids[0], first, xn, w, f);
                string e1 = Broadcast(ids[1], ArgType(c, 1), xn, w, f);
                return Ext(f, Glsl.SmoothStep, TypeIdOf(xType, f), e0, e1, ids[2]);
            }

            case "Refract":
                return Ext(f, Glsl.Refract, TypeIdOf(first, f), ids[0], ids[1], ids[2]);

            case "FaceForward":
                return Ext(f, Glsl.FaceForward, TypeIdOf(first, f), ids[0], ids[1], ids[2]);

            case "Mod":
            {
                // GLSL mod: x - y * floor(x / y)
                string y = Broadcast(ids[1], ArgType(c, 1), n, width, f);
                string t = TypeIdOf(first, f);
                string div = Emit(f, SpirVOpTable.OpFDiv, t, ids[0], y);
                string floor = Ext(f, Glsl.Floor, t, div);
                string mul = Emit(f, SpirVOpTable.OpFMul, t, y, floor);
                return Emit(f, SpirVOpTable.OpFSub, t, ids[0], mul);
            }

            case "Remainder":
            {
                string y = Broadcast(ids[1], ArgType(c, 1), n, width, f);
                SpirVOperation op = n switch
                {
                    Num.Float or Num.Double or Num.Half => SpirVOpTable.OpFRem,
                    Num.Int => SpirVOpTable.OpSRem,
                    _ => SpirVOpTable.OpUMod
                };
                return Emit(f, op, TypeIdOf(first, f), ids[0], y);
            }

            case "Log10":
            {
                string t = TypeIdOf(first, f);
                string log2 = Ext(f, Glsl.Log2, t, ids[0]);
                return Emit(f, SpirVOpTable.OpFMul, t, log2, ConstOf(n, 0.30102999566398120, width, f));
            }

            case "Exp10":
            {
                string t = TypeIdOf(first, f);
                string scaled = Emit(f, SpirVOpTable.OpFMul, t, ids[0], ConstOf(n, 3.32192809488736235, width, f));
                return Ext(f, Glsl.Exp2, t, scaled);
            }

            case "SinPi":
            case "CosPi":
            case "TanPi":
            {
                string t = TypeIdOf(first, f);
                string scaled = Emit(f, SpirVOpTable.OpFMul, t, ids[0], ConstOf(n, Math.PI, width, f));
                Glsl op = name == "SinPi" ? Glsl.Sin : name == "CosPi" ? Glsl.Cos : Glsl.Tan;
                return Ext(f, op, t, scaled);
            }

            case "IsNaN":
                return Emit(f, SpirVOpTable.OpIsNan, resultTypeId, ids[0]);

            case "IsInfinity":
                return Emit(f, SpirVOpTable.OpIsInf, resultTypeId, ids[0]);

            case "IsFinite":
            {
                string nan = Emit(f, SpirVOpTable.OpIsNan, resultTypeId, ids[0]);
                string inf = Emit(f, SpirVOpTable.OpIsInf, resultTypeId, ids[0]);
                string either = Emit(f, SpirVOpTable.OpLogicalOr, resultTypeId, nan, inf);
                return Emit(f, SpirVOpTable.OpLogicalNot, resultTypeId, either);
            }

            // ---- geometría y álgebra lineal ----
            case "Dot":
                return EmitDot(ids[0], ids[1], first, result, f);

            case "LengthSquared":
                return EmitDot(ids[0], ids[0], first, result, f);

            case "DistanceSquared":
            {
                string diff = Emit(f, SpirVOpTable.OpFSub, TypeIdOf(first, f), ids[0], ids[1]);
                return Emit(f, SpirVOpTable.OpDot, TypeIdOf(result, f), diff, diff);
            }

            case "Transpose":
                return Emit(f, SpirVOpTable.OpTranspose, resultTypeId, ids[0]);

            case "OuterProduct":
                return Emit(f, SpirVOpTable.OpOuterProduct, resultTypeId, ids[0], ids[1]);

            case "MatrixMultiply":
                return EmitMatrixMultiply(c, ids, result, f);

            case "MatrixComponentMultiply":
                return EmitMatrixComponentMultiply(c, ids, (IRTypes.Matrix)first, f);

            // ---- comparación y lógica por componente ----
            case "Equal":
            case "NotEqual":
            case "LessThan":
            case "LessThanEqual":
            case "GreaterThan":
            case "GreaterThanEqual":
                return EmitComparison(name, n, ids, resultTypeId, f);

            case "Any":
            case "All":
            {
                string source = ids[0];
                if (n != Num.Bool)
                {
                    string boolVec = VectorTypeId(Num.Bool, width, f);
                    source = Emit(f, n is Num.Float or Num.Double or Num.Half ? SpirVOpTable.OpFUnordNotEqual : SpirVOpTable.OpINotEqual,
                        boolVec, ids[0], ConstOf(n, 0, width, f));
                }

                if (width == 1) return source;
                return Emit(f, name == "Any" ? SpirVOpTable.OpAny : SpirVOpTable.OpAll,
                    ScalarTypeId(Num.Bool, f), source);
            }

            case "Not":
                return Emit(f, SpirVOpTable.OpLogicalNot, resultTypeId, ids[0]);

            case "Select":
                return EmitSelect(c, ids, result, f);

            // ---- bits ----
            case "CountOneBits":
                return Emit(f, SpirVOpTable.OpBitCount, resultTypeId, ids[0]);

            case "ReverseBits":
                return Emit(f, SpirVOpTable.OpBitReverse, TypeIdOf(first, f), ids[0]);

            case "ExtractBits":
                return Emit(f, n == Num.Uint ? SpirVOpTable.OpBitFieldUExtract : SpirVOpTable.OpBitFieldSExtract,
                    TypeIdOf(first, f), ids[0], ids[1], ids[2]);

            case "InsertBits":
                return Emit(f, SpirVOpTable.OpBitFieldInsert, TypeIdOf(first, f), ids[0], ids[1], ids[2], ids[3]);

            case "FindLeastSignificantBit":
                return Ext(f, Glsl.FindILsb, resultTypeId, ids[0]);

            case "FindMostSignificantBit":
                return Ext(f, n == Num.Uint ? Glsl.FindUMsb : Glsl.FindSMsb, resultTypeId, ids[0]);

            case "CountLeadingZeros":
            {
                // 31 - msb(x); si x == 0, 32.
                string t = TypeIdOf(first, f);
                string msb = Ext(f, Glsl.FindUMsb, t, ids[0]);
                string lz = Emit(f, SpirVOpTable.OpISub, t, ConstOf(n, 31, width, f), msb);
                string isZero = Emit(f, SpirVOpTable.OpIEqual, VectorTypeId(Num.Bool, width, f), ids[0], ConstOf(n, 0, width, f));
                return Emit(f, SpirVOpTable.OpSelect, t, isZero, ConstOf(n, 32, width, f), lz);
            }

            case "CountTrailingZeros":
            {
                string t = TypeIdOf(first, f);
                string lsb = Ext(f, Glsl.FindILsb, t, ids[0]);
                string isZero = Emit(f, SpirVOpTable.OpIEqual, VectorTypeId(Num.Bool, width, f), ids[0], ConstOf(n, 0, width, f));
                return Emit(f, SpirVOpTable.OpSelect, t, isZero, ConstOf(n, 32, width, f), lsb);
            }

            case "FloatBitsToInt":
            case "IntBitsToFloat":
            {
                // as_type<T> es genérico: el destino se deduce del origen (float <-> int del mismo ancho).
                Num declared = NumOf(result);
                Num target = declared is not Num.Other && WidthOf(result) == width
                    ? declared
                    : name == "IntBitsToFloat" ? Num.Float : Num.Int;
                if (target == n) return ids[0];
                return Emit(f, SpirVOpTable.OpBitcast, VectorTypeId(target, width, f), ids[0]);
            }
        }

        throw new NotSupportedException(
            $"La intrínseca '{name}' todavía no está soportada por el compilador SPIR-V.");
    }

    /// <summary>GLSL.std.450 solo admite 16/32 bits en la trigonometría, exponenciales y logaritmos.</summary>
    private static readonly HashSet<string> FloatOnly =
    [
        "Sin", "Cos", "Tan", "Asin", "Acos", "Atan", "Sinh", "Cosh", "Tanh", "Asinh", "Acosh", "Atanh", "Atan2", "Pow",
        "Exp", "Log", "Exp2", "Log2", "Log10", "Exp10", "Radians", "Degrees", "SinPi", "CosPi", "TanPi"
    ];

    /// <summary>Los doubles se calculan en float y el resultado vuelve a double (SPIR-V no tiene esas funciones en 64 bits).</summary>
    private string EmitViaFloat(IntrinsicCall c, string[] ids, ref Function function)
    {
        Function f = function;
        var floatIds = new string[ids.Length];
        var floatTypes = new Storage?[ids.Length];
        for (int i = 0; i < ids.Length; i++)
        {
            Storage type = ArgType(c, i);
            if (NumOf(type) == Num.Double)
            {
                floatIds[i] = ConvertNum(ids[i], Num.Double, Num.Float, WidthOf(type), f);
                floatTypes[i] = TypeOfNum(Num.Float, WidthOf(type));
            }
            else
            {
                floatIds[i] = ids[i];
                floatTypes[i] = type;
            }
        }

        Storage? floatResult = c.Result is not null && NumOf(c.Result) == Num.Double
            ? TypeOfNum(Num.Float, WidthOf(c.Result))
            : c.Result;
        var asFloat = c with { Types = floatTypes, Result = floatResult };
        string value = EmitValueIntrinsic(asFloat, floatIds, ref function)!;

        return c.Result is not null && NumOf(c.Result) == Num.Double
            ? ConvertNum(value, Num.Float, Num.Double, WidthOf(c.Result), f)
            : value;
    }

    private static readonly HashSet<string> MatrixSpecific =
        ["MatrixMultiply", "Transpose", "Determinant", "MatrixInverse", "OuterProduct", "MatrixComponentMultiply"];

    /// <summary>Las funciones por componente aplicadas a una matriz (HLSL, MSL) se aplican columna a columna.</summary>
    private string EmitPerColumn(IntrinsicCall c, string[] ids, ref Function function)
    {
        Function f = function;
        IRTypes.Matrix matrix = (IRTypes.Matrix)Enumerable.Range(0, c.Args.Count).Select(i => ArgType(c, i))
            .First(t => t is IRTypes.Matrix);
        Num n = NumOf(matrix);
        if (c.Result is not IRTypes.Matrix && c.Name is not ("Dot" or "Length" or "Distance") ||
            c.Name is "IsNaN" or "IsInfinity" or "IsFinite" or "FloatBitsToInt" or "IntBitsToFloat" or "Frexp" or "Modf"
                or "Ldexp" or "Equal" or "NotEqual" or "LessThan" or "LessThanEqual" or "GreaterThan" or "GreaterThanEqual")
        {
            throw new NotSupportedException($"La intrínseca '{c.Name}' no está soportada sobre matrices.");
        }

        int rows = (int)matrix.Rows;
        Storage columnType = TypeOfNum(n, rows);
        string columnTypeId = TypeIdOf(columnType, f);
        var columns = new string[matrix.Columns];

        for (uint col = 0; col < matrix.Columns; col++)
        {
            var columnIds = new string[ids.Length];
            var columnTypes = new Storage?[ids.Length];
            for (int i = 0; i < ids.Length; i++)
            {
                Storage type = ArgType(c, i);
                if (type is IRTypes.Matrix)
                {
                    columnIds[i] = EmitRaw(f, SpirVOpTable.OpCompositeExtract, columnTypeId, P(ids[i]), Lit(col));
                    columnTypes[i] = columnType;
                }
                else
                {
                    columnIds[i] = ids[i];
                    columnTypes[i] = type;
                }
            }

            var perColumn = c with { Types = columnTypes, Result = c.Result is IRTypes.Matrix ? columnType : c.Result };
            columns[col] = (c.Name.StartsWith("Derivative") || c.Name.StartsWith("Fwidth"))
                ? EmitDerivativeOf(perColumn, columnIds[0], columnType, f)
                : EmitValueIntrinsic(perColumn, columnIds, ref function)!;
        }

        if (c.Result is not IRTypes.Matrix) return columns[0];
        return Emit(f, SpirVOpTable.OpCompositeConstruct, TypeIdOf(c.Result, f), columns);
    }

    /// <summary>OpDot solo existe para floats: los vectores enteros se multiplican y se suman por componente.</summary>
    private string EmitDot(string a, string b, Storage type, Storage result, Function f)
    {
        Num n = NumOf(type);
        if (n is Num.Float or Num.Double or Num.Half) return Emit(f, SpirVOpTable.OpDot, ScalarTypeId(n, f), a, b);

        int width = WidthOf(type);
        string scalar = ScalarTypeId(n, f);
        if (width == 1) return Emit(f, SpirVOpTable.OpIMul, scalar, a, b);

        string product = Emit(f, SpirVOpTable.OpIMul, TypeIdOf(type, f), a, b);
        string sum = EmitRaw(f, SpirVOpTable.OpCompositeExtract, scalar, P(product), Lit(0));
        for (int i = 1; i < width; i++)
        {
            string part = EmitRaw(f, SpirVOpTable.OpCompositeExtract, scalar, P(product), Lit((uint)i));
            sum = Emit(f, SpirVOpTable.OpIAdd, scalar, sum, part);
        }

        return sum;
    }

    private string EmitComparison(string name, Num n, string[] ids, string resultTypeId, Function f)
    {
        bool floating = n is Num.Float or Num.Double or Num.Half;
        bool signed = n == Num.Int;

        if (n == Num.Bool)
        {
            return name switch
            {
                "Equal" => Emit(f, SpirVOpTable.OpLogicalEqual, resultTypeId, ids[0], ids[1]),
                "NotEqual" => Emit(f, SpirVOpTable.OpLogicalNotEqual, resultTypeId, ids[0], ids[1]),
                _ => throw new NotSupportedException($"{name} no admite bool.")
            };
        }

        SpirVOperation op = name switch
        {
            "Equal" => floating ? SpirVOpTable.OpFOrdEqual : SpirVOpTable.OpIEqual,
            "NotEqual" => floating ? SpirVOpTable.OpFUnordNotEqual : SpirVOpTable.OpINotEqual,
            "LessThan" => floating ? SpirVOpTable.OpFOrdLessThan : signed ? SpirVOpTable.OpSLessThan : SpirVOpTable.OpULessThan,
            "LessThanEqual" => floating ? SpirVOpTable.OpFOrdLessThanEqual : signed ? SpirVOpTable.OpSLessThanEqual : SpirVOpTable.OpULessThanEqual,
            "GreaterThan" => floating ? SpirVOpTable.OpFOrdGreaterThan : signed ? SpirVOpTable.OpSGreaterThan : SpirVOpTable.OpUGreaterThan,
            _ => floating ? SpirVOpTable.OpFOrdGreaterThanEqual : signed ? SpirVOpTable.OpSGreaterThanEqual : SpirVOpTable.OpUGreaterThanEqual
        };

        return Emit(f, op, resultTypeId, ids[0], ids[1]);
    }

    /// <summary>
    /// select/mix con selector bool: HLSL pone la condición primero (cond, t, f); GLSL, MSL y WGSL la ponen
    /// al final (a, b, cond) y devuelven cond ? b : a.
    /// </summary>
    private string EmitSelect(IntrinsicCall c, string[] ids, Storage result, Function f)
    {
        int condIndex = -1;
        for (int i = 0; i < c.Args.Count; i++)
        {
            if (NumOf(ArgType(c, i)) == Num.Bool) { condIndex = i; break; }
        }

        if (condIndex < 0) throw new NotSupportedException("Select necesita un argumento bool.");

        string whenTrue, whenFalse;
        if (condIndex == 0)
        {
            whenTrue = ids[1];
            whenFalse = ids[2];
        }
        else
        {
            whenFalse = ids[0];
            whenTrue = ids[1];
        }

        string cond = ids[condIndex];
        Storage valueType = ArgType(c, condIndex == 0 ? 1 : 0);

        // OpSelect pide que el selector tenga el mismo ancho que los valores.
        int width = WidthOf(valueType);
        if (WidthOf(ArgType(c, condIndex)) == 1 && width > 1)
        {
            cond = Splat(cond, Num.Bool, width, f);
        }

        return Emit(f, SpirVOpTable.OpSelect, TypeIdOf(valueType, f), cond, whenTrue, whenFalse);
    }

    private string EmitMatrixMultiply(IntrinsicCall c, string[] ids, Storage result, Function f)
    {
        Storage a = ArgType(c, 0), b = ArgType(c, 1);
        string t = TypeIdOf(result, f);

        return (a, b) switch
        {
            (IRTypes.Matrix, IRTypes.Matrix) => Emit(f, SpirVOpTable.OpMatrixTimesMatrix, t, ids[0], ids[1]),
            (IRTypes.Matrix, IRTypes.Vector) => Emit(f, SpirVOpTable.OpMatrixTimesVector, t, ids[0], ids[1]),
            (IRTypes.Vector, IRTypes.Matrix) => Emit(f, SpirVOpTable.OpVectorTimesMatrix, t, ids[0], ids[1]),
            (IRTypes.Matrix, _) => Emit(f, SpirVOpTable.OpMatrixTimesScalar, t, ids[0], ids[1]),
            (_, IRTypes.Matrix) => Emit(f, SpirVOpTable.OpMatrixTimesScalar, t, ids[1], ids[0]),
            // HLSL mul(): vector·vector es el producto punto; con un escalar, una multiplicación normal.
            (IRTypes.Vector, IRTypes.Vector) => EmitDot(ids[0], ids[1], a, result, f),
            (IRTypes.Vector, _) => Emit(f, SpirVOpTable.OpVectorTimesScalar, t, ids[0], ids[1]),
            (_, IRTypes.Vector) => Emit(f, SpirVOpTable.OpVectorTimesScalar, t, ids[1], ids[0]),
            _ => Emit(f, NumOf(a) is Num.Float or Num.Double or Num.Half ? SpirVOpTable.OpFMul : SpirVOpTable.OpIMul, t, ids[0], ids[1])
        };
    }

    private string EmitMatrixComponentMultiply(IntrinsicCall c, string[] ids, IRTypes.Matrix matrix, Function f)
    {
        Num n = NumOf(matrix);
        string columnType = VectorTypeId(n, (int)matrix.Rows, f);
        var columns = new string[matrix.Columns];

        for (uint i = 0; i < matrix.Columns; i++)
        {
            string left = EmitRaw(f, SpirVOpTable.OpCompositeExtract, columnType, P(ids[0]), Lit(i));
            string right = EmitRaw(f, SpirVOpTable.OpCompositeExtract, columnType, P(ids[1]), Lit(i));
            columns[i] = Emit(f, SpirVOpTable.OpFMul, columnType, left, right);
        }

        return Emit(f, SpirVOpTable.OpCompositeConstruct, TypeIdOf(matrix, f), columns);
    }

    // ---------------------------------------------------------------------------------------- argumentos de salida

    /// <summary>
    /// Intrínsecas que en el lenguaje original tienen parámetros out (frexp, modf, sincos, uaddCarry...). En la
    /// firma C# son parámetros por valor: si el argumento es una variable (tiene dirección) se le escribe el resultado.
    /// </summary>
    private string? EmitWithOutputs(IntrinsicCall c, ref Function function)
    {
        Function f = function;
        Storage first = ArgType(c, 0);
        Num n = NumOf(first);
        int width = WidthOf(first);
        string firstId = EmitValue(c.Args[0], ref function);

        switch (c.Name)
        {
            case "SinCos":
            {
                string t = TypeIdOf(first, f);
                string sin = Ext(f, Glsl.Sin, t, firstId);
                string cos = Ext(f, Glsl.Cos, t, firstId);
                StoreOut(c, 1, sin, ref function);
                StoreOut(c, 2, cos, ref function);
                return null;
            }

            case "Frexp":
            case "Modf":
            {
                bool frexp = c.Name == "Frexp";

                // WGSL: frexp(x) / modf(x) devuelven un struct { fract, exp | whole }.
                if (c.Args.Count == 1 && c.Result is IRTypes.CustomStruct)
                {
                    return Ext(f, frexp ? Glsl.FrexpStruct : Glsl.ModfStruct, TypeIdOf(c.Result, f), firstId);
                }

                if (c.Args.Count > 1 && IsAddressable(c.Args[1]))
                {
                    string pointer = EmitAddress(c.Args[1], ref function);
                    return Ext(f, frexp ? Glsl.Frexp : Glsl.Modf, TypeIdOf(first, f), firstId, pointer);
                }

                throw new NotSupportedException(
                    $"'{c.Name}' necesita una variable como segundo argumento donde escribir su resultado.");
            }
        }

        if (c.Name is "AddCarry" or "SubtractBorrow" or "MultiplyExtended") return EmitExtendedArithmetic(c, firstId, ref function);

        throw new NotSupportedException(
            $"La intrínseca '{c.Name}' (resultado en parámetros de salida) todavía no está soportada.");
    }

    /// <summary>uaddCarry, usubBorrow, imulExtended/umulExtended: SPIR-V devuelve un struct de dos miembros.</summary>
    private string? EmitExtendedArithmetic(IntrinsicCall c, string firstId, ref Function function)
    {
        Function f = function;
        Storage type = ArgType(c, 0);
        Num n = NumOf(type);
        if (c.Args.Count < 3 || n is not (Num.Int or Num.Uint))
        {
            throw new NotSupportedException($"'{c.Name}' solo se soporta con argumentos enteros y parámetros de salida.");
        }

        string second = EmitValue(c.Args[1], ref function);
        var pair = new IRTypes.CustomStruct("extended", StorageKind.Local, null)
        {
            TypeName = $"__extended_{n}_{WidthOf(type)}",
            Properties = [type, type]
        };

        SpirVOperation op = c.Name switch
        {
            "AddCarry" => SpirVOpTable.OpIAddCarry,
            "SubtractBorrow" => SpirVOpTable.OpISubBorrow,
            _ => n == Num.Uint ? SpirVOpTable.OpUMulExtended : SpirVOpTable.OpSMulExtended
        };

        string typeId = TypeIdOf(type, f);
        string result = Emit(f, op, TypeIdOf(pair, f), firstId, second);
        string low = EmitRaw(f, SpirVOpTable.OpCompositeExtract, typeId, P(result), Lit(0));
        string high = EmitRaw(f, SpirVOpTable.OpCompositeExtract, typeId, P(result), Lit(1));

        if (c.Name == "MultiplyExtended")
        {
            // imulExtended(x, y, out msb, out lsb)
            StoreOut(c, 2, high, ref function);
            StoreOut(c, 3, low, ref function);
            return null;
        }

        StoreOut(c, 2, high, ref function);
        return low;
    }

    private void StoreOut(IntrinsicCall c, int index, string valueId, ref Function function)
    {
        if (index >= c.Args.Count || !IsAddressable(c.Args[index])) return;
        string pointer = EmitAddress(c.Args[index], ref function);
        _module.AddLine(SpirVOpTable.OpStore, P(pointer), P(valueId));
    }

    // ---------------------------------------------------------------------------------------- derivadas, control

    private string EmitDerivative(IntrinsicCall c, ref Function function)
    {
        string value = EmitValue(c.Args[0], ref function);
        return EmitDerivativeOf(c, value, ArgType(c, 0), function);
    }

    private string EmitDerivativeOf(IntrinsicCall c, string value, Storage type, Function f)
    {
        string name = c.Name;

        SpirVOperation op = name switch
        {
            _ when name.StartsWith("DerivativeXCoarse") => SpirVOpTable.OpDPdxCoarse,
            _ when name.StartsWith("DerivativeYCoarse") => SpirVOpTable.OpDPdyCoarse,
            _ when name.StartsWith("DerivativeXFine") => SpirVOpTable.OpDPdxFine,
            _ when name.StartsWith("DerivativeYFine") => SpirVOpTable.OpDPdyFine,
            _ when name.StartsWith("DerivativeX") => SpirVOpTable.OpDPdx,
            _ when name.StartsWith("DerivativeY") => SpirVOpTable.OpDPdy,
            _ when name.StartsWith("FwidthCoarse") => SpirVOpTable.OpFwidthCoarse,
            _ when name.StartsWith("FwidthFine") => SpirVOpTable.OpFwidthFine,
            _ => SpirVOpTable.OpFwidth
        };

        // Las derivadas de SPIR-V exigen 32 bits: un half se deriva en float y vuelve a half.
        if (NumOf(type) == Num.Half)
        {
            int w = WidthOf(type);
            string asFloat = ConvertNum(value, Num.Half, Num.Float, w, f);
            string derived = Emit(f, op, VectorTypeId(Num.Float, w, f), asFloat);
            return ConvertNum(derived, Num.Float, Num.Half, w, f);
        }

        return Emit(f, op, TypeIdOf(type, f), value);
    }

    private string? EmitDiscardIfNegative(IntrinsicCall c, ref Function function)
    {
        Function f = function;
        string value = EmitValue(c.Args[0], ref function);
        Storage type = ArgType(c, 0);
        Num n = NumOf(type);
        int width = WidthOf(type);

        string negative = Emit(f, n is Num.Float or Num.Double or Num.Half ? SpirVOpTable.OpFOrdLessThan : SpirVOpTable.OpSLessThan,
            VectorTypeId(Num.Bool, width, f), value, ConstOf(n, 0, width, f));
        string any = width == 1 ? negative : Emit(f, SpirVOpTable.OpAny, ScalarTypeId(Num.Bool, f), negative);

        string killLabel = NewLabel("discard_kill", f);
        string continueLabel = NewLabel("discard_cont", f);
        _module.AddLine(SpirVOpTable.OpSelectionMerge, P(continueLabel), NoneControl);
        _module.AddLine(SpirVOpTable.OpBranchConditional, P(any), P(killLabel), P(continueLabel));
        _terminated = true;

        BeginBlock(killLabel);
        _module.AddLine(SpirVOpTable.OpKill);
        _terminated = true;

        BeginBlock(continueLabel);
        return null;
    }

    private string? EmitInterpolate(IntrinsicCall c, ref Function function)
    {
        Function f = function;
        string name = c.Name;

        if (!IsAddressable(c.Args[0]))
        {
            throw new NotSupportedException(
                $"'{name}' necesita una variable de entrada del fragment shader como primer argumento.");
        }

        string pointer = EmitAddress(c.Args[0], ref function);
        Storage type = ArgType(c, 0);
        string t = TypeIdOf(type, f);

        if (name.StartsWith("InterpolateAtCentroid")) return Ext(f, Glsl.InterpolateAtCentroid, t, pointer);

        string extra = EmitValue(c.Args[1], ref function);
        Storage extraType = ArgType(c, 1);
        if (name.StartsWith("InterpolateAtOffset") && NumOf(extraType) is Num.Int or Num.Uint)
        {
            // HLSL EvaluateAttributeSnapped: offsets en 1/16 de pixel.
            int w = WidthOf(extraType);
            string asFloat = ConvertNum(extra, NumOf(extraType), Num.Float, w, f);
            extra = Emit(f, SpirVOpTable.OpFMul, VectorTypeId(Num.Float, w, f), asFloat, ConstOf(Num.Float, 1.0 / 16.0, w, f));
        }

        return Ext(f, name.StartsWith("InterpolateAtSample") ? Glsl.InterpolateAtSample : Glsl.InterpolateAtOffset,
            t, pointer, extra);
    }

    private string? EmitBarrier(IntrinsicCall c, ref Function function)
    {
        const uint device = 1, workgroup = 2;
        const uint acquireRelease = 8, uniform = 64, workgroupMemory = 256, image = 2048;

        string scope(uint v) => _module.DeclareUIntConstant((int)v);

        if (c.Name == "ThreadGroupBarrier")
        {
            _module.AddLine(SpirVOpTable.OpControlBarrier, P(scope(workgroup)), P(scope(workgroup)),
                P(scope(acquireRelease | workgroupMemory)));
            return null;
        }

        (uint memoryScope, uint semantics) = c.Name switch
        {
            "MemoryBarrierBuffer" => (device, acquireRelease | uniform),
            "MemoryBarrierImage" => (device, acquireRelease | image),
            "MemoryBarrierShared" => (workgroup, acquireRelease | workgroupMemory),
            "MemoryBarrierGroup" => (workgroup, acquireRelease | uniform | workgroupMemory | image),
            _ => (device, acquireRelease | uniform | workgroupMemory | image)
        };

        _module.AddLine(SpirVOpTable.OpMemoryBarrier, P(scope(memoryScope)), P(scope(semantics)));
        return null;
    }

    // ---------------------------------------------------------------------------------------- atómicas

    private string? EmitAtomic(IntrinsicCall c, ref Function function)
    {
        Function f = function;
        string name = c.Name;
        Num n = name.EndsWith("Uint") ? Num.Uint : Num.Int;
        string typeId = ScalarTypeId(n, f);

        string scope = _module.DeclareUIntConstant(1);       // Device
        string relaxed = _module.DeclareUIntConstant(0);     // sin ordenación de memoria

        if (!IsAddressable(c.Args[0]))
        {
            throw new NotSupportedException(
                $"'{name}' necesita una variable (campo de un buffer o memoria compartida) como primer argumento.");
        }

        string pointer = EmitAddress(c.Args[0], ref function);

        if (name.StartsWith("AtomicLoad"))
        {
            return Emit(f, SpirVOpTable.OpAtomicLoad, typeId, pointer, scope, relaxed);
        }

        string value = EmitValue(c.Args[1], ref function);

        if (name.StartsWith("AtomicStore"))
        {
            _module.AddLine(SpirVOpTable.OpAtomicStore, P(pointer), P(scope), P(relaxed), P(value));
            return null;
        }

        if (name.StartsWith("AtomicCompareExchange"))
        {
            // (dest, compare, value): OpAtomicCompareExchange recibe primero el valor y luego el comparador.
            string newValue = EmitValue(c.Args[2], ref function);
            string old = Emit(f, SpirVOpTable.OpAtomicCompareExchange, typeId, pointer, scope, relaxed, relaxed,
                newValue, value);
            StoreOut(c, 3, old, ref function);
            return old;
        }

        SpirVOperation op = name switch
        {
            _ when name.StartsWith("AtomicAdd") => SpirVOpTable.OpAtomicIAdd,
            _ when name.StartsWith("AtomicSub") => SpirVOpTable.OpAtomicISub,
            _ when name.StartsWith("AtomicMin") => n == Num.Uint ? SpirVOpTable.OpAtomicUMin : SpirVOpTable.OpAtomicSMin,
            _ when name.StartsWith("AtomicMax") => n == Num.Uint ? SpirVOpTable.OpAtomicUMax : SpirVOpTable.OpAtomicSMax,
            _ when name.StartsWith("AtomicAnd") => SpirVOpTable.OpAtomicAnd,
            _ when name.StartsWith("AtomicOr") => SpirVOpTable.OpAtomicOr,
            _ when name.StartsWith("AtomicXor") => SpirVOpTable.OpAtomicXor,
            _ when name.StartsWith("AtomicExchange") => SpirVOpTable.OpAtomicExchange,
            _ => throw new NotSupportedException($"La atómica '{name}' no está soportada.")
        };

        string previous = Emit(f, op, typeId, pointer, scope, relaxed, value);

        // HLSL: InterlockedAdd(dest, value, out original)
        if (c.Args.Count > 2 && NumOf(ArgType(c, 2)) == n) StoreOut(c, 2, previous, ref function);

        return previous;
    }
}
