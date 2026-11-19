extends Node3D
## Loads the shader that YotsubaShader generated from Shaders/PlasmaShader.cs and animates its uniforms.
## The addon regenerates `res://yotsuba_shaders` every time the project runs.

var _material := ShaderMaterial.new()


func _ready() -> void:
	_material.shader = load("res://yotsuba_shaders/Examples/GodotShaders/PlasmaShader.spatial.gdshader")
	_material.set_shader_parameter("P_Tint", Color(1.0, 0.9, 0.7))
	$Plasma.material_override = _material


func _process(_delta: float) -> void:
	_material.set_shader_parameter("P_Time", Time.get_ticks_msec() / 1000.0)
