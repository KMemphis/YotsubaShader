extends SceneTree
## Loads every .gdshader given after `--` on a mesh (spatial) or a ColorRect (canvas_item) and lets the
## renderer compile it. Godot prints "SHADER ERROR" / "Shader compilation failed" for a bad one; the caller
## looks for those lines. Usage: godot --path <this folder> --script check.gd -- a.gdshader b.gdshader

func _init() -> void:
	var root_3d := Node3D.new()
	get_root().add_child(root_3d)
	var count := 0
	for path in OS.get_cmdline_user_args():
		var shader := Shader.new()
		shader.code = FileAccess.get_file_as_string(path)
		var material := ShaderMaterial.new()
		material.shader = shader
		if shader.get_mode() == Shader.MODE_CANVAS_ITEM:
			var rect := ColorRect.new()
			rect.size = Vector2(16, 16)
			rect.material = material
			get_root().add_child(rect)
		else:
			var mesh := MeshInstance3D.new()
			mesh.mesh = BoxMesh.new()
			mesh.material_override = material
			root_3d.add_child(mesh)
		count += 1
	print("YOTSUBA_CHECK loaded ", count, " shader(s)")
	await create_timer(1.0).timeout
	print("YOTSUBA_CHECK done")
	quit()
