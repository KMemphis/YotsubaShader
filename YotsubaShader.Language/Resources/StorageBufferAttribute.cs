namespace YotsubaShader.Language.Resources;

[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Parameter)]
public class StorageBufferAttribute : YotsubaShaderAttribute
{
    public ResourceAccess Access = ResourceAccess.ReadWrite;
}
