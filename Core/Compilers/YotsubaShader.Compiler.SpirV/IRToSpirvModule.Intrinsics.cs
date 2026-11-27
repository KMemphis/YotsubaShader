using YotsubaShader.Compiler.SpirV.YS_Types;
using YotsubaShader.IR.Intermediate;
using YotsubaShader.IR.Intermediate.FunctionBody.Expressions.FunctionCalls;
using YotsubaShader.IR.Intermediate.Types;

namespace YotsubaShader.Compiler.SpirV;

public partial class IRToSpirvModule
{
    private static void AddHalfCapabilities(HashSet<SpirVCapability> capabilities)
    {
        capabilities.Add(SpirVCapability.Float16);
        capabilities.Add(SpirVCapability.StorageInputOutput16);
    }

    /// <summary>Las capabilities que exige emitir la intrínseca con esos tipos de argumento.</summary>
    private static void AddIntrinsicCapabilities(
        GPUIntrinsic intrinsic,
        IReadOnlyList<Storage?> argumentTypes,
        Storage? resultType,
        HashSet<SpirVCapability> capabilities)
    {
        foreach (Storage? type in argumentTypes)
        {
            if (type is not null) AddCapabilityForStorage(type, capabilities);
        }

        if (resultType is not null) AddCapabilityForStorage(resultType, capabilities);

        string name = intrinsic.ToString();

        if (name.Contains("Coarse") || name.Contains("Fine")) capabilities.Add(SpirVCapability.DerivativeControl);
        if (name.StartsWith("InterpolateAt")) capabilities.Add(SpirVCapability.InterpolationFunction);
        if (name.StartsWith("TextureSize") || name.StartsWith("Query")) capabilities.Add(SpirVCapability.ImageQuery);
        if (name.StartsWith("ImageLoad")) capabilities.Add(SpirVCapability.StorageImageReadWithoutFormat);
        if (name.StartsWith("ImageStore")) capabilities.Add(SpirVCapability.StorageImageWriteWithoutFormat);

        // Los offsets de texels son siempre constantes (ConstOffset): no necesitan ImageGatherExtended.

        foreach (Storage? type in argumentTypes)
        {
            if (type is Texture { Multisampled: true, Arrayed: true, Usage: TextureUsage.Sampled })
            {
                capabilities.Add(SpirVCapability.ImageMSArray);
            }
        }
    }
}
