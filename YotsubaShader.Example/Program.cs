using Microsoft.Xna.Framework;
using YotsubaShader.Compiler;
using YotsubaShader.Compiler.Builder;
using YotsubaShader.Compiler.Compiler;
using YotsubaShader.Compiler.YS_Types;

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