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

using YotsubaShader.Compiler.SpirV;
using YotsubaShader.Compiler.SpirV.Builder;
using YotsubaShader.Compiler.SpirV.YS_Types;

namespace YotsubaShader.Example;

/// <summary>#19: un error interno de IDs o de estructura se detecta antes de serializar, con un mensaje de Yotsuba.</summary>
[Collection("ShaderProcessor")]
public class SpirVModuleValidationTests
{
    private static InvalidOperationException Rejected(SpirVModule module) =>
        Assert.Throws<InvalidOperationException>(() => new SpirVCompiler().Compile(module));

    /// <summary>
    /// La instrucción por su nombre. La tabla SpirVOpTable la genera el compilador en tiempo de build y la CLI,
    /// que abre este proyecto con MSBuildWorkspace, no la ve; por eso se busca por reflexión y no por nombre en el código.
    /// </summary>
    private static SpirVOperation Op(string name) =>
        (SpirVOperation)typeof(SpirVModule).Assembly
            .GetType("YotsubaShader.Compiler.SpirV.SpirVOpTable")!
            .GetField(name)!.GetValue(null)!;

    private static SpirVInstructionParameter Id(string id) => new(id, SpirVInstructionType.IdRef);

    private static SpirVInstructionParameter FunctionControlNone => new("None", SpirVInstructionType.Enumerant, 0);

    /// <summary>Un módulo con %void y %fn_type, listo para abrir una función.</summary>
    private static SpirVModule WithFunctionType()
    {
        var module = new SpirVModule();
        module.DeclareType(typeof(void));
        module.AddResultLine("%fn_type", Op("OpTypeFunction"), Id("%void"));
        return module;
    }

    private static void BeginFunction(SpirVModule module)
    {
        module.BeginFunction();
        module.AddResultLine("%fn", Op("OpFunction"), Id("%void"), FunctionControlNone, Id("%fn_type"));
    }

    [Fact]
    public void A_missing_operand_names_the_instruction_and_the_id()
    {
        var module = new SpirVModule();
        module.AddResultLine("%v4float", Op("OpTypeVector"), Id("%float"),
            new("4", SpirVInstructionType.UIntValue));

        InvalidOperationException error = Rejected(module);

        Assert.Contains("%float", error.Message);
        Assert.Contains("never defined", error.Message);
        Assert.Contains("OpTypeVector", error.Message);
        Assert.Contains("%v4float", error.Message);
    }

    [Fact]
    public void A_missing_result_type_names_the_instruction_and_the_id()
    {
        var module = WithFunctionType();
        BeginFunction(module);
        module.AddResultLine("%entry", Op("OpLabel"));
        module.AddResultLine("%value", Op("OpLoad"), Id("%missing_type"), Id("%missing_pointer"));
        module.AddLine(Op("OpReturn"));
        module.AddLine(Op("OpFunctionEnd"));
        module.EndFunction();

        InvalidOperationException error = Rejected(module);

        Assert.Contains("%missing_type", error.Message);
        Assert.Contains("result type", error.Message);
        Assert.Contains("OpLoad", error.Message);
    }

    [Fact]
    public void A_reference_to_an_id_defined_later_is_allowed()
    {
        // OpName y las decoraciones se escriben antes que lo que nombran: una referencia hacia delante es válida.
        var module = new SpirVModule();
        module.Name("%void", "later");
        module.DeclareType(typeof(void));

        Assert.NotEmpty(new SpirVCompiler().Compile(module));
    }

    [Fact]
    public void A_block_without_a_terminator_is_rejected_before_the_next_label()
    {
        var module = WithFunctionType();
        BeginFunction(module);
        module.AddResultLine("%entry", Op("OpLabel"));
        module.AddResultLine("%second", Op("OpLabel"));
        module.AddLine(Op("OpReturn"));
        module.AddLine(Op("OpFunctionEnd"));
        module.EndFunction();

        InvalidOperationException error = Rejected(module);

        Assert.Contains("%second", error.Message);
        Assert.Contains("terminator", error.Message);
    }

    [Fact]
    public void A_function_whose_last_block_has_no_terminator_is_rejected()
    {
        var module = WithFunctionType();
        BeginFunction(module);
        module.AddResultLine("%entry", Op("OpLabel"));
        module.AddLine(Op("OpFunctionEnd"));
        module.EndFunction();

        InvalidOperationException error = Rejected(module);

        Assert.Contains("%fn", error.Message);
        Assert.Contains("terminator", error.Message);
    }

    [Fact]
    public void A_function_without_OpFunctionEnd_is_rejected()
    {
        var module = WithFunctionType();
        BeginFunction(module);
        module.AddResultLine("%entry", Op("OpLabel"));
        module.AddLine(Op("OpReturn"));
        module.EndFunction();

        InvalidOperationException error = Rejected(module);

        Assert.Contains("OpFunctionEnd", error.Message);
    }

    [Fact]
    public void A_well_formed_function_is_serialized()
    {
        var module = WithFunctionType();
        BeginFunction(module);
        module.AddResultLine("%entry", Op("OpLabel"));
        module.AddLine(Op("OpReturn"));
        module.AddLine(Op("OpFunctionEnd"));
        module.EndFunction();

        Assert.NotEmpty(new SpirVCompiler().Compile(module));
    }
}
