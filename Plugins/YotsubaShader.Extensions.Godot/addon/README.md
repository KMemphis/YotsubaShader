# YotsubaShader for Godot

Write your shaders in C# with YotsubaShader and use them in Godot 4 as `.gdshader` files.

## 1. Install the CLI

```bash
dotnet tool install -g YotsubaShader.Compiler.CLI
```

The first `--target godot` run installs the `YotsubaShader.Extensions.Godot` plugin from NuGet.

## 2. Enable the addon

1. Copy `addons/yotsubashader` into your project's `res://addons/` folder.
2. *Project > Project Settings > Plugins*: enable **YotsubaShader**.
3. In *Project Settings > General* (turn on *Advanced Settings*), open **yotsuba_shader** and set:

| Setting | Meaning | Default |
|---|---|---|
| `project` | Path to the `.csproj` that holds your C# shaders (absolute or relative to the Godot project) | empty |
| `output_dir` | Where the `.gdshader` files are written | `res://yotsuba_shaders` |
| `shader_type` | `spatial`, `canvas_item` or `both` | `spatial` |
| `command` | The yotsuba command | `dotnet yotsuba` |
| `regenerate_on_run` | Regenerate every time the project runs | on |

The shaders are regenerated whenever you press Play, or from *Project > Tools > Regenerate YotsubaShader shaders*.
If Godot cannot find `dotnet` (it was started from the Dock or Finder), put the full path to the tool in `command`.

## 3. Use them

Each C# shader class `Triangle` becomes `Triangle.spatial.gdshader` (or `Triangle.canvasitem.gdshader`). Assign it to a
`ShaderMaterial`:

```gdscript
var material := ShaderMaterial.new()
material.shader = load("res://yotsuba_shaders/Triangle.spatial.gdshader")
$MeshInstance3D.material_override = material
```

Uniforms keep their C# names: a `[Uniform] Params P` field with `float4 Tint` becomes `P_Tint`:

```gdscript
material.set_shader_parameter("P_Tint", Color.RED)
```

## What is translated

- `[POSITION]` → `VERTEX`, `[NORMAL]`, `[TANGENT]`, `[TEXCOORD(0)]` → `UV`, `[TEXCOORD(1)]` → `UV2`, `[COLOR]` (`canvas_item`: position, color and the first texcoord).
- Vertex outputs → `varying`. `SV_Position` → `POSITION` (spatial) or `VERTEX` (canvas_item).
- `SV_Target0` → `ALBEDO`/`ALPHA` (spatial, `render_mode unshaded, cull_disabled`) or `COLOR` (canvas_item). `SV_Depth` → `DEPTH`.
- A C# vertex shader writes clip-space positions itself, so the spatial shader ignores Godot's model/view/projection matrices (pass your own as a `[Uniform]`) and is double-sided.
- Textures become `uniform sampler2D`; the sampler state comes from the Godot texture hints, not from the C# `SamplerState`.

Compute shaders, storage buffers and several render targets have no equivalent in Godot Shading Language and are reported
as errors by the CLI. Add `<YotsubaShaderTargets>Godot</YotsubaShaderTargets>` to the shaders `.csproj` to see them in the IDE (YS0710).
