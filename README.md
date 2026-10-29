<p align="center">
  <img src="assets/Cherry%20Blossom%20Lake%20at%20Sunset.png" alt="Cherry blossoms over a mountain lake at sunset" />
</p>

<p align="center">
  <img src="assets/Yotsuba%20Hybrid%20Logo.webp" alt="Yotsuba Shader" width="220" />
</p>

<h1 align="center">YotsubaShader</h1>

<p align="center">
  A C# shader language for teams that want to write once, use familiar conventions, and compile for the backend they choose.
</p>

<p align="center">
  <a href="https://yotsubashader.com">Website</a> ·
  <a href="https://yotsubashader.com/documentation/">Documentation</a> ·
  <a href="https://yotsubashader.com/playground/">Playground</a> ·
  <a href="https://github.com/KMemphis/YotsubaShader">Repository</a>
</p>

## The product

YotsubaShader aims to be the shader authoring layer for your engine, framework, or content tool. Your team writes shaders in C# using types, structures, and attributes. The compiler interprets their interfaces and stages, lowers them to a shared intermediate representation, and generates output for the selected backend.

The central idea is simple: **the attribute style you use does not determine the output target**. HLSL, GLSL, Metal, Vulkan, and WGSL provide equivalent authoring vocabularies. Choose the one your team knows best, then select SPIR-V or another backend when compiling.

```text
C# with familiar attributes
          ↓
semantic analysis and validation
          ↓
Yotsuba IR
          ↓
SPIR-V · future backends
```

## Why YotsubaShader

- Write shaders alongside your C# code using your project's structures, types, and conventions.
- Keep the shader vocabulary your team already knows.
- Describe inputs, outputs, resources, locations, bindings, and stages explicitly.
- Choose the output backend independently of the authoring style.
- Integrate compilation into CI, a content tool, or your engine's build process.

## One shader, five authoring styles

The five examples below express the same basic intent: declare a vertex shader and identify its GPU position output. Some also show how each vocabulary names the built-in vertex index.

> An HLSL-like style does not require an HLSL target, and a WGSL-like style does not require WebGPU. These are C# authoring choices; the compilation target is selected separately.

### HLSL-like code style: a familiar color shader

The HLSL-like style is a good place to start because the relationship between stages, inputs, position, and fragment color is easy to see. This example receives a position and color for each vertex, passes the color through, and returns it from the fragment stage.

```csharp
using Microsoft.Xna.Framework;
using YotsubaShader.Language;
using YotsubaShader.HlslSyntax; // Attribute names inspired by HLSL.

public struct VertexInput
{
    [POSITION]
    public Vector2 Position;

    [COLOR]
    public Vector4 Color;
}

public struct VertexOutput
{
    [SV_Position]
    public Vector4 Position;

    [COLOR]
    public Vector4 Color;
}

public class BasicColorShader
{
    [ShaderStage(Stage.Vertex)]
    public VertexOutput Vertex(VertexInput input)
    {
        VertexOutput output = new VertexOutput();

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
    [return: SV_Target(0)]
    public Vector4 Fragment(VertexOutput input)
    {
        return input.Color;
    }
}
```

This small shader shows the essential pieces of the language:

- `VertexInput` defines the data supplied by the engine.
- `[POSITION]` and `[COLOR]` identify the HLSL-style vertex semantics supplied by the engine.
- `VertexOutput` defines what the vertex shader passes to the next stage.
- `[SV_Position]` marks the position required by the GPU.
- `[ShaderStage]` identifies the vertex and fragment stages.
- `[COLOR]` carries color between those stages.
- `[SV_Target(0)]` identifies the first render-target output of the fragment stage.

The same class can grow to include uniforms, textures, buffers, and other stages. These HLSL-style semantics describe the shader interface; the output backend is chosen when compiling.

### GLSL-like code style

```csharp
using Microsoft.Xna.Framework;
using YotsubaShader.Language;
using YotsubaShader.GlslSyntax; // Attribute names inspired by GLSL.

public struct VertexOutput
{
    [gl_Position]
    public Vector4 Position;
}

public class Triangle
{
    [ShaderStage(Stage.Vertex)]
    public VertexOutput Vertex([gl_VertexIndex] uint vertexIndex)
    {
        VertexOutput output = new VertexOutput();
        output.Position = new Vector4(0.0f, 0.0f, 0.0f, 1.0f);
        return output;
    }
}
```

### Metal-like code style

```csharp
using Microsoft.Xna.Framework;
using YotsubaShader.Language;
using YotsubaShader.MslSyntax; // Attribute names inspired by Metal Shading Language.

public struct VertexOutput
{
    [position]
    public Vector4 Position;
}

public class Triangle
{
    [ShaderStage(Stage.Vertex)]
    public VertexOutput Vertex([vertex_id] uint vertexId)
    {
        VertexOutput output = new VertexOutput();
        output.Position = new Vector4(0.0f, 0.0f, 0.0f, 1.0f);
        return output;
    }
}
```

### Vulkan-like code style

```csharp
using Microsoft.Xna.Framework;
using YotsubaShader.Language;
using YotsubaShader.SpirvSyntax; // Attribute names inspired by Vulkan/SPIR-V.

public struct VertexOutput
{
    [BuiltIn_Position]
    public Vector4 Position;
}

public class Triangle
{
    [ShaderStage(Stage.Vertex)]
    public VertexOutput Vertex([BuiltIn_VertexIndex] uint vertexIndex)
    {
        VertexOutput output = new VertexOutput();
        output.Position = new Vector4(0.0f, 0.0f, 0.0f, 1.0f);
        return output;
    }
}
```

### WGSL-like code style

```csharp
using Microsoft.Xna.Framework;
using YotsubaShader.Language;
using YotsubaShader.WgslSyntax; // Attribute names inspired by WGSL.

public struct VertexOutput
{
    [@builtin_position]
    public Vector4 Position;
}

public class Triangle
{
    [ShaderStage(Stage.Vertex)]
    public VertexOutput Vertex([@builtin_vertex_index] uint vertexIndex)
    {
        VertexOutput output = new VertexOutput();
        output.Position = new Vector4(0.0f, 0.0f, 0.0f, 1.0f);
        return output;
    }
}
```

These names deliberately resemble their source languages. In C#, WGSL-inspired attributes use `@` in the identifier, as in `[@builtin_position]`, while remaining valid C#.

## Interfaces and resources

You can describe interfaces and resources with attributes from the syntax you are using. In the HLSL-style vocabulary, `[POSITION]` and `[COLOR]` describe vertex semantics, while resource declarations use `YotsubaShader.HlslSyntax.Resources`. Explicit numbered locations are provided separately by `YotsubaShader.SpirvSyntax` through `[Location(n)]`. Each `YotsubaShader.{Language}Syntax` project (`GlslSyntax`, `HlslSyntax`, `MslSyntax`, `SpirvSyntax`, `WgslSyntax`) is an optional vocabulary built on the shared `YotsubaShader.Language` stage and type model.

```csharp
using Microsoft.Xna.Framework;
using YotsubaShader.Language;
using YotsubaShader.HlslSyntax;
using YotsubaShader.HlslSyntax.Resources;

public struct VertexInput
{
    [POSITION]
    public Vector2 Position;

    [COLOR]
    public Vector4 Color;
}

public sealed class Material
{
    public float Exposure;
}

public class Lighting
{
    [cbuffer]
    [register(RegisterPrefix.b, 0)]
    public Material Material;
}
```

Here the input semantics use the HLSL-style names, and `Lighting.Material` is bound as a constant buffer at register `b0`. The equivalent interface and resource declarations use the corresponding vocabulary when you choose GLSL, MSL, SPIR-V, or WGSL.

## Compiling to SPIR-V

### Compilation CLI — planned interface

The CLI is intended for builds, CI, and content tools. It is not part of the current distribution; the following command illustrates the planned product interface.

```bash
yotsuba compile Game.Graphics.csproj \
  --target spirv \
  --output artifacts/
```

The command would scan the `.csproj`, discover every shader entry point marked with `[ShaderStage]`, validate each interface, generate Yotsuba IR, and emit the selected output for every discovered shader in the output directory. `--target` selects the backend independently of the attributes used to write the shader.

### Project compilation library — planned interface

The planned library entry point receives a `.csproj`, scans the project with Roslyn, discovers the shader entry points, and returns one compilation result for each shader it finds. The exact type and method names are not final; this illustrates the intended data flow rather than a released API.

```csharp
using YotsubaShader.Compilation;

// Planned shape: one result per discovered shader in the project.
var shaderCompilations = await YotsubaCompiler.CompileProjectAsync(
    "Game.Graphics.csproj",
    target: ShaderTarget.SpirV);

foreach (var shaderCompilation in shaderCompilations)
{
    Console.WriteLine(shaderCompilation.ShaderName);
    // Each result can expose its generated target output and diagnostics.
}
```

The project scan is intended to include the source files and references from the `.csproj`, identify methods marked with `[ShaderStage]`, compile each discovered shader for the selected target, and return the outputs and diagnostics together. The final API and package names may change before the first release.

## Implementation status

YotsubaShader is under active development. The repository currently contains:

- Language attributes for HLSL, GLSL, Metal, Vulkan, and WGSL styles.
- Roslyn-based discovery of C# classes and methods marked with `[ShaderStage]`.
- An intermediate representation for stages, functions, storage, types, interface slots, and expressions.
- Low-level SPIR-V 1.6 module construction through `SpirVModule` and `SpirVCompiler`.
- Helpers for SPIR-V scalar, vector, matrix, image, sampler, array, and pointer types.

The pipeline that converts a complete C# shader class to IR and then SPIR-V is still being assembled. The CLI and high-level API shown above have not been released. The authoring examples show the intended language; not every operation in a shader body can be compiled yet.

## Working with the repository

Requirements:

- .NET 8 SDK
- macOS, Linux, or Windows with a compatible SDK

```bash
dotnet build YotsubaShader.sln
```

The current example project constructs a low-level SPIR-V module explicitly:

```bash
dotnet run --project YotsubaShader.Example/YotsubaShader.Example.csproj
```

This creates `mi modulo spirv.spv` in the working directory. For details on the module header and the SPIR-V output, see [SPIR-V in the documentation](https://yotsubashader.com/technologies/spir-v/).

## Repository architecture

| Project | Role |
| --- | --- |
| `YotsubaShader.Concepts` | Shared semantic concepts for stages, interface slots, built-ins, bindings, resources, and shader metadata. |
| `YotsubaShader.Language` | Common C# authoring primitives, including shader stages and native type declarations. |
| `YotsubaShader.{Glsl,Hlsl,Msl,Spirv,Wgsl}Syntax` | Optional syntax vocabularies that map familiar backend names to the shared concepts. |
| `YotsubaShader.Roslyn` | Roslyn-based discovery and processing of C# shaders into the intermediate representation. |
| `YotsubaShader.IR` | Intermediate model for shaders, stages, functions, storage, interface slots, expressions, and types. |
| `YotsubaShader.Compiler.SpirV` | Low-level SPIR-V module construction and binary encoding. |
| `YotsubaShader.Generator` | SPIR-V operation metadata generated from the Khronos grammar. |
| `YotsubaShaderCross` | Separate submodule that translates SPIR-V into GLSL, HLSL, MSL, or reflection output. |
| `YotsubaShader.Example` | Executable example that builds a low-level SPIR-V module. |
| `YotsubaShader.Frontend.Tests` | Tests for shader discovery, semantic analysis, and frontend processing. |

## Website and playground

The website at [yotsubashader.com](https://yotsubashader.com) (English and Spanish) and its in-browser playground live in their own repository, [YotsubaShader-Web](https://github.com/KMemphis/YotsubaShader-Web). The attribute reference pages are generated from `YotsubaShader.Language`, so the site stays in step with this repository.

## Contributing

Contributions are welcome. For design changes, open an issue describing the problem, your proposal, and a motivating shader or backend example. Attribute changes should explain their semantic equivalence and include tests where appropriate.

## Licensing

Yotsuba Shader is released under the [Microsoft Public License (MS-PL)](LICENSE). Third-party notices are in [NOTICE](NOTICE).

The name and logo are not covered by the license. See [assets/README.md](assets/README.md).

If Yotsuba Shader is useful to you or your company, consider [sponsoring the project](https://github.com/sponsors/KMemphis).
