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

using YotsubaShader.IR.Intermediate;
using YotsubaShader.IR.Intermediate.Slot;
using YotsubaShader.IR.Intermediate.Slot.Types;

namespace YotsubaShader.Compiler.SpirV;

/// <summary>
/// Comprueba la metadata de la interfaz de cada etapa antes de generar SPIR-V: que cada variable de entrada
/// o salida tenga una Location o un BuiltIn válidos para su etapa. Un fallo aquí nombra la función, el miembro
/// y el valor responsables, en lugar de producir una variable sin decoración o con una decoración inválida.
/// </summary>
internal static class InterfaceValidator
{
    public static void Validate(Shader shader)
    {
        foreach (Function function in shader.Functions)
        {
            if (function.Stage == Stage.None)
                continue;

            foreach (Storage storage in function.Inputs.Concat(function.Outputs))
            {
                if (storage.Kind is not (StorageKind.Input or StorageKind.Output))
                    continue;

                foreach (var (leaf, _) in BuildIrTypes.InterfaceVariables(function, storage))
                {
                    ValidateLeaf(function, storage, leaf);
                }
            }
        }
    }

    private static void ValidateLeaf(Function function, Storage owner, Storage leaf)
    {
        string where = $"'{leaf.HumanName}' of '{function.HumanName}' ({function.Stage} {Direction(leaf)})";

        switch (leaf.Slot)
        {
            case null:
                // Un fragment shader que devuelve un valor sin anotar escribe en el render target 0.
                if (function.Stage == Stage.Fragment &&
                    leaf.Kind == StorageKind.Output &&
                    owner is not YotsubaShader.IR.Intermediate.Types.CustomStruct)
                {
                    return;
                }

                throw new InvalidOperationException(
                    $"The interface member {where} has no Location or built-in. " +
                    "Annotate it with a location, a vertex semantic or a built-in value.");

            case LocationSlot { Location: < 0 } location:
                throw new InvalidOperationException(
                    $"The interface member {where} has the negative location {location.Location}. " +
                    "Locations start at 0.");

            case VertexSlot { Semantic.Index: < 0 } semantic:
                throw new InvalidOperationException(
                    $"The interface member {where} has the negative semantic index {semantic.Semantic.Index} " +
                    $"on {semantic.Semantic.Usage}. Semantic indices start at 0.");

            case BuiltInSlot builtIn when !IsValidBuiltIn(builtIn.BuiltIn, function.Stage, leaf.Kind):
                throw new InvalidOperationException(
                    $"The built-in {builtIn.BuiltIn} cannot be used by {where}. " +
                    $"It is only valid as {ValidUse(builtIn.BuiltIn)}.");
        }
    }

    private static string Direction(Storage leaf) => leaf.Kind.ToString().ToLowerInvariant();

    private static bool IsValidBuiltIn(BuiltIn builtIn, Stage stage, StorageKind kind) =>
        (builtIn, stage, kind) switch
        {
            (BuiltIn.Position or BuiltIn.PointSize, Stage.Vertex, StorageKind.Output) => true,
            (BuiltIn.VertexIndex or BuiltIn.InstanceIndex, Stage.Vertex, StorageKind.Input) => true,
            (BuiltIn.FragCoord or BuiltIn.FrontFacing, Stage.Fragment, StorageKind.Input) => true,
            (BuiltIn.FragDepth, Stage.Fragment, StorageKind.Output) => true,
            (BuiltIn.GlobalInvocationId or BuiltIn.LocalInvocationId or BuiltIn.LocalInvocationIndex or
                BuiltIn.WorkgroupId or BuiltIn.NumWorkgroups, Stage.Compute, StorageKind.Input) => true,
            _ => false
        };

    private static string ValidUse(BuiltIn builtIn) => builtIn switch
    {
        BuiltIn.Position or BuiltIn.PointSize => "a vertex shader output",
        BuiltIn.VertexIndex or BuiltIn.InstanceIndex => "a vertex shader input",
        BuiltIn.FragCoord or BuiltIn.FrontFacing => "a fragment shader input",
        BuiltIn.FragDepth => "a fragment shader output",
        _ => "a compute shader input"
    };
}
