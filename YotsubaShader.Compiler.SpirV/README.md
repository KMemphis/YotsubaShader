# YotsubaShader.Compiler.SpirV

## Purpose

`YotsubaShader.Compiler.SpirV` is the SPIR-V backend. It owns the SPIR-V instruction model, module construction, binary encoding, and the lowering used by the supported C# shader compilation path.

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

## Buffer layout

A uniform or storage-buffer resource must be a struct. The struct becomes a SPIR-V `Block` and every member gets an `Offset`, so the application knows where to write each value. The policy depends on the kind of resource:

| Resource | Layout |
| --- | --- |
| `Uniform` | std140 |
| `StorageBuffer`, `PushConstant` | std430 |

`N` is the size of a component: 4 bytes for `int`, `uint` and `float`, 8 bytes for `double`.

| Member | Alignment | Size |
| --- | --- | --- |
| scalar | `N` | `N` |
| vector of 2 | `2N` | `2N` |
| vector of 3 | `4N` | `3N` |
| vector of 4 | `4N` | `4N` |
| nested struct | its largest member alignment (std140: at least 16) | rounded up to its alignment |

Each member starts at the next multiple of its alignment. A scalar can fill the fourth component that a vector of 3 leaves free, so `{ float A; Vector3 B; float C; }` places `B` at offset 16 and `C` at offset 28 in both layouts.

A member that has no defined layout fails with a message that names it: `bool` (use `uint`), matrices and arrays, which are not supported in buffers yet. A resource that is not a struct fails too; wrap the value in a struct. One struct type cannot be used with two layouts that give different offsets, for example a nested struct shared by a uniform and a storage buffer; use one struct per kind of buffer.

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

Instruction generation, manual module construction, text rendering, and binary encoding exist. The high-level compiler lowers its supported IR subset into this backend; automatic lowering for every IR declaration and arbitrary C# construct is not complete.

See the [repository README](../README.md) for the intended end-to-end API.
