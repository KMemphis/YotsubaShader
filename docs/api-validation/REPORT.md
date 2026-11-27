# Errores de los compiladores gráficos que el analizador de YotsubaShader no tiene

Analizador examinado: `Core/Analyzers` del repo KMemphis/YotsubaShader, versión 0.9.5 (83 reglas YS, 33 perfiles de API).
Fecha de las fuentes: 26-nov-2026.

## 1. Qué modela el analizador por API

Cada `Target` (Vulkan 1.0–1.4, OpenGL 2.0–4.6, OpenGL ES 2.0–3.2, Direct3D 8.0–12, Metal 1–4, WebGPU) tiene un perfil en `GraphicsApi/*.cs`.
Un perfil solo dice tres cosas:

| Qué | Detalle |
|---|---|
| 8 características | `Float64`, `Float16`, `Int16`, `Int64`, `Compute`, `StorageBuffer`, `StorageTexture`, `StorageTextureWithoutFormat`: garantizada, opcional (aviso) o no disponible (error si el shader declara esa API) |
| 5 límites | render targets, entradas de vértice, variables entre etapas, sets de descriptores, tamaño de workgroup |
| Tablas por familia | `BuiltInTable` (built-ins que faltan en WebGPU/D3D12/OpenGL) y reglas de portabilidad YS0701–YS0709, casi todas pensadas para WebGPU/Metal |

No hay nada específico de Shader Model, de versión de GLSL, de precisión (GLSL ES), de límites de instrucciones o registros, ni de sintaxis de texturas por perfil. `DirectX_90c` (SM3) solo añade `Emits("hlsl","3.0")` y dos límites.

## 2. Fuentes oficiales y su formato

| API | Fuente | Formato | Entradas extraídas |
|---|---|---|---|
| Vulkan / SPIR-V | `registry/validusage.json` de Vulkan-Headers (api 1.4.369) | **JSON oficial** | 27 487 VUID en total; 572 de entorno SPIR-V (`StandaloneSpirv` 206 + `RuntimeSpirv` 366) |
| Direct3D 12 (DXC) | `lib/DxilValidation/*.cpp` de DirectXShaderCompiler | Código fuente (sin JSON ni lista pública) | 295 nombres de regla `ValidationRule` (solo el nombre, sin mensaje) |
| Direct3D 9/10/11 (FXC) | Microsoft Learn, "HLSL errors and warnings" | Tabla HTML oficial | 477 códigos (3xxx y 4xxx) |
| WebGPU (naga) | `naga/src/valid/*.rs` de wgpu | Código fuente (`#[error(...)]`) | 293 mensajes |
| OpenGL / GLES (glslang) | `ParseHelper.cpp` y `Versions.cpp` | Código fuente, sin lista oficial | 587 mensajes únicos (772 llamadas); **sin clasificar** |
| Metal | Especificación MSL | PDF | No hay lista oficial de errores del compilador; no se extrajo nada |

No encontré un formato estándar para los errores de DXC, glslang ni Metal. Vulkan es la única API con JSON oficial.
En DXC también existe `DiagnosticSemaKinds.td` (TableGen): 195 `err_hlsl_*` y 1 691 `err_*` en total. No los clasifiqué, porque son errores de sintaxis y semántica de HLSL como texto, y el analizador recibe C#.

## 3. Resumen

| API | Total | Cubiertas | **No cubiertas** | Fuera de alcance | Internas del IR |
|---|---|---|---|---|---|
| Vulkan / SPIR-V | 572 | 29 | **212** | 331 | — |
| Direct3D 12 (DXC) | 295 | 4 | **80** | 39 | 172 |
| Direct3D 9/10/11 (FXC) | 477 | 8 | **115** | — | 354 son de lenguaje/sintaxis HLSL |
| WebGPU (naga) | 293 | 17 | **34** | 66 | 176 |

- **Cubiertas:** el analizador tiene una regla equivalente (se indica cuál en la columna `regla_yotsuba` de los CSV).
- **Fuera de alcance:** etapas y extensiones que el CLI no genera (solo Vertex, Fragment y Compute): ray tracing, mesh/task, tessellation, geometry, cooperative matrix, tile shading, etc.
- **Internas del IR:** validación de DXIL o de naga que solo salta por un fallo del propio compilador, no por lo que escribe el usuario.
- **FXC:** de los 477 códigos, 354 son de sintaxis/semántica HLSL que un shader en C# no puede provocar. Los 115 "no cubiertos" dependen del perfil de destino y sí los puede provocar un shader C# válido.

## 4. Qué falta, por API

### Vulkan / SPIR-V (212 reglas, todas en `vulkan-no-cubiertas.txt`)

- **Semántica de memoria y atómicas** (~20): `MemorySemantics-10865…10879`, `UnequalMemorySemantics-*`, `None-04641`. El analizador solo tiene YS0604 (atómica sobre variable local).
- **Interpolación:** un entero de entrada en fragment debe ser `Flat` (`Flat-04744`); `Flat/NoPerspective/Sample/Centroid` solo en Input/Output (`04670`, `06201`, `06202`). No hay ninguna regla de interpolación.
- **Decoración `Component`** (`04920–04923`, `07703`, `10583`).
- **Imágenes:** tipo muestreado y formato (`04656`, `04657`, `04965`), `Dim` Rect (`09638`), SubpassData (`06214`, `04660`), Dref sobre 3D (`04777`), offsets (`04865`), `OpImageRead` debe ser vec4 (`04780`).
- **Modos de ejecución de fragment:** `OriginLowerLeft` (`04653`) y `PixelCenterInteger` (`04654`) prohibidos.
- **Inicializadores** por clase de almacenamiento (`04651`, `04734`).
- **Memoria compartida:** tamaño total de `Workgroup` (`06530`) y zero-init (`06372`).
- **Indexado dinámico de arrays de recursos** (`10127–10133`, `10134–10147`).
- **Float controls:** denorm, redondeo, cero con signo, `FMA` (`06289–06306`, `09559–09562`, `10977`, `10978`).
- **Clip/cull distance:** límites (`00708–00710`, `08448`, `08449`).
- **Subgroup:** disponibilidad de operaciones (`06342`, `06343`, `06275–06277`).
- **Otros:** `InterpolateAtOffset` rango (`06344–06347`), stores/atómicas en vertex/fragment (`06340`, `06341`), `Invariant` (`04677`), `FPRoundingMode` (`04675`), constantes de especialización en tamaño de array (`04681`), input attachments (`06678`, `12551`, `12552`), tamaño de push constants (`12455`).

### Direct3D 12, DXC (80 reglas, `dxc-no-cubiertas.txt`)

Solo se cubren 4 (`FlowNoRecursion`, `SmMaxTheadGroup`, `SmIncompatibleStage`, `SmCSNoSignatures`). Faltan, por ejemplo:

- **Constant buffers:** `SmCBufferSize`, `SmCBufferElementOverflow`, `SmCBufferOffsetOverlap`, `SmCBufferArrayOffsetAlignment`, `SmCBufferTemplateTypeMustBeStruct`.
- **Pixel shader:** `SmPSMultipleDepthSemantic`, `SmPSOutputSemantic`, `SmPSTargetCol0`, `SmPSTargetIndexMatchesRow`, `SmPSConsistentInterp`, `SmConstantInterpMode`, `SmNoInterpMode`, `SmROVOnlyInPS`.
- **Salidas e interfaz:** `SmUndefinedOutput`, `SmCompletePosition`, `SmSemantic`, `SmName`, `SmViewIDNeedsSlot`.
- **Recursos:** `SmResourceRangeOverlap`, `SmInvalidResourceKind`, `SmInvalidResourceCompType`, `SmInvalidTextureKindOnUAV`, `SmSampleCountOnlyOn2DMS`.
- **Grupos y wave:** `SmThreadGroupChannelRange`, `SmIncompatibleThreadGroupDim`, 13 reglas `SmWaveSize*`.
- **Flujo y tipos:** `FlowReducible`, `FlowDeadLoop`, `TypesNoVector`, `TypesIntWidth`, `DeclShaderReturnVoid`.

### Direct3D 9/10/11, FXC (115 códigos, `fxc-no-cubiertas.txt`)

El analizador cubre 8: double, int64, UAV y compute según versión, y derivadas indeterminadas. Faltan, entre otros:

- **Destinos antiguos (SM2/SM3):** `X3522 ERR_TEXTURE_OBJECTS_UNSUPPORTED` (texturas con sintaxis DX10), `X3511 ERR_CANT_UNROLL`, `X3528 ERR_CANT_BRANCH`, `X3525 ERR_NOT_SIMPLE_LOOP`, `X3526 ERR_GRADIENT_WITH_BREAK`, `X3527 ERR_TEXTURE_OFFSET`, `X3512 ERR_ARRAY_INDEX_MUST_BE_LITERAL`, `X3535 ERR_UNSUPPORTED_OPERATION` (operaciones bit a bit), `X3041 ERR_SHADER_VERSION`.
- **Semánticas:** `X4016 ERR_INVALID_SEMANTIC`, `X4528–X4538` (salidas de color/depth de pixel shader: 4 componentes, contiguas, `SV_Target0` completo).
- **Límites de registros e instrucciones:** `X4505 ERR_MAX_TEMP_EXCEEDED`, `X4506 ERR_MAX_INPUT_EXCEEDED`, `X4507 ERR_MAX_CONST_EXCEEDED`, `X4510 ERR_MAX_SAMPLER_EXCEEDED`, `X4519 ERR_TOO_MANY_TEXREADS`, `X4521 ERR_OUT_OF_TEMP`, `X4551 ERR_MAX_LOOP_EXCEEDED`, `X4018 ERR_TOO_MANY_PHASES`.
- **Tipos y recursos:** `X3650–X3652` (half, double, enteros pequeños), `X4545 ERR_TEXTURE_NOT_SUPPORTED`, `X4596 ERR_TEX1D_UNSUPPORTED`, `X4586–X4591` (límites de groupshared).

### WebGPU, naga (34 reglas, `naga-no-cubiertas.txt`)

- **Interpolación:** `InvalidInterpolationForInteger` (`@interpolate(flat)` obligatorio en enteros), `MissingInterpolation`, `InvalidInterpolationInStage`, `InvalidInterpolationSamplingCombination`.
- **Dual-source blending (`@blend_src`):** 5 reglas (`InvalidBlendSrcIndex`, `IncompleteBlendSrcUsage`, `InvalidBlendSrcWithOtherBindings`, `BlendSrcOutputTypeMismatch`, `BlendSrcNotOnStructMember`).
- **Espacios de dirección:** `InvalidUsage`, `InvalidType`, `Alignment`, `InitializerNotAllowed`, `InitializerType`, `StorageAddressSpaceWriteOnlyNotSupported`.
- **Entry points:** `Conflict` (puntos de entrada en conflicto), `UnexpectedEarlyDepthTest`, `InvalidInputAttributeInStage`, `ForbiddenStageOperations`, `InvalidGlobalUsage`.
- **Memoria:** `@coherent`/`@volatile` (`InvalidMemoryDecorationsAddressSpace`, `CoherentNotSupported`, `VolatileNotSupported`).

### OpenGL / OpenGL ES (glslang)

587 mensajes únicos en `glslang-errors.tsv`, sin clasificar: glslang no publica una lista de errores y hay que leer el código. El analizador modela de GL solo las 8 características y los 5 límites. No hay reglas de versión de GLSL por perfil, de precisión (`mediump`/`highp`) ni de extensiones.

### Metal

Sin lista oficial de errores. El analizador modela las mismas 8 características y un límite de recursos propio de Metal (más de 31 texturas, 16 samplers o 31 buffers, en `ResourceRules.cs:208`).

## 5. Relación con los fallos que vimos en FNA

| Fallo | Qué lo habría detectado | ¿Está en el analizador? |
|---|---|---|
| `SV_VertexID` en `fx_2_0` (6 shaders): "Invalid semantic" (E5013 de DXC/ShadowDusk) | El más parecido en FXC: `X4016 ERR_INVALID_SEMANTIC` | No |
| Texturas con `.Sample` (Bricks, Pool): SD0303 | `X3522 ERR_TEXTURE_OBJECTS_UNSUPPORTED` | No |
| Ocean: "Temp register r4 used uninitialized" (MojoShader) | No está en ninguna de estas listas; es un error del cargador de FNA | No |
| GL4: Bricks tras Ocean ("varying vTexCoord0…") | No es una regla de compilación: es estado del backend nativo | No |
| Mandelbulb colgado en la compilación | No es un error de validación | No |

## 6. Cómo está hecha la clasificación (y sus límites)

- **Cubierta:** solo si identifiqué una regla YS equivalente leyendo el código (`Rules/*.cs`, `GraphicsApi/*.cs`). La lista de equivalencias está en `classify.js` y la columna `regla_yotsuba` de los CSV.
- **Fuera de alcance, internas, sintaxis:** clasificadas con expresiones regulares sobre el identificador y el texto. Hay posibles errores de clasificación, sobre todo en las reglas de Vulkan que mencionan varias extensiones a la vez.
- **"No cubierta" significa que no vi una regla equivalente**, no que lo haya probado con un shader. Una regla parcial (por ejemplo, offsets de textura en BodyRules) puede figurar como no cubierta.
- Las reglas de Vulkan de `RuntimeSpirv` dependen de características del dispositivo; algunas solo se pueden avisar, no marcar como error (como hace el analizador con YS0711).
- DXC: solo tengo los nombres de regla, sin el mensaje. FXC: la tabla de Learn no incluye todos los errores reales de `fxc.exe`.
- Metal y glslang quedan sin cubrir: no encontré una lista oficial.

## 7. Archivos

| Archivo | Contenido |
|---|---|
| `vulkan-spirv-rules.csv` | las 572 reglas, con estado y regla YS equivalente |
| `vulkan-no-cubiertas.txt` | las 212 que aplican y faltan |
| `dxc-validation-rules.csv` / `dxc-no-cubiertas.txt` | 295 reglas / las 80 que faltan |
| `fxc-errors.csv` / `fxc-no-cubiertas.txt` | 477 códigos / los 115 que faltan |
| `naga-validation-errors.csv` / `naga-no-cubiertas.txt` | 293 mensajes / los 34 que faltan |
| `glslang-errors.tsv` | 587 mensajes de glslang, sin clasificar |
