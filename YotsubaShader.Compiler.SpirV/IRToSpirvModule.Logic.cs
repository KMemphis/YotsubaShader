using YotsubaShader.Compiler.SpirV.Builder;
using YotsubaShader.Compiler.SpirV.YS_Types;
using YotsubaShader.IR.Intermediate;

namespace YotsubaShader.Compiler.SpirV;

public partial class IRToSpirvModule
{

    private void BuildHeader(SpirVModule module, Shader shader)
    {
        module.AddLine(SpirVOpTable.OpCapability, 
            SpirVInstructionParameter.Enumerant(SpirVCapability.Shader));
        
        module.AddLine(SpirVOpTable.OpMemoryModel, new []
        {
            SpirVInstructionParameter.Enumerant(SpirVAddressingModel.Logical),
            SpirVInstructionParameter.Enumerant(SpirVMemoryModel.GLSL450)
        });
    }

    private void BuildEntryPoints(SpirVModule module, Shader shader)
    {
        foreach (var shaderFunction in shader.Functions)
        {
            if(shaderFunction.Stage == Stage.None) continue;
            
            string functionId = GetFunctionId(shaderFunction);
            SpirVExecutionModel executionModel;
            switch (shaderFunction.Stage)
            {
                case Stage.Vertex:
                    executionModel = SpirVExecutionModel.Vertex;
                    break;
                case Stage.Fragment:
                    executionModel = SpirVExecutionModel.Fragment;
                    break;
                case Stage.Compute:
                    executionModel = SpirVExecutionModel.GLCompute;
                    break;
                default:
                    throw new NotSupportedException(
                        $"La etapa '{shaderFunction.Stage}' no esta soportada");
            }
            
            var parameters = new List<SpirVInstructionParameter>
            {
                SpirVInstructionParameter.Enumerant(executionModel),
                SpirVModuleHelpers.Id(functionId),
                new(shaderFunction.HumanName, SpirVInstructionType.StringLiteral)
            };

            parameters.AddRange(
                shaderFunction.Inputs
                    .Concat(shaderFunction.Outputs)
                    .Where(storage =>
                        storage.Kind is StorageKind.Input or StorageKind.Output)
                    .Select(storage =>
                        SpirVModuleHelpers.Id(
                            $"%{shaderFunction.HumanName}_{storage.Kind}_{storage.HumanName}")));

            module.AddLine(SpirVOpTable.OpEntryPoint, [.. parameters]);
            if (shaderFunction.Stage == Stage.Fragment)
            {
                module.AddLine(SpirVOpTable.OpExecutionMode,
                    new SpirVInstructionParameter(functionId, SpirVInstructionType.IdRef),
                    SpirVInstructionParameter.Enumerant(SpirVExecutionMode.OriginUpperLeft));
            }
            
        }
    }


    private void BuildFunctionTypes(SpirVModule module)
    {
        string voidTypeId = module.DeclareType(typeof(void));

        module.AddResultLine(
            SpirVTableTypesIds.VoidFunctionID,
            SpirVOpTable.OpTypeFunction,
            new SpirVInstructionParameter(voidTypeId, SpirVInstructionType.IdRef)
        );
    }

    private static void BuildFunctionSkeletons(
        SpirVModule module,
        Shader shader)
    {
        foreach (var shaderFunction in shader.Functions)
        {
            if(shaderFunction.Stage == Stage.None) continue;

            string functionId = GetFunctionId(shaderFunction);
            string labelId = $"%label_{shaderFunction.HumanName}";

            module.AddResultLine(
                functionId,
                SpirVOpTable.OpFunction,
                new SpirVInstructionParameter(
                    SpirVTableTypesIds.VoidID,
                    SpirVInstructionType.IdRef),
                SpirVInstructionParameter.Enumerant(SpirVFunctionControl.None),
                new SpirVInstructionParameter(
                    SpirVTableTypesIds.VoidFunctionID,
                    SpirVInstructionType.IdRef));

            module.AddResultLine(labelId, SpirVOpTable.OpLabel);
            module.AddLine(SpirVOpTable.OpReturn);
            module.AddLine(SpirVOpTable.OpFunctionEnd);
        }
    }

    private static string GetFunctionId(Function function)
    {
        return $"%fn_{function.HumanName}";
    }
}