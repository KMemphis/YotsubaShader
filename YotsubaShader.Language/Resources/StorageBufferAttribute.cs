namespace YotsubaShader.Language.Resources;

[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Parameter)]
public class StorageBufferAttribute : Attribute
{
    public ResourceAccess Access = ResourceAccess.ReadWrite;
}
