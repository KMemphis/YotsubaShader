# YotsubaShader.Generator

## Purpose

`YotsubaShader.Generator` is a Roslyn incremental source generator that converts the official SPIR-V core grammar into strongly typed opcode metadata used by the SPIR-V backend.

## Position in the build

```text
spirv.core.grammar.json → incremental generator → SpirVOp and SpirVOpTable
```

The generator is referenced by `YotsubaShader.Compiler.SpirV` as an analyzer. Its assembly is used at build time and is not referenced by the backend at runtime.

## How it works

1. Locate `spirv.core.grammar.json` among Roslyn additional files.
2. Parse instruction names, opcode numbers, classes, operands, versions, capabilities, extensions, and aliases.
3. Generate opcode and instruction-class enums.
4. Generate lookup tables and metadata used to build and validate instructions.
5. Report diagnostics when the grammar is missing, duplicated, or invalid.

## Responsibilities

- Keep generated SPIR-V metadata synchronized with the grammar.
- Produce deterministic source from a given grammar.
- Avoid requiring handwritten opcode tables in the backend.

## Out of scope

The generator does not compile shaders, construct modules, or translate Yotsuba IR. It generates backend support code only.

## Dependencies

- `Microsoft.CodeAnalysis.CSharp` 4.11 as a private build dependency
- `netstandard2.0`, required for Roslyn component compatibility

## Status

The generator produces opcode enums, instruction classes, metadata tables, aliases, and diagnostics from the supplied core grammar.

See the [SPIR-V backend README](../YotsubaShader.Compiler.SpirV/README.md) for its consumer.
