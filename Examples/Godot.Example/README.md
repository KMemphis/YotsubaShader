# Godot example

A Godot 4 project that renders a full-screen plasma written in C# (`Shaders/PlasmaShader.cs`) through YotsubaShader.

## Run it

1. Install the CLI: `dotnet tool install -g YotsubaShader.Compiler.CLI` (or use a local build of `Core/YotsubaShader.Compiler.CLI`).
2. Restore the shader project once: `dotnet build Shaders/Godot.Shaders.csproj`.
3. Open this folder in Godot 4.4+ and press Play.

The addon in `addons/yotsubashader` (a symlink to `Plugins/YotsubaShader.Extensions.Godot/addon/addons/yotsubashader`) is
already enabled in `project.godot`. Before the project runs it calls `yotsuba compile Shaders/Godot.Shaders.csproj --target godot`
and writes `res://yotsuba_shaders/Examples/GodotShaders/PlasmaShader.spatial.gdshader`; `main.gd` loads that file and
animates the `P_Time` and `P_Tint` uniforms (`[Uniform] PlasmaParams P` in C#).

If `dotnet` is not on the PATH that Godot sees, set *Project Settings > yotsuba_shader > command* to the full path of the `yotsuba` tool.
On Windows, where git may not have created the symlink, copy `Plugins/YotsubaShader.Extensions.Godot/addon/addons/yotsubashader` into `addons/`.
