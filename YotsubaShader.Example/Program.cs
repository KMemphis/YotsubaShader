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

using Microsoft.Xna.Framework;
using YotsubaShader.Compiler;
using YotsubaShader.Compiler.SpirV;
using YotsubaShader.Compiler.SpirV.Builder;
using YotsubaShader.Compiler.SpirV.YS_Types;

var module = new SpirVModule();
        
//First Line
module.AddLine(SpirVOpTable.OpCapability, 
    SpirVInstructionParameter.Enumerant(SpirVCapability.Shader));
        
//Second Line
module.AddLine(SpirVOpTable.OpMemoryModel, new []
{
    SpirVInstructionParameter.Enumerant(SpirVAddressingModel.Logical),
    SpirVInstructionParameter.Enumerant(SpirVMemoryModel.GLSL450)
});
        
//Third Line
module.AddLine(SpirVOpTable.OpEntryPoint, new []
{
    SpirVInstructionParameter.Enumerant(SpirVExecutionModel.Fragment),
    new SpirVInstructionParameter(SpirVTableTypesIds.FragmentMainID, SpirVInstructionType.IdRef),
    new SpirVInstructionParameter("fs_main", SpirVInstructionType.StringLiteral),
    new SpirVInstructionParameter("%f_color", SpirVInstructionType.IdRef),
});

//Four Line
module.AddLine(SpirVOpTable.OpExecutionMode, new []
{
    new SpirVInstructionParameter("%fs_main", SpirVInstructionType.IdRef),
    SpirVInstructionParameter.Enumerant(SpirVExecutionMode.OriginUpperLeft),
});

//Five Line
module.AddLine(SpirVOpTable.OpDecorate, new []
{
    new SpirVInstructionParameter("%f_color", SpirVInstructionType.IdRef),
    SpirVInstructionParameter.Enumerant(SpirVDecoration.Location),
    new SpirVInstructionParameter("0", SpirVInstructionType.IntValue)
});

//Sext Line
module.DeclareType(typeof(void));

//Sept Line
module.AddResultLine(SpirVTableTypesIds.VoidFunctionID, SpirVOpTable.OpTypeFunction, new SpirVInstructionParameter(SpirVTableTypesIds.VoidID, SpirVInstructionType.IdRef));

//Eight Line
module.DeclareType(typeof(float));

//Nine Line
string vec4 = module.DeclareType(typeof(Vector4));

//Ten Line
string vec4Out = module.DeclarePointer(SpirVStorageClass.Output, vec4);



//Eleven Line
module.AddResultLine("%f_color", SpirVOpTable.OpVariable, new[]
{
    new SpirVInstructionParameter(vec4Out,
        SpirVInstructionType.IdRef),
    SpirVInstructionParameter.Enumerant(SpirVStorageClass.Output)
});

//Twelve Line
module.AddResultLine("%float_0", SpirVOpTable.OpConstant, new[]
{
    new SpirVInstructionParameter(SpirVTableTypesIds.FloatID,
        SpirVInstructionType.IdRef),
    new SpirVInstructionParameter("0", SpirVInstructionType.FloatValue)
});

//Third Line
module.AddResultLine("%float_1", SpirVOpTable.OpConstant, new[]
{
    new SpirVInstructionParameter(SpirVTableTypesIds.FloatID,
        SpirVInstructionType.IdRef),
    new SpirVInstructionParameter("1", SpirVInstructionType.FloatValue)
});

//Fourteen Line
module.AddResultLine("%magenta", SpirVOpTable.OpConstantComposite, new[]
{
    new SpirVInstructionParameter(SpirVTableTypesIds.VectorFloatID(4),
        SpirVInstructionType.IdRef),
    new SpirVInstructionParameter("%float_1", SpirVInstructionType.IdRef),
    new SpirVInstructionParameter("%float_0", SpirVInstructionType.IdRef),
    new SpirVInstructionParameter("%float_1", SpirVInstructionType.IdRef),
    new SpirVInstructionParameter("%float_1", SpirVInstructionType.IdRef),
});

//Fifteen
module.AddResultLine(SpirVTableTypesIds.FragmentMainID, SpirVOpTable.OpFunction, new[]
{
    new SpirVInstructionParameter(SpirVTableTypesIds.VoidID, SpirVInstructionType.IdRef),
    SpirVInstructionParameter.Enumerant(SpirVFunctionControl.None),
    new SpirVInstructionParameter(SpirVTableTypesIds.VoidFunctionID, SpirVInstructionType.IdRef)
});

//Sexteen
module.AddResultLine("%entry", SpirVOpTable.OpLabel);

//Seventeen
module.AddLine(SpirVOpTable.OpStore, new SpirVInstructionParameter("%f_color", SpirVInstructionType.IdRef),
    new SpirVInstructionParameter("%magenta", SpirVInstructionType.IdRef));

//EigthTeen
module.AddLine(SpirVOpTable.OpReturn);

//NineTeen
module.AddLine(SpirVOpTable.OpFunctionEnd);

SpirVCompiler compiler = new SpirVCompiler();
byte[] spirV = compiler.Compile(module);
Console.WriteLine(module.ToText());
Console.WriteLine("------------------------------------------------------");
File.WriteAllBytes("mi modulo spirv.spv", spirV);