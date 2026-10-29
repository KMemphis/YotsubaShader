using System.Numerics;
using YotsubaShader.Compiler.SpirV.Builder;
using YotsubaShader.Compiler.SpirV.YS_Types;
using YotsubaShader.IR.Intermediate;
using IRTypes = YotsubaShader.IR.Intermediate.Types;

namespace YotsubaShader.Compiler.SpirV;

public static class BuildIrTypes
{
    public static void BuildRequiredTypes(SpirVModule module, Shader shader)
    {
        foreach (var shaderFunction in shader.Functions)
        {
            foreach (var shaderFunctionInput in shaderFunction.Inputs)
            {
                BuildStorageTypes(module, shaderFunction, shaderFunctionInput);
            }

            foreach (var shaderFunctionOutput in shaderFunction.Outputs)
            {
                BuildStorageTypes(module, shaderFunction, shaderFunctionOutput);
            }
        }
    }

    private static void BuildStorageTypes(SpirVModule module, Function shaderFunction, Storage storage)
    {
        // if (storage is IRTypes.CustomStruct structure)
        // {
        //     foreach (var structureProperty in structure.Properties)
        //     {
        //         BuildStorageTypes(module, shaderFunction, structureProperty);
        //     }
        //     return;
        // }

        SpirVStorageClass storageClass = storage.Kind switch
        {
            StorageKind.Input => SpirVStorageClass.Input,
            StorageKind.Output => SpirVStorageClass.Output,
            StorageKind.Uniform => SpirVStorageClass.Uniform,
            StorageKind.StorageBuffer => SpirVStorageClass.StorageBuffer,
            StorageKind.PushConstant => SpirVStorageClass.PushConstant,
            StorageKind.Texture => SpirVStorageClass.UniformConstant,
            StorageKind.Sampler => SpirVStorageClass.UniformConstant,
            StorageKind.Local => SpirVStorageClass.Function,
            _ => throw new NotSupportedException(
                $"'{storage.Kind}' no es una interfaz de shader.")
        };

        string typeId = module.DeclareType(storage);
        string pointerTypeId = module.DeclarePointer(
            storageClass,
            typeId);
        
        string variableId = $"%{shaderFunction.HumanName}_{storage.Kind}_{storage.HumanName}";

        module.AddResultLine(
            variableId,
            SpirVOpTable.OpVariable,
            SpirVModuleHelpers.Id(pointerTypeId),
            SpirVInstructionParameter.Enumerant(storageClass));
    }
}