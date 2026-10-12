namespace YotsubaShader.Language.Attributes.MetalStyle;

/// <summary>
/// Posicion que asigna el vertex shader a la gpu para que dibuje el vertice
/// </summary>
[AttributeUsage(AttributeUsagesTable.Input)]
public class vertex_id() : Attribute;