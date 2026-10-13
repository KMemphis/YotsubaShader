<p align="center">
  <img src="assets/Yotsuba%20Hybrid%20Logo.webp" alt="Yotsuba Shader" width="220" />
</p>

<h1 align="center">YotsubaShader</h1>

<p align="center">
  Un lenguaje de shaders en C# para equipos que quieren escribir una vez, conservar sus convenciones favoritas y compilar hacia el backend que elijan.
</p>

<p align="center">
  <a href="https://github.com/KMemphis/YotsubaShader">Repositorio</a>
  ·
  <a href="docs/SPIRV_GENERATOR.md">Notas de SPIR-V</a>
  ·
  <a href="website/README.md">Sitio web</a>
</p>

## El producto

YotsubaShader aspira a ser la capa de autoría de shaders de tu motor, framework o herramienta de contenido. El equipo escribe shaders como código C# con tipos, estructuras y atributos; el compilador interpreta sus interfaces y etapas, las lleva a una representación intermedia común y genera el artefacto del backend solicitado.

La idea central es sencilla: **el estilo con el que anotas un shader no decide su destino**. HLSL, GLSL, Metal, Vulkan y WGSL son vocabularios de autoría equivalentes. Elige el que resulte más natural para tu equipo; después selecciona SPIR-V u otro backend en la compilación.

```text
C# con atributos familiares
          ↓
análisis semántico y validación
          ↓
Yotsuba IR
          ↓
SPIR-V · y futuros backends
```

## Por qué YotsubaShader

- Escribe shaders junto a tu código C#, con estructuras, tipos y convenciones del proyecto.
- Conserva el vocabulario de shader que tu equipo ya conoce.
- Describe entradas, salidas, recursos, ubicaciones, bindings y etapas de forma explícita.
- Separa el lenguaje de autoría del formato final: el mismo shader puede viajar por una sola canalización hacia distintos targets.
- Integra la compilación en CI, en la herramienta de contenido o directamente en el build de tu engine.

## Un shader, cinco estilos de autoría

Los cinco ejemplos siguientes expresan la misma intención: declarar un vertex shader y asociar su salida con la posición que espera la GPU. Algunos también muestran el identificador del vértice para que veas cómo se expresa una entrada built-in en cada vocabulario.

> Estos estilos no son “HLSL para HLSL”, “WGSL para WebGPU”, etc. Son opciones ergonómicas de C#; el target se elige después, al compilar.

### HLSL Like code Style: un shader de color claro y familiar

HLSL es el estilo recomendado para empezar porque hace muy visible la relación entre la etapa, los datos de entrada, la posición de salida y el color que recibe el fragment shader. Este ejemplo recibe una posición y un color por vértice, conserva ambos datos y devuelve el color en la etapa de fragmentos.

```csharp
using Microsoft.Xna.Framework;
using YotsubaShader.Language;
using YotsubaShader.Language.Attributes.HlslStyle; // Vocabulario de atributos inspirado en HLSL.
using YotsubaShader.Language.Attributes;

public struct VertexInput
{
    [Location(0)]
    public Vector2 Position;

    [Location(1)]
    public Vector4 Color;
}

public struct VertexOutput
{
    [SV_Position]
    public Vector4 Position;

    [Location(0)]
    public Vector4 Color;
}

public static class BasicColorShader
{
    [ShaderStage(Stage.Vertex)]
    public static VertexOutput Vertex(VertexInput input)
    {
        VertexOutput output;

        output.Position = new Vector4(
            input.Position.X,
            input.Position.Y,
            0.0f,
            1.0f
        );

        output.Color = input.Color;

        return output;
    }

    [ShaderStage(Stage.Fragment)]
    public static Vector4 Fragment(VertexOutput input)
    {
        return input.Color;
    }
}
```

Este pequeño shader ya muestra las piezas esenciales del lenguaje:

- `VertexInput` define los datos que llegan desde el motor.
- `[Location(0)]` y `[Location(1)]` conectan esos datos con las entradas del pipeline.
- `VertexOutput` define lo que el vertex shader entrega a la siguiente etapa.
- `[SV_Position]` marca la posición que necesita la GPU.
- `[ShaderStage]` separa vertex y fragment shader.
- `Location(0)` permite transportar el color entre ambas etapas.

La misma clase puede crecer con uniforms, texturas, buffers y más etapas. Lo importante es que los nombres `SV_Position` y `Location` describen la interfaz semántica; el backend final se selecciona en la compilación.

### GLSL Like code Style

```csharp
using Microsoft.Xna.Framework;
using YotsubaShader.Language;
using YotsubaShader.Language.Attributes.GlslStyle; // Vocabulario de atributos inspirado en GLSL.

public struct VertexOutput
{
    [gl_Position]
    public Vector4 Position;
}

public static class Triangle
{
    [ShaderStage(Stage.Vertex)]
    public static VertexOutput Vertex([gl_VertexIndex] uint vertexIndex) => new()
    {
        Position = new Vector4(0.0f, 0.0f, 0.0f, 1.0f)
    };
}
```

### Metal Like code Style

```csharp
using Microsoft.Xna.Framework;
using YotsubaShader.Language;
using YotsubaShader.Language.Attributes.MetalStyle; // Vocabulario de atributos inspirado en Metal Shading Language.

public struct VertexOutput
{
    [position]
    public Vector4 Position;
}

public static class Triangle
{
    [ShaderStage(Stage.Vertex)]
    public static VertexOutput Vertex([vertex_id] uint vertexId) => new()
    {
        Position = new Vector4(0.0f, 0.0f, 0.0f, 1.0f)
    };
}
```

### Vulkan Like code Style

```csharp
using Microsoft.Xna.Framework;
using YotsubaShader.Language;
using YotsubaShader.Language.Attributes.VulkanStyle; // Vocabulario de atributos inspirado en Vulkan/SPIR-V.

public struct VertexOutput
{
    [BuiltIn_Position]
    public Vector4 Position;
}

public static class Triangle
{
    [ShaderStage(Stage.Vertex)]
    public static VertexOutput Vertex([BuiltIn_VertexIndex] uint vertexIndex) => new()
    {
        Position = new Vector4(0.0f, 0.0f, 0.0f, 1.0f)
    };
}
```

### WGSL Like code Style

```csharp
using Microsoft.Xna.Framework;
using YotsubaShader.Language;
using YotsubaShader.Language.Attributes.WgslStyle; // Vocabulario de atributos inspirado en WGSL.

public struct VertexOutput
{
    [@builtin_position]
    public Vector4 Position;
}

public static class Triangle
{
    [ShaderStage(Stage.Vertex)]
    public static VertexOutput Vertex([@builtin_vertex_index] uint vertexIndex) => new()
    {
        Position = new Vector4(0.0f, 0.0f, 0.0f, 1.0f)
    };
}
```

Los nombres conservan deliberadamente la forma del lenguaje de origen. En C#, las anotaciones WGSL usan `@` en el identificador —por ejemplo, `[@builtin_position]`— para seguir siendo fieles a su sintaxis sin dejar de ser código C# válido.

## Interfaces y recursos

Además de los built-ins, puedes describir una interfaz con ubicaciones y recursos con atributos. `[Location(n)]` se usa para campos o propiedades de entrada/salida; el proyecto también incluye atributos para uniforms, storage buffers, bindings, descriptor sets y los equivalentes de los distintos estilos.

```csharp
using Microsoft.Xna.Framework;
using YotsubaShader.Language.Attributes;
using YotsubaShader.Language.Resources;

public struct VertexInput
{
    [Location(0)]
    public Vector2 Position;

    [Location(1)]
    public Vector4 Color;
}

public sealed class Material
{
    [Uniform]
    public float Exposure;
}
```

## Compilar para SPIR-V

### CLI de compilación — interfaz objetivo

La CLI es la experiencia prevista para builds, CI y herramientas de contenido. Aún no forma parte de la distribución actual; este es el contrato de uso que persigue el producto.

```bash
yotsuba compile shaders/Triangle.cs \
  --entry Triangle.Vertex \
  --target spirv \
  --output artifacts/triangle.spv
```

El comando descubrirá la entrada marcada con `[ShaderStage]`, validará su interfaz, generará Yotsuba IR y emitirá el módulo `.spv`. La opción `--target` selecciona el backend: no la familia de atributos usada al escribir el shader.

### Biblioteca de compilación — interfaz objetivo

Un engine o framework podrá hacer lo mismo sin arrancar un proceso externo:

```csharp
using YotsubaShader.Compilation;

var request = new ShaderCompilationRequest
{
    SourceFiles = ["shaders/Triangle.cs"],
    EntryPoints = ["Triangle.Vertex"],
    Target = ShaderTarget.SpirV
};

ShaderCompilationResult result = await YotsubaCompiler.CompileAsync(request);

await File.WriteAllBytesAsync(
    "artifacts/triangle.spv",
    result.Binary);
```

Esta API también es de producto objetivo; los nombres y paquetes se estabilizarán antes de su primera versión pública. La intención es que CLI y biblioteca produzcan el mismo resultado y compartan diagnósticos, validación y configuración.

## Estado de la implementación

YotsubaShader está en desarrollo activo. El repositorio ya contiene:

- Atributos de lenguaje para HLSL, GLSL, Metal, Vulkan y WGSL.
- Descubrimiento de clases y métodos C# marcados con `[ShaderStage]` mediante Roslyn.
- Un modelo de representación intermedia para etapas, funciones, almacenamientos, tipos, slots y expresiones.
- Un ensamblador de bajo nivel de módulos SPIR-V 1.6: `SpirVModule` + `SpirVCompiler`.
- Ayudantes para tipos escalares, vectores, matrices, imágenes, samplers, arrays y punteros de SPIR-V.

Todavía se están uniendo las piezas que convierten una clase de shader C# completa en IR y después en SPIR-V; la CLI y la API de alto nivel descritas arriba aún no están publicadas. Los ejemplos de autoría muestran el lenguaje que el producto busca ofrecer, no una promesa de que toda operación de su cuerpo ya sea compilable hoy.

## Trabajar con el repositorio

Requisitos:

- .NET 8 SDK
- macOS, Linux o Windows con un SDK compatible

```bash
dotnet build YotsubaShader.sln
```

El proyecto de ejemplo actual construye un módulo SPIR-V de bajo nivel de forma explícita:

```bash
dotnet run --project YotsubaShader.Example/YotsubaShader.Example.csproj
```

Esto crea `mi modulo spirv.spv` en el directorio de trabajo. Para conocer el formato del encabezado y validarlo con SPIRV-Tools, consulta [docs/SPIRV_GENERATOR.md](docs/SPIRV_GENERATOR.md).

## Arquitectura del repositorio

| Proyecto | Papel en el producto |
| --- | --- |
| `YotsubaShader.Language` | Lenguaje de autoría: etapas, atributos, recursos y tipos. |
| `YotsubaShader.Frontend` | Descubrimiento y análisis semántico de shaders escritos en C#. |
| `YotsubaShader.IR` | Modelo común entre el frontend y los backends. |
| `YotsubaShader.Compiler` | Construcción y codificación de módulos SPIR-V. |
| `YotsubaShader.Generator` | Generador de la tabla de operaciones SPIR-V desde la gramática de Khronos. |
| `YotsubaShader.Example` | Ejemplo ejecutable del ensamblador SPIR-V de bajo nivel. |
| `YotsubaShader.Frontend.Tests` | Pruebas de descubrimiento y análisis del frontend. |

## Contribuir

Las contribuciones son bienvenidas. Para cambios de diseño, abre primero un issue con el problema, la propuesta y un ejemplo de shader o backend que la motive. Los cambios de atributos deben explicar su equivalencia semántica y añadir pruebas cuando corresponda.

## Licencia

La distribución pública de YotsubaShader se ofrece bajo la **GNU Affero General Public License, versión 3.0 (AGPL-3.0-only)**. Consulta el texto completo en [LICENSE](LICENSE).

Esto no autoriza automáticamente el uso de versiones posteriores de la licencia.

### Licencia comercial para patrocinadores

Los patrocinadores comerciales que aparezcan publicados en la página pública **Sponsors** del sitio oficial de YotsubaShader pueden recibir una licencia alternativa para integrar YotsubaShader en un producto independiente, como un motor de videojuegos o framework. Entre sus beneficios, permite integrar y adaptar YotsubaShader en productos propietarios, distribuir el componente integrado en binario sin publicar el código propio del producto y conservar los derechos sobre las versiones ya cubiertas.

La página **Sponsors** es el registro público que determina quién se considera sponsor; debe identificar el nombre, periodo, nivel y versiones cubiertas. El patrocinio por sí solo no concede derechos comerciales mientras el sponsor no aparezca publicado allí. La licencia comercial no autoriza crear o comercializar un compilador, biblioteca o fork de shaders que compita con YotsubaShader. Consulta [SPONSOR_COMMERCIAL_LICENSE.md](SPONSOR_COMMERCIAL_LICENSE.md).
