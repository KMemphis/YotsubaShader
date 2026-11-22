@tool
extends SyntaxHighlighter
## Lexical highlighting (keywords, strings, comments, numbers) from a CodeHighlighter, with Roslyn's semantic
## classification painted over the identifiers: classes, structs, enums, interfaces, methods, parameters, fields...

var base: CodeHighlighter:
	set(value):
		base = value
		# A highlighter only reads text through the editor it is attached to, so the lexical pass runs on a
		# hidden copy of the text.
		_mirror.syntax_highlighter = base
var _mirror := CodeEdit.new()
## line -> Array of [start, end, Color]
var _by_line := {}


func sync_text(text: String) -> void:
	if _mirror.text != text:
		_mirror.text = text
		clear_highlighting_cache()


func set_spans(spans: Array, colors: Dictionary) -> void:
	_by_line.clear()
	for span in spans:
		var color = colors.get(str(span[3]))
		if color == null:
			continue
		var line := int(round(float(span[0])))
		if not _by_line.has(line):
			_by_line[line] = []
		_by_line[line].append([int(round(float(span[1]))), int(round(float(span[2]))), color])
	clear_highlighting_cache()
	update_cache()


func _get_line_syntax_highlighting(line: int) -> Dictionary:
	var lexical: Dictionary = base.get_line_syntax_highlighting(line)
	if not _by_line.has(line):
		return lexical
	var result := lexical.duplicate()
	var columns := lexical.keys()
	columns.sort()
	for span in _by_line[line]:
		var start: int = span[0]
		var finish: int = span[1]
		# The lexical color in effect where the span ends, so the text after it keeps its own color.
		var after: Dictionary = {"color": get_text_edit().get_theme_color("font_color")}
		for column in columns:
			if column <= finish:
				after = lexical[column]
		# A span the lexical pass already split (a keyword, a string edge) is left as it is.
		var inside := false
		for column in columns:
			if column > start and column < finish:
				inside = true
		if inside:
			continue
		result[start] = {"color": span[2]}
		if not result.has(finish):
			result[finish] = after
	return result
