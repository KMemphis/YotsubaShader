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
| `command` | The yotsuba command | `dotnet yotsuba` (if it is not a local tool, the global `yotsuba` in `~/.dotnet/tools` is used) |
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

## C# editor tab (prototype)

The addon adds a **Yotsuba** tab next to 2D / 3D / Script: pick a `.cs` file of your shaders project, edit it with C#
highlighting and see errors live, underlined like in an IDE. The diagnostics come from `yotsuba lsp <project.csproj>`,
a language server that opens your project with Roslyn and runs both the C# compiler and the YotsubaShader rules (`YS....`,
including `YS0710` for what Godot cannot do). Your unsaved text is what gets analyzed; **Save** (or Ctrl/Cmd+S) writes the file.

It needs a CLI that has the `lsp` command, and Godot 4.4 or newer (`OS.execute_with_pipe`). If the command in `command` is not
that CLI, set `yotsuba_shader/lsp_command`; a path with spaces must be a single executable (for example a wrapper script).
Completion (as you type, after a dot, filtered by what you wrote) and hover documentation also come from Roslyn.
Types that are not imported yet are suggested too (`Stopwatch  (using System.Diagnostics)`); accepting one with Tab adds the
`using`. If a type is already written but not imported, the error says `[Ctrl+. to import]`: press Ctrl/Cmd+. (or Alt+Enter)
on it and pick the `using`. Not done yet: go to definition and signature help.

The left side shows the whole project as a tree (folders and text files; `bin`/`obj` are hidden). Use the **New file**,
**New folder** and **Delete** buttons, or right-click an entry. Deleting moves it to the trash. New `.cs` files are analyzed
right away.

Colors follow Visual Studio: set `yotsuba_shader/editor_theme` to `vs_dark` (default), `vs_light` or `godot`. Identifiers are
colored by meaning, as Roslyn classifies them (classes, structs, enums, methods, parameters, locals, fields), on top of the
keyword, string and comment coloring.
