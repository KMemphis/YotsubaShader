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

using System.Numerics;
using YotsubaShader.Compiler.SpirV.Builder;
using YotsubaShader.Compiler.SpirV.YS_Types;
using YotsubaShader.IR.Intermediate;
using IRTypes = YotsubaShader.IR.Intermediate.Types;

namespace YotsubaShader.Compiler.SpirV;

public static class BuildIrTypes
{
    
    internal static string GlobalVariableId(Storage storage) =>
        $"%resource_{storage.HumanName}";

    internal static string InterfaceVariableId(
        Function function,
        Storage storage) =>
        $"%{function.HumanName}_{storage.Kind}_{storage.HumanName}";
    
    
    internal static bool IsInterfaceStruct(Function function, Storage storage) =>
        function.Stage != Stage.None &&
        storage is IRTypes.CustomStruct &&
        storage.Kind is StorageKind.Input or StorageKind.Output;

    internal static string InterfaceMemberVariableId(
        Function function,
        Storage structure,
        Storage member) =>
        $"{InterfaceVariableId(function, structure)}_{member.HumanName}";

    /// Las variables de interfaz reales: una por miembro si es un struct, o la propia variable.
    internal static IEnumerable<(Storage Storage, string VariableId)> InterfaceVariables(
        Function function,
        Storage storage)
    {
        if (!IsInterfaceStruct(function, storage))
        {
            yield return (storage, InterfaceVariableId(function, storage));
            yield break;
        }

        var structure = (IRTypes.CustomStruct)storage;

        foreach (Storage member in structure.Properties)
        {
            if (member is IRTypes.CustomStruct)
            {
                throw new NotSupportedException(
                    $"Nested struct '{member.HumanName}' inside the stage interface struct " +
                    $"'{structure.TypeName}' is not supported yet.");
            }

            yield return (
                member with { Kind = structure.Kind },
                InterfaceMemberVariableId(function, structure, member));
        }
    }
    
    public static void BuildRequiredTypes(SpirVModule module, Shader shader)
    {
        foreach (var shaderGlobalResource in shader.GlobalResources)
        {
            BuildStorageTypes(module, shaderGlobalResource, GlobalVariableId(shaderGlobalResource), Stage.None);
        }

        foreach (var shaderFunction in shader.Functions)
        {
            
            if (shaderFunction.Stage == Stage.None)
                continue;
            
            foreach (var shaderFunctionInterface in shaderFunction.Inputs.Concat(shaderFunction.Outputs))
            {
                foreach (var (leaf, variableId) in InterfaceVariables(shaderFunction, shaderFunctionInterface))
                {
                    BuildStorageTypes(module, leaf, variableId, shaderFunction.Stage);
                }
            }
        }
    }

    private static void BuildStorageTypes(SpirVModule module, Storage storage, string variableId, Stage stage)
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

        string typeId = module.DeclareType(storage, stage);

        DeclareStructMemberAccessTypes(module, storage, storageClass, stage);

        string pointerTypeId = module.DeclarePointer(
            storageClass,
            typeId);

        DeclareStructMemberIndices(module, storage);

        module.AddResultLine(
            variableId,
            SpirVOpTable.OpVariable,
            SpirVModuleHelpers.Id(pointerTypeId),
            SpirVInstructionParameter.Enumerant(storageClass));
    }
    
      
    private static void DeclareStructMemberIndices(
        SpirVModule module,
        Storage storage)
    {
        if (storage is not IRTypes.CustomStruct structure)
        {
            return;
        }

        for (int index = 0; index < structure.Properties.Count; index++)
        {
            module.DeclareUIntConstant(index);
        }

        foreach (Storage member in structure.Properties)
        {
            DeclareStructMemberIndices(module, member);
        }
    }
    
    private static void DeclareStructMemberAccessTypes(
        SpirVModule module,
        Storage storage,
        SpirVStorageClass storageClass, Stage stage)
    {
        if (storage is not IRTypes.CustomStruct structure)
        {
            return;
        }

        foreach (Storage member in structure.Properties)
        {
            string memberTypeId = module.DeclareType(member,stage);

            module.DeclarePointer(
                storageClass,
                memberTypeId);

            DeclareStructMemberAccessTypes(
                module,
                member,
                storageClass, 
                stage);
        }
    }
}