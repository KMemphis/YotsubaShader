#region MS-PL License - Copyright (c) 2026 Memphis Yomael Núñez Cruz - Dominican Republic
/*
╔════════════════════════════════════════════════════════════════════════════════════╗
║                                                                                    ║
║  ███╗   ███╗███████╗      ██████╗ ██╗                                              ║
║  ████╗ ████║██╔════╝      ██╔══██╗██║                                              ║
║  ██╔████╔██║███████╗█████╗██████╔╝██║                                              ║
║  ██║╚██╔╝██║╚════██║╚════╝██╔═══╝ ██║                                              ║
║  ██║ ╚═╝ ██║███████║      ██║     ███████╗                                         ║
║  ╚═╝     ╚═╝╚══════╝      ╚═╝     ╚══════╝                                         ║
║                                                                                    ║
╠════════════════════════════════════════════════════════════════════════════════════╣
║  Copyright (c) 2026 Memphis Yomael Núñez Cruz - Dominican Republic                 ║
╠════════════════════════════════════════════════════════════════════════════════════╣
║                                                                                    ║
║  Microsoft Public License (MS-PL)                                                  ║
║                                                                                    ║
║  This license governs use of the accompanying software. If you use the software,   ║
║  you accept this license. If you do not accept the license, do not use the         ║
║  software.                                                                         ║
║                                                                                    ║
║  1. Definitions                                                                    ║
║  The terms "reproduce," "reproduction," "derivative works," and "distribution"     ║
║  have the same meaning here as under U.S. copyright law.                           ║
║  A "contribution" is the original software, to which copyright notices are         ║
║  attached, and any additions or changes to the software.                           ║
║  A "contributor" is any person that distributes its contribution under this        ║
║  license.                                                                          ║
║  "Licensed patents" are a contributor's patent claims that read directly on its    ║
║  contribution.                                                                     ║
║                                                                                    ║
║  2. Grant of Rights                                                                ║
║  (A) Copyright Grant- Subject to the terms of this license, including the license  ║
║  conditions and limitations in section 3, each contributor grants you a non-       ║
║  exclusive, worldwide, royalty-free copyright license to reproduce its             ║
║  contribution, prepare derivative works of its contribution, and distribute its    ║
║  contribution or any derivative works that you create.                             ║
║  (B) Patent Grant- Subject to the terms of this license, including the license     ║
║  conditions and limitations in section 3, each contributor grants you a non-       ║
║  exclusive, worldwide, royalty-free license under its licensed patents to make,    ║
║  have made, use, sell, offer for sale, import, and/or otherwise dispose of its     ║
║  contribution in the software or derivative works of the contribution in the       ║
║  software.                                                                         ║
║                                                                                    ║
║  3. Conditions and Limitations                                                     ║
║  (A) No Trademark License- This license does not grant you rights to use any       ║
║  contributors' name, logo, or trademarks.                                          ║
║  (B) If you bring a patent claim against any contributor over patents that you     ║
║  claim are infringed by the software, your patent license from such contributor    ║
║  to the software ends automatically.                                               ║
║  (C) If you distribute any portion of the software, you must retain all copyright, ║
║  patent, trademark, and attribution notices that are present in the software.      ║
║  (D) If you distribute any portion of the software in source code form, you may do ║
║  so only under this license by including a complete copy of this license with      ║
║  your distribution. If you distribute any portion of the software in compiled or   ║
║  object code form, you may only do so under a license that complies with this      ║
║  license.                                                                          ║
║  (E) The software is licensed "as-is." You bear the risk of using it. The          ║
║  contributors give no express warranties, guarantees or conditions. You may have   ║
║  additional consumer rights under your local laws which this license cannot        ║
║  change. To the extent permitted under your local laws, the contributors exclude   ║
║  the implied warranties of merchantability, fitness for a particular purpose and   ║
║  non-infringement.                                                                 ║
║                                                                                    ║
╚════════════════════════════════════════════════════════════════════════════════════╝
*/
#endregion

using YotsubaShader.Compiler.SpirV.Builder;
using YotsubaShader.Compiler.SpirV.YS_Types;
using YotsubaShader.IR.Intermediate;
using YotsubaShader.IR.Intermediate.Slot;
using YotsubaShader.IR.Intermediate.Slot.Types;
using YotsubaShader.IR.Intermediate.Types;

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

            if (shaderFunction.Stage is Stage.Compute)
            {
                if (shaderFunction.ExtraParams is not ComputeParams compute)
                {
                    throw new InvalidOperationException(
                        $"El compute shader '{shaderFunction.HumanName}' no tiene ComputeParams.");
                }

                module.AddLine(SpirVOpTable.OpExecutionMode,
                    SpirVModuleHelpers.Id(functionId),
                    SpirVInstructionParameter.Enumerant(SpirVExecutionMode.LocalSize),
                    new SpirVInstructionParameter(compute.X.ToString(), SpirVInstructionType.UIntValue),
                    new SpirVInstructionParameter(compute.Y.ToString(), SpirVInstructionType.UIntValue),
                    new SpirVInstructionParameter(compute.Z.ToString(), SpirVInstructionType.UIntValue));
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

    private void BuildDecorations(SpirVModule module, Shader shader)
    {
        foreach (var shaderFunction in shader.Functions)
        {
            if(shaderFunction.Stage == Stage.None) continue;

            foreach (var storage in shaderFunction.Inputs.Concat(shaderFunction.Outputs))
            {
                if(storage.Kind is not (StorageKind.Input or StorageKind.Output)) continue;

                string variableId = $"%{shaderFunction.HumanName}_{storage.Kind}_{storage.HumanName}";

                if (storage is CustomStruct structure)
                {
                    string structId = SpirVTableTypesIds.StructID(structure.HumanName);

                    for (int memberIndex = 0; memberIndex < structure.Properties.Count; memberIndex++)
                    {
                        DecorateMember(module, structId, memberIndex, structure.Properties[memberIndex].Slot);
                    }
                    continue;
                }

                DecorateVariable(module, variableId, storage.Slot);
            }
        }
    }

    private void DecorateVariable(SpirVModule module, string variableId, InterfaceSlot storageSlot)
    {
        switch (storageSlot)
        {
            case BuiltInSlot builtInSlot:
                module.AddLine(SpirVOpTable.OpDecorate,
                    SpirVModuleHelpers.Id(variableId),
                    SpirVInstructionParameter.Enumerant(SpirVDecoration.BuiltIn),
                    SpirVInstructionParameter.Enumerant(ToSpirVBuiltIn(builtInSlot.BuiltIn)));
                break;
            case LocationSlot locationSlot:
                module.AddLine(SpirVOpTable.OpDecorate,
                    SpirVModuleHelpers.Id(variableId),
                    SpirVInstructionParameter.Enumerant(SpirVDecoration.Location),
                    new(locationSlot.Location.ToString(), SpirVInstructionType.UIntValue));
                break;
        }
    }

    private void DecorateMember(SpirVModule module, string structId, int memberIndex, InterfaceSlot? slot)
    {

        if (slot is null) return;
        switch (slot)
        {
            case BuiltInSlot builtInSlot:
                module.AddLine(SpirVOpTable.OpMemberDecorate,
                    SpirVModuleHelpers.Id(structId),
                    new(memberIndex.ToString(),SpirVInstructionType.UIntValue),
                    SpirVInstructionParameter.Enumerant(SpirVDecoration.BuiltIn),
                    SpirVInstructionParameter.Enumerant(ToSpirVBuiltIn(builtInSlot.BuiltIn)));
                break;
            case LocationSlot locationSlot:
                module.AddLine(SpirVOpTable.OpMemberDecorate,
                    SpirVModuleHelpers.Id(structId),
                    new(memberIndex.ToString(), SpirVInstructionType.UIntValue),
                    SpirVInstructionParameter.Enumerant(SpirVDecoration.Location),
                    new(locationSlot.Location.ToString(), SpirVInstructionType.UIntValue));
                break;
            case VertexSlot vertex:
                int location = ResolveVertexLocation(vertex.Semantic);
                module.AddLine(
                    SpirVOpTable.OpMemberDecorate,
                    SpirVModuleHelpers.Id(structId),
                    new SpirVInstructionParameter(memberIndex.ToString(), SpirVInstructionType.UIntValue),
                    SpirVInstructionParameter.Enumerant(SpirVDecoration.Location),
                    new(location.ToString(), SpirVInstructionType.UIntValue));
                break;
        }
    }

    private int ResolveVertexLocation(VertexSemantic vertexSemantic)
    {
        switch (vertexSemantic.Usage)
        {
            case VertexUsage.TextureCoordinate:
                return vertexSemantic.Index;
            case VertexUsage.Color:
                return 8 + vertexSemantic.Index;
            case VertexUsage.Normal:
                return 16 + vertexSemantic.Index;
            case VertexUsage.Tangent:
                return 16 + vertexSemantic.Index;
            case VertexUsage.Binormal:
                return 32 + vertexSemantic.Index;
            default:
                throw new NotSupportedException(
                    $"El semántico '{vertexSemantic.Usage}{vertexSemantic.Index}' aún no tiene una Location SPIR-V.");
        }
    }
    
    private static SpirVBuiltIn ToSpirVBuiltIn(BuiltIn builtIn) =>
        builtIn switch
        {
            BuiltIn.Position => SpirVBuiltIn.Position,
            BuiltIn.PointSize => SpirVBuiltIn.PointSize,
            BuiltIn.VertexIndex => SpirVBuiltIn.VertexIndex,
            BuiltIn.InstanceIndex => SpirVBuiltIn.InstanceIndex,
            BuiltIn.FragCoord => SpirVBuiltIn.FragCoord,
            BuiltIn.FrontFacing => SpirVBuiltIn.FrontFacing,
            BuiltIn.FragDepth => SpirVBuiltIn.FragDepth,
            BuiltIn.GlobalInvocationId => SpirVBuiltIn.GlobalInvocationId,
            BuiltIn.LocalInvocationId => SpirVBuiltIn.LocalInvocationId,
            BuiltIn.LocalInvocationIndex => SpirVBuiltIn.LocalInvocationIndex,
            BuiltIn.WorkgroupId => SpirVBuiltIn.WorkgroupId,
            BuiltIn.NumWorkgroups => SpirVBuiltIn.NumWorkgroups,
            _ => throw new NotSupportedException(
                $"El built-in '{builtIn}' aún no está soportado por SPIR-V."),
        };
}
