using YotsubaShader.Compiler.SpirV.Builder;
using YotsubaShader.Compiler.SpirV.YS_Types;
using YotsubaShader.IR.Intermediate;
using YotsubaShader.IR.Intermediate.FunctionBody;
using YotsubaShader.IR.Intermediate.FunctionBody.Expressions;
using YotsubaShader.IR.Intermediate.FunctionBody.Expressions.Literals;
using IRTypes = YotsubaShader.IR.Intermediate.Types;

namespace YotsubaShader.Compiler.SpirV;

/// <summary>Intrínsecas de texturas: muestreo, gather, fetch, imágenes de almacenamiento y consultas.</summary>
internal sealed partial class FunctionBodyEmitter
{
    private const uint OperandBias = 0x1, OperandLod = 0x2, OperandGrad = 0x4, OperandConstOffset = 0x8, OperandSample = 0x40;

    /// <summary>La textura de un argumento: preferimos la variable real (con su formato) al tipo del parámetro del stub.</summary>
    private IRTypes.Texture TextureOf(IntrinsicCall c, int index) =>
        TryGetExpressionStorage(c.Args[index]) as IRTypes.Texture
        ?? ArgType(c, index) as IRTypes.Texture
        ?? throw new NotSupportedException($"El argumento {index} de '{c.Name}' no es una textura.");

    private static int CoordinateComponents(IRTypes.Texture t) =>
        t.Dimensions switch
        {
            IRTypes.TextureDimensions.Texture_1D => 1,
            IRTypes.TextureDimensions.Texture_2D => 2,
            _ => 3
        } + (t.Arrayed ? 1 : 0);

    /// <summary>Un OpSampledImage a partir de la textura y su sampler (o la textura combinada tal cual).</summary>
    private string SampledImage(IntrinsicCall c, IRTypes.Texture texture, ref int next, ref Function function)
    {
        Function f = function;
        string image = EmitValue(c.Args[0], ref function);

        if (texture.CombinedSampler)
        {
            return image;
        }

        if (next >= c.Args.Count || ArgType(c, next) is not IRTypes.Sampler)
        {
            throw new NotSupportedException($"'{c.Name}' necesita un sampler junto a la textura.");
        }

        string sampler = EmitValue(c.Args[next++], ref function);
        string imageType = TypeIdOf(texture, f);
        string sampledType = _module.DeclareSampledImageType(imageType);
        return Emit(f, SpirVOpTable.OpSampledImage, sampledType, image, sampler);
    }

    /// <summary>La imagen "desnuda" (sin sampler), requerida por fetch, read/write y las consultas de tamaño.</summary>
    private string PlainImage(IntrinsicCall c, IRTypes.Texture texture, ref Function function)
    {
        string image = EmitValue(c.Args[0], ref function);
        if (!texture.CombinedSampler) return image;

        string plain = TypeIdOf(texture with { CombinedSampler = false }, function);
        return Emit(function, SpirVOpTable.OpImage, plain, image);
    }

    private static bool IsFloatScalar(Storage? t) => t is IRTypes.Float;
    private static bool IsIntScalar(Storage? t) => t is IRTypes.Int;
    private static bool IsIntVector(Storage? t) => t is IRTypes.Vector { ComponentsType: IRTypes.VectorComponentsType.Int };

    private Storage TexelResult(IRTypes.Texture texture, Storage? declared)
    {
        if (declared is IRTypes.Vector { ComponentsLength: 4, ComponentsType: not IRTypes.VectorComponentsType.Bool }) return declared;

        Num n = texture.TextureComponentsType switch
        {
            IRTypes.TextureComponentTypes.Int => Num.Int,
            IRTypes.TextureComponentTypes.Uint => Num.Uint,
            _ => Num.Float
        };
        return TypeOfNum(n, 4);
    }

    /// <summary>Los resultados de imagen son siempre vec4 en SPIR-V: se recortan al tipo que declara el método (float, vec2...).</summary>
    private string FitTexel(string id, Storage texel, Storage? declared, Function f)
    {
        if (declared is null) return id;
        int have = WidthOf(texel), want = WidthOf(declared);
        Num hn = NumOf(texel), wn = NumOf(declared);
        if (wn == Num.Other || want >= have && wn == hn) return id;

        string current = id;
        if (want < have) current = ShuffleHead(id, hn, have, want, f);
        return hn == wn ? current : ConvertNum(current, hn, wn, Math.Min(want, have), f);
    }

    private string? EmitTextureIntrinsic(IntrinsicCall c, ref Function function)
    {
        Function f = function;
        string name = c.Name;
        string? leadingComponent = null;
        if (name.StartsWith("Gather") && c.Args.Count > 1 && ArgType(c, 0) is IRTypes.Int && ArgType(c, 1) is IRTypes.Texture)
        {
            // WGSL: textureGather(component, texture, sampler, coords, ...)
            leadingComponent = EmitValue(c.Args[0], ref function);
            c = c with { Args = c.Args.Skip(1).ToList(), Types = c.Types.Skip(1).ToList() };
        }

        IRTypes.Texture texture = TextureOf(c, 0);
        int next = 1;

        for (int i = 0; i < c.Args.Count; i++)
        {
            if (ArgType(c, i) is IRTypes.CustomStruct option)
            {
                throw new NotSupportedException(
                    $"'{name}': el argumento de tipo '{option.TypeName}' (opciones de MSL como bias/level/gradient/component) todavía no está soportado.");
            }
        }

        if (name.StartsWith("TextureSize") || name.StartsWith("QueryLevels") || name.StartsWith("QuerySamples") ||
            name.StartsWith("QueryLayers"))
        {
            return EmitImageQuery(c, texture, ref function);
        }

        if (name.StartsWith("TexelFetch")) return EmitFetch(c, texture, ref function);
        if (name.StartsWith("ImageLoad") || name.StartsWith("ImageStore")) return EmitImageReadWrite(c, texture, ref function);

        if (name.StartsWith("QueryLod"))
        {
            string si = SampledImage(c, texture, ref next, ref function);
            string coord = EmitValue(c.Args[next], ref function);
            uint lodComponent = c.Args.Count > next + 1 && c.Args[^1] is IntLiteral marker ? uint.Parse(marker.Value) : 0;
            string lodPair = Emit(f, SpirVOpTable.OpImageQueryLod, VectorTypeId(Num.Float, 2, f), si, coord);
            if (lodComponent > 0 || WidthOf(c.Result) == 1)
            {
                // El resultado escalar es el LOD con clamp (x) o sin clamp (y).
                return EmitRaw(f, SpirVOpTable.OpCompositeExtract, ScalarTypeId(Num.Float, f), P(lodPair), Lit(lodComponent));
            }

            return FitTexel(lodPair, TypeOfNum(Num.Float, 2), c.Result, f);
        }

        bool gather = name.StartsWith("Gather");
        bool compare = name.Contains("Compare");
        bool proj = name.StartsWith("SampleProj");
        bool explicitLod = name.StartsWith("SampleLevel") || name.StartsWith("SampleGrad") ||
                           name.StartsWith("SampleCompareLevel");

        string sampled = SampledImage(c, texture, ref next, ref function);
        string coordinate = EmitValue(c.Args[next], ref function);
        Storage coordType = ArgType(c, next);
        next++;

        // Los argumentos restantes, en orden, con sus tipos.
        int restStart = next;
        var rest = new List<(string Id, Storage Type)>();
        for (int i = next; i < c.Args.Count; i++) rest.Add((EmitValue(c.Args[i], ref function), ArgType(c, i)));

        coordinate = MergeArrayIndex(coordinate, coordType, texture, rest, Num.Float, f);

        string? dref = null;
        string? component = null;
        string? bias = null, lod = null, ddx = null, ddy = null, offset = null;

        int takeIndex = 0;
        (string Id, Storage Type)? Take() => takeIndex < rest.Count ? rest[takeIndex++] : null;

        // La referencia de comparación: argumento separado (HLSL/MSL/WGSL) o última componente de la coordenada (GLSL).
        if (compare)
        {
            if (rest.Count > 0 && IsFloatScalar(rest[0].Type))
            {
                dref = rest[0].Id;
                takeIndex = 1;
            }
            else
            {
                int total = WidthOf(coordType);
                int needed = CoordinateComponents(texture);
                if (total > needed)
                {
                    dref = EmitRaw(f, SpirVOpTable.OpCompositeExtract, ScalarTypeId(Num.Float, f), P(coordinate), Lit((uint)(total - 1)));
                    coordinate = ShuffleHead(coordinate, Num.Float, total, needed, f);
                }
                else throw new NotSupportedException($"'{name}' no tiene referencia de comparación.");
            }
        }

        if (leadingComponent is not null) component = leadingComponent;

        if (gather && !compare && component is null)
        {
            if (rest.Count > 0 && IsIntScalar(rest[0].Type)) { component = rest[0].Id; takeIndex = 1; }
        }

        if (!gather)
        {
            if (name.StartsWith("SampleBias")) bias = Take()?.Id;
            else if (name.StartsWith("SampleLevel") || name.StartsWith("SampleCompareLevel"))
            {
                if (rest.Count > takeIndex && IsFloatScalar(rest[takeIndex].Type)) lod = Take()?.Id;
            }
            else if (name.StartsWith("SampleGrad")) { ddx = Take()?.Id; ddy = Take()?.Id; }
            else if (name.StartsWith("Sample") && rest.Count > takeIndex && IsFloatScalar(rest[takeIndex].Type) && !compare)
            {
                // texture(sampler, P, bias)
                bias = Take()?.Id;
            }
        }

        while (takeIndex < rest.Count)
        {
            var extra = rest[takeIndex++];
            // Un uint suelto es el 'status' de recursos por mosaicos (HLSL), no un offset.
            if (IsIntVector(extra.Type) || extra.Type is IRTypes.Int { Signed: true })
            {
                if (offset is not null)
                {
                    throw new NotSupportedException($"'{name}' con cuatro offsets (ConstOffsets) todavía no está soportada.");
                }

                offset = ConstantOffset(c.Args[restStart + takeIndex - 1], name, f);
            }
        }

        if (offset is not null && texture.Dimensions == IRTypes.TextureDimensions.Cube)
        {
            throw new NotSupportedException($"Un texture cube no admite offsets ('{name}').");
        }

        // Muestreo implícito con dref/offset en stages sin derivadas: se deja a elección del llamador.
        var mask = 0u;
        var operands = new List<SpirVInstructionParameter>();
        if (bias is not null) { mask |= OperandBias; operands.Add(P(bias)); }
        if (lod is not null) { mask |= OperandLod; operands.Add(P(lod)); }
        if (ddx is not null) { mask |= OperandGrad; operands.Add(P(ddx)); operands.Add(P(ddy!)); }
        if (offset is not null) { mask |= OperandConstOffset; operands.Add(P(offset)); }

        // Las variantes sin LOD explícito en un contexto explícito (Compare Level con lod 0) usan Lod = 0.
        if (explicitLod && lod is null && ddx is null)
        {
            mask |= OperandLod;
            operands.Insert(0, P(ConstOf(Num.Float, 0, 1, f)));
        }

        var all = new List<SpirVInstructionParameter>();
        Storage resultType;
        SpirVOperation op;

        if (gather)
        {
            resultType = TexelResult(texture, c.Result);
            if (compare)
            {
                op = SpirVOpTable.OpImageDrefGather;
                all.Add(P(sampled)); all.Add(P(coordinate)); all.Add(P(dref!));
            }
            else
            {
                op = SpirVOpTable.OpImageGather;
                all.Add(P(sampled)); all.Add(P(coordinate));
                all.Add(P(component ?? _module.DeclareIntConstant("0", 32, true)));
            }
        }
        else if (compare)
        {
            resultType = Scalar(Num.Float);
            op = explicitLod
                ? (proj ? SpirVOpTable.OpImageSampleProjDrefExplicitLod : SpirVOpTable.OpImageSampleDrefExplicitLod)
                : (proj ? SpirVOpTable.OpImageSampleProjDrefImplicitLod : SpirVOpTable.OpImageSampleDrefImplicitLod);
            all.Add(P(sampled)); all.Add(P(coordinate)); all.Add(P(dref!));
        }
        else
        {
            resultType = TexelResult(texture, c.Result);
            op = explicitLod
                ? (proj ? SpirVOpTable.OpImageSampleProjExplicitLod : SpirVOpTable.OpImageSampleExplicitLod)
                : (proj ? SpirVOpTable.OpImageSampleProjImplicitLod : SpirVOpTable.OpImageSampleImplicitLod);
            all.Add(P(sampled)); all.Add(P(coordinate));
        }

        if (mask != 0)
        {
            all.Add(Lit(mask));
            all.AddRange(operands);
        }

        string sampledValue = EmitRaw(f, op, TypeIdOf(resultType, f), [.. all]);
        return resultType is IRTypes.Vector ? FitTexel(sampledValue, resultType, c.Result, f) : sampledValue;
    }

    /// <summary>
    /// MSL y WGSL pasan el índice del array aparte de la coordenada: se une en un único vector
    /// (como float para muestreo, como entero para fetch).
    /// </summary>
    private string MergeArrayIndex(string coordinate, Storage coordType, IRTypes.Texture texture,
        List<(string Id, Storage Type)> rest, Num target, Function f)
    {
        int needed = CoordinateComponents(texture);
        int width = WidthOf(coordType);
        if (width >= needed) return coordinate;

        // Componentes sueltos (índice de array, cara de un cube) que se anexan a la coordenada.
        Num coordNum = NumOf(coordType);
        if (coordNum != target) coordinate = ConvertNum(coordinate, coordNum, target, width, f);

        var parts = new List<string>();
        if (width == 1) parts.Add(coordinate);
        else
        {
            for (int i = 0; i < width; i++)
                parts.Add(EmitRaw(f, SpirVOpTable.OpCompositeExtract, ScalarTypeId(target, f), P(coordinate), Lit((uint)i)));
        }

        while (parts.Count < needed && rest.Count > 0 && NumOf(rest[0].Type) is not Num.Other && WidthOf(rest[0].Type) == 1)
        {
            var (index, indexType) = rest[0];
            rest.RemoveAt(0);
            parts.Add(ConvertNum(index, NumOf(indexType), target, 1, f));
        }

        if (parts.Count != needed) return coordinate;
        return Emit(f, SpirVOpTable.OpCompositeConstruct, VectorTypeId(target, needed, f), [.. parts]);
    }

    /// <summary>Los primeros <paramref name="count"/> componentes de un vector.</summary>
    private string ShuffleHead(string vector, Num n, int total, int count, Function f)
    {
        if (count == 1)
        {
            return EmitRaw(f, SpirVOpTable.OpCompositeExtract, ScalarTypeId(n, f), P(vector), Lit(0));
        }

        var parts = new SpirVInstructionParameter[count + 3];
        parts[0] = P(VectorTypeId(n, count, f));
        parts[1] = P(vector);
        parts[2] = P(vector);
        for (int i = 0; i < count; i++) parts[3 + i] = Lit((uint)i);
        string id = Tmp(f);
        _module.AddResultLine(id, SpirVOpTable.OpVectorShuffle, parts);
        return id;
    }

    private string EmitFetch(IntrinsicCall c, IRTypes.Texture texture, ref Function function)
    {
        Function f = function;
        string image = PlainImage(c, texture, ref function);
        string coord = EmitValue(c.Args[1], ref function);
        Storage coordType = ArgType(c, 1);

        var operands = new List<SpirVInstructionParameter>();
        uint mask = 0;

        var restArgs = new List<(string Id, Storage Type)>();
        for (int i = 2; i < c.Args.Count; i++) restArgs.Add((EmitValue(c.Args[i], ref function), ArgType(c, i)));
        if (texture.Dimensions == IRTypes.TextureDimensions.Cube)
        {
            throw new NotSupportedException($"'{c.Name}': SPIR-V no permite OpImageFetch sobre imágenes cube.");
        }

        NormalizeIntCoordinate(ref coord, coordType, f);
        coordType = TypeOfNum(Num.Int, WidthOf(coordType));
        coord = MergeArrayIndex(coord, coordType, texture, restArgs, Num.Int, f);

        // HLSL Load(int3(x, y, mip)): el nivel (o la muestra, en multisample) va en la última componente de la coordenada.
        // SPIR-V quiere solo las componentes de la coordenada y el nivel como operando Lod/Sample: una coordenada más
        // larga la acepta spirv-val pero ignora el nivel (siempre leería el 0) y naga la rechaza.
        int needed = CoordinateComponents(texture);
        if (restArgs.Count == 0 && WidthOf(coordType) == needed + 1)
        {
            string level = EmitRaw(f, SpirVOpTable.OpCompositeExtract, ScalarTypeId(Num.Int, f), P(coord), Lit((uint)needed));
            coord = ShuffleHead(coord, Num.Int, needed + 1, needed, f);
            coordType = TypeOfNum(Num.Int, needed);
            restArgs.Add((level, TypeOfNum(Num.Int, 1)));
        }

        if (restArgs.Count > 0)
        {
            string extra = restArgs[0].Id;
            NormalizeIntCoordinate(ref extra, restArgs[0].Type, f);
            if (texture.Multisampled) { mask = OperandSample; }
            else if (texture.Dimensions != IRTypes.TextureDimensions.Cube) { mask = OperandLod; }
            if (mask != 0) operands.Add(P(extra));
        }
        else if (!texture.Multisampled)
        {
            mask = OperandLod;
            operands.Add(P(_module.DeclareIntConstant("0", 32, true)));
        }

        var all = new List<SpirVInstructionParameter> { P(image), P(coord) };
        if (mask != 0) { all.Add(Lit(mask)); all.AddRange(operands); }
        Storage texel = TexelResult(texture, c.Result);
        return FitTexel(EmitRaw(f, SpirVOpTable.OpImageFetch, TypeIdOf(texel, f), [.. all]), texel, c.Result, f);
    }

    private void NormalizeIntCoordinate(ref string coord, Storage type, Function f)
    {
        Num n = NumOf(type);
        if (n is Num.Uint or Num.Float) coord = ConvertNum(coord, n, Num.Int, WidthOf(type), f);
    }

    private string? EmitImageReadWrite(IntrinsicCall c, IRTypes.Texture texture, ref Function function)
    {
        Function f = function;
        if (texture.Usage != IRTypes.TextureUsage.Storage)
        {
            throw new NotSupportedException(
                $"'{c.Name}' necesita una textura de almacenamiento (storage), pero '{texture.HumanName}' es de muestreo.");
        }

        string image = EmitValue(c.Args[0], ref function);
        bool store = c.Name.StartsWith("ImageStore");

        // GLSL: imageStore(image, coord, data); MSL: write(texture, data, coord, [array]).
        int dataIndex = -1;
        if (store)
        {
            dataIndex = WidthOf(ArgType(c, 1)) == 4 && CoordinateComponents(texture) < 4 ? 1 : c.Args.Count - 1;
        }

        var others = new List<int>();
        for (int i = 1; i < c.Args.Count; i++)
        {
            if (i != dataIndex) others.Add(i);
        }

        string coord = EmitValue(c.Args[others[0]], ref function);
        Storage coordType = ArgType(c, others[0]);
        NormalizeIntCoordinate(ref coord, coordType, f);
        coordType = TypeOfNum(Num.Int, WidthOf(coordType));

        var rest = new List<(string Id, Storage Type)>();
        foreach (int i in others.Skip(1)) rest.Add((EmitValue(c.Args[i], ref function), ArgType(c, i)));
        coord = MergeArrayIndex(coord, coordType, texture, rest, Num.Int, f);

        if (!store)
        {
            var all = new List<SpirVInstructionParameter> { P(image), P(coord) };
            if (texture.Multisampled && rest.Count > 0)
            {
                all.Add(Lit(OperandSample));
                all.Add(P(rest[0].Id));
            }

            Storage texel = TexelResult(texture, c.Result);
            return FitTexel(EmitRaw(f, SpirVOpTable.OpImageRead, TypeIdOf(texel, f), [.. all]), texel, c.Result, f);
        }

        string data = EmitValue(c.Args[dataIndex], ref function);
        var write = new List<SpirVInstructionParameter> { P(image), P(coord), P(data) };
        if (texture.Multisampled && rest.Count > 0)
        {
            write.Add(Lit(OperandSample));
            write.Add(P(rest[0].Id));
        }

        _module.AddLine(SpirVOpTable.OpImageWrite, [.. write]);
        return null;
    }

    private string EmitImageQuery(IntrinsicCall c, IRTypes.Texture texture, ref Function function)
    {
        Function f = function;
        string name = c.Name;
        string image = PlainImage(c, texture, ref function);
        string intType = ScalarTypeId(Num.Int, f);

        if (name.StartsWith("QueryLevels"))
            return FitResult(Emit(f, SpirVOpTable.OpImageQueryLevels, intType, image), Scalar(Num.Int), c.Result, f);
        if (name.StartsWith("QuerySamples"))
            return FitResult(Emit(f, SpirVOpTable.OpImageQuerySamples, intType, image), Scalar(Num.Int), c.Result, f);

        int dims = CoordinateComponents(texture);
        if (texture.Dimensions == IRTypes.TextureDimensions.Cube) dims = 2 + (texture.Arrayed ? 1 : 0);
        string sizeType = VectorTypeId(Num.Int, dims, f);

        bool noLod = texture.Multisampled || texture.Usage == IRTypes.TextureUsage.Storage;
        string size;
        if (noLod)
        {
            size = Emit(f, SpirVOpTable.OpImageQuerySize, sizeType, image);
        }
        else
        {
            string lod = c.Args.Count > 1 ? EmitValue(c.Args[1], ref function) : _module.DeclareIntConstant("0", 32, true);
            size = Emit(f, SpirVOpTable.OpImageQuerySizeLod, sizeType, image, lod);
        }

        if (name.StartsWith("QueryLayers"))
        {
            return FitResult(EmitRaw(f, SpirVOpTable.OpCompositeExtract, intType, P(size), Lit((uint)(dims - 1))),
                Scalar(Num.Int), c.Result, f);
        }

        // TextureSize: el resultado puede pedirse con otro ancho (se recorta) o como uint.
        int wanted = WidthOf(c.Result);
        string trimmed = wanted > 0 && wanted < dims ? ShuffleHead(size, Num.Int, dims, wanted, f) : size;
        int width = wanted > 0 && wanted <= dims ? wanted : dims;
        if (NumOf(c.Result) == Num.Uint) return ConvertNum(trimmed, Num.Int, Num.Uint, width, f);
        return trimmed;
    }

    /// <summary>
    /// El offset de texels de un muestreo o gather. SPIR-V lo pide como constante (<c>ConstOffset</c>): un offset que
    /// se calcula en ejecución necesitaría <c>Offset</c> + ImageGatherExtended y HLSL, GLSL y WGSL tampoco lo admiten.
    /// </summary>
    private string ConstantOffset(Expression expression, string name, Function function)
    {
        if (expression is IntLiteral) return EmitValue(expression, ref function);

        if (expression is VectorExpression vector && vector.Values.All(value => value is IntLiteral))
        {
            string typeId = _module.DeclareType(new IRTypes.Vector("vector", StorageKind.Local, null)
            {
                ComponentsLength = vector.Values.Count,
                ComponentsType = vector.ComponentsType
            }, function.Stage);

            var components = vector.Values.Select(value => EmitValue(value, ref function)).ToList();
            string compositeId = "%constoffset_" + string.Join("_", components.Select(id => id.TrimStart('%')));
            if (!_module.IsDefined(compositeId))
            {
                _module.AddResultLine(compositeId, SpirVOpTable.OpConstantComposite,
                    [SpirVModuleHelpers.Id(typeId), .. components.Select(SpirVModuleHelpers.Id)]);
            }

            return compositeId;
        }

        throw new NotSupportedException($"'{name}' con un offset que no es constante: los offsets de textura dinámicos no están soportados. Usa un valor conocido al compilar.");
    }
}
