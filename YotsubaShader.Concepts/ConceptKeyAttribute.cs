namespace YotsubaShader.Concepts;

[AttributeUsage(AttributeTargets.Parameter | AttributeTargets.Field)]
public class ConceptKeyAttribute(string key) : Attribute
{
    public string Key = key;
}