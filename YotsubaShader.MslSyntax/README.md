# YotsubaShader.MslSyntax

## Purpose

`YotsubaShader.MslSyntax` provides an optional C# authoring vocabulary inspired by Metal Shading Language attributes, built-ins, address spaces, and resources.

It does not parse or emit Metal source. The Roslyn frontend consumes its metadata and normalizes it through `YotsubaShader.Concepts`.

## Example

```csharp
using YotsubaShader.MslSyntax;

public struct VertexOutput
{
    [position]
    public Vector4 Position;
}
```

## Vocabulary

- Stage-interface attributes include `position`, `vertex_id`, `instance_id`, `color`, and fragment/compute built-ins.
- Compute attributes describe thread, threadgroup, and SIMD-group values.
- Resource declarations model buffers, textures, samplers, address spaces, and attribute indices.

## Responsibilities

- Offer familiar Metal names to C# shader authors.
- Associate Metal spellings with canonical shader concepts.
- Remain independent from output backend selection.

## Out of scope

This project is not a Metal compiler or an MSL output backend. Metal-style attributes can be used with any backend that supports their canonical meaning.

## Dependencies

- `YotsubaShader.Language`
- `YotsubaShader.Concepts`

## Status

The current Metal-inspired vocabulary is defined. Translation support depends on the frontend and selected backend implementing each concept.

See the [repository README](../README.md) for complete examples.
