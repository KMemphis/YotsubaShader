<p align="center">
  <img src="assets/yotsuba-shader.png" alt="Yotsuba Shader" width="220" />
</p>

<h1 align="center">YotsubaShader</h1>

<p align="center">
  Un lenguaje y una cadena de compilación .NET para describir shaders y generar representación intermedia orientada a SPIR-V.
</p>

<p align="center">
  <a href="https://github.com/KMemphis/YotsubaShader">Repositorio</a>
  ·
  <a href="docs/SPIRV_GENERATOR.md">Generador SPIR-V</a>
  ·
  <a href="website/README.md">Sitio web</a>
</p>

## Qué es

YotsubaShader es un proyecto experimental para modelar shaders con tipos y atributos de C#. Su objetivo es ofrecer una capa común para describir etapas, recursos, tipos nativos y entradas/salidas de GPU, manteniendo la sintaxis específica de cada ecosistema de shaders.

El proyecto está pensado para evolucionar hacia una cadena completa:

```text
C# / atributos de shader
        ↓
Frontend y descubrimiento
        ↓
IR de YotsubaShader
        ↓
Backend SPIR-V y otros backends
```

## Características actuales

- Modelo de lenguaje basado en atributos de C#.
- Representación intermedia para shaders, etapas, recursos, tipos y operaciones.
- Atributos separados por estilo de lenguaje:
  - `HlslStyle`
  - `MetalStyle`
  - `VulkanStyle`
  - `GlslStyle`
  - `WgslStyle`
- Tipos nativos para vectores, matrices, texturas y samplers.
- Descripción de built-ins de vertex, fragment y compute.
- Atributos para `location`, bindings, etapas y tamaños de workgroup.
- Generador experimental de operaciones SPIR-V.
- Proyecto de ejemplo y pruebas de frontend.

## Estructura del repositorio

| Proyecto | Responsabilidad |
| --- | --- |
| `YotsubaShader.Language` | Atributos, etapas y tipos que forman el lenguaje de shaders. |
| `YotsubaShader.Frontend` | Descubrimiento y procesamiento de shaders escritos en C#. |
| `YotsubaShader.Frontend.Tests` | Pruebas del frontend y ejemplos compilables. |
| `YotsubaShader.IR` | Representación intermedia de funciones, expresiones, recursos y tipos. |
| `YotsubaShader.Compiler` | Construcción experimental de módulos y operaciones SPIR-V. |
| `YotsubaShader.Generator` | Herramientas de generación para el compilador. |
| `YotsubaShader.Example` | Programa de ejemplo para probar el proyecto. |
| `website` | Documentación y página web del proyecto. |

## Estilos de atributos

Cada estilo conserva los nombres y convenciones del lenguaje de destino. Esto permite que una misma idea, como la posición de salida del vertex shader, pueda representarse según el backend:

```csharp
// HLSL / Slang
[SV_Position]
public float4 Position;

// Vulkan / SPIR-V
[BuiltIn_Position]
public float4 Position;

// WGSL
[@builtin_position]
public float4 Position;
```

Los atributos WGSL incluyen el prefijo `@` también en sus nombres de archivo para reflejar la sintaxis nativa, por ejemplo `@builtin_position.cs` y `@workgroup_size.cs`.

## Ejemplo conceptual

Un shader puede describirse con una etapa y atributos de entrada/salida:

```csharp
using YotsubaShader.Language;
using YotsubaShader.Language.Attributes.HlslStyle;

public static class BasicShader
{
    [ShaderStage(Stage.Vertex)]
    public static float4 Vertex(
        float3 position,
        [SV_VertexID] uint vertexId)
    {
        return new float4(position, 1.0f);
    }
}
```

La API está en desarrollo; los nombres exactos y los backends pueden cambiar mientras se consolida el diseño del lenguaje.

## Requisitos

- .NET 8 SDK
- macOS, Linux o Windows con un SDK compatible

Comprueba la instalación con:

```bash
dotnet --info
```

## Compilar

Desde la raíz del repositorio:

```bash
dotnet build YotsubaShader.Language/YotsubaShader.Language.csproj
```

Para compilar la solución completa:

```bash
dotnet build YotsubaShader.sln
```

Para ejecutar las pruebas del frontend:

```bash
dotnet test YotsubaShader.Frontend.Tests/YotsubaShader.Frontend.Tests.csproj
```

## Estado del proyecto

YotsubaShader se encuentra en una fase temprana de desarrollo. El modelo de atributos y la representación intermedia están creciendo por etapas. El backend SPIR-V todavía es experimental y no debe considerarse una implementación completa del estándar.

Las prioridades naturales del proyecto son:

1. Consolidar el modelo de tipos y atributos.
2. Completar el frontend y la validación semántica.
3. Ampliar la generación de SPIR-V.
4. Añadir backends y validación cruzada para HLSL, Slang, GLSL, WGSL y Metal.
5. Mejorar reflexión, ejemplos y documentación.

## Contribuir

Las contribuciones son bienvenidas. Para cambios importantes, abre primero un issue describiendo el diseño propuesto. Los cambios de atributos deben incluir ejemplos o pruebas que expliquen cómo se traducen al backend correspondiente.

## Licencia

Consulta la licencia del repositorio antes de redistribuir el proyecto. Si todavía no aparece un archivo de licencia, el código debe considerarse sin licencia explícita.

