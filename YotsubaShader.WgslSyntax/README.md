# YotsubaShader.WgslSyntax

## Purpose

`YotsubaShader.WgslSyntax` provides an optional C# authoring vocabulary inspired by WGSL attributes, built-ins, and resources.

C# identifiers that mirror WGSL's `@` spelling use C# verbatim identifiers, such as `[@builtin_position]`. This project does not parse or emit WGSL.

## Example

```csharp
using YotsubaShader.WgslSyntax;

public struct VertexOutput
{
    [@builtin_position]
    public Vector4 Position;
}
```

## Vocabulary

- `builtin_*` attributes represent WGSL stage built-ins.
- `location` and `workgroup_size` describe interface and compute metadata.
- Resource attributes represent bindings, groups, uniforms, storage, textures, and samplers.

## Responsibilities

- Offer familiar WGSL names to C# shader authors.
- Map WGSL spellings to canonical concepts.
- Keep authoring style independent from backend selection.

## Out of scope

This project is not a WGSL parser or output backend. WGSL-style metadata may target SPIR-V or any future backend that supports the same concepts.

## Dependencies

- `YotsubaShader.Language`
- `YotsubaShader.Concepts`

## Status

The current WGSL-inspired built-in and resource vocabulary is defined. End-to-end support depends on frontend and backend coverage.

See the [repository README](../README.md) for complete examples.
