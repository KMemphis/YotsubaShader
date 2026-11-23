<p align="center">
  <img src="assets/Readme-Banner.webp" alt="Yotsuba Shader over cherry blossoms, mountains, and a sunset lake" />
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

YotsubaShader is the shader authoring layer for your engine, framework, or content tool. Your team writes shaders in C# using types, structures, and attributes. The compiler interprets their interfaces and stages, lowers the supported shader subset to a shared intermediate representation, and generates output for the selected backend.

The central idea is simple: **the attribute style you use does not determine the output target**. HLSL, GLSL, Metal, Vulkan, and WGSL provide equivalent authoring vocabularies. Choose the one your team knows best, then select SPIR-V or another backend when compiling. WGSL-style authoring is available; WGSL output itself remains planned.

```text
C# with familiar attributes
          ↓
semantic analysis and validation
          ↓
Yotsuba IR
          ↓
SPIR-V · HLSL · GLSL · GLSL ES · MSL · C++
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

### Compilation CLI

The repository includes a CLI for builds, CI, and content tools. It opens the supplied `.csproj`, discovers shader entry points marked with `[ShaderStage]`, compiles them, and writes the selected targets to the output directory.

```bash
dotnet run --project Core/YotsubaShader.Compiler.CLI/YotsubaShader.Compiler.CLI.csproj -- \
  compile Game.Graphics.csproj \
  --target spirv \
  --output artifacts/
```

YotsubaShader is backend-agnostic: the same C# shader can be compiled to every target below, and `--target` selects the backend independently of the attributes used to write the shader. Repeat `--target` to generate several targets in one invocation.

| `--target` | Output | Accessor in `YotsubaShader.g.cs` |
|---|---|---|
| `spirv` (`spv`) | `Shader.spv`, one SPIR-V binary per shader, without the `SPV_GOOGLE_*` extensions (they only carry HLSL semantics for the HLSL translation, which keeps using them internally) | `ToSpirV()` |
| `hlsl` | `Shader.<Stage>.hlsl`, one file per stage | `ToHlsl(Stage)` |
| `glsl` | `Shader.<Stage>.glsl`, one file per stage | `ToGlsl(Stage)` |
| `gles` (`glsles`) | `Shader.<Stage>.gles`, one file per stage | `ToGlslEs(Stage)` |
| `msl` (`metal`) | `Shader.<Stage>.msl`, one file per stage | `ToMSL(Stage)` |
| `cpp` | `Shader.<Stage>.cpp`, one file per stage | not generated by `add` |
| `wgsl` | `Shader.wgsl`, one file per shader with all its entry points (see below) | `ToWgsl()`, returns `string`; generated by `add` only when `--target wgsl` is given |
| `mgfx` (`monogame`) | `Shader.<platform>.mgfx`, one file per `MonoGamePlatform` (`desktopgl`, `windowsdx11`, `windowsdirectx12`, `desktopvulkan`, `ios`, `android`, `gl4`) | `ToMGFX(MonoGamePlatform)` |
| `kni` (`kni-webgl`) | `Shader.knifx`, one binary per shader | `ToKNIWebGL_FX()` |
| `fna` | `Shader.fxb`, one binary per shader | `ToFNA_FX()` |
| `godot` (`gdshader`) | `Shader.<type>.gdshader`, one text file per Godot shader type (`spatial`, `canvasitem`) | `ToGodotShader(GodotShaderType)`, returns `string` |

The `spirv`, `hlsl`, `glsl`, `gles`, `msl`, and `cpp` targets come from the SPIR-V module through YotsubaShaderCross. The `mgfx`, `kni`, and `fna` targets produce a compiled effect for the MonoGame, KNI, and FNA content pipelines through [ShadowDusk](ThirdParty/ShadowDusk): they write one binary per shader instead of one file per stage.

The `mgfx`, `kni`, and `fna` targets compile the vertex and fragment stages through ShadowDusk, which needs its native DXC and vkd3d-shader libraries. They are not in the repository: run `ThirdParty/ShadowDusk/tools/restore.sh` once (it downloads them and verifies their SHA-256) before using them. If one binary fails to compile, the CLI reports the reason, keeps going with the others, and exits with code 1. Loading and rendering the generated effects is exercised by the load tests in `Examples/` (MonoGame, KNI, and FNA); CI runs the MonoGame and KNI ones on Linux. The current compiler path covers the `Vertex`, `Fragment`, and `Compute` stages and the supported subset of shader-body operations.

`mgfx`, `kni`, `fna` and `godot` come from plugins (`YotsubaShader.Extensions.*` NuGet packages). The CLI installs each one into `~/.yotsuba/plugins` (or `$YOTSUBA_HOME/plugins`) the first time it is used, together with its dependencies and the native libraries for the current platform; `yotsuba plugin list | install | update | remove` manages them by hand. The version installed is the newest one with the same major and minor version as the CLI and not newer than it, because a plugin built against a newer `YotsubaShader.Extensions` does not load in an older CLI. When the installed plugin already has the CLI's version no network request is made, and without a connection the installed copy is used. Several `yotsuba` processes (a parallel build) can install plugins at the same time: they wait for each other.

The `godot` target translates the SPIR-V module to Godot Shading Language: `main` becomes `vertex()` and `fragment()`, vertex inputs map to `VERTEX`, `NORMAL`, `UV`, `UV2` and `COLOR` by semantic, vertex outputs become `varying`s, `SV_Target0` is written to `ALBEDO`/`ALPHA` (`spatial`, emitted with `render_mode unshaded, cull_disabled`) or `COLOR` (`canvas_item`), and `[Uniform]` structs become one `uniform` per field named `<Field>_<Member>`. It has no ShadowDusk dependency. Compute shaders, storage buffers, several render targets and built-ins without a Godot counterpart are reported as errors for that file. The plugin also ships an editor addon (`addons/yotsubashader`) that regenerates the `.gdshader` files whenever the project is built; see `Plugins/YotsubaShader.Extensions.Godot/addon/README.md`.

### Choosing the output language version

`--target <name>:<version>` asks for a specific version of the output language. Without a version the compiler emits its default.

| Target | Accepted versions | Default |
|---|---|---|
| `spirv` | `1.3` (Vulkan 1.1), `1.4`, `1.5` (Vulkan 1.2), `1.6` (Vulkan 1.3) | `1.6` |
| `glsl` | `110`, `120`, `130`, `140`, `150`, `330`, `400` to `460` (or `4.3` style) | `450` |
| `gles` | `100`, `300`, `310`, `320` | `310` |
| `msl` | `1.0` to `4.x`, as `major[.minor[.patch]]` (for example `2.4`, `3.1`) | `1.2` |
| `hlsl` | Shader Model `3.0`, `4.0`, `4.1`, `5.0`, `5.1`, `6.0` to `6.8` (or `50`, `51`, `60`...) | `5.0` |

```bash
yotsuba compile Game.csproj --target spirv:1.3 --target glsl:430 --target msl:2.4 --target hlsl:6.0
```

- **MSL default.** MSL is produced by YotsubaShaderCross (a port of SPIRV-Cross) and, when no version is given, it keeps SPIRV-Cross's default: MSL 1.2. The compiler never raises it on its own, so shaders that need features from a newer MSL version must request it (for example `msl:2.4` or `msl:3.1`) and the device must support that version.
- **SPIR-V.** The compiler always emits SPIR-V 1.6. `spirv:1.3` to `1.5` rewrite the module for older drivers: the header version changes and, before 1.4, the entry-point interface lists are reduced to Input/Output variables. Before 1.4, `OpCopyLogical` (the copy between a buffer struct and the same struct in a local variable) is rewritten member by member with `OpCompositeExtract`/`OpCompositeConstruct`, which is equivalent. If the shader uses another instruction that does not exist in the requested version (the pointer comparisons need 1.4, `OpTerminateInvocation` and `OpDemoteToHelperInvocation` need 1.6), the CLI reports an error for that file instead of emitting an invalid module.
- **GLSL ES 300.** ESSL 3.00 has no compute shaders; compute stages requested with `gles:300` are reported as errors while graphics stages are emitted.
- **What a language cannot express is an error for that file, not for the command.** For example `double` in GLSL ES, `PrimitiveId` in MSL below 2.2, `SV_SamplePosition` in HLSL, `BaseVertex` in GLSL ES or WGSL. `compile` prints `Class.Stage (target): reason`, writes every other file, deletes the stale file from a previous run, and exits with code 1.
- **Several versions of one target** in the same command are written side by side; the version is added to the file name (`Shader.Vertex.430.glsl`).
- From code, `CompiledCode` has the same options: `ToSpirV(major, minor)`, `ToGlsl(stage, version)`, `ToGlslEs(stage, version)`, `ToHlsl(stage, shaderModel)` and `ToMsl(stage, major, minor, patch)`.

### WGSL output (CLI only)

`--target wgsl` writes `Shader.wgsl` for each shader class, containing every entry point. It converts the compiled SPIR-V with [naga](https://github.com/gfx-rs/wgpu/tree/trunk/naga), which is native code (Rust), so it is a dependency of the CLI only: the compiler library (`YotsubaShader.Compiler.Library`) stays pure C# and does not emit WGSL. The `SPV_GOOGLE_*` extensions and the decorations they add are removed before the conversion, since naga rejects them, and `OpCopyLogical` is rewritten with instructions naga knows. Read-only buffers (`StructuredBuffer<T>`, `ImmutableArray<T>`, `storage(read)`...) become `var<storage, read>`, which WebGPU requires in the vertex stage. What WGSL cannot express is reported for that shader: built-ins such as `BaseVertex` or `SV_SamplePosition`, and copying a whole struct that has a field used with atomics (WGSL makes that field `atomic<u32>`).

The native library is looked up in `runtimes/<rid>/native/` next to the CLI (or in the path given by `YOTSUBA_NAGA_PATH`). Release builds ship it for `win-x64`, `win-arm64`, `linux-x64`, `linux-arm64`, `osx-x64` and `osx-arm64`; the sources are in `Core/YotsubaShader.Compiler.CLI/Native/naga` and `Native/naga/build.sh` builds the one for the current machine (requires a Rust toolchain). `wgsl` does not take a version.

### Generated project accessors

Use the `add` command when the project should contain a visible generated C# file instead of only external shader artifacts:

```bash
dotnet run --project Core/YotsubaShader.Compiler.CLI/YotsubaShader.Compiler.CLI.csproj -- \
  add Game.Graphics.csproj \
  --target spirv
```

The command discovers and compiles the shader classes, then writes `YotsubaShader.g.cs` beside the supplied `.csproj`. The generated file contains the public static `YotsubaShader_ShadersCompilations` extension class. For each discovered shader it exposes:

- `ToSpirV(this ShaderType shader)`, returning the embedded SPIR-V `byte[]`.
- `ToHlsl(Stage stage)`, `ToGlsl(Stage stage)`, `ToGlslEs(Stage stage)`, and `ToMSL(Stage stage)`, returning the generated source text for that stage.
- `ToMGFX(this ShaderType shader, MonoGamePlatform platform)`, `ToKNIWebGL_FX(this ShaderType shader)`, and `ToFNA_FX(this ShaderType shader)`, returning the embedded engine effect `byte[]`; `ToGodotShader(this ShaderType shader, GodotShaderType type)` returns the `.gdshader` source as a `string`. `MonoGamePlatform` is an enum declared inside the generated file. If ShadowDusk cannot compile a target while the file is generated, that method throws `NotSupportedException` with the reason instead of failing the whole command.

The generated source uses raw string literals so the emitted shader text remains readable. `CompilerGenerated` and `EditorBrowsable(Never)` keep the helper out of normal IntelliSense lists while leaving the generated file and its methods available to the project. The `add` command currently requires at least one `--target` for CLI argument compatibility; its generated helper set is not filtered by that value. Use `compile` when you need target-specific files in an output directory.

`add` always generates `ToHlsl`, `ToGlsl`, `ToGlslEs` and `ToMSL`. When one stage cannot be written in one of those languages (for example a shader that uses `double`, which GLSL ES does not have), that case of the method throws `NotSupportedException` with the reason and the command prints a warning; it is an error (exit code 1) only when that target was requested with `--target`. The file is rewritten only when its content changes, so an unchanged project is not recompiled.

The previous `YotsubaShader.g.cs` is left out when the project is analyzed: renaming or deleting a shader class does not block the next `add`, and errors in code that calls a generated method that does not exist yet (`ToSpirV`, `ToMGFX`...) are ignored. If the project was never restored, the CLI runs `dotnet restore` once before opening it.

### Compiler library

The `YotsubaShader.Compiler.Library` library exposes the compilation pipeline through `CSharpToSpirvCompiler.Compile`. It accepts a Roslyn `Compilation`, discovers shader entry points, builds the intermediate representation for supported shader constructs, generates SPIR-V, and returns a `CompiledCode` result for each shader.

```csharp
using Microsoft.CodeAnalysis;
using YotsubaShader.Compiler.Library;

// `compilation` is the Roslyn compilation for the project.
List<CompiledCode> shaderCompilations =
    CSharpToSpirvCompiler.Compile(compilation);

foreach (var shaderCompilation in shaderCompilations)
{
    Console.WriteLine(shaderCompilation.CSharpFullClassName);
    File.WriteAllBytes(
        $"{shaderCompilation.CSharpFullClassName}.spv",
        shaderCompilation.SpirV);
}
```

Each `CompiledCode` result contains the generated SPIR-V and shader reflection. It also provides helpers for emitting HLSL, GLSL, GLSL ES, Metal, and C++ for each shader stage. These helpers operate on the stages and body operations supported by the current frontend and backend.

## Compiler components

The repository brings together:

- Language attributes for HLSL, GLSL, Metal, Vulkan, and WGSL styles.
- Roslyn-based discovery of C# classes and methods marked with `[ShaderStage]`.
- An intermediate representation for stages, functions, storage, types, interface slots, and expressions.
- A C#-to-SPIR-V compiler pipeline exposed through `YotsubaShader.Compiler.Library`.
- Low-level SPIR-V 1.6 module construction through `SpirVModule` and `SpirVCompiler`.
- Helpers for SPIR-V scalar, vector, matrix, image, sampler, array, and pointer types.
- A command-line compiler in `YotsubaShader.Compiler.CLI` for project builds and multi-target output.

## Working with the repository

Requirements:

- .NET 10 SDK to build the whole solution (the CLI targets `net10.0`; the libraries and analyzers target `net8.0`/`netstandard2.0` and work in .NET 8 projects)
- macOS, Linux, or Windows with a compatible SDK

```bash
dotnet build YotsubaShader.sln
```

The example project contains `BasicColorShader`, a representative vertex, fragment, and compute shader. Building the project also invokes the current CLI `add` hook and regenerates its project-local `YotsubaShader.g.cs` file:

```bash
dotnet build Examples/YotsubaShader.Example/YotsubaShader.Example.csproj
```

To create external `.spv`, HLSL, GLSL, GLSL ES, MSL, or C++ artifacts, invoke the CLI with `compile` and the targets you need. The low-level `SpirVModule` API remains available for explicit module construction; it is separate from the C# shader compilation path.

## Repository architecture

| Project | Role |
| --- | --- |
| `YotsubaShader.Concepts` | Shared semantic concepts for stages, interface slots, built-ins, bindings, resources, and shader metadata. |
| `YotsubaShader.Language` | Common C# authoring primitives, including shader stages and native type declarations. |
| `YotsubaShader.{Glsl,Hlsl,Msl,Spirv,Wgsl}Syntax` | Optional syntax vocabularies that map familiar backend names to the shared concepts. |
| `YotsubaShader.Roslyn` | Roslyn-based discovery and processing of C# shaders into the intermediate representation. |
| `YotsubaShader.IR` | Intermediate model for shaders, stages, functions, storage, interface slots, expressions, and types. |
| `YotsubaShader.Compiler.Library` | C# shader compilation pipeline, reflection, SPIR-V generation, and target conversion. |
| `YotsubaShader.Compiler.SpirV` | Low-level SPIR-V module construction and binary encoding. |
| `YotsubaShader.Compiler.CLI` | Command-line project compilation and target emission. |
| `YotsubaShader.Generator` | SPIR-V operation metadata generated from the Khronos grammar. |
| `YotsubaShaderCross` | Separate submodule that translates SPIR-V into GLSL, HLSL, MSL, or reflection output. |
| `YotsubaShader.Example` | `BasicColorShader`, frontend tests, and the generated project accessors used by the example build. |

## Website and playground

The website at [yotsubashader.com](https://yotsubashader.com) (English and Spanish) and its in-browser playground live in their own repository, [YotsubaShader-Web](https://github.com/KMemphis/YotsubaShader-Web). The attribute reference pages are generated from `YotsubaShader.Language`, so the site stays in step with this repository.

## Contributing

Contributions are welcome. For design changes, open an issue describing the problem, your proposal, and a motivating shader or backend example. Attribute changes should explain their semantic equivalence and include tests where appropriate.

## Licensing

Yotsuba Shader is released under the [Microsoft Public License (MS-PL)](LICENSE). Third-party notices are in [NOTICE](NOTICE).

The name and logo are not covered by the license. See [assets/README.md](assets/README.md).

If Yotsuba Shader is useful to you or your company, consider [sponsoring the project](https://github.com/sponsors/KMemphis).
