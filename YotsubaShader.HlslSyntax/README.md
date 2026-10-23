# YotsubaShader.HlslSyntax

## Purpose

`YotsubaShader.HlslSyntax` provides an optional C# authoring vocabulary inspired by HLSL semantics and resource declarations.

It does not parse or emit HLSL. Its types are C# attributes and metadata that the Roslyn frontend normalizes through `YotsubaShader.Concepts`.

## Examples

```csharp
using YotsubaShader.HlslSyntax;

public struct VertexOutput
{
    [SV_Position]
    public Vector4 Position;

    [COLOR(0)]
    public Vector4 Color;
}
```

## Vocabulary

- `SV_*` attributes describe HLSL-style system-value semantics.
- `POSITION`, `COLOR`, `NORMAL`, `TANGENT`, `TEXCOORD`, and `BINORMAL` describe user semantics.
- `numthreads` describes compute workgroup dimensions.
- Resource attributes and types model registers, constant buffers, structured buffers, textures, and samplers.

## Responsibilities

- Offer familiar HLSL names to C# shader authors.
- Associate those names with canonical shader concepts.
- Remain independent from the selected output backend.

## Out of scope

Using this project does not select an HLSL compiler or an HLSL output target. Backend selection is independent from authoring vocabulary.

## Dependencies

- `YotsubaShader.Language`
- `YotsubaShader.Concepts`

## Status

The project defines the current HLSL-inspired stage-interface and resource vocabulary. Individual constructs become operational as frontend and backend support is implemented.

See the [repository README](../README.md) for complete examples.
