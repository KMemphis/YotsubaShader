@tool
extends RefCounted
## Minimal LSP client over the stdio of `yotsuba lsp <project.csproj>`.
## Two threads read the server (messages on stdout, logs on stderr) and hand everything to the main thread.

signal diagnostics_received(path: String, items: Array)
signal log_received(text: String)
signal stopped

var _stdio: FileAccess
var _stderr: FileAccess
var _pid := -1
var _running := false
var _reader: Thread
var _error_reader: Thread
var _next_id := 1
var _pending := {}  # request id -> Callable that receives the result


## `command` is the same setting as for the generator ("dotnet yotsuba", or a path to an executable).
func start(command: String, project_path: String) -> bool:
	var parts := _split(command)
	var args := PackedStringArray()
	for i in range(1, parts.size()):
		args.append(parts[i])
	args.append_array(["lsp", project_path])

	var pipe := OS.execute_with_pipe(parts[0], args, false)
	if pipe.is_empty():
		return false
	_stdio = pipe["stdio"]
	_stderr = pipe["stderr"]
	_pid = pipe["pid"]
	_running = true
	_reader = Thread.new()
	_reader.start(_read_messages)
	_error_reader = Thread.new()
	_error_reader.start(_read_logs)

	_request("initialize", {"processId": OS.get_process_id(), "rootUri": null, "capabilities": {}})
	_notify("initialized", {})
	return true


func is_running() -> bool:
	return _running


func open_document(path: String, text: String) -> void:
	_notify("textDocument/didOpen", {"textDocument": {"uri": to_uri(path), "languageId": "csharp", "version": 1, "text": text}})


func change_document(path: String, text: String, version: int) -> void:
	_notify("textDocument/didChange", {"textDocument": {"uri": to_uri(path), "version": version}, "contentChanges": [{"text": text}]})


func close_document(path: String) -> void:
	_notify("textDocument/didClose", {"textDocument": {"uri": to_uri(path)}})


## Asks for the completion items at a position; `callback` receives the array of LSP CompletionItems.
func request_completion(path: String, line: int, column: int, callback: Callable) -> void:
	_request("textDocument/completion", _position_params(path, line, column), func(result):
		var items: Array = []
		if result is Dictionary:
			items = result.get("items", [])
		callback.call(items))


## Asks for the hover text at a position; `callback` receives a String ("" when there is nothing).
func request_hover(path: String, line: int, column: int, callback: Callable) -> void:
	_request("textDocument/hover", _position_params(path, line, column), func(result):
		var text := ""
		if result is Dictionary:
			text = str(result.get("contents", {}).get("value", ""))
		callback.call(text))


## Roslyn's semantic classification: an array of [line, start column, end column, kind] for identifiers.
func request_classification(path: String, callback: Callable) -> void:
	_request("yotsuba/classify", {"textDocument": {"uri": to_uri(path)}}, func(result):
		callback.call(result if result is Array else []))


## Quick fixes (add the missing `using`) for the given diagnostics; `callback` receives the array of LSP CodeActions.
func request_code_actions(path: String, diagnostics: Array, callback: Callable) -> void:
	if diagnostics.is_empty():
		callback.call([])
		return
	_request("textDocument/codeAction", {"textDocument": {"uri": to_uri(path)}, "range": diagnostics[0]["range"],
			"context": {"diagnostics": diagnostics}}, func(result):
		callback.call(result if result is Array else []))


func _position_params(path: String, line: int, column: int) -> Dictionary:
	return {"textDocument": {"uri": to_uri(path)}, "position": {"line": line, "character": column}}


func stop() -> void:
	if not _running:
		return
	_running = false
	_request("shutdown", null)
	_notify("exit", null)
	OS.kill(_pid)  # closes the pipes, which wakes up the blocking reads
	_reader.wait_to_finish()
	_error_reader.wait_to_finish()
	stopped.emit()


static func to_uri(path: String) -> String:
	return "file://" + path.uri_encode().replace("%2F", "/")


static func from_uri(uri: String) -> String:
	return uri.trim_prefix("file://").uri_decode()


static func _split(command: String) -> PackedStringArray:
	command = command.strip_edges()
	if FileAccess.file_exists(command):
		return PackedStringArray([command])
	var parts := command.split(" ", false)
	return parts if not parts.is_empty() else PackedStringArray(["dotnet", "yotsuba"])


func _request(method: String, params: Variant, callback: Callable = Callable()) -> void:
	if callback.is_valid():
		_pending[_next_id] = callback
	_send({"jsonrpc": "2.0", "id": _next_id, "method": method, "params": params})
	_next_id += 1


func _notify(method: String, params: Variant) -> void:
	_send({"jsonrpc": "2.0", "method": method, "params": params})


func _send(message: Dictionary) -> void:
	if _stdio == null:
		return
	var body := JSON.stringify(message).to_utf8_buffer()
	_stdio.store_buffer(("Content-Length: %d\r\n\r\n" % body.size()).to_utf8_buffer())
	_stdio.store_buffer(body)
	_stdio.flush()


# Both readers run on their own thread. The pipes do not block when there is nothing to read, so they poll:
# take whatever bytes are there, cut complete messages (or lines) out of the buffer, and sleep a little otherwise.
func _read_messages() -> void:
	var buffer := PackedByteArray()
	while _running:
		var chunk := _stdio.get_buffer(4096)
		if chunk.is_empty():
			if not OS.is_process_running(_pid):
				return
			OS.delay_msec(15)
			continue
		buffer.append_array(chunk)
		while true:
			var message := _cut_message(buffer)
			if message.is_empty():
				break
			_handle.call_deferred(message)


## Removes one complete "Content-Length: n\r\n\r\n{json}" message from the front of `buffer`, or returns {}.
func _cut_message(buffer: PackedByteArray) -> Dictionary:
	var separator := "\r\n\r\n".to_utf8_buffer()
	var end := -1
	for i in range(buffer.size() - 3):
		if buffer[i] == 13 and buffer[i + 1] == 10 and buffer[i + 2] == 13 and buffer[i + 3] == 10:
			end = i
			break
	if end < 0:
		return {}
	var header := buffer.slice(0, end).get_string_from_ascii()
	var length := -1
	for line in header.split("\r\n"):
		if line.to_lower().begins_with("content-length:"):
			length = int(line.substr(15).strip_edges())
	var start := end + separator.size()
	if length < 0 or buffer.size() < start + length:
		return {}
	var body := buffer.slice(start, start + length).get_string_from_utf8()
	var rest := buffer.slice(start + length)
	buffer.clear()
	buffer.append_array(rest)
	var parsed = JSON.parse_string(body)
	return parsed if parsed is Dictionary else {"method": "ignored"}


func _read_logs() -> void:
	var pending := ""
	while _running:
		var chunk := _stderr.get_buffer(1024)
		if chunk.is_empty():
			if not OS.is_process_running(_pid):
				return
			OS.delay_msec(30)
			continue
		pending += chunk.get_string_from_utf8()
		var lines := pending.split("\n")
		pending = lines[lines.size() - 1]
		for i in range(lines.size() - 1):
			if not lines[i].strip_edges().is_empty():
				log_received.emit.call_deferred(lines[i].strip_edges())


func _handle(message: Dictionary) -> void:
	if message.has("id") and not message.has("method"):
		var id := int(message["id"])
		if _pending.has(id):
			var callback: Callable = _pending[id]
			_pending.erase(id)
			callback.call(message.get("result"))
		return
	if message.get("method", "") == "textDocument/publishDiagnostics":
		var params: Dictionary = message["params"]
		diagnostics_received.emit(from_uri(params["uri"]), params["diagnostics"])
