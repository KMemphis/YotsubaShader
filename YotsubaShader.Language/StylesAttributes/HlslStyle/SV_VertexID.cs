namespace YotsubaShader.Language.Attributes.HlslStyle;


/// <summary>
/// Posicion que asigna el vertex shader a la gpu para que dibuje el vertice
/// </summary>
[AttributeUsage(AttributeUsagesTable.Input)]
public class SV_VertexID() : Attribute;