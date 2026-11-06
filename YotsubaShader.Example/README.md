# YotsubaShader.Example

## Purpose

`YotsubaShader.Example` contains the representative `BasicColorShader` declarations, xUnit coverage for the C# frontend, and the generated project accessors produced by the CLI `add` command.

## Current coverage

- Building in-memory C# compilations with Roslyn.
- Finding shader types and stage entry points.
- Preserving entry-point declaration order.
- Exposing parameters and return types.
- Ignoring ordinary CPU/helper methods.
- Discovering nested types and multiple namespaces.
- Matching attributes by symbol identity instead of source spelling.
- Ignoring look-alike attributes and types from referenced assemblies.
- Returning no shaders when the language assembly is unavailable.

`BasicColorShader` provides representative `Vertex`, `Fragment`, and `Compute` entry points. The example project is also used to exercise the supported C#-to-SPIR-V and backend-source compilation path.

## Running the tests

```bash
dotnet test YotsubaShader.Example/YotsubaShader.Example.csproj
```

## Dependencies

- xUnit and Microsoft.NET.Test.Sdk
- MonoGame DesktopGL for shader data types used by fixtures
- `YotsubaShader.Roslyn`
- `YotsubaShader.Language`
- HLSL and SPIR-V authoring vocabularies

## Out of scope

The suite is not a complete conformance suite for every IR node, SPIR-V instruction, resource type, or backend. It covers the current discovery behavior and representative supported compilation paths; unsupported shader-body operations and complete engine integration remain outside this example.

## Status

The current suite combines discovery tests with representative compilation coverage. It should continue to grow as additional statement, expression, resource, and backend paths become supported.

See the [Roslyn frontend README](../YotsubaShader.Roslyn/README.md) for the component under test.
