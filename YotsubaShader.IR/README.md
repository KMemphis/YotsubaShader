# YotsubaShader.IR

## Purpose

`YotsubaShader.IR` contains the backend-neutral intermediate representation shared by frontends and output backends.

## Position in the pipeline

```text
Roslyn IOperation → Yotsuba IR → SPIR-V or another backend
```

The frontend converts C# symbols and operations into this model. Backends consume the model without needing Roslyn or the original C# syntax tree.

## Current model

- `Shader` contains translated functions.
- `Function` contains its stage, interface inputs, outputs, and body statements.
- `Storage` and `StorageKind` represent interface values, resources, and local values.
- `InterfaceSlot` implementations represent locations, built-ins, and vertex semantics.
- Scalar, vector, matrix, array, texture, sampler, and custom-struct storage types describe shader data.
- Statements and expressions currently include assignments, storage references, member/element references, and literals.

## Responsibilities

- Represent shader meaning independently from C# and SPIR-V.
- Preserve enough type, stage, interface, and control-flow information for backends.
- Provide a stable boundary between semantic analysis and code generation.

## Design rule

IR nodes must not contain Roslyn symbols, syntax nodes, or SPIR-V instructions. Source-specific information must be normalized before entering the IR, and backend-specific decisions must happen after it.

## Out of scope

This project does not discover C# entry points or encode output binaries. Framework-specific conversion helpers should not become requirements of the core IR.

## Dependencies

The project has no project or package dependencies and targets .NET 8.

## Status

The interface, storage, type, literal, assignment, and supported function-body foundations exist. The statement and expression model continues to expand for calls, operators, returns, local declarations, and structured control flow; an IR node declaration does not by itself promise frontend or backend support.

See the [repository README](../README.md) for the full compilation flow.
