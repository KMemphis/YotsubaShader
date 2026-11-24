# Progreso: traducción de C# y validación "si compila en C#, compila en la GPU"

Plan aprobado: `~/.claude/plans/en-los-5-estilos-polymorphic-floyd.md`. Nada está commiteado.

## Estado de las suites
- `Examples/YotsubaShader.Example`: **235/235** (`dotnet test -p:CompileYotsubaShaders=false`).
- `Core/Analyzers/YotsubaShader.IDE.Analyzer.Tests`: **99/99** (proyecto nuevo, añadido al .sln en la carpeta Tests).
- `SpirvPipeline.Compile` (Example) compila C# → SPIR-V y lo pasa por `spirv-val --target-env vulkan1.3`. El compilador emite SPIR-V 1.6, así que Vulkan 1.2 no sirve.

## Hecho

### 1. Reglas compartidas — `Core/Analyzers/YotsubaShader.Rules/`
Se enlazan como código fuente en el analizador del IDE y en `YotsubaShader.Roslyn`.

- **Catálogo y modelo:**
  - `ShaderRules.cs`: catálogo YS0001–YS0709. Las severidades salen de aquí y `AnalyzerReleases.Unshipped.md` se regenera con un script desde este archivo.
  - `ShaderModel.cs`: detección de conceptos por meta-atributos y de la etapa (`[ShaderStage]`).
  - `GpuType.cs`: clasificación de tipos de C# para la GPU.
  - `BuiltInTable.cs`: tabla de built-ins por stage, dirección y tipo, con `Supported` y portabilidad.
  - `ShaderTargets.cs`: enum grueso interno; los destinos ya no vienen de la propiedad MSBuild `YotsubaShaderTargets` (retirada) sino de `[ShaderTargets]` / `[ShaderEngines]` (ver `GraphicsApi/`).
- **Reglas:**
  - `DeclarationRules.cs`, `InterfaceRules.cs`, `ResourceRules.cs`, `BufferRules.cs`, `VectorRules.cs`: entry points, interfaz, recursos y layouts.
  - `BodyRules.cs`: C# no soportado, uso por stage, portabilidad y control de flujo no uniforme.
  - `ShaderSession.cs`: grafo de llamadas, recursión, `ShaderValidator.Validate`.
  - `MathSupport.cs` y `MathPolyfillNames.cs`.
- **`SupportedFeatures`** (dentro de `BufferRules.cs`): banderas que hay que poner en `true` al implementar cada cosa. Hoy todas están en `true`.
- **Analizador del IDE:** `ShaderAnalyzer.cs`. Se borró el `Vector.cs` vacío.
  - **Configuración:** `build/YotsubaShader.IDE.Analyzer.props` expone `CompilerVisibleProperty`.
  - **Ejecución:** los cuerpos de las clases shader se analizan en cuanto se escriben; lo que cruza funciones, en `CompilationEnd`.
- **Compilador:** `ShaderProcessor.Process` ejecuta `ShaderValidator` y lanza `ShaderValidationException` (nueva) con los YS.
  - Los tests negativos existentes se actualizaron para esperar los IDs YS.

### 2. C# que ahora compila (todo validado con spirv-val)
- **`default` y constantes:** `default`/`default(T)` usan la expresión IR `ZeroValue` (OpConstantNull). Las constantes `const` se pliegan a literales.
- **Creación de vectores:** `new float4 { X = ... }` sin constructor parte del valor cero.
- **`switch`:**
  - Sentencia y expresión, con IR `Switch` y `OpSwitch`.
  - Patrones, `when`, `and/or/not`.
  - `break` sale del switch y `continue` llega al bucle.
- **Parámetros por referencia:** `ref`/`out`/`in` en funciones auxiliares (`Function.ParameterModes`, punteros, temporal y copia de vuelta) y `out var x`.
- **Métodos de instancia de structs:** reciben `this` por referencia. Por eso funciona `Deconstruct`.
- **Funciones locales:** con capturas por referencia o `in` (`CaptureInfo`), anidadas, `static` y de cuerpo de expresión.
- **Tuplas y deconstrucción:** `var (a, b) = ...`, intercambio, funciones que devuelven tuplas y `Deconstruct`.
- **Arrays:**
  - Parámetros array con una versión de la función por longitud (`FunctionsInfo.ArrayLengths`, `Id`).
  - `Length` y `foreach` sobre parámetros, campos y buffers.
- **Buffers:**
  - Matrices (ColMajor, MatrixStride).
  - Arrays con `[ArrayLength(n)]` (atributo nuevo en Language).
  - Arrays sin tamaño en storage buffers (`OpTypeRuntimeArray`, IR `RuntimeArrayLength` → `OpArrayLength`).
  - Tipos con layout separados por clase de buffer, y `OpCopyLogical` al leer o escribir.
- **Funciones internas (`Math`, `BitConverter`):**
  - `Roslyn/Polyfills/MathPolyfills.cs` es un recurso embebido que se añade a la compilación; `MathPolyfillResolver` redirige las llamadas a él.
  - Cubre: Round con dígitos o modo, Cbrt, CopySign, IEEERemainder, Max/MinMagnitude, ILogB, MathF.BitIncrement/Decrement y DivRem.
  - `BitConverter` Single↔Int32 se mapea a intrínsecas.
- **Correcciones del compilador encontradas al validar contra Vulkan:**
  - Decoración `Flat` en entradas enteras de fragment.
  - Clase de almacenamiento en el acceso a arrays dentro de buffers.
  - Copia de arrays pasados por valor.
  - El test de Float64 se ajustó porque ahora la conversión se pliega.

## Pendiente (en este orden)

### Paso 4: HECHO
- `ShaderProcessor.ResolveBuiltIn` usa `BuiltInTable` (la misma tabla del IDE).
- **Built-ins nuevos en el IR y en SPIR-V:** BaseVertex, BaseInstance, SampleId, SamplePosition, PrimitiveId, HelperInvocation, FragDepthGreater y FragDepthLess.
- **Capabilities:** SampleRateShading, DrawParameters y Geometry.
- **Modos de ejecución:** DepthReplacing, DepthGreater y DepthLess.
- **Flat:** también en los built-ins enteros de fragment.
- **Tests:** `Examples/YotsubaShader.Example/BuiltInTests.cs`; todos pasan spirv-val.
- **Nueva regla:** `long`/`ulong` es un error YS0503 (en C#, `uint + int` da `long`). Bandera `SupportedFeatures.Int64 = false`.

### Paso 5: HECHO
- **Reglas nuevas:**
  - Punteros, `stackalloc`, `fixed` y `unsafe` (YS0502).
  - Escritura en buffers de solo lectura (YS0308): MSL `device(readOnly)`, WGSL `storage(read)` y el concepto ReadOnlyStorageBuffer.
  - `StructuredBuffer<T>`/`ConstantBuffer<T>` todavía no están soportados (YS0306).
  - Campos del shader con semántica o built-in, validados contra las etapas del shader.
- **Documentación:** `docs/rules/YSxxxx.md`, 80 páginas más un índice, generadas con `scripts/generate-rule-docs.py`. `AnalyzerReleases.Unshipped.md` se regenera del catálogo.
- **Ejemplo:** `Examples/Shaders/TriangleShader.cs` declara `[ShaderTargets(...)]`; la propiedad `YotsubaShaderTargets` ya no existe.

### Paso 6: HECHO
- `Examples/YotsubaShader.Example/InvariantTests.cs`: 62 shaders raros o mal hechos (slots, etiquetas, etapas, recursos, C# extraño, operaciones de otra etapa).
- **Invariante:** cada uno da error YS, o produce SPIR-V que pasa `spirv-val --target-env vulkan1.3`. Los 62 lo cumplen.

### Paso 7: HECHO
- `dotnet build YotsubaShader.sln` con la generación de shaders activa: 0 errores.

## Buffers envoltorio: HECHO
- **Qué son:** `StructuredBuffer<T>`, `RWStructuredBuffer<T>` y `ConstantBuffer<T>` (HlslSyntax/Resources).
- **Stubs:** ahora tienen indexador (con `ref` en RW), `Length` y `Value`.
- **Compilador (`ShaderProcessor.TryBuildBufferWrapper`):** cada envoltorio pasa a ser un bloque `{ T Items[]; }` o el bloque `T`.
- **Traducción (`ProcessBufferWrapperAccess`):** el indexador accede al array, `Length` usa `OpArrayLength` y `Value` devuelve el bloque.
- **Reglas:** `ResourceRules.WrapperElement` y `BufferRules.AnalyzeWrapper`. Las atómicas aceptan `ref Items[i].X`.

## Colecciones de .NET como buffers: HECHO
- **Qué tipos:** un campo `ImmutableArray<T>` o `ImmutableList<T>` es un storage buffer de solo lectura; `T[]` o `List<T>` es uno de lectura y escritura.
- **Uso:** se declaran con binding y funcionan el indexador, `Length`/`Count`, `foreach` y las atómicas (`ref arr[i].X`).
- **Código:** `ShaderProcessor.CollectionElement`, `ResourceRules.CollectionElement` y `IsElementBuffer`. El campo se usa como su array `Items`.
- **Excepción:** con `[Uniform]`, un `float[]` no se convierte en storage buffer (da error, porque un uniform necesita un struct).
- **Límites:** los métodos de List/ImmutableArray (`Add`, `Contains`...) dan YS0406, y las colecciones locales dan YS0503.

## Límites conocidos (dan error YS claro; no rompen el invariante)
- **Tipos y C#:** `long`/`ulong` (YS0503). Escribir en una entrada del shader (YS0603; hay que copiarla a un local).
- **Built-ins:** ClipDistance, Layer/ViewportIndex, SampleMask, StencilRef, Barycentrics y Subgroups (YS0219).
- **Intrínsecas:** algunas variantes sobre matrices y gather con 4 offsets (YS0508), según lo que ya rechaza `FunctionBodyEmitter.Intrinsics.cs`.
- **Etapas:** no hay tessellation, geometry, mesh ni ray tracing.

## Siguiente paso sugerido
- Ampliar el corpus de `InvariantTests` cada vez que se encuentre un caso nuevo.
- Re-ejecutar el probe de intrínsecas del scratchpad contra `vulkan1.3`, porque hasta ahora usaba el entorno universal.

## Idioma de los diagnósticos
- **Base:** los textos de los diagnósticos del analizador están en inglés en el código (`ShaderRules.cs`, notas de los perfiles de `GraphicsApi` y mensajes de las reglas).
- **Español:** `YotsubaShader.Rules/Localization/Loc.Es.cs` traduce cada texto (la clave es el inglés). Se usa cuando la cultura de la interfaz es `es*`; con otra cultura sale el inglés.
- **Código:** `Loc.T("...")` y `Loc.F("... {0}", arg)` en las reglas; los descriptores usan `Loc.Text`. Los datos estáticos (notas de perfiles y de `BuiltInTable`) se traducen donde se consumen.
- **Cobertura:** `LocalizationTests` recorre las fuentes y falla si falta una traducción o si un texto no está en inglés.
- **Fuera de alcance:** los mensajes de excepción del compilador (`YotsubaShader.Roslyn`, `Compiler.SpirV`) siguen en español.
