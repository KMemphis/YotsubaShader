@tool
extends HSplitContainer
## Main-screen editor for the C# shaders: a CodeEdit with C# highlighting and live diagnostics from `yotsuba lsp`
## (the Roslyn compiler plus the YotsubaShader rules, YS....).

const LspClient := preload("res://addons/yotsubashader/lsp_client.gd")
const SemanticHighlighter := preload("res://addons/yotsubashader/semantic_highlighter.gd")

const ERROR_COLOR := Color(0.95, 0.35, 0.35)
const WARNING_COLOR := Color(0.95, 0.8, 0.3)

const HIDDEN_FOLDERS := ["bin", "obj", "node_modules"]
const TEXT_EXTENSIONS := ["cs", "csproj", "props", "targets", "json", "md", "txt", "editorconfig", "xml", "config", "sln", "slnx"]
const KEYWORDS := ["abstract", "as", "base", "break", "case", "catch", "checked", "class", "const", "continue", "default",
	"delegate", "do", "else", "enum", "event", "explicit", "extern", "finally", "fixed", "for", "foreach", "goto", "if",
	"implicit", "in", "interface", "internal", "is", "lock", "namespace", "new", "operator", "out", "override", "params",
	"private", "protected", "public", "readonly", "ref", "return", "sealed", "sizeof", "stackalloc", "static", "struct",
	"switch", "this", "throw", "try", "typeof", "unchecked", "unsafe", "using", "virtual", "volatile", "while", "var",
	"true", "false", "null", "get", "set", "record", "required", "init"]
const CONTROL_KEYWORDS := ["if", "else", "for", "foreach", "while", "return", "switch", "case", "break", "continue", "do",
	"goto", "throw", "try", "catch", "finally", "yield"]
const TYPES := ["bool", "byte", "char", "decimal", "double", "float", "int", "long", "object", "sbyte", "short", "string",
	"uint", "ulong", "ushort", "void", "float2", "float3", "float4", "int2", "int3", "int4", "uint2", "uint3", "uint4",
	"float2x2", "float3x3", "float4x4", "Vector2", "Vector3", "Vector4", "Matrix"]

## Visual Studio's own colors (Dark and Light), used for the editor surface and the highlighting.
const VS_DARK := {
	"background": Color("1e1e1e"), "text": Color("dcdcdc"), "caret": Color("ffffff"), "selection": Color("264f78"),
	"current_line": Color(1, 1, 1, 0.045), "line_number": Color("2b91af"), "completion_selected": Color("04395e"),
	"keyword": Color("569cd6"), "control": Color("d8a0df"), "type": Color("4ec9b0"), "string": Color("d69d85"),
	"number": Color("b5cea8"), "comment": Color("57a64a"), "method": Color("dcdcaa"), "symbol": Color("b4b4b4"),
	"parameter": Color("9cdcfe"), "local": Color("9cdcfe"), "member": Color("dcdcdc"), "enum_member": Color("4fc1ff"),
}
const VS_LIGHT := {
	"background": Color("ffffff"), "text": Color("000000"), "caret": Color("000000"), "selection": Color("add6ff"),
	"current_line": Color(0, 0, 0, 0.04), "line_number": Color("2b91af"), "completion_selected": Color("cce8ff"),
	"keyword": Color("0000ff"), "control": Color("8f08c4"), "type": Color("2b91af"), "string": Color("a31515"),
	"number": Color("000000"), "comment": Color("008000"), "method": Color("74531f"), "symbol": Color("000000"),
	"parameter": Color("1f377f"), "local": Color("1f377f"), "member": Color("000000"), "enum_member": Color("0070c1"),
}

var theme_name := "vs_dark"  # "vs_dark", "vs_light" or "godot" (the editor's own colors)
var project_path := ""
var command := "dotnet yotsuba"

var _client: RefCounted
var _files: Tree
var _name_dialog: ConfirmationDialog
var _name_edit: LineEdit
var _name_is_folder := false
var _delete_dialog: ConfirmationDialog
var _file_menu: PopupMenu
var _context_path := ""  # folder or file the toolbar and menu act on
var _editor: CodeEdit
var _problems: ItemList
var _status: Label
var _save_button: Button
var _title: Label

var _open_path := ""
var _version := 0
var _dirty := false
var _diagnostics: Array = []
var _hover_text := ""
var _hover_timer: Timer
var _hover_at := Vector2i(-1, -1)
var _completion_version := 0
var _loading := false
var _palette_now: Dictionary = {}
var _declared_types := {}
var _highlighter: CodeHighlighter
var _semantic: SyntaxHighlighter
var _saved_text := ""
var _quick_menu: PopupMenu
var _quick_actions: Array = []

## Diagnostics that a quick fix can solve by adding a `using`.
const IMPORTABLE := ["CS0246", "CS0103", "CS0234", "CS0305"]

const KIND_MAP := {2: CodeEdit.KIND_FUNCTION, 3: CodeEdit.KIND_FUNCTION, 5: CodeEdit.KIND_MEMBER, 10: CodeEdit.KIND_MEMBER,
	6: CodeEdit.KIND_VARIABLE, 7: CodeEdit.KIND_CLASS, 8: CodeEdit.KIND_CLASS, 9: CodeEdit.KIND_CLASS,
	22: CodeEdit.KIND_CLASS, 13: CodeEdit.KIND_ENUM}


func _ready() -> void:
	_build_ui()


func _exit_tree() -> void:
	shutdown()


## Called when the main screen becomes visible: lists the C# files and starts the language server once.
func activate() -> void:
	if project_path.is_empty() or not FileAccess.file_exists(project_path):
		_status.text = "Set Project Settings > yotsuba_shader/project to your shaders .csproj."
		return
	if _files.get_root() == null:
		_list_files(project_path.get_base_dir())
	if _client == null:
		_start_server()


func shutdown() -> void:
	if _client != null:
		_client.stop()
		_client = null


func _build_ui() -> void:
	size_flags_horizontal = Control.SIZE_EXPAND_FILL
	size_flags_vertical = Control.SIZE_EXPAND_FILL

	var left := VBoxContainer.new()
	left.custom_minimum_size.x = 230
	add_child(left)
	var header := HBoxContainer.new()
	left.add_child(header)
	var files_label := Label.new()
	files_label.text = "Project"
	files_label.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	header.add_child(files_label)
	header.add_child(_icon_button("New file", "New file in the selected folder", func() -> void: _ask_name(false)))
	header.add_child(_icon_button("New folder", "New folder in the selected folder", func() -> void: _ask_name(true)))
	header.add_child(_icon_button("Delete", "Move the selected file or folder to the trash", _ask_delete))
	_files = Tree.new()
	_files.size_flags_vertical = Control.SIZE_EXPAND_FILL
	_files.hide_root = true
	_files.select_mode = Tree.SELECT_SINGLE
	_files.allow_rmb_select = true
	_files.item_selected.connect(_on_file_selected)
	_files.item_mouse_selected.connect(_on_file_mouse_selected)
	_files.item_activated.connect(func() -> void: _toggle_folder(_files.get_selected()))
	left.add_child(_files)

	_file_menu = PopupMenu.new()
	_file_menu.add_item("New file", 0)
	_file_menu.add_item("New folder", 1)
	_file_menu.add_separator()
	_file_menu.add_item("Delete", 2)
	_file_menu.id_pressed.connect(_on_file_menu)
	add_child(_file_menu)

	_name_dialog = ConfirmationDialog.new()
	_name_edit = LineEdit.new()
	_name_edit.custom_minimum_size.x = 280
	_name_dialog.add_child(_name_edit)
	_name_dialog.register_text_enter(_name_edit)
	_name_dialog.confirmed.connect(_create_entry)
	add_child(_name_dialog)

	_delete_dialog = ConfirmationDialog.new()
	_delete_dialog.ok_button_text = "Move to trash"
	_delete_dialog.confirmed.connect(_delete_entry)
	add_child(_delete_dialog)

	var right := VBoxContainer.new()
	right.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	add_child(right)

	var bar := HBoxContainer.new()
	right.add_child(bar)
	_title = Label.new()
	_title.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	bar.add_child(_title)
	_status = Label.new()
	bar.add_child(_status)
	_save_button = Button.new()
	_save_button.text = "Save"
	_save_button.disabled = true
	_save_button.pressed.connect(_save)
	bar.add_child(_save_button)

	var split := VSplitContainer.new()
	split.size_flags_vertical = Control.SIZE_EXPAND_FILL
	right.add_child(split)
	split.set_deferred("split_offset", -170)  # leaves room for the problem list under the editor

	_editor = CodeEdit.new()
	_editor.size_flags_vertical = Control.SIZE_EXPAND_FILL
	_editor.gutters_draw_line_numbers = true
	_editor.indent_use_spaces = true
	_editor.indent_size = 4
	_editor.indent_automatic = true
	_editor.auto_brace_completion_enabled = true
	_editor.line_folding = true
	_editor.gutters_draw_fold_gutter = true
	_editor.editable = false
	_editor.code_completion_enabled = true
	_editor.code_completion_prefixes = ["."]
	_editor.code_completion_requested.connect(_on_completion_requested)
	_editor.text_changed.connect(_on_text_changed)
	_editor.draw.connect(_draw_squiggles)
	_editor.gui_input.connect(_on_editor_input)
	split.add_child(_editor)

	_problems = ItemList.new()
	_problems.custom_minimum_size.y = 120
	_problems.item_selected.connect(_on_problem_selected)
	split.add_child(_problems)

	_quick_menu = PopupMenu.new()
	_quick_menu.id_pressed.connect(_on_quick_fix_chosen)
	add_child(_quick_menu)

	_hover_timer = Timer.new()
	_hover_timer.one_shot = true
	_hover_timer.wait_time = 0.3
	_hover_timer.timeout.connect(_request_hover)
	add_child(_hover_timer)

	_apply_editor_theme()
	apply_theme(theme_name)


func _apply_editor_theme() -> void:
	if not Engine.is_editor_hint():
		return
	var theme := EditorInterface.get_editor_theme()
	if theme.has_font("source", "EditorFonts"):
		_editor.add_theme_font_override("font", theme.get_font("source", "EditorFonts"))
		_editor.add_theme_font_size_override("font_size", theme.get_font_size("source_size", "EditorFonts"))


## Applies a color theme: "vs_dark", "vs_light" or "godot".
func apply_theme(name: String) -> void:
	theme_name = name
	_palette_now = _palette(name)
	_highlighter = _make_highlighter(_palette_now)
	_semantic = SemanticHighlighter.new()
	_semantic.base = _highlighter
	_editor.syntax_highlighter = _semantic
	_semantic.sync_text(_editor.text)
	_request_semantic_colors()
	_style_editor(_palette_now)
	_declared_types.clear()
	_note_declared_types(_editor.text)


func _palette(name: String) -> Dictionary:
	match name:
		"vs_dark":
			return VS_DARK
		"vs_light":
			return VS_LIGHT
	var settings: EditorSettings = EditorInterface.get_editor_settings() if Engine.is_editor_hint() else null
	var text := _setting_color(settings, "text_color", Color(0.88, 0.88, 0.88))
	return {
		"text": text, "method": text,
		"keyword": _setting_color(settings, "keyword_color", Color(1.0, 0.44, 0.52)),
		"control": _setting_color(settings, "control_flow_keyword_color", Color(1.0, 0.55, 0.8)),
		"type": _setting_color(settings, "base_type_color", Color(0.26, 1.0, 0.76)),
		"comment": _setting_color(settings, "comment_color", Color(0.8, 0.81, 0.82, 0.5)),
		"string": _setting_color(settings, "string_color", Color(1.0, 0.93, 0.63)),
		"number": _setting_color(settings, "number_color", Color(0.63, 1.0, 0.88)),
		"symbol": _setting_color(settings, "symbol_color", Color(0.67, 0.79, 1.0)),
	}


## Editor surface: background, text, caret, selection, line numbers, completion list. Without a background in the
## palette (the "godot" theme) the overrides are removed and the editor looks like Godot's own.
func _style_editor(palette: Dictionary) -> void:
	var overrides := {
		"background_color": "background", "font_color": "text", "caret_color": "caret", "selection_color": "selection",
		"current_line_color": "current_line", "line_number_color": "line_number",
		"completion_background_color": "background", "completion_selected_color": "completion_selected",
		"completion_existing_color": "current_line",
	}
	for theme_color in overrides:
		var key: String = overrides[theme_color]
		if palette.has(key):
			_editor.add_theme_color_override(theme_color, palette[key])
		else:
			_editor.remove_theme_color_override(theme_color)


func _make_highlighter(p: Dictionary) -> CodeHighlighter:
	var highlighter := CodeHighlighter.new()
	highlighter.number_color = p["number"]
	highlighter.symbol_color = p["symbol"]
	highlighter.function_color = p["method"]  # an identifier followed by "(", like Visual Studio's method names
	highlighter.member_variable_color = p["text"]
	for word in KEYWORDS:
		highlighter.add_keyword_color(word, p["control"] if word in CONTROL_KEYWORDS else p["keyword"])
	for word in TYPES:
		highlighter.add_keyword_color(word, p["type"])
	highlighter.add_color_region("//", "", p["comment"], true)
	highlighter.add_color_region("/*", "*/", p["comment"])
	highlighter.add_color_region("\"", "\"", p["string"])
	return highlighter


## Types declared in the file (class, struct, enum, interface, record) get the type color wherever they are used.
func _note_declared_types(text: String) -> void:
	if _highlighter == null or _palette_now.is_empty():
		return
	var declaration := RegEx.create_from_string("\\b(?:class|struct|enum|interface|record)\\s+([A-Za-z_]\\w*)")
	for found in declaration.search_all(text):
		var name := found.get_string(1)
		if not _declared_types.has(name):
			_declared_types[name] = true
			_highlighter.add_keyword_color(name, _palette_now["type"])
	_editor.queue_redraw()


func _setting_color(settings: EditorSettings, key: String, fallback: Color) -> Color:
	if settings != null and settings.has_setting("text_editor/theme/highlighting/" + key):
		return settings.get_setting("text_editor/theme/highlighting/" + key)
	return fallback


func _icon_button(text: String, tip: String, action: Callable) -> Button:
	var button := Button.new()
	button.text = text
	button.tooltip_text = tip
	button.flat = true
	button.pressed.connect(action)
	return button


## Shows the whole project folder as a tree. Only text files open in the editor; build output stays hidden.
func _list_files(directory: String) -> void:
	var remembered := _open_path
	_files.clear()
	var root := _files.create_item()
	root.set_metadata(0, directory)
	_fill_folder(root, directory)
	if not remembered.is_empty():
		_select_path(root, remembered)


func _fill_folder(parent: TreeItem, directory: String) -> void:
	var folders := Array(DirAccess.get_directories_at(directory))
	folders.sort_custom(func(a: String, b: String) -> bool: return a.naturalnocasecmp_to(b) < 0)
	for sub in folders:
		if sub in HIDDEN_FOLDERS or sub.begins_with("."):
			continue
		var item := _files.create_item(parent)
		item.set_text(0, sub)
		item.set_icon(0, _theme_icon("Folder"))
		item.set_icon_modulate(0, _entry_color("folder"))
		item.set_metadata(0, directory.path_join(sub))
		item.set_meta("folder", true)
		_fill_folder(item, directory.path_join(sub))
	var files := Array(DirAccess.get_files_at(directory))
	files.sort_custom(func(a: String, b: String) -> bool: return a.naturalnocasecmp_to(b) < 0)
	for file in files:
		if file.ends_with(".g.cs") or file.begins_with("."):
			continue
		var item := _files.create_item(parent)
		item.set_text(0, file)
		var kind := _entry_kind(file)
		item.set_icon(0, _theme_icon(ICON_NAMES[kind]))
		item.set_icon_modulate(0, _entry_color(kind))
		if kind != "text":
			item.set_custom_color(0, _entry_color(kind))
		item.set_metadata(0, directory.path_join(file))
		if not _is_text_file(file):
			item.set_custom_color(0, Color(1, 1, 1, 0.35))
			item.set_tooltip_text(0, "Not a text file: it is not opened here")


## Colors of the project tree by kind of entry, in the spirit of Visual Studio's solution explorer.
const ENTRY_COLORS_DARK := {"folder": Color("dcb67a"), "cs": Color("4ec9b0"), "project": Color("c586c0"),
	"config": Color("9cdcfe"), "text": Color("c8c8c8")}
const ENTRY_COLORS_LIGHT := {"folder": Color("b8862b"), "cs": Color("2b91af"), "project": Color("8f08c4"),
	"config": Color("1f6fb5"), "text": Color("444444")}
const ICON_NAMES := {"cs": "Script", "project": "Object", "config": "TextFile", "text": "TextFile"}


func _entry_kind(file: String) -> String:
	match file.get_extension().to_lower():
		"cs":
			return "cs"
		"csproj", "sln", "slnx", "props", "targets":
			return "project"
		"json", "xml", "config", "editorconfig":
			return "config"
	return "text"


func _entry_color(kind: String) -> Color:
	var table := ENTRY_COLORS_LIGHT if theme_name == "vs_light" else ENTRY_COLORS_DARK
	return table[kind]


func _is_text_file(file: String) -> bool:
	return file.get_extension().to_lower() in TEXT_EXTENSIONS or file in ["Directory.Build.props", ".editorconfig"]


func _theme_icon(name: String) -> Texture2D:
	if Engine.is_editor_hint() and EditorInterface.get_editor_theme().has_icon(name, "EditorIcons"):
		return EditorInterface.get_editor_theme().get_icon(name, "EditorIcons")
	return null


func _select_path(item: TreeItem, path: String) -> bool:
	for child in item.get_children():
		if child.get_metadata(0) == path:
			child.select(0)
			return true
		if child.has_meta("folder") and path.begins_with(str(child.get_metadata(0)) + "/") and _select_path(child, path):
			return true
	return false


func _toggle_folder(item: TreeItem) -> void:
	if item != null and item.has_meta("folder"):
		item.collapsed = not item.collapsed


## The folder new entries go into: the selected folder, the folder of the selected file, or the project root.
func _target_folder() -> String:
	var path := _context_path
	if path.is_empty():
		var selected := _files.get_selected()
		path = str(selected.get_metadata(0)) if selected != null else project_path.get_base_dir()
	return path if DirAccess.dir_exists_absolute(path) else path.get_base_dir()


func _ask_name(is_folder: bool) -> void:
	_name_is_folder = is_folder
	_name_dialog.title = "New folder" if is_folder else "New file"
	_name_edit.text = "" if is_folder else "NewShader.cs"
	var base := _target_folder().trim_prefix(project_path.get_base_dir()).trim_prefix("/")
	_name_dialog.dialog_text = "In: " + (base if not base.is_empty() else "(project root)")
	_name_dialog.popup_centered()
	_name_edit.grab_focus()
	_name_edit.select(0, _name_edit.text.length() - (3 if not is_folder else 0))


func _create_entry() -> void:
	var entry := _name_edit.text.strip_edges()
	var folder := _target_folder()
	_context_path = ""
	if entry.is_empty() or entry.contains("/") or entry.contains("\\") or entry.begins_with("."):
		_status.text = "Invalid name: %s" % entry
		return
	var path := folder.path_join(entry)
	if FileAccess.file_exists(path) or DirAccess.dir_exists_absolute(path):
		_status.text = "%s already exists" % entry
		return
	if _name_is_folder:
		DirAccess.make_dir_recursive_absolute(path)
	else:
		if entry.get_extension().is_empty():
			path += ".cs"
		var file := FileAccess.open(path, FileAccess.WRITE)
		if file == null:
			_status.text = "Could not create %s" % path.get_file()
			return
		file.store_string(_template_for(path))
		file.close()
	_list_files(project_path.get_base_dir())
	if not _name_is_folder:
		_select_path(_files.get_root(), path)  # opens the new file


func _template_for(path: String) -> String:
	if path.get_extension().to_lower() != "cs":
		return ""
	var space := project_path.get_file().get_basename()
	return "using System;\n\nnamespace %s;\n\n" % space


func _ask_delete() -> void:
	var item := _files.get_selected()
	if item == null and _context_path.is_empty():
		return
	var path := _context_path if not _context_path.is_empty() else str(item.get_metadata(0))
	if path == project_path.get_base_dir() or path == project_path:
		_status.text = "The project root and its .csproj cannot be deleted here."
		_context_path = ""
		return
	_context_path = path
	var what := "folder and everything inside it" if DirAccess.dir_exists_absolute(path) else "file"
	_delete_dialog.title = "Delete"
	_delete_dialog.dialog_text = "Move %s to the trash?\n(%s)" % [path.get_file(), what]
	_delete_dialog.popup_centered()


func _delete_entry() -> void:
	var path := _context_path
	_context_path = ""
	if path.is_empty():
		return
	if _open_path == path or _open_path.begins_with(path + "/"):
		_dirty = false  # the file is going away: do not save it back
		_close_current()
		_editor.editable = false
		_loading = true
		_editor.text = ""
		set_deferred("_loading", false)
		_diagnostics = []
		_refresh_problems()
		_title.text = ""
	var result := OS.move_to_trash(ProjectSettings.globalize_path(path) if path.begins_with("res://") else path)
	_status.text = "Moved %s to the trash" % path.get_file() if result == OK else "Could not delete %s" % path.get_file()
	_list_files(project_path.get_base_dir())


func _on_file_mouse_selected(_position: Vector2, mouse_button: int) -> void:
	if mouse_button != MOUSE_BUTTON_RIGHT:
		return
	var item := _files.get_selected()
	_context_path = str(item.get_metadata(0)) if item != null else project_path.get_base_dir()
	_file_menu.popup(Rect2i(Vector2i(DisplayServer.mouse_get_position()), Vector2i.ZERO))


func _on_file_menu(id: int) -> void:
	match id:
		0: _ask_name(false)
		1: _ask_name(true)
		2: _ask_delete()


func _start_server() -> void:
	_client = LspClient.new()
	_client.diagnostics_received.connect(_on_diagnostics)
	_client.log_received.connect(_on_server_log)
	_status.text = "Starting language server..."
	if not _client.start(command, project_path):
		_status.text = "Could not start '%s'. Set yotsuba_shader/lsp_command." % command
		_client = null


func _on_file_selected() -> void:
	var item := _files.get_selected()
	if item == null or item.has_meta("folder"):
		return
	var path: String = item.get_metadata(0)
	if path == _open_path or not _is_text_file(path.get_file()):
		return
	if _dirty:
		_save()
	_close_current()
	var file := FileAccess.open(path, FileAccess.READ)
	if file == null:
		_status.text = "Could not read %s" % path
		return
	_open_path = path
	_version = 1
	_editor.editable = true
	_loading = true  # setting the text must not count as an edit
	_editor.text = file.get_as_text()
	_editor.clear_undo_history()
	set_deferred("_loading", false)
	_declared_types.clear()
	_note_declared_types(_editor.text)
	_semantic.sync_text(_editor.text)
	_saved_text = _editor.text
	_dirty = false
	_diagnostics = []
	_refresh_problems()
	_update_title()
	if _client != null and path.get_extension().to_lower() == "cs":
		_client.open_document(path, _editor.text)
		_status.text = "Analyzing..."
	else:
		_status.text = ""


func _close_current() -> void:
	if not _open_path.is_empty() and _client != null:
		_client.close_document(_open_path)
	_open_path = ""


func _on_text_changed() -> void:
	if _open_path.is_empty() or _loading:
		return
	_dirty = _editor.text != _saved_text  # undoing back to the saved text makes it clean again
	_version += 1
	_update_title()
	if _client != null:
		_client.change_document(_open_path, _editor.text, _version)
		_status.text = "Analyzing..."
		_note_declared_types(_editor.get_line(_editor.get_caret_line()))
		_semantic.sync_text(_editor.text)
		_trigger_completion()


## Asks for completion after an identifier character or a dot. CodeEdit's own trigger depends on how the text was
## typed, so the panel starts it itself on every edit.
func _trigger_completion() -> void:
	var column := _editor.get_caret_column()
	if column <= 0:
		return
	var typed := _editor.get_line(_editor.get_caret_line())[column - 1]
	if typed == "." or typed == "_" or typed.is_valid_identifier() or typed.is_valid_int():
		_editor.request_code_completion.call_deferred(true)


func _save() -> void:
	if _open_path.is_empty():
		return
	var file := FileAccess.open(_open_path, FileAccess.WRITE)
	if file == null:
		_status.text = "Could not write %s" % _open_path
		return
	file.store_string(_editor.text)
	file.close()
	_saved_text = _editor.text
	_dirty = false
	_update_title()


func _update_title() -> void:
	_title.text = _open_path.get_file() + (" (modified)" if _dirty else "")
	_save_button.disabled = not _dirty


func _on_editor_input(event: InputEvent) -> void:
	if event is InputEventKey and event.pressed and event.keycode in [KEY_TAB, KEY_ENTER, KEY_KP_ENTER]:
		_remember_import_of_selected_completion()
	if event is InputEventKey and event.pressed and event.keycode == KEY_S and event.is_command_or_control_pressed():
		_save()
		_editor.accept_event()
	elif event is InputEventKey and event.pressed and (
			(event.keycode == KEY_PERIOD and event.is_command_or_control_pressed()) or (event.keycode == KEY_ENTER and event.alt_pressed)):
		_show_quick_fixes()
		_editor.accept_event()
	elif event is InputEventMouseMotion:
		var position := Vector2i(event.position)
		var at := _editor.get_line_column_at_pos(position, false)
		_hover_text = ""
		for item in _diagnostics:
			if _covers(item, at.y, at.x):
				_hover_text += ("" if _hover_text.is_empty() else "\n") + "%s: %s%s" % [item["code"], item["message"], _import_hint(item)]
		_editor.tooltip_text = _hover_text
		# No problem under the mouse: ask the language server what the symbol is.
		if _hover_text.is_empty() and _client != null and not _editor.get_word_at_pos(position).is_empty():
			if at != _hover_at:
				_hover_at = at
				_hover_timer.start()
		else:
			_hover_at = Vector2i(-1, -1)
			_hover_timer.stop()


func _request_hover() -> void:
	if _client == null or _open_path.is_empty() or _hover_at.y < 0:
		return
	var asked := _hover_at
	_client.request_hover(_open_path, asked.y, asked.x, func(text: String) -> void:
		if text.is_empty() or asked != _hover_at:
			return
		_editor.tooltip_text = text)


## Completion: ask for the items at the caret and let CodeEdit show, filter and insert them.
func _on_completion_requested() -> void:
	if _client == null or _open_path.is_empty():
		return
	_completion_version += 1
	var version := _completion_version
	var line := _editor.get_caret_line()
	var column := _editor.get_caret_column()
	_client.request_completion(_open_path, line, column, func(items: Array) -> void:
		if version != _completion_version or items.is_empty():
			return
		items.sort_custom(func(a, b): return str(a.get("sortText", a["label"])) < str(b.get("sortText", b["label"])))
		var prefix := _word_before_caret()
		var rank := 0
		for item in items:
			var kind: int = KIND_MAP.get(int(item.get("kind", 1)), CodeEdit.KIND_PLAIN_TEXT)
			# CodeEdit sorts by `location`: what starts with the typed word goes first, then Roslyn's own order.
			var label := str(item["label"])
			var location := (0 if prefix.is_empty() or label.begins_with(prefix) else 100000) + rank
			rank += 1
			# An item that also needs a `using` carries that edit; it is applied when the item is accepted.
			var value = {"edits": item["additionalTextEdits"]} if item.has("additionalTextEdits") else null
			_editor.add_code_completion_option(kind, label, str(item.get("insertText", label)), _kind_color(kind), null, value, location)
		_editor.update_code_completion_options(true))


## Runs while the completion list is open and Tab/Enter is pressed: if the chosen item needs a `using`, add it once the
## text has been inserted.
func _remember_import_of_selected_completion() -> void:
	var index := _editor.get_code_completion_selected_index()
	if index < 0 or _editor.get_code_completion_options().is_empty():
		return
	var value = _editor.get_code_completion_option(index).get("default_value")
	if value is Dictionary and value.has("edits"):
		_apply_edits.call_deferred(value["edits"])


## Applies LSP TextEdits (positions in the text before the edits), as one undo step, keeping the caret on its text.
func _apply_edits(edits: Array) -> void:
	if edits.is_empty():
		return
	var line := _editor.get_caret_line()
	var column := _editor.get_caret_column()
	var sorted := edits.duplicate()
	sorted.sort_custom(func(a, b):  # bottom to top, so the earlier positions stay valid
		var sa: Dictionary = a["range"]["start"]
		var sb: Dictionary = b["range"]["start"]
		return sa["line"] > sb["line"] or (sa["line"] == sb["line"] and sa["character"] > sb["character"]))
	var added_before_caret := 0
	_editor.begin_complex_operation()
	for edit in sorted:
		var start: Dictionary = edit["range"]["start"]
		var end: Dictionary = edit["range"]["end"]
		if end["line"] != start["line"] or end["character"] != start["character"]:
			_editor.remove_text(start["line"], start["character"], end["line"], end["character"])
		var new_text := str(edit["newText"])
		_editor.insert_text(new_text, start["line"], start["character"])
		if start["line"] <= line:
			added_before_caret += new_text.count("\n") - (end["line"] - start["line"])
	_editor.end_complex_operation()
	if _editor.get_caret_line() == line and added_before_caret != 0:
		_editor.set_caret_line(line + added_before_caret, false)
		_editor.set_caret_column(column)


func _import_hint(item: Dictionary) -> String:
	return "   [Ctrl+. to import]" if str(item["code"]) in IMPORTABLE else ""


## Ctrl/Cmd+. (or Alt+Enter): quick fixes for the problems at the caret.
func _show_quick_fixes() -> void:
	if _client == null or _open_path.is_empty():
		return
	var line := _editor.get_caret_line()
	var column := _editor.get_caret_column()
	var here: Array = []
	for item in _diagnostics:
		if _covers(item, line, column) or (item["range"]["start"]["line"] == line and str(item["code"]) in IMPORTABLE):
			here.append(item)
	if here.is_empty():
		_status.text = "No quick fixes here"
		return
	_client.request_code_actions(_open_path, here, func(actions: Array) -> void:
		_quick_actions = actions
		_quick_menu.clear()
		for i in actions.size():
			_quick_menu.add_item(str(actions[i]["title"]), i)
		if actions.is_empty():
			_status.text = "No quick fixes here"
			return
		var at := Vector2i(_editor.get_screen_position() + _editor.get_caret_draw_pos() + Vector2(0, 24))
		_quick_menu.popup(Rect2i(at, Vector2i.ZERO)))


func _on_quick_fix_chosen(id: int) -> void:
	var changes: Dictionary = _quick_actions[id].get("edit", {}).get("changes", {})
	for uri in changes:
		_apply_edits(changes[uri])
	_editor.grab_focus()


func _word_before_caret() -> String:
	var text := _editor.get_line(_editor.get_caret_line()).substr(0, _editor.get_caret_column())
	var start := text.length()
	while start > 0 and (text[start - 1] == "_" or text[start - 1].is_valid_identifier() or text[start - 1].is_valid_int()):
		start -= 1
	return text.substr(start)


func _kind_color(kind: int) -> Color:
	match kind:
		CodeEdit.KIND_FUNCTION: return Color(0.34, 0.7, 1.0)
		CodeEdit.KIND_MEMBER: return Color(0.55, 0.85, 0.7)
		CodeEdit.KIND_VARIABLE: return Color(0.95, 0.8, 0.5)
		CodeEdit.KIND_CLASS: return Color(0.3, 1.0, 0.75)
		CodeEdit.KIND_ENUM: return Color(0.8, 0.6, 1.0)
		_: return Color(0.85, 0.85, 0.85)


func _covers(item: Dictionary, line: int, column: int) -> bool:
	var start: Dictionary = item["range"]["start"]
	var end: Dictionary = item["range"]["end"]
	if line < start["line"] or line > end["line"]:
		return false
	if line == start["line"] and column < start["character"]:
		return false
	if line == end["line"] and column > max(end["character"], start["character"] + 1):
		return false
	return true


func _on_diagnostics(path: String, items: Array) -> void:
	if path != _open_path:
		return
	_diagnostics = items
	_refresh_problems()
	_request_semantic_colors()


## Colors by meaning: what Roslyn says each identifier is, mapped to the active theme's colors.
func _request_semantic_colors() -> void:
	if _client == null or _open_path.is_empty() or _semantic == null or not _open_path.ends_with(".cs"):
		return
	var asked := _open_path
	_client.request_classification(asked, func(spans: Array) -> void:
		if asked == _open_path and _semantic != null:
			_semantic.set_spans(spans, _semantic_colors()))


func _semantic_colors() -> Dictionary:
	var p := _palette_now
	var type_color: Color = p["type"]
	var colors := {}
	for kind in ["class name", "struct name", "enum name", "interface name", "delegate name", "record class name",
			"record struct name", "type parameter name", "static class name"]:
		colors[kind] = type_color
	colors["namespace name"] = p["text"]
	for kind in ["method name", "extension method name", "static method name"]:
		colors[kind] = p["method"]
	colors["parameter name"] = p.get("parameter", p["text"])
	colors["local name"] = p.get("local", p["text"])
	for kind in ["field name", "property name", "event name", "enum member name", "constant name"]:
		colors[kind] = p.get("member", p["text"])
	colors["enum member name"] = p.get("enum_member", p.get("member", p["text"]))
	colors["label name"] = p["text"]
	return colors


func _on_server_log(text: String) -> void:
	if text.contains("Proyecto cargado"):
		_status.text = "Ready"
	elif text.contains("No se pudo"):
		_status.text = "Project failed to load (see output)"
		push_warning(text)


func _refresh_problems() -> void:
	_problems.clear()
	var errors := 0
	var warnings := 0
	for item in _diagnostics:
		var is_error: bool = item["severity"] == 1
		errors += 1 if is_error else 0
		warnings += 0 if is_error else 1
		var line: int = item["range"]["start"]["line"] + 1
		var index := _problems.add_item("%s  %s  line %d: %s%s" % ["error" if is_error else "warning", item["code"], line, str(item["message"]).split("\n")[0], _import_hint(item)])
		_problems.set_item_custom_fg_color(index, ERROR_COLOR if is_error else WARNING_COLOR)
		_problems.set_item_metadata(index, item)

	for line in _editor.get_line_count():
		_editor.set_line_background_color(line, Color(0, 0, 0, 0))
	for item in _diagnostics:
		var tint := Color(0.95, 0.35, 0.35, 0.12) if item["severity"] == 1 else Color(0.95, 0.8, 0.3, 0.08)
		var line: int = item["range"]["start"]["line"]
		if line < _editor.get_line_count():
			_editor.set_line_background_color(line, tint)

	if _client != null and _status.text != "Starting language server...":
		_status.text = "%d error(s), %d warning(s)" % [errors, warnings]
	_editor.queue_redraw()


func _on_problem_selected(index: int) -> void:
	var item: Dictionary = _problems.get_item_metadata(index)
	var start: Dictionary = item["range"]["start"]
	_editor.set_caret_line(start["line"])
	_editor.set_caret_column(start["character"])
	_editor.center_viewport_to_caret()
	_editor.grab_focus()


## Wavy underline under each diagnostic, like an IDE.
func _draw_squiggles() -> void:
	var first := _editor.get_first_visible_line()
	var last := _editor.get_last_full_visible_line() + 1
	for item in _diagnostics:
		var start: Dictionary = item["range"]["start"]
		var end: Dictionary = item["range"]["end"]
		var line: int = start["line"]
		if line < first or line > last or line >= _editor.get_line_count():
			continue
		var line_length := _editor.get_line(line).length()
		# The ranges come from the last analysis; the text may already be shorter.
		if start["character"] > line_length:
			continue
		var end_column: int = end["character"] if end["line"] == line else line_length
		end_column = mini(maxi(end_column, start["character"] + 1), line_length)
		var from := _editor.get_rect_at_line_column(line, start["character"])
		var to := _editor.get_rect_at_line_column(line, end_column)
		if from.position.x < 0 or from.size.x < 0:
			continue
		var y := float(from.end.y) - 2.0
		var x := float(from.position.x)
		var x_end := maxf(float(to.position.x), x + 6.0)
		var color := ERROR_COLOR if item["severity"] == 1 else WARNING_COLOR
		var points := PackedVector2Array()
		var up := false
		while x < x_end:
			points.append(Vector2(x, y - (2.0 if up else 0.0)))
			up = not up
			x += 2.0
		if points.size() > 1:
			_editor.draw_polyline(points, color, 1.0)
