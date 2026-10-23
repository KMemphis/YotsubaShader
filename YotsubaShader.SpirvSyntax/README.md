# YotsubaShader.SpirvSyntax

## Purpose

`YotsubaShader.SpirvSyntax` provides an optional C# authoring vocabulary inspired by SPIR-V built-ins and Vulkan-style interface/resource decorations.

Despite its name, this project does not generate SPIR-V. It supplies authoring metadata that is normalized by the frontend.

## Example

```csharp
using YotsubaShader.SpirvSyntax;

public struct VertexOutput
{
    [BuiltIn_Position]
    public Vector4 Position;
}
```

## Vocabulary

- `BuiltIn_*` attributes represent SPIR-V built-in interface values.
- `Location` identifies user-defined interface locations.
- Resource attributes represent bindings, descriptor sets, uniforms, storage buffers, images, and samplers.

## Responsibilities

- Offer SPIR-V/Vulkan terminology to C# shader authors.
- Associate built-ins and resources with canonical concepts.
- Keep authoring vocabulary separate from binary generation.

## Out of scope

This project does not contain SPIR-V instructions, module construction, lowering, validation, or binary encoding. Those responsibilities belong to `YotsubaShader.Compiler.SpirV`.

## Dependencies

- `YotsubaShader.Language`
- `YotsubaShader.Concepts`

## Status

The current built-in and resource vocabulary is defined. Availability in generated modules depends on frontend and backend implementation.

See the [SPIR-V backend README](../YotsubaShader.Compiler.SpirV/README.md) for binary generation.
