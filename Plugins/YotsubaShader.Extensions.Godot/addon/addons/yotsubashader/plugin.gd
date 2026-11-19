@tool
extends EditorPlugin
## YotsubaShader for Godot: regenerates the .gdshader files from the C# shaders before the project runs.

const Generator := preload("res://addons/yotsubashader/generator.gd")

const SETTING_PROJECT := "yotsuba_shader/project"
const SETTING_OUTPUT := "yotsuba_shader/output_dir"
const SETTING_TYPE := "yotsuba_shader/shader_type"
const SETTING_COMMAND := "yotsuba_shader/command"
const SETTING_ON_RUN := "yotsuba_shader/regenerate_on_run"

const MENU_TITLE := "Regenerate YotsubaShader shaders"


func _enter_tree() -> void:
	_define(SETTING_PROJECT, "", TYPE_STRING, PROPERTY_HINT_GLOBAL_FILE, "*.csproj")
	_define(SETTING_OUTPUT, "res://yotsuba_shaders", TYPE_STRING, PROPERTY_HINT_DIR, "")
	_define(SETTING_TYPE, "spatial", TYPE_STRING, PROPERTY_HINT_ENUM, "spatial,canvas_item,both")
	_define(SETTING_COMMAND, "dotnet yotsuba", TYPE_STRING, PROPERTY_HINT_NONE, "")
	_define(SETTING_ON_RUN, true, TYPE_BOOL, PROPERTY_HINT_NONE, "")
	add_tool_menu_item(MENU_TITLE, _on_menu)


func _exit_tree() -> void:
	remove_tool_menu_item(MENU_TITLE)


## Called by the editor right before it runs the project.
func _build() -> bool:
	if ProjectSettings.get_setting(SETTING_ON_RUN, true):
		_regenerate()
	return true  # a shader that fails to translate is reported, but it never blocks running the game


func _on_menu() -> void:
	_regenerate()


func _regenerate() -> void:
	var result: Dictionary = Generator.regenerate(
		str(ProjectSettings.get_setting(SETTING_PROJECT, "")),
		str(ProjectSettings.get_setting(SETTING_OUTPUT, "res://yotsuba_shaders")),
		str(ProjectSettings.get_setting(SETTING_TYPE, "spatial")),
		str(ProjectSettings.get_setting(SETTING_COMMAND, "dotnet yotsuba")))

	var log_text: String = str(result["log"]).strip_edges()
	if result["skipped"]:
		push_warning(log_text)
		return
	if not result["ok"]:
		push_error("YotsubaShader: some shaders could not be translated.\n" + log_text)
	else:
		if not log_text.is_empty():
			print_rich("[color=gray]YotsubaShader:[/color] ", log_text)
		print("YotsubaShader: %d shader file(s) up to date." % result["files"].size())
	EditorInterface.get_resource_filesystem().scan()


func _define(name: String, default_value: Variant, type: int, hint: int, hint_string: String) -> void:
	if not ProjectSettings.has_setting(name):
		ProjectSettings.set_setting(name, default_value)
	ProjectSettings.set_initial_value(name, default_value)
	ProjectSettings.add_property_info({"name": name, "type": type, "hint": hint, "hint_string": hint_string})
