# Game Engine construido sobre YotsubaShader: arquitectura, tecnologías y apuesta de mercado

**Fecha: 9 de noviembre de 2026. Documento de diseño y estrategia, interno.** Nombre de trabajo: *Yotsuba Engine*. El nombre final es una decisión pendiente del creador (ver §12).

> **Premisa de este documento.** Por petición del creador, se asume que YotsubaShader está **terminado**: compila un subconjunto documentado de C# a Yotsuba IR y de ahí a SPIR-V, y Cross emite GLSL, HLSL, MSL y WGSL con reflexión. Esto **no** describe el estado actual del repositorio (`MARKET_STUDY_2026-10-23.md` §4 documenta que hoy hay statements sin implementar y una ruta integral en ensamblaje). Todo lo que sigue es **diseño propuesto**; nada está implementado. Las cifras de negocio son hipótesis de planificación, no datos observados.

## 1. La tesis en una frase

**Un motor donde el código del juego y el código de la GPU son el mismo C#, comparten los mismos tipos y se depuran con las mismas herramientas.**

Los motores actuales parten el mundo en dos: un lenguaje de gameplay y otro de shaders, con un contrato de datos mantenido a mano entre ambos (layouts, bindings, nombres). YotsubaShader permite que el motor se construya sin esa frontera. Es la única ventaja estructural que ningún competidor puede copiar rápido, porque exige tener el compilador. Todo lo demás (ECS, render graph, editor) es ingeniería conocida; la tesis solo se sostiene si el motor la **explota hasta el fondo** en lugar de tratar el shader como un añadido.

Lo que el motor **no** intenta ser: otro Unity o Godot generalista. El estudio de mercado ya concluyó que competir por portabilidad o por amplitud es una posición débil.

## 2. Qué exige el motor a YotsubaShader (contratos)

Como la premisa es «terminado», conviene escribir qué significa, para que sea verificable. El motor depende de estos seis contratos; si alguno falla, el diseño cambia.

| # | Contrato | Por qué lo necesita el motor |
|---|---|---|
| C1 | Subconjunto C# documentado, con diagnósticos con archivo y línea | Autoría de materiales y compute sin sorpresas |
| C2 | Los mismos structs `unmanaged` valen en CPU y GPU, con layout comprobado en compilación | ECS y GPU-driven rendering sin duplicar datos |
| C3 | Cualquier función del subconjunto se ejecuta en .NET (modo CPU) | Breakpoints, tests unitarios y servidor dedicado |
| C4 | Reflexión completa: bindings, offsets, workgroup size | Generar el código de enlace y el render graph automáticamente |
| C5 | Compilación incremental en milisegundos y módulos reutilizables (NuGet) | Hot reload y ecosistema de paquetes de shaders |
| C6 | Mapa de fuente SPIR-V → C# | Depurar y perfilar en herramientas de plataforma mostrando C# |

## 3. Arquitectura

### 3.1 Vista general

```text
┌──────────────────────────── Editor (opcional, proceso aparte) ────────────────────────────┐
│  Escena · Inspector · Visor de shaders (CPU/GPU lado a lado) · Profiler · Depurador       │
└───────────────────────────────────────────────┬────────────────────────────────────────────┘
                                                │ protocolo de comandos (pipe / socket)
┌───────────────────────────────────────────────┴────────────────────────────────────────────┐
│ Juego (C#)                                                                                  │
│   Gameplay · Sistemas ECS · Scripts           ←  mismos structs  →   Kernels y materiales  │
├─────────────────────────────────────────────────────────────────────────────────────────────┤
│ Núcleo: ECS de arquetipos · Scheduler de jobs · Asset DB · Reflexión · Serialización         │
├─────────────────────────────────────────────────────────────────────────────────────────────┤
│ Render: Render Graph · Materiales · GPU-driven · Compute                                     │
├─────────────────────────────────────────────────────────────────────────────────────────────┤
│ RHI (capa fina propia): Vulkan · D3D12 · Metal · WebGPU                                      │
├─────────────────────────────────────────────────────────────────────────────────────────────┤
│ Plataforma: ventana/entrada · audio · físicas · red · archivos                               │
└─────────────────────────────────────────────────────────────────────────────────────────────┘
                 ▲
                 │ en build: YotsubaShader (C# → IR → SPIR-V → Cross → MSL/HLSL/GLSL/WGSL)
```

### 3.2 Principio rector: un solo dato, dos ejecuciones

El componente de ECS **es** el struct que lee la GPU. No hay paso de «subida» con conversión: el almacenamiento por arquetipo (SoA por columnas) se copia en bloque a un buffer de GPU.

```csharp
using System.Numerics;
using YotsubaShader.Language;
using YotsubaShader.HlslSyntax;

// API propuesta del motor (no existe todavía). [GpuLayout] pide a Yotsuba
// que verifique en compilación que el layout CPU y GPU coinciden (contrato C2).
[Component, GpuLayout(Packing.Std430)]
public struct Transform
{
    public Vector3 Position;
    public float Scale;
    public Quaternion Rotation;
}

[Component, GpuLayout(Packing.Std430)]
public struct Velocity
{
    public Vector3 Value;
    public float Drag;
}

public static class Movement
{
    // Un kernel: se ejecuta en GPU como compute y, con el mismo cuerpo,
    // en CPU para tests, depuración y servidor dedicado (contrato C3).
    [Kernel(Stage.Compute, WorkGroup = 64)]
    public static void Integrate(
        [GlobalInvocationId] uint i,
        [Column] Span<Transform> transforms,
        [Column] ReadOnlySpan<Velocity> velocities,
        [Uniform] FrameData frame)
    {
        transforms[(int)i].Position += velocities[(int)i].Value * frame.DeltaTime;
        velocities[(int)i].Value *= 1f - velocities[(int)i].Drag * frame.DeltaTime;
    }
}
```

El planificador elige dónde correr cada sistema (CPU en hilos, o GPU) con un atributo y una política, sin reescribir:

```csharp
world.AddSystem(Movement.Integrate, Execute.Auto); // GPU si hay millones de entidades, CPU si hay pocas
```

Este es el efecto «guau» medible que exige el estudio de mercado (§5): una simulación de millones de entidades con **un** código fuente, que además se depura con breakpoints.

### 3.3 Capas y responsabilidades

| Capa | Responsabilidad | Decisión clave |
|---|---|---|
| Núcleo | ECS, jobs, asset DB, reflexión, serialización | ECS de arquetipos generado por source generators; cero reflexión en tiempo de ejecución para soportar NativeAOT |
| Render | Render graph, materiales, culling, GPU-driven | El grafo se declara en C# y los bindings se derivan de la reflexión de Yotsuba (C4) |
| RHI | Abstracción mínima de la API gráfica | Fina y propia; el 90% del trabajo difícil ya lo hace el compilador de shaders |
| Plataforma | Ventana, entrada, audio, físicas, red | Se compra/adopta lo que ya es bueno (§4) |
| Editor | Herramientas visuales | Proceso separado del juego, hablan por protocolo; el juego nunca depende del editor |
| Herramientas de build | Asset cooker, empaquetado | Reproducible y con caché por hash de contenido |

### 3.4 Render graph y materiales

El grafo de render es código C# con nodos tipados. El shader declara qué lee y escribe; el motor deduce barreras, alias de memoria y orden.

```csharp
public sealed class ForwardPlus : IRenderPipeline
{
    public void Build(RenderGraph g, in Camera camera)
    {
        var depth  = g.CreateTexture(Format.D32, Size.Screen);
        var lights = g.Compute<LightCulling>(camera);          // Kernel con bindings reflejados
        var color  = g.Raster<OpaquePass>(depth, lights);      // Material: vertex+fragment en C#
        g.Raster<Tonemap>(color, g.Backbuffer);
    }
}
```

Un material es una clase con stages, igual que en el README de YotsubaShader. El motor añade solo una capa: el material declara parámetros editables en el inspector con atributos, y la reflexión genera el panel.

```csharp
public class PbrMaterial
{
    [Parameter(Range = 0f..1f)] public float Roughness;
    [Parameter] public Texture2D Albedo;

    [ShaderStage(Stage.Fragment)]
    [return: SV_Target(0)]
    public Vector4 Fragment(SurfaceInput input) => Pbr.Shade(input, Albedo, Roughness);
}
```

### 3.5 Hot reload como característica de primera clase

Dos niveles, ambos posibles solo porque el compilador es el mismo para código y shaders (contrato C5):

1. **Código de juego:** recompilación incremental con Roslyn y recarga del ensamblado en el editor (CoreCLR). El estado del mundo se conserva por serialización de componentes.
2. **Shaders:** al cambiar un `.cs` de material, Yotsuba recompila solo ese módulo y el motor reemplaza el pipeline sin reiniciar. Objetivo de diseño a medir: menos de 500 ms desde guardar hasta ver el cambio en un material típico. Es un objetivo, no una medición.

### 3.6 Depuración: la ventaja que debe verse en el editor

El Visor de shaders ejecuta el **mismo** kernel en CPU y en GPU sobre el mismo buffer y muestra la diferencia. Con ello:

- breakpoints en C# sobre un píxel o un hilo concreto (modo CPU, contrato C3);
- comparación automática CPU/GPU en tests de CI para detectar divergencias de precisión o de driver;
- perfilado en herramientas de plataforma (RenderDoc, Xcode, PIX) con nombres C# gracias al mapa de fuente (contrato C6).

Límite honesto: texturas con derivadas, atomics y subgroups no son triviales en CPU; el modo CPU debe documentar qué subconjunto reproduce con exactitud. Es el mismo aviso que ya hace el estudio de mercado.

### 3.7 Modelo de contenido y distribución

- **Asset DB** direccionada por hash de contenido: caché de build compartible entre máquinas y CI.
- **Escenas y prefabs** como texto estable (diff-friendly en Git), compilables a binario para release.
- **Paquetes por NuGet**, incluidos paquetes de **shaders y kernels**. Es una capacidad que Unity y Godot no ofrecen con la misma limpieza (a validar contra el contrato C5), y es la base del ecosistema comercial de §8.

## 4. Tecnologías elegidas

Criterio: adoptar lo maduro y de licencia permisiva en todo lo que **no** sea la tesis; escribir propio solo donde la tesis lo exija.

| Área | Elección | Motivo | Riesgo y alternativa |
|---|---|---|---|
| Lenguaje y runtime | C# sobre .NET (LTS vigente al arrancar), CoreCLR en el editor, **NativeAOT** en release | Mismo lenguaje que Yotsuba; AOT da arranque rápido y es lo que exigen iOS y consolas | NativeAOT restringe reflexión: por eso source generators en todo el núcleo |
| Compilación de shaders | **YotsubaShader + Cross** | Es la tesis | Riesgo de dependencia de un solo proyecto: ver §10 |
| API gráfica primaria | **Vulkan** (SPIR-V nativo), luego **Metal** (MSL vía Cross) y **D3D12** (HLSL/DXIL) | SPIR-V es la salida de primera clase de Yotsuba | WebGPU como cuarta API cuando el emisor WGSL esté validado |
| Bindings nativos | Generados; **Silk.NET** como referencia o base | Evita escribir a mano miles de firmas | Silk.NET es dependencia comunitaria; el RHI aísla el cambio |
| Ventana, entrada, gamepads | **SDL3** | Madura, multiplataforma, licencia zlib | Alternativa GLFW; SDL3 gana por gamepads y móviles |
| Físicas | **Jolt Physics** (C++, MIT) vía binding | Calidad probada en juegos comerciales | Integración determinista y en red por validar; alternativa Bepu (C# puro) para quien quiera cero nativo |
| Audio | **miniaudio** + capa propia de mezcla y 3D; **FMOD/Wwise** como integración opcional | Cero dependencias para empezar; estudios grandes traen su middleware | Audio espacial avanzado queda para una fase posterior |
| UI en juego | **Retained mode propio** escrito en C# con kernels Yotsuba para el render | Demuestra la tesis y evita otra dependencia | Coste de desarrollo alto; alternativa inicial Dear ImGui para herramientas |
| UI del editor | **Avalonia** | C# puro, multiplataforma, XAML | Alternativa Dear ImGui (rápido pero menos pulido para producto final) |
| Imágenes y modelos | **KTX2/Basis** para texturas, **glTF 2.0** como formato de intercambio | Estándares abiertos | Importadores FBX vía librería externa, bajo demanda |
| Red | **Transporte propio sobre UDP** con snapshots y rollback | El ECS de datos planos facilita snapshots baratos | Alternativa: integrar un transporte existente; decidir tras prototipo |
| Scripting | **C# normal**, sin lenguaje de scripting aparte | Es la propuesta; elimina un segundo lenguaje | Ver §10, riesgo de barrera de entrada para no programadores |
| Build y CI | `dotnet`, caché por hash, runners macOS/Linux/Windows | Reproducibilidad es un problema de adopción que cita el estudio | — |
| Control de versiones de assets | Git + LFS o Perforce, sin servidor propio | No reinventar | — |

**Plataformas y orden de lanzamiento:** Windows, Linux y macOS (escritorio) → iOS y Android → Web (WebGPU) → consolas por acuerdo de plataforma y solo con cliente pagando. No se prometen consolas sin acceso a los kits de desarrollo, igual que el plan de MonoGame.

### 4.1 Por qué **no** usar un motor existente como base

| Alternativa | Por qué no |
|---|---|
| Construir sobre MonoGame o Stride | Cada uno tiene su propio pipeline de shaders; hay que romperlo para que la tesis llegue hasta el núcleo. Siguen siendo **canales de entrada** (§7), no base |
| Construir sobre Godot | Su contrato de materiales y su lenguaje de shaders son nativos; SPIR-V no los sustituye (estudio de mercado §6) |
| Escribir todo en C++/Rust y exponer C# | Se pierde la coherencia de una sola cadena y se duplica el esfuerzo del runtime |

## 5. Estructura del repositorio propuesta

El motor es un producto distinto de YotsubaShader. Va en su propio repositorio y consume Yotsuba como paquete versionado, igual que lo consumiría un cliente. Así se prueba el contrato C1–C6 con un usuario exigente real: el propio motor.

```text
YotsubaEngine/
  src/
    Engine.Core/          ECS, jobs, assets, serialización
    Engine.Rendering/     render graph, materiales, GPU-driven
    Engine.Rhi/           abstracción gráfica + backends Vulkan/D3D12/Metal/WebGPU
    Engine.Platform/      SDL3, audio, entrada
    Engine.Physics/       Jolt + integración con ECS
    Engine.Net/           transporte, snapshots, rollback
    Engine.Generators/    source generators (componentes, reflexión, bindings)
    Editor/               Avalonia, protocolo de comandos
    Cooker/               herramientas de build de contenido
  packages/               paquetes oficiales de shaders y kernels (NuGet)
  samples/                un sample por plataforma (exigencia de calidad)
  tests/                  conformidad CPU↔GPU, golden images, rendimiento
```

## 6. Producto y apuesta de mercado

### 6.1 Dónde ganar (y dónde no)

El estudio de mercado del 23-10-2026 concluye que la demanda de «C# para shaders» **no está demostrada** y que un TAM defendible es no verificable. Este documento no lo contradice: la apuesta de un motor es **más grande y más arriesgada** que la de un compilador, y por eso el plan de §9 la condiciona a hitos.

**No competir en:** generalismo, editor «todo en uno» para principiantes, ni amplitud de plataformas desde el día uno.

**Competir en un nicho acotado donde la tesis se ve y duele:**

| Cuña | Por qué la tesis gana | Competencia directa |
|---|---|---|
| Juegos y simulaciones **con mucha computación en GPU** (partículas, fluidos, voxels, procedural, enjambres, RTS masivos) | Un solo código en CPU y GPU, depurable, con millones de entidades | Unity DOTS + compute (dos lenguajes), Godot (compute poco integrado) |
| **Estudios y equipos .NET** que ya sufren su pipeline | Sin frontera entre lenguajes; builds reproducibles | Stride, MonoGame con toolchain propio |
| **Herramientas, visualización y tiempo real no-juego** (simulación, gemelos digitales, viz científica) | Compute y render comparten tipos con la aplicación | ComputeSharp/ILGPU (sin render), Unity (pesado) |
| Servidor dedicado con lógica compartida | El kernel corre en CPU sin cambios (C3) | Equipos que mantienen dos bases de código |

Se elige **la primera cuña** como bandera: es la que hace evidente y espectacular la tesis, y la más fácil de mostrar en un vídeo de treinta segundos. Las otras se atienden por pull, no por empuje.

### 6.2 Propuesta de valor por comprador

| Comprador | Mensaje |
|---|---|
| Programador de gameplay C# | «Escribe el sistema una vez; elige si corre en CPU o GPU con una línea» |
| Rendering engineer | «Tu render graph, tus kernels y tus structs viven en un solo proyecto; el compilador comprueba el contrato» |
| Director técnico | «Un lenguaje, un pipeline, un build reproducible; sin impuesto por juego vendido» |
| Equipo pequeño/indie | «Deja de mantener tres lenguajes y un archivo de bindings» |

### 6.3 Modelo de negocio

Principio: **el motor es abierto; se cobra por lo que ahorra tiempo a equipos, no por permiso de uso.** Sin regalías por juego ni cuota por instalación. Es una decisión de confianza: el sector castiga duramente los cambios de términos retroactivos, y el estudio de mercado recomendó no elegir una licencia irreversible antes de validar. Aquí se propone, condicionado a esa validación, el esquema **open-core**.

| Capa | Qué incluye | Licencia propuesta | Ingreso |
|---|---|---|---|
| Motor y compilador | Núcleo, render, editor básico, kernels | Código abierto permisivo (MIT o Apache-2.0; decisión del creador) | 0 directo; es el canal de adquisición |
| **Studio** (suscripción por equipo) | Visor CPU/GPU, profiler avanzado, caché de build en la nube, panel de CI, soporte con respuesta acotada | Propietaria, por equipo | 100/mes por equipo (hipótesis) |
| **Partner** | Mantenimiento acotado de versión y targets acordados, acceso al código bajo contrato | Contrato | 250–400/mes por organización (hipótesis) |
| **Console Kit** | Backends de consola, solo con acuerdo de plataforma y cliente que financia | Contrato + NDA de plataforma | Proyecto + mantenimiento |
| **Mercado de paquetes** | Paquetes de shaders, kernels y herramientas por NuGet, con firma y reseñas | Comisión baja (hipótesis 10–15%) | Se activa solo con masa crítica |
| Consultoría de integración | Pilotos pagados con estudios | Contrato | Caja inicial; no escala |

Los importes coinciden con las hipótesis del estudio de mercado (Studio 100/mes, Partner 250/mes). Siguen siendo **precios a probar**, no datos de mercado.

### 6.4 Posicionamiento y mensaje

- **Frase:** «El motor donde CPU y GPU hablan el mismo C#.»
- **Prueba:** un vídeo corto de una simulación masiva depurada con breakpoint y la misma lógica corriendo en GPU.
- **Anti-mensaje:** no decir «mejor que Unity» ni «para todos». Decir «para quien ya piensa en datos y en la GPU».
- **Honestidad técnica visible:** una página pública «qué reproduce exactamente el modo CPU» y «qué no soportamos». La confianza se gana mostrando límites; el estudio de mercado ya señaló que prometer C# general crea expectativas insostenibles.

### 6.5 Distribución y canales

1. **Contenido técnico:** artículos y vídeos de una técnica por semana, cada uno con un repositorio ejecutable.
2. **Game jams con tema técnico:** «jam de simulación masiva»; el motor ofrece plantillas y premios modestos. Mide adopción real, no estrellas.
3. **Un juego bandera propio o de socio:** sin un título público y sólido, el motor es una promesa. Es la inversión de marketing más valiosa del plan.
4. **Integración con MonoGame y Stride** como rampa: los kernels y el paquete de shaders funcionan también como biblioteca en esos motores (ver `MONOGAME_CLIENT_STRATEGY_2026-10-23.md`). Es el puente entre el negocio del compilador y el del motor.
5. **Comunidad .NET:** conferencias y podcasts del ecosistema, no de «game dev» generalista.
6. **Estudios piloto pagados:** 3 a 5 equipos que aporten requisitos reales, con descuento a cambio de caso de éxito autorizado.

## 7. Estrategia de lanzamiento por fases con puertas

Cada fase tiene una **puerta**: si no se cumple, no se avanza. Es la protección contra gastar años en un motor que nadie pidió.

| Fase | Ventana (relativa) | Entregable | Puerta para continuar |
|---|---|---|---|
| 0. Contratos | 1 a 2 meses | Verificar C1–C6 con un spike: ECS + un kernel compartido CPU/GPU + un material | El spike funciona en dos APIs y el modo CPU coincide con la GPU en un corpus mínimo |
| 1. Núcleo y demo | 4 a 6 meses | Render graph, ECS, Vulkan y Metal, hot reload, demo de 1 millón de entidades | Un tercero reproduce la demo desde cero y el vídeo se entiende sin explicación |
| 2. Beta privada | 3 a 4 meses | Editor mínimo, 3 a 5 estudios piloto, Windows/macOS/Linux | Al menos un piloto avanza hacia producción propia y otro acepta pagar |
| 3. Beta pública | 3 a 6 meses | Paquete instalable, documentación con ejemplos C#, jam | Uso activo sostenido (equipos que compilan repetidamente) y soporte dentro de capacidad |
| 4. 1.0 y Studio | 6 meses | Suscripción Studio, Partner, móviles | Ingresos recurrentes que cubran el coste de soporte |
| 5. Plataformas | Bajo demanda | Web (WebGPU), consolas con cliente pagando | Contrato firmado antes de invertir |

Los tiempos son ventanas de gestión, no estimaciones verificadas. Un equipo de una persona tardaría mucho más; ver §10.

## 8. Qué mide el éxito

Se registran **uso**, no atención: estrellas y descargas no son clientes (lección del estudio de mercado).

| Métrica | Cómo se mide | Umbral orientativo (hipótesis) |
|---|---|---|
| Equipos activos | Proyectos que compilan semanalmente, deduplicados | 50 a 100 al final de la beta pública |
| Tiempo al primer render | Medido en instalación limpia | menos de 15 minutos |
| Ahorro en integración | Tarea real frente a baseline HLSL/Slang | al menos 30% menos tiempo (criterio del estudio de mercado) |
| Conversión a pago | Equipos Studio o Partner sobre equipos activos | 5 a 10% |
| Retención a 6 meses | Cohortes, no churn agregado | por encima de 70% |
| Coste de soporte | Horas por cliente al mes | por debajo del ahorro que justifica el precio |
| Juegos publicados | Títulos en tienda | al menos 3 en 12 meses tras la beta |

## 9. Cómo se financia el camino

El plan de negocio no debe depender de que el motor genere ingresos antes de tener producto. Orden de preferencia:

1. **Los ingresos del compilador** (pilotos y licencias de YotsubaShader) financian la fase 0 y 1, como ya propone el estudio de mercado.
2. **Pilotos de estudio** pagados en fase 2.
3. **Patrocinio de infraestructura** (fundaciones, empresas con interés en C# y GPU).
4. **Suscripción Studio** a partir de la fase 4.
5. Capital externo **solo** si existe tracción medida; no antes.

Un motor completo no se financia con 400/mes. La ruta realista es un equipo pequeño con alcance estrictamente acotado por las puertas de §7.

## 10. Riesgos principales

| Riesgo | Nivel | Mitigación |
|---|---|---|
| **La premisa «Yotsuba terminado» no se cumple** o llega tarde | Muy alto | La fase 0 existe para comprobarlo antes de invertir; los contratos C1–C6 son el criterio |
| Alcance de un motor completo para una persona | Muy alto | Núcleo mínimo; adoptar SDL3, Jolt, miniaudio; nicho acotado; no editor «todo en uno» |
| Falta de demanda del nicho | Alto | Puertas de mercado por fase; pilotos pagados antes de ampliar |
| Barrera de entrada de C# para artistas y diseñadores | Alto | Editor visual de materiales que **genera C#** legible; no un segundo lenguaje |
| Rendimiento de NativeAOT/GC en tiempo real | Medio | Datos `unmanaged`, sin asignaciones en el bucle caliente, pruebas de rendimiento en CI |
| Equivalencia CPU/GPU no exacta | Medio | Documentar el subconjunto exacto; comparación automática en CI |
| Dependencia de Silk.NET/Cross/terceros | Medio | RHI y capas de aislamiento; avisos de licencia y versiones fijadas |
| Confianza por cambio de licencia | Alto si ocurre | Licencia abierta desde el inicio y compromiso público de no cambiarla retroactivamente |
| Consolas inaccesibles | Medio | Solo bajo contrato con plataforma y cliente que financia |
| Dependencia de un único autor | Alto | Releases fijables, documentación técnica, derechos de continuidad (mismo criterio del plan MonoGame) |

## 11. Qué se debe decidir y qué no se decide aquí

Decisiones que corresponden al creador (AGENTS.md: el desarrollador es la autoridad final):

1. **Licencia del motor** (MIT vs Apache-2.0) y política de contribuciones.
2. **Relación con Yotsuba Framework.** La memoria del proyecto registra que Framework (framework de juegos con plataformas) y Shader son productos distintos. No sé si el motor debe nacer como su evolución, convivir con él o ser un tercer producto independiente. Este documento lo trata como producto separado por defecto.
3. **Nombre.** «Yotsuba Engine» es un nombre de trabajo y puede colisionar con el del Framework.
4. **Alcance de la bandera comercial:** el juego bandera (propio o de socio).
5. **Política sobre IA en el motor,** si se aspira a contribuir a proyectos como MonoGame con su política de procedencia.

## 12. Próximos pasos recomendados (sin aplicar nada)

Todo esto son sugerencias; no se ha implementado nada.

1. Escribir los contratos C1–C6 como un documento corto de aceptación y convertirlos en pruebas.
2. Preparar el spike de la fase 0: un componente `[GpuLayout]`, un kernel `Integrate` ejecutado en CPU y GPU y una comparación automática.
3. Decidir la licencia y la relación con Framework antes de crear el repositorio público.
4. Elegir el juego bandera y su alcance (una simulación masiva visualmente llamativa).
5. Reutilizar el plan de piloto con MonoGame como canal de entrada, tratándolo como la primera rampa comercial del compilador.

Veredicto: la tesis es potente y única, pero un motor es una apuesta de años. El camino defendible es **demostrar primero el spike y la demo masiva, vender pilotos del compilador mientras tanto y ampliar el motor solo si las puertas se cumplen**.
