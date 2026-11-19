namespace YotsubaShader.Concepts.Methods;

/// <summary>
    /// Operaciones intrínsecas de hardware: solo se llaman, no se definen.
    /// Cada sobrecarga es un elemento distinto.
    /// </summary>
    public enum Intrinsic
    {
        // ── Helpers matemáticos (Math / MathF) ──
        Sin, Cos, Tan, Asin, Acos, Atan, Atan2,
        Sinh, Cosh, Tanh, Asinh, Acosh, Atanh,
        Pow, Exp, Log, Log2, Log10, Sqrt, InverseSqrt,
        Abs, Sign, Floor, Ceiling, Round, Truncate,
        Min, Max, Clamp, FusedMultiplyAdd,
    
        // ── Derivadas en pantalla (solo fragment) ──
        DerivativeXScalar, DerivativeXVector,
        DerivativeYScalar, DerivativeYVector,
        FwidthScalar, FwidthVector,
        DerivativeXCoarseScalar, DerivativeXCoarseVector,
        DerivativeYCoarseScalar, DerivativeYCoarseVector,
        DerivativeXFineScalar, DerivativeXFineVector,
        DerivativeYFineScalar, DerivativeYFineVector,
        FwidthCoarseScalar, FwidthCoarseVector,
        FwidthFineScalar, FwidthFineVector,

        // ── Muestreo con LOD implícito ──
        Sample1D, Sample2D, Sample3D, SampleCube, Sample1DArray, Sample2DArray, SampleCubeArray,

        // ── Muestreo con bias ──
        SampleBias1D, SampleBias2D, SampleBias3D, SampleBiasCube,
        SampleBias1DArray, SampleBias2DArray, SampleBiasCubeArray,

        // ── Muestreo con LOD explícito ──
        SampleLevel1D, SampleLevel2D, SampleLevel3D, SampleLevelCube,
        SampleLevel1DArray, SampleLevel2DArray, SampleLevelCubeArray,

        // ── Muestreo con gradientes explícitos ──
        SampleGrad1D, SampleGrad2D, SampleGrad3D, SampleGradCube,
        SampleGrad1DArray, SampleGrad2DArray, SampleGradCubeArray,

        // ── Muestreo con offset en texels ──
        SampleOffset1D, SampleOffset2D, SampleOffset3D, SampleOffset1DArray, SampleOffset2DArray,

        // ── Muestreo proyectivo ──
        SampleProj1DVec2, SampleProj1DVec4, SampleProj2DVec3, SampleProj2DVec4, SampleProj3D,

        // ── Comparación de profundidad (sombras) ──
        SampleCompare1D, SampleCompare2D, SampleCompareCube,
        SampleCompare1DArray, SampleCompare2DArray, SampleCompareCubeArray,

        // ── Comparación de profundidad con LOD explícito ──
        SampleCompareLevel1D, SampleCompareLevel2D, SampleCompareLevel1DArray,

        // ── Gather (4 texels del filtro bilineal) ──
        Gather2D, Gather2DArray, GatherCube, GatherCubeArray,

        // ── Gather con comparación de profundidad ──
        GatherCompare2D, GatherCompare2DArray, GatherCompareCube, GatherCompareCubeArray,

        // ── Texel sin filtrar ──
        TexelFetch1D, TexelFetch2D, TexelFetch3D, TexelFetch1DArray, TexelFetch2DArray,
        TexelFetch2DMS, TexelFetch2DMSArray, TexelFetchBuffer,

        // ── Textura de almacenamiento: leer ──
        ImageLoad1D, ImageLoad2D, ImageLoad3D, ImageLoadCube, ImageLoad2DArray, ImageLoad2DMS,

        // ── Textura de almacenamiento: escribir ──
        ImageStore1D, ImageStore2D, ImageStore3D, ImageStoreCube, ImageStore2DArray, ImageStore2DMS,

        // ── Tamaño de la textura ──
        TextureSize1D, TextureSize2D, TextureSize3D, TextureSizeCube,
        TextureSize1DArray, TextureSize2DArray, TextureSizeCubeArray, TextureSize2DMS,

        // ── Niveles de mip ──
        QueryLevels1D, QueryLevels2D, QueryLevels3D, QueryLevelsCube,
        QueryLevels1DArray, QueryLevels2DArray, QueryLevelsCubeArray,

        // ── Número de muestras ──
        QuerySamples2DMS, QuerySamples2DMSArray,

        // ── Número de capas ──
        QueryLayers1DArray, QueryLayers2DArray, QueryLayersCubeArray,

        // ── LOD de un muestreo implícito ──
        QueryLod1D, QueryLod2D, QueryLod3D, QueryLodCube, QueryLod1DArray, QueryLod2DArray, QueryLodCubeArray,

        // ── Control del fragment ──
        Discard,
        DiscardIfNegative,
        InterpolateAtCentroidScalar, InterpolateAtCentroidVector,
        InterpolateAtSampleScalar, InterpolateAtSampleVector,
        InterpolateAtOffsetScalar, InterpolateAtOffsetVector,

        // ── Compute: sincronización ──
        ThreadGroupBarrier,
        MemoryBarrierAll, MemoryBarrierBuffer, MemoryBarrierImage, MemoryBarrierShared, MemoryBarrierGroup,

        // ── Compute: atómicas ──
        AtomicAddInt, AtomicAddUint,
        AtomicSubInt, AtomicSubUint,
        AtomicMinInt, AtomicMinUint,
        AtomicMaxInt, AtomicMaxUint,
        AtomicAndInt, AtomicAndUint,
        AtomicOrInt, AtomicOrUint,
        AtomicXorInt, AtomicXorUint,
        AtomicExchangeInt, AtomicExchangeUint,
        AtomicCompareExchangeInt, AtomicCompareExchangeUint,
        AtomicLoadInt, AtomicLoadUint,
        AtomicStoreInt, AtomicStoreUint,

        // ── Matemática escalar y vectorial adicional ──
        Fract, Mod, Remainder, Lerp, Step, SmoothStep, Saturate, Radians, Degrees, Exp2, Exp10, Reciprocal, RoundEven, Ldexp, Frexp, Modf, SinCos, SinPi, CosPi, TanPi, MultiplyExtended, IsNaN, IsInfinity, IsFinite,

        // ── Geometría y álgebra lineal ──
        Dot, Cross, Length, LengthSquared, Distance, DistanceSquared, Normalize, Reflect, Refract, FaceForward, OuterProduct, Transpose, Determinant, MatrixInverse, MatrixMultiply, MatrixComponentMultiply,

        // ── Comparación y lógica por componente ──
        Equal, NotEqual, LessThan, LessThanEqual, GreaterThan, GreaterThanEqual, Any, All, Not, Select,

        // ── Bits y enteros ──
        CountOneBits, CountLeadingZeros, CountTrailingZeros, FindLeastSignificantBit, FindMostSignificantBit, ReverseBits, ExtractBits, InsertBits, AddCarry, SubtractBorrow, FloatBitsToInt, IntBitsToFloat,

        // ── Empaquetado ──
        PackHalf2x16, UnpackHalf2x16, PackSnorm2x16, UnpackSnorm2x16, PackUnorm2x16, UnpackUnorm2x16, PackSnorm4x8, UnpackSnorm4x8, PackUnorm4x8, UnpackUnorm4x8,

        // ── Texturas adicionales ──
        SampleCompareLevel2DArray, SampleCompareLevelCube, SampleCompareLevelCubeArray, TexelFetchCube,
    }
