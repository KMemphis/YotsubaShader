# YotsubaShader.Frontend.Tests

## Purpose

`YotsubaShader.Frontend.Tests` contains xUnit tests for the C# frontend and its Roslyn-based shader discovery behavior.

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

`BasicColorShader` provides a representative shader declaration used by frontend tests.

## Running the tests

```bash
dotnet test YotsubaShader.Frontend.Tests/YotsubaShader.Frontend.Tests.csproj
```

## Dependencies

- xUnit and Microsoft.NET.Test.Sdk
- MonoGame DesktopGL for shader data types used by fixtures
- `YotsubaShader.Roslyn`
- `YotsubaShader.Language`
- HLSL and SPIR-V authoring vocabularies

## Out of scope

This project does not currently provide comprehensive IR-node, SPIR-V lowering, binary validation, or end-to-end compilation coverage.

## Status

The current suite is concentrated on discovery. It should grow alongside statement/expression processing and should eventually be complemented by IR and backend tests.

See the [Roslyn frontend README](../YotsubaShader.Roslyn/README.md) for the component under test.
