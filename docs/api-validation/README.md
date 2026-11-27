# El analizador de YotsubaShader frente a los compiladores de cada API

Resumen de la revisión del analizador (`Core/Analyzers`, YotsubaShader 0.9.5) cruzada con las listas de errores de Vulkan, DXC, FXC y naga.
El detalle completo, con los casos concretos de cada API, está en [`ApiErrors/REPORT.md`](ApiErrors/REPORT.md).

## Cómo modela las APIs el analizador

Cada una de las 33 versiones de API (Vulkan, OpenGL, GLES, Direct3D, Metal, WebGPU) solo declara:

- **8 características:** `double`, `half`, enteros de 16 y 64 bits, compute, buffers de almacenamiento, texturas de almacenamiento y texturas de almacenamiento sin formato.
- **5 límites:** render targets, entradas de vértice, variables entre etapas, sets de descriptores y tamaño de workgroup.

No hay nada específico de Shader Model, versión de GLSL, precisión (GLSL ES), registros o instrucciones por perfil, ni de la sintaxis de texturas por perfil.

## Fuentes oficiales y su formato

Solo Vulkan publica un JSON oficial. En el resto hay que leer código fuente o una tabla web.

| API | Fuente | Formato |
|---|---|---|
| Vulkan | `validusage.json` | JSON oficial |
| Direct3D 12 (DXC) | código de `DxilValidation` | solo nombres de regla, sin mensaje |
| Direct3D 9/10/11 (FXC) | Microsoft Learn | tabla HTML con 477 códigos |
| WebGPU (naga) | `valid/*.rs` | código Rust |
| OpenGL/GLES (glslang) | `ParseHelper.cpp` | solo código, sin lista oficial |
| Metal | especificación MSL | no hay lista de errores |

## Lo que no cubre el analizador

| API | Reglas | Cubiertas | **No cubiertas** | Fuera de alcance |
|---|---|---|---|---|
| Vulkan / SPIR-V | 572 | 29 | **212** | 331 |
| Direct3D 12 (DXC) | 295 | 4 | **80** | 39 (más 172 internas del IR) |
| Direct3D 9/10/11 (FXC) | 477 | 8 | **115** | los 354 restantes son de sintaxis HLSL |
| WebGPU (naga) | 293 | 17 | **34** | 66 (más 176 internas del IR) |

"Fuera de alcance" son etapas que el CLI no genera: ray tracing, mesh, tessellation, geometry, cooperative matrix y tile shading.

Ejemplos de lo que falta en Vulkan: semántica de memoria de atómicas, enteros de entrada que deben ser `Flat`, decoración `Component`, tipos de imagen y tamaño de memoria compartida.

## Relación con los fallos de FNA

| Fallo | Qué lo habría detectado |
|---|---|
| `SV_VertexID` en `fx_2_0` (6 shaders) | El más parecido en FXC: `X4016 ERR_INVALID_SEMANTIC` |
| Texturas con `.Sample` en Bricks y Pool | `X3522 ERR_TEXTURE_OBJECTS_UNSUPPORTED` |
| Ocean: "Temp register r4 used uninitialized" | Nada de estas listas; es un error del cargador de FNA |
| GL4: Bricks tras Ocean | No es una regla de compilación: es estado del backend nativo |
| Mandelbulb colgado al compilar | No es un error de validación |

## Límites de este análisis

- La clasificación usa expresiones regulares más mi lectura del código; no probé cada regla con un shader.
- "No cubierta" significa que no vi una regla equivalente, así que una cobertura parcial puede figurar como ausente.
- Las 587 entradas de glslang quedaron sin clasificar, y de Metal no extraje nada.

## Archivos en `ApiErrors/`

| Archivo | Contenido |
|---|---|
| `REPORT.md` | informe completo con el detalle por API |
| `vulkan-spirv-rules.csv`, `vulkan-no-cubiertas.txt` | 572 reglas con estado / las 212 que faltan |
| `dxc-validation-rules.csv`, `dxc-no-cubiertas.txt` | 295 reglas / las 80 que faltan |
| `fxc-errors.csv`, `fxc-no-cubiertas.txt` | 477 códigos / los 115 que faltan |
| `naga-validation-errors.csv`, `naga-no-cubiertas.txt` | 293 mensajes / los 34 que faltan |
| `glslang-errors.tsv` | 587 mensajes de glslang, sin clasificar |

## Fuentes

- [Vulkan-Headers `validusage.json`](https://github.com/KhronosGroup/Vulkan-Headers/blob/main/registry/validusage.json)
- [HLSL errors and warnings, Microsoft Learn](https://learn.microsoft.com/en-us/windows/win32/direct3dhlsl/hlsl-errors-and-warnings)
- [DirectXShaderCompiler, `DxilValidation.cpp`](https://github.com/microsoft/DirectXShaderCompiler/blob/main/lib/DxilValidation/DxilValidation.cpp)
- [wgpu, naga `valid`](https://github.com/gfx-rs/wgpu/tree/trunk/naga/src/valid)
- [glslang `ParseHelper.cpp`](https://github.com/KhronosGroup/glslang/blob/main/glslang/MachineIndependent/ParseHelper.cpp)
