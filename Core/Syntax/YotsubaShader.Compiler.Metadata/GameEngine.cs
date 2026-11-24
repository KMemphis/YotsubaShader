namespace YotsubaShader.Compiler.Metadata;

/// <summary>Motores de juego con reglas propias que se validan además de las de la API gráfica.</summary>
[Flags]
public enum GameEngine : ulong
{
    None = 0UL,
    Godot = 1UL << 0,
    All = Godot
}
