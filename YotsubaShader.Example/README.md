# YotsubaShader.Example

## Purpose

`YotsubaShader.Example` is an executable development example for the current SPIR-V module builder and binary encoder.

## What it demonstrates

The program manually constructs a fragment-stage `SpirVModule` that writes a constant magenta color. It declares capabilities, the memory model, an entry point, output location, scalar/vector types, constants, a function, and a basic block. It then:

1. Prints the assembly-like module representation.
2. Encodes the module through `SpirVCompiler`.
3. Writes `mi modulo spirv.spv` in the process working directory.

## Running it

```bash
dotnet run --project YotsubaShader.Example
```

The generated file is a local example artifact and should not be treated as source.

## Dependencies

- `YotsubaShader.Compiler.SpirV`

## Out of scope

This example does not currently demonstrate C# shader discovery, Roslyn-to-IR translation, or automatic IR-to-SPIR-V lowering. It exercises the lower-level SPIR-V construction API directly.

## Status

The example reflects the manual backend workflow available today. It should evolve into an end-to-end example after IR lowering is implemented.

See the [repository README](../README.md) for the intended product workflow.
