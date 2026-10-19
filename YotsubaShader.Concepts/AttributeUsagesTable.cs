namespace YotsubaShader.Concepts;

public static class AttributeUsagesTable
{
    public const AttributeTargets Input = AttributeTargets.Field | AttributeTargets.Parameter | AttributeTargets.Property;

    public const AttributeTargets Resource = AttributeTargets.Field | AttributeTargets.Parameter | AttributeTargets.Property;
    
    public const AttributeTargets Output = AttributeTargets.Field | AttributeTargets.Parameter
                                                                  | AttributeTargets.ReturnValue;

    public const AttributeTargets InputOutput = Input | Output;

    public const AttributeTargets Method = AttributeTargets.Method;

}
