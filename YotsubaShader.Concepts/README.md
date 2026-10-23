# YotsubaShader.Concepts

## Purpose

`YotsubaShader.Concepts` defines the canonical meaning behind the different authoring vocabularies. It allows attributes with names such as HLSL `SV_Position`, GLSL `gl_Position`, Metal `position`, SPIR-V `BuiltIn_Position`, and WGSL `builtin_position` to represent the same shader concept.

## Position in the pipeline

```text
Dialect attribute → canonical concept → Roslyn frontend → IR slot/resource
```

Dialect projects decorate their public attributes with concept attributes. The frontend can therefore normalize equivalent spellings without coupling the IR to a source-language vocabulary.

## Main API

- `YotsubaShaderConcept` is the base marker for canonical concepts.
- Vertex concepts describe position, indices, viewport, layer, and user semantics.
- Fragment concepts describe coordinates, depth, samples, facing, barycentrics, and related built-ins.
- Compute concepts describe invocation, workgroup, and subgroup values.
- Resource concepts describe uniforms, buffers, textures, samplers, bindings, and locations.
- `AttributeUsagesTable` centralizes valid C# attribute targets.

## Responsibilities

- Give equivalent dialect attributes one backend-neutral meaning.
- Define semantic keys understood by the frontend.
- Remain independent from Roslyn, IR nodes, and output formats.

## Out of scope

Concept attributes do not perform validation or compilation. A declared concept is supported only when the frontend maps it to IR and the selected backend lowers that IR.

## Dependencies

The project has no project or package dependencies. Dialect projects depend on it.

## Status

The project covers the current vertex, fragment, compute, and resource vocabularies. Coverage should grow only when a stable canonical meaning is available across authoring styles.

See the [repository README](../README.md) for the product-level model.
