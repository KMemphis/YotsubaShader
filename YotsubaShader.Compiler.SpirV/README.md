# YotsubaShader.Compiler.SpirV

## Purpose

`YotsubaShader.Compiler.SpirV` is the SPIR-V backend. It owns the SPIR-V instruction model, module construction, binary encoding, and the planned lowering from Yotsuba IR.

## Position in the pipeline

```text
Yotsuba IR → SPIR-V lowering → SpirVModule → SPIR-V binary
```

## How it works today

- `spirv.core.grammar.json` is supplied to `YotsubaShader.Generator` as an additional file.
- The generator creates opcode metadata and lookup tables at build time.
- `SpirVModule` collects typed instructions and can render assembly-like text for inspection.
- Module helpers declare common types, constants, pointers, and resources.
- `SpirVCompiler` encodes an already constructed module into SPIR-V words and emits the binary header.

## Main API

- `SpirVModule` represents an instruction module.
- `SpirVModuleHelpers` provides common module-building operations.
- `SpirVCompiler.Compile(SpirVModule)` serializes a module to `byte[]`.
- SPIR-V enums and records represent capabilities, execution models, decorations, storage classes, operations, parameters, functions, and blocks.

## Responsibilities

- Lower backend-neutral IR into legal SPIR-V instructions.
- Allocate and resolve SPIR-V IDs.
- Build module declarations and function bodies in valid logical order.
- Encode modules into the standard little-endian binary format.

## Out of scope

This project must not discover C# methods or inspect Roslyn operations. Those tasks belong to `YotsubaShader.Roslyn`.

## Dependencies

- `YotsubaShader.IR`
- `YotsubaShader.Generator`, loaded as a build-time analyzer
- MonoGame DesktopGL, currently used by module type helpers

## Status

Instruction generation, manual module construction, text rendering, and binary encoding exist. Automatic lowering from `YotsubaShader.IR` to `SpirVModule` is not yet complete.

See the [repository README](../README.md) for the intended end-to-end API.
