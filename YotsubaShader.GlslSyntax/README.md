# YotsubaShader.GlslSyntax

## Purpose

`YotsubaShader.GlslSyntax` provides an optional C# authoring vocabulary inspired by GLSL built-ins, layouts, and resources.

It does not parse or emit GLSL. Its declarations are metadata consumed by the Roslyn frontend and normalized through `YotsubaShader.Concepts`.

## Example

```csharp
using YotsubaShader.GlslSyntax;

public struct VertexOutput
{
    [gl_Position]
    public Vector4 Position;
}
```

## Vocabulary

- `gl_*` attributes represent vertex, fragment, and compute built-ins.
- `location` and `layout_local_size` describe interface positions and compute dimensions.
- Resource declarations represent uniforms, buffers, layouts, textures, and samplers.

## Responsibilities

- Offer familiar GLSL names to C# shader authors.
- Map equivalent GLSL concepts to the common semantic layer.
- Keep the authoring style independent from output selection.

## Out of scope

This project is not a GLSL parser, transpiler, runtime, or output backend. Choosing GLSL-style attributes does not require a GLSL target.

## Dependencies

- `YotsubaShader.Language`
- `YotsubaShader.Concepts`

## Status

The project defines the current GLSL-inspired stage-interface and resource vocabulary. End-to-end support depends on frontend and backend coverage.

See the [repository README](../README.md) for complete examples.
