# YotsubaShader.Roslyn

## Purpose

`YotsubaShader.Roslyn` is the C# frontend. It discovers shader entry points through Roslyn symbols, interprets their interfaces, and translates method operations into `YotsubaShader.IR`.

## Position in the pipeline

```text
C# Compilation → discovery and semantic analysis → Yotsuba IR
```

## How it works

1. `ShaderDiscovery.Find` scans types declared by the current compilation.
2. Methods marked with `YotsubaShader.Language.ShaderStageAttribute` become entry-point candidates.
3. `ShaderProcessor` converts stages, parameters, return values, slots, resources, and custom structures into IR storage.
4. Roslyn `IOperation` nodes from method bodies are dispatched through `StatementOperationProcessor`.
5. The resulting functions are collected into IR shaders.

Symbol identity is used instead of matching source text, so aliases and fully qualified attribute names can be handled consistently.

## Main API

- `ShaderDiscovery` finds shader classes and entry points.
- `ShaderInfo`, `EntryPointInfo`, and `ParameterInfo` expose discovery results.
- `ShaderProcessor` constructs IR shaders from the discovered symbols.
- `IStatementOperationProcessor` defines operation handlers.
- `StatementOperationProcessor` dispatches Roslyn operations to those handlers.

## Responsibilities

- Validate the supported C# shader subset.
- Normalize authoring dialect attributes through canonical concepts.
- Translate symbols, types, interfaces, statements, and expressions into IR.
- Produce clear diagnostics for unsupported or invalid shader constructs.

## Out of scope

The frontend must not construct SPIR-V instructions or depend on a specific output backend. It also does not execute shader methods as ordinary CPU code.

## Dependencies

- `Microsoft.CodeAnalysis.CSharp` 4.11
- `YotsubaShader.Language`
- `YotsubaShader.Concepts`
- `YotsubaShader.IR`

## Status

Discovery and much of interface/type processing exist. Statement dispatch is defined, but most operation handlers still throw `NotImplementedException`; complete body lowering is therefore still in progress.

See the [repository README](../README.md) for the overall architecture.
