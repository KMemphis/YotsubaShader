# Yotsuba Engine: diseño de arquitectura, tecnología y salida al mercado

> Estado: **propuesta de diseño**. Nada de lo descrito en las secciones 4 a 10 está implementado.
> Los hechos sobre YotsubaShader de la sección 2 salen del código y del `README.md` del repositorio
> (revisados el 2026-11-09). No se usó ningún otro documento del repositorio.
> "Yotsuba Engine" es un nombre provisional; el nombre y el logo no están cubiertos por la licencia.

## 1. Tesis

Un motor tipo raylib de siguiente generación **construido alrededor del compilador de shaders**:

- raylib ganó por tres cosas: API pequeña y legible, cero fricción de instalación y una ABI de C que
  cualquier lenguaje puede enlazar.
- Lo que raylib no puede ofrecer es una **GPU moderna sin dolor**: compute de primera clase, un solo
  shader para Vulkan, Metal y WebGPU, y shaders escritos en el mismo lenguaje que el juego.
- YotsubaShader ya resuelve esa última pieza. El motor es el vehículo que la vuelve imprescindible.

Frase de producto: **"Escribe tu juego y tus shaders en el mismo lenguaje; ejecútalo en cualquier GPU."**

## 2. Punto de partida real (hechos del código)

Lo que existe hoy en este repositorio:

```csharp
// YotsubaShader.Language/Stage.cs: son TRES etapas (más None).
public enum Stage { Vertex, Fragment, Compute, None }
```

- **Authoring**: solo C# (Roslyn). Cinco vocabularios de atributos (HLSL, GLSL, MSL, SPIR-V, WGSL) sobre
  conceptos compartidos en `YotsubaShader.Concepts`.
- **Pipeline**: C# → análisis semántico → Yotsuba IR → SPIR-V 1.6.
- **Salidas**: SPIR-V, HLSL, GLSL, GLSL ES, MSL y C++ (el CLI expone `spirv`, `hlsl`, `glsl`, `gles`,
  `msl`, `cpp`). Las salidas no-SPIR-V pasan por `YotsubaShaderCross`, un port automático de SPIRV-Cross.
  **WGSL como salida está planeado, no existe.**
- **Reflexión** de shaders (`ShaderReflection`) incluida en `CompiledCode`.
- **Integración de build**: `compile` (artefactos) y `add` (genera `YotsubaShader.g.cs` con
  `ToSpirV()`, `ToHlsl(stage)`, `ToGlsl(stage)`, `ToGlslEs(stage)`, `ToMSL(stage)`).
- El README del IR indica que llamadas, operadores, retornos, locales y control de flujo estructurado
  **siguen expandiéndose**. El compilador es hoy la ruta crítica del motor.

Consecuencias directas para el motor:

1. "Todas las etapas de YotsubaShader" significa hoy **Vertex, Fragment y Compute**. El motor debe
   exponerlas completas desde el día uno (compute incluido) y tener un punto de extensión para etapas
   futuras (geometry, tessellation, mesh, ray tracing) que **no se exponga hasta que el compilador las emita**.
2. "Todos los lenguajes que YotsubaShader soporte" significa hoy **C#**. Un lenguaje nuevo de *authoring*
   es un frontend nuevo hacia la misma IR; el motor no debe depender de qué frontend produjo el shader.
3. WebGPU espera a la salida WGSL, que no existe. Vulkan (SPIR-V) y Metal (MSL) ya tienen salida hoy,
   así que las plataformas nativas no dependen de ese trabajo.

## 3. Principios

1. **El shader se compila en build, nunca en runtime.** El runtime del motor no incluye Roslyn ni .NET
   obligatorio. Recibe un *bundle* ya traducido.
2. **Núcleo con ABI de C estable; todo lo demás son bindings.** Un solo origen de verdad para la API.
3. **Dos niveles de API**: modo simple (estilo raylib) y modo explícito de GPU, sobre el mismo núcleo.
4. **Cada afirmación de soporte tiene una prueba.** "Funciona en X" significa "pasa la suite de imágenes
   de referencia en X". Nada de matrices de marketing sin test.
5. **Cero dependencias obligatorias** para el usuario final: un paquete, una línea para empezar.

## 4. Arquitectura

```text
Tu juego (C#, y luego otros lenguajes)
        │  bindings idiomáticos generados desde yotsuba.api.json
        ▼
┌──────────────────────────────────────────────────────────┐
│  yotsuba.h: ABI de C estable (versionada)                 │
├──────────────────────────────────────────────────────────┤
│  Módulos: Render2D · Render3D · Texto · Audio · Input ·   │
│           Assets · Matemática · Compute · (Física opc.)   │
├──────────────────────────────────────────────────────────┤
│  Capa de shaders: carga de bundles, reflexión, layouts    │
├──────────────────────────────────────────────────────────┤
│  RHI Yotsuba: Vulkan · Metal · WebGPU  (solo estos tres)  │
├──────────────────────────────────────────────────────────┤
│  Plataforma: ventana, entrada, archivos, hilos, audio     │
└──────────────────────────────────────────────────────────┘

En build (herramienta, no runtime):
C# shader → YotsubaShader.CLI → IR → SPIR-V (Vulkan) + MSL (Metal) + WGSL (WebGPU, pendiente)
                                   → reflexión → empaquetado .ysb
```

### 4.1 El bundle de shaders (`.ysb`)

Es la pieza que conecta el compilador con el motor. Por cada shader y etapa guarda todas las salidas
que el compilador ya produce, más la reflexión. El RHI elige la que su backend consume:

```csharp
// Propuesta (no implementada): el motor tiene exactamente tres backends.
enum RhiBackend { Vulkan, Metal, WebGpu }

static ShaderBlob Pick(RhiBackend backend, ShaderBundle b) => backend switch
{
    RhiBackend.Vulkan => b.SpirV,   // ya lo emite el compilador (SPIR-V 1.6)
    RhiBackend.Metal  => b.Msl,     // ToMSL(stage), vía YotsubaShaderCross
    RhiBackend.WebGpu => b.Wgsl,    // BLOQUEANTE: la salida WGSL aún no existe
    _ => throw new NotSupportedException()
};
```

HLSL, GLSL, GLSL ES, MSL y C++ siguen siendo salidas del compilador para quien las quiera, pero el
motor no las consume. Con tres backends el bundle es mínimo y desaparecen el paso `dxc`/DXIL y el
perfil GLES.

La reflexión valida en build que el `struct` de C# usado en CPU y el bloque que ve la GPU tienen el
mismo layout. Ese es el diferenciador técnico: **un solo tipo para CPU y GPU, verificado por el compilador.**

### 4.2 RHI propio y delgado

Un RHI de unas pocas docenas de funciones (dispositivo, cola, buffer, textura, sampler, pipeline de
render, pipeline de cómputo, bind group, comandos, presentación). Vulkan, Metal y WebGPU son APIs explícitas de pipelines, grupos de recursos y compute nativo, por lo que
la capa es pequeña. Alternativa: usar `wgpu-native` o Dawn (que ya corren sobre Vulkan, Metal y WebGPU)
como único backend y no escribir los tres; se pierde control directo a cambio de menos código.

### 4.3 ABI de C y generación de bindings

Un archivo `yotsuba.api.json` describe cada función, struct, enum y callback. De él se generan
`yotsuba.h`, los bindings por lenguaje y la documentación. Nadie edita un binding a mano salvo una capa
"idiomática" opcional encima.

### 4.4 Dos niveles de API

Propuesta de superficie en C# (no implementada):

```csharp
// Nivel 1: simple, estilo raylib.
using Yotsuba;

Window.Init(1280, 720, "Hola");
var tex = Texture.Load("player.png");

while (!Window.ShouldClose)
{
    Draw.Begin();
    Draw.Clear(Color.DarkBlue);
    Draw.Texture(tex, 100, 100);
    Draw.End();
}
```

```csharp
// Nivel 1 con shader propio: el tipo del shader es el material.
var mat = Material.Load<MyWaveShader>();   // MyWaveShader es C# compilado por YotsubaShader
mat.Set(nameof(MyWaveShader.Time), Time.Elapsed);

Draw.Begin();
Draw.UseMaterial(mat);
Draw.Texture(tex, 100, 100);
Draw.End();
```

```csharp
// Nivel 2: explícito. Cómputo y render comparten buffers sin copias.
var particles = Gpu.Buffer<Particle>(capacity: 1_000_000, usage: BufferUsage.Storage | BufferUsage.Vertex);
var sim = Gpu.ComputePipeline<ParticleSim>();      // etapa Compute
var draw = Gpu.RenderPipeline<ParticleDraw>();     // etapas Vertex + Fragment

using var frame = Gpu.BeginFrame();
frame.Dispatch(sim, groups: (1_000_000 / 64, 1, 1), bind: particles);
frame.Pass(target: frame.Backbuffer, draw, bind: particles);
```

El shader de la primera mitad ya es compilable hoy con la forma que muestra el README: una clase con
métodos marcados con `[ShaderStage(Stage.Vertex)]`, `[ShaderStage(Stage.Fragment)]` y
`[ShaderStage(Stage.Compute)]` (este último con `[numthreads(...)]` en el ejemplo del repositorio).

## 5. Tecnologías elegidas

- **Núcleo: Rust**, compilado a `cdylib` y `staticlib` con ABI de C (`cbindgen` para el encabezado).
  Razones: sin GC ni runtime obligatorio, WebAssembly de primera clase, iOS y Android sólidos, seguridad de
  memoria en un motor que corre código de terceros y herramienta de build que no se interpone.
  Alternativa válida: C++20 con `extern "C"`. Se elige Rust salvo que las consolas pesen más que la
  seguridad (ver riesgos).
- **Alternativa descartada: núcleo en C# NativeAOT.** Comparte código con el compilador y es tentador
  para una sola persona, pero amarra a todos los lenguajes a un runtime .NET, y Web y consolas son su flanco débil.
  El lado C# del producto vive en los bindings y en la herramienta de build, no en el núcleo.
- **Plataforma**: SDL3 (ventana, entrada, gamepad, archivos, audio de dispositivo). Es el estándar de facto,
  con puertos a escritorio, móvil, Web (Emscripten) y consolas por vías de socios.
- **Gráficos**: RHI propio con **solo tres backends: Vulkan, Metal y WebGPU**. Sin D3D12 ni GLES/WebGL.
  Vulkan cubre Windows, Linux y Android; Metal cubre macOS, iOS, tvOS y visionOS de forma nativa (sin
  MoltenVK); WebGPU cubre el navegador. Dispositivos sin ninguna de las tres APIs quedan fuera de soporte.
- **Audio**: mezclador propio mínimo sobre la salida de SDL3 (cargadores WAV, OGG, MP3, FLAC).
- **Fuente, imagen**: `stb`-equivalentes en Rust, atlas SDF para texto.
- **Build de shaders**: `YotsubaShader.CLI` invocado desde un target MSBuild en proyectos C# y desde un
  comando `yotsuba pack` independiente en el resto de lenguajes.
- **Distribución**: NuGet para C#, y binarios precompilados por plataforma para el resto
  (`yotsuba-vX.Y.Z-<plataforma>.zip` con `lib` + `yotsuba.h`).

## 6. Cobertura de etapas y plataformas

Estado objetivo expresado como código para que sea comprobable por una prueba, no un enunciado:

```csharp
// Propuesta: el motor publica sus capacidades en runtime y la suite las verifica.
public readonly record struct Capabilities(
    bool Vertex,        // = true en todas las plataformas
    bool Fragment,      // = true en todas las plataformas
    bool Compute,       // = true: Vulkan, Metal y WebGPU lo soportan
    bool Geometry,      // = false: el compilador no la emite
    bool Tessellation,  // = false: idem
    bool MeshShading,   // = false: idem
    bool RayTracing);   // = false: idem

// Plataformas, en el orden en que se prometen:
// Fase 1: Windows, Linux (Vulkan); macOS (Metal)
// Fase 2: Android (Vulkan), iOS (Metal), Web (WebGPU, requiere salida WGSL)
// Fase 3: Steam Deck, VR (Quest vía Android/Vulkan), visionOS (Metal)
// Fase 4: consolas con Vulkan (p. ej. Switch), vía socios y bajo NDA. Xbox y PlayStation quedan
//         fuera mientras el motor sea solo Vulkan/Metal/WebGPU.
```

La Web depende por completo de la salida WGSL del compilador; hasta que exista, el motor corre en
Vulkan y Metal (todas las plataformas nativas). Compute en Web llega junto con WebGPU, sin camino de degradación a WebGL.

## 7. Lenguajes

- **Authoring de shaders**: C# hoy. Cada frontend nuevo (otro lenguaje de authoring) es trabajo del
  compilador, no del motor.
- **Uso del motor**: C# primero, luego los lenguajes por popularidad de comunidad de juegos
  indie: C/C++ (directo con `yotsuba.h`), Rust, Python, Go, Zig, TypeScript/JS (Web y Node), Java/Kotlin
  (Android), Swift (Apple), Lua (scripting).
- **Regla**: un binding se publica cuando pasa los mismos tests de conformidad que el de C#
  (abre ventana, dibuja un triángulo con un shader del bundle, ejecuta un compute, compara imagen).
- **Shaders desde otros lenguajes**: los consumen como bundles precompilados (`.ysb`) construidos con el CLI.
  Es una limitación real hoy: el compilador necesita .NET en la máquina de build.

## 8. Apuesta de mercado

Esta sección son **hipótesis a validar**, no datos medidos.

### 8.1 Quién es el primer usuario

Desarrolladores de C# que hoy luchan con shaders: usuarios de MonoGame/FNA (efectos en HLSL con
herramienta de compilación frágil y solo Windows en la práctica), de Godot C# (lenguaje de shader propio)
y de Unity (ShaderLab/HLSL aparte del código). El README ya usa tipos de `Microsoft.Xna.Framework`, lo
que indica afinidad con ese público.

### 8.2 Cuña (wedge)

"Mismo shader, todas las GPU, en C#". Es verificable y demostrable en un video de 60 segundos:
un único proyecto C# corriendo en Windows, macOS, Linux, iPhone, Android y navegador.

### 8.3 Por qué ganar contra raylib, MonoGame, Godot y Bevy

- **raylib**: simple pero con OpenGL como centro y compute casi ausente. Se compite en ergonomía moderna.
- **MonoGame/FNA**: mismo idioma, pero shaders atados a HLSL/MGFX. Se compite con un camino de migración
  (puente de compatibilidad con la API XNA para efectos y proyectos existentes).
- **Godot/Unity**: son editores. Esto es una librería: sin editor, sin proyecto impuesto, sin runtime obligatorio.
- **Bevy/wgpu**: potentes pero Rust-only para el usuario final. Aquí el lenguaje lo elige el usuario.

### 8.4 Modelo de negocio

- Núcleo abierto bajo MS-PL (el repositorio ya lo usa). La licencia no otorga la marca, lo que protege el
  nombre y permite certificar "compatible con Yotsuba".
- Ingresos: patrocinios (GitHub Sponsors ya enlazado), **kit de consolas** y portado como servicio
  (las partes bajo NDA viven en módulos cerrados separados), soporte y licencias para estudios, servicio
  de build/CI de shaders y assets, y un *marketplace* de materiales y efectos en bundle.
- Lo que no se cobra: el núcleo, la API simple y cualquier plataforma de la Fase 1 a 3.

### 8.5 Distribución y comunidad

1. Demo de la cuña y repositorio de ejemplos pequeños (un archivo cada uno, estilo raylib).
2. Playground web (ya existe uno para el compilador) ampliado para ejecutar ejemplos del motor en el navegador.
3. Jams de 48 h con premios para obligar a la comunidad a producir juegos reales en plataformas distintas.
4. Documentación bilingüe (inglés y español), alineada con el sitio actual.
5. Programa de "puertos oficiales" para atraer a mantenedores de bindings con sello de conformidad.

### 8.6 Métricas de éxito (por fase)

- Fase 1: tiempo desde `dotnet add package` hasta ventana con shader < 5 minutos; la suite de imágenes
  de referencia pasa en las 3 plataformas de escritorio.
- Fase 2: un juego publicado de terceros en tienda móvil o Web.
- Fase 3+: bindings mantenidos por la comunidad y primeros ingresos de consolas.

## 9. Hoja de ruta con dependencias del compilador

```csharp
// Cada hito del motor está bloqueado por una capacidad del compilador (gate):
var roadmap = new[]
{
    ("M0 Triángulo en Vulkan y Metal desde un shader C#",
        gate: "SPIR-V y MSL del ejemplo actual"),
    ("M1 Render2D con materiales propios",
        gate: "uniformes, texturas y samplers en IR (existen conceptos; confirmar soporte end-to-end)"),
    ("M2 Compute de primera clase",
        gate: "buffers de almacenamiento, barreras y control de flujo en el IR"),
    ("M3 Render3D (PBR básico)",
        gate: "operadores, llamadas, bucles y condicionales estables en el frontend"),
    ("M4 Web con WebGPU",
        gate: "salida WGSL en el compilador"),
    ("M5 Bindings 2..N",
        gate: "yotsuba.api.json estable y suite de conformidad"),
};
```

## 10. Riesgos

1. **Madurez del compilador.** Si el control de flujo y las llamadas no están completos, M2 y M3 se
   retrasan. Mitigar con un conjunto de shaders de referencia que crece con el compilador.
2. **WGSL es camino crítico.** Sin la salida WGSL no hay Web. Mitigar priorizándola justo después de M1,
   o usando temporalmente un traductor externo SPIR-V→WGSL (p. ej. Tint/naga) solo en el empaquetado.
   Metal y Vulkan no dependen de esto: ya salen de `ToMSL` y del SPIR-V actual.
3. **Rust y consolas.** Los kits oficiales de consola esperan C/C++. Con solo Vulkan/Metal/WebGPU, Xbox y
   PlayStation quedan fuera de alcance; el soporte de consola se limita a plataformas con Vulkan.
4. **Alcance.** "Todos los lenguajes y todas las plataformas" es una meta, no un hito. Sin la regla de la
   sección 3.4 (cada soporte lo prueba un test) el proyecto promete más de lo que puede sostener.
5. **Dependencia de una sola persona.** El generador de bindings y la suite de conformidad son lo que
   permite que otros mantengan lenguajes sin bloquear el núcleo.
6. **Licencia MS-PL.** Es de copyleft por archivo para distribución en código fuente. Confirmar que encaja
   con el modelo de módulos cerrados para consolas antes de prometerlo.

## 11. Decisiones que necesito de ti primero

1. Núcleo en **Rust** (recomendado) o **C++20**.
2. Si el primer entregable es **Render2D simple** (recomendado) o ya **3D**.
3. Si WGSL se obtiene con un **traductor externo temporal** o esperando la salida nativa del compilador.
4. Si el puente de compatibilidad con **MonoGame/XNA** entra en la Fase 1 (acelera adopción, añade alcance).
