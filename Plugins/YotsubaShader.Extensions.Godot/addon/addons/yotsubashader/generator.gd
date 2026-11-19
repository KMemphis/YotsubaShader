@tool
extends RefCounted
## Runs `yotsuba compile <project.csproj> --target godot --output <dir>` and tidies the output folder.
## Kept apart from the EditorPlugin so it can also be run from a plain `--script` (tests, CI).

const SHADER_TYPES := {
	"spatial": ".spatial.gdshader",
	"canvas_item": ".canvasitem.gdshader",
}


## Returns {"ok": bool, "skipped": bool, "log": String, "files": PackedStringArray}.
## `shader_type` is "spatial", "canvas_item" or "both". Shaders that fail to translate for the chosen
## type are reported in `log` and make `ok` false; the ones that did translate are still written.
static func regenerate(project: String, output_dir: String, shader_type: String, command: String) -> Dictionary:
	var result := {"ok": true, "skipped": false, "log": "", "files": PackedStringArray()}

	if project.strip_edges().is_empty():
		result["skipped"] = true
		result["log"] = "YotsubaShader: set Project Settings > yotsuba_shader/project to your shaders .csproj."
		return result

	var project_path := _resolve(project)
	var output_path := _resolve(output_dir)
	if not FileAccess.file_exists(project_path):
		result["ok"] = false
		result["log"] = "YotsubaShader: project not found: %s" % project_path
		return result

	DirAccess.make_dir_recursive_absolute(output_path)

	# A single path to an executable may contain spaces; otherwise the command is split into words.
	var parts := PackedStringArray([command.strip_edges()]) if FileAccess.file_exists(command.strip_edges()) \
		else command.strip_edges().split(" ", false)
	if parts.is_empty() or parts[0].is_empty():
		parts = PackedStringArray(["dotnet", "yotsuba"])
	var program := parts[0]
	var args := PackedStringArray()
	for i in range(1, parts.size()):
		args.append(parts[i])
	args.append_array(["compile", project_path, "--target", "godot", "--output", output_path])

	var lines: Array = []
	var exit_code := OS.execute(program, args, lines, true)
	var text := ""
	for line in lines:
		text += str(line)
	result["log"] = text

	if exit_code == -1:
		result["ok"] = false
		result["log"] = "YotsubaShader: could not start '%s'. Install the tool (dotnet tool install -g YotsubaShader.Compiler.CLI) or set yotsuba_shader/command to its full path.\n%s" % [program, text]
		return result

	# Keep only the type(s) the project uses, so stale or unwanted variants do not pile up.
	var keep: PackedStringArray = []
	for key in SHADER_TYPES:
		if shader_type == "both" or shader_type == key:
			keep.append(SHADER_TYPES[key])
	var files := PackedStringArray()
	for file in _gdshaders_under(output_path):
		var wanted := false
		for suffix in keep:
			if file.ends_with(suffix):
				wanted = true
		if wanted:
			files.append(file)
		else:
			DirAccess.remove_absolute(file)
	result["files"] = files

	# A failure only matters for the types the project asked for: a 3D shader that cannot be a
	# canvas_item is not an error when the project only uses spatial. If nothing was produced at
	# all (for example the C# project does not build) it is always an error.
	if exit_code != 0:
		var relevant := files.is_empty()
		for suffix in keep:
			if text.find(suffix) != -1:
				relevant = true
		result["ok"] = not relevant
	return result


## The CLI mirrors the namespace folders of each shader, so the files can be nested.
static func _gdshaders_under(directory: String) -> PackedStringArray:
	var found := PackedStringArray()
	for file in DirAccess.get_files_at(directory):
		if file.ends_with(".gdshader"):
			found.append(directory.path_join(file))
	for sub in DirAccess.get_directories_at(directory):
		found.append_array(_gdshaders_under(directory.path_join(sub)))
	return found


static func _resolve(path: String) -> String:
	if path.begins_with("res://") or path.begins_with("user://"):
		return ProjectSettings.globalize_path(path)
	if path.is_absolute_path():
		return path
	return ProjectSettings.globalize_path("res://").path_join(path).simplify_path()
