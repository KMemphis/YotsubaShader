namespace YotsubaShader.Concepts;

[AttributeUsage(AttributeTargets.All)]
public class YotsubaShaderConcept : Attribute
{
    public readonly Dictionary<string, (MetadataTypes, object)> Metadata = null;
    public YotsubaShaderConcept(params (string, (MetadataTypes, object))[] metadata)
    {
        var span = metadata.AsSpan();
        if(span.IsEmpty) return;
        
        Metadata = new Dictionary<string, (MetadataTypes, object)>();
        foreach (var valueTuple in span)
        {
            Metadata.Add(valueTuple.Item1, valueTuple.Item2);
        }
    }
}

public enum MetadataTypes
{
    Int,
    UInt,
    Float,
    String,
    Null
}