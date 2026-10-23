# YotsubaShader.Language

## Purpose

`YotsubaShader.Language` defines the backend-neutral C# vocabulary used to author a YotsubaShader shader. It contains the stage declaration API and metadata for shader-native vectors, matrices, textures, and samplers.

## Position in the pipeline

```text
C# shader source → Language metadata → Roslyn frontend → Yotsuba IR → backend
```

User projects reference this assembly when declaring shader entry points. The Roslyn frontend later reads its attributes from symbols; the attributes do not compile or execute shaders themselves.

## Main API

- `ShaderStageAttribute` and `Stage` identify vertex, fragment, and compute entry points.
- `YotsubaShaderAttribute` is the common base for language metadata.
- `VectorAttribute`, `MatrixAttribute`, `TextureAttribute`, and `SamplerAttribute` describe shader-native types.
- `ShaderIgnore` marks declarations that should not participate in shader processing.
- `CompilerGeneratedCodeAttribute` identifies generated binary formats.

## Responsibilities

- Provide a small, neutral authoring contract.
- Keep authoring declarations independent from Roslyn and output formats.
- Describe shader types without embedding SPIR-V-specific behavior.

## Out of scope

This project does not discover shaders, validate method bodies, construct IR, or emit binaries. Those responsibilities belong to `YotsubaShader.Roslyn`, `YotsubaShader.IR`, and the selected backend.

## Dependencies

The project has no project or package dependencies. It targets .NET 8.

## Status

The core stage and type metadata exists. Support for each declaration still depends on the frontend and backend implementing the corresponding translation.

See the [repository README](../README.md) for the complete pipeline.
