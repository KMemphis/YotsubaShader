namespace YotsubaShader.Language;

public static class AttributeUsagesTable
{
    public const AttributeTargets Input = AttributeTargets.Field | AttributeTargets.Parameter;
    
    public const AttributeTargets Output = AttributeTargets.Field | AttributeTargets.Parameter
                                                                  | AttributeTargets.ReturnValue;

    public const AttributeTargets InputOutput = Input | Output;

    public const AttributeTargets Method = AttributeTargets.Method;

}
