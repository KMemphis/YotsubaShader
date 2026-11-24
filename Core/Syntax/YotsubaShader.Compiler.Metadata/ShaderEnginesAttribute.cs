namespace YotsubaShader.Compiler.Metadata;

/// <summary>
/// Motores de juego para los que el shader debe ser válido (por ejemplo lo que <see cref="GameEngine.Godot"/> acepta
/// en las entradas del vertex shader). Se combina con <see cref="ShaderTargetsAttribute"/>: primero las reglas de
/// la API y después las del motor. Va en la clase o en el ensamblado; la clase manda.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Assembly)]
public sealed class ShaderEnginesAttribute(GameEngine engines) : Attribute
{
    public GameEngine Engines { get; } = engines;
}
