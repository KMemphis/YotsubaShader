<p align="center">
  <img src="assets/Yotsuba%20Hybrid%20Logo.webp" alt="Yotsuba Shader" width="220" />
</p>

<h1 align="center">YotsubaShader</h1>

<p align="center">
  A C# shader language for teams that want to write once, use familiar conventions, and compile for the backend they choose.
</p>

<p align="center">
  <a href="https://github.com/KMemphis/YotsubaShader">Repository</a>
  ·
  <a href="docs/SPIRV_GENERATOR.md">SPIR-V notes</a>
  ·
  <a href="website/README.md">Website</a>
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
using YotsubaShader.Language.Attributes.HlslStyle; // Attribute names inspired by HLSL.
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

This small shader shows the essential pieces of the language:

- `VertexInput` defines the data supplied by the engine.
- `[Location(0)]` and `[Location(1)]` identify the pipeline inputs.
- `VertexOutput` defines what the vertex shader passes to the next stage.
- `[SV_Position]` marks the position required by the GPU.
- `[ShaderStage]` identifies the vertex and fragment stages.
- `[Location(0)]` carries color between those stages.

The same class can grow to include uniforms, textures, buffers, and other stages. `SV_Position` and `Location` describe the shader interface; the output backend is chosen when compiling.

### GLSL-like code style

```csharp
using Microsoft.Xna.Framework;
using YotsubaShader.Language;
using YotsubaShader.Language.Attributes.GlslStyle; // Attribute names inspired by GLSL.

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

### Metal-like code style

```csharp
using Microsoft.Xna.Framework;
using YotsubaShader.Language;
using YotsubaShader.Language.Attributes.MetalStyle; // Attribute names inspired by Metal Shading Language.

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

### Vulkan-like code style

```csharp
using Microsoft.Xna.Framework;
using YotsubaShader.Language;
using YotsubaShader.Language.Attributes.VulkanStyle; // Attribute names inspired by Vulkan/SPIR-V.

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

### WGSL-like code style

```csharp
using Microsoft.Xna.Framework;
using YotsubaShader.Language;
using YotsubaShader.Language.Attributes.WgslStyle; // Attribute names inspired by WGSL.

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

These names deliberately resemble their source languages. In C#, WGSL-inspired attributes use `@` in the identifier, as in `[@builtin_position]`, while remaining valid C#.

## Interfaces and resources

You can describe interfaces with locations and resources with attributes. Use `[Location(n)]` on input and output fields or properties. The project also includes attributes for uniforms, storage buffers, bindings, descriptor sets, and their counterparts in the different styles.

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

## Compiling to SPIR-V

### Compilation CLI — planned interface

The CLI is intended for builds, CI, and content tools. It is not part of the current distribution; the following command illustrates the planned product interface.

```bash
yotsuba compile shaders/Triangle.cs \
  --entry Triangle.Vertex \
  --target spirv \
  --output artifacts/triangle.spv
```

The command would find the entry point marked with `[ShaderStage]`, validate its interface, generate Yotsuba IR, and emit a `.spv` module. `--target` selects the backend independently of the attributes used to write the shader.

### Compilation library — planned interface

An engine or framework could compile the same shader without starting a separate process:

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

This API is also planned; names and packages may change before its first release. The CLI and library are intended to share compilation behavior, diagnostics, validation, and configuration.

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

This creates `mi modulo spirv.spv` in the working directory. For details on the module header and validation with SPIRV-Tools, see [docs/SPIRV_GENERATOR.md](docs/SPIRV_GENERATOR.md).

## Repository architecture

| Project | Role |
| --- | --- |
| `YotsubaShader.Language` | Authoring language: stages, attributes, resources, and types. |
| `YotsubaShader.Frontend` | Discovery and semantic analysis of C# shaders. |
| `YotsubaShader.IR` | Shared model between the frontend and backends. |
| `YotsubaShader.Compiler` | SPIR-V module construction and encoding. |
| `YotsubaShader.Generator` | SPIR-V operation table generated from the Khronos grammar. |
| `YotsubaShader.Example` | Executable example of low-level SPIR-V assembly. |
| `YotsubaShader.Frontend.Tests` | Frontend discovery and analysis tests. |

## Contributing

Contributions are welcome. For design changes, open an issue describing the problem, your proposal, and a motivating shader or backend example. Attribute changes should explain their semantic equivalence and include tests where appropriate.

## License

The public distribution of YotsubaShader is licensed under the **GNU Affero General Public License, version 3.0 only (AGPL-3.0-only)**. See [LICENSE](LICENSE) for the full text.

Later versions of the license are not automatically permitted.

### Commercial license for sponsors

Commercial sponsors listed on the official YotsubaShader website's public **Sponsors** page may receive an alternative license to integrate YotsubaShader into an independent product, such as a game engine or framework. This permits integration and adaptation in proprietary products, distribution of the integrated component in binary form without publishing the product's own source code, and continued use of covered versions.

The **Sponsors** page is the public record of eligibility and must identify each sponsor's name, sponsorship period, tier, and covered versions. Sponsorship alone does not grant commercial rights until the sponsor is listed there. The commercial license does not permit a competing shader compiler, library, or fork. See [SPONSOR_COMMERCIAL_LICENSE.md](SPONSOR_COMMERCIAL_LICENSE.md).
