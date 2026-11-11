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

using System.Text;
using Microsoft.Xna.Framework.Graphics;
using YotsubaShader.Compiler.SpirV.YS_Types;
using YotsubaShader.IR.Intermediate;

namespace YotsubaShader.Compiler.SpirV.Builder;

public class SpirVModule
{

     internal IEnumerable<SpirVInstruction> Instructions
     {
          get
          {
               if (_currentFunction is not null)
               {
                    throw new InvalidOperationException(
                         "No se puede serializar un módulo con una función abierta.");
               }
     
               foreach (SpirVInstruction instruction in _header)
                    yield return instruction;
     
               foreach (SpirVInstruction instruction in _entryPoints)
                    yield return instruction;
               
               foreach (SpirVInstruction instruction in _debug)
                    yield return instruction;
     
               foreach (SpirVInstruction instruction in _annotations)
                    yield return instruction;
     
               foreach (SpirVInstruction instruction in _types)
                    yield return instruction;
     
               foreach (SpirVInstruction instruction in _constants)
                    yield return instruction;
     
               foreach (SpirVInstruction instruction in _globals)
                    yield return instruction;
     
               foreach (FunctionSection function in _functions)
               {
                    foreach (SpirVInstruction instruction in function.Header)
                         yield return instruction;
     
                    foreach (SpirVInstruction instruction in function.Variables)
                         yield return instruction;
     
                    foreach (SpirVInstruction instruction in function.Body)
                         yield return instruction;
               }
          }
     }
     
     private readonly List<SpirVInstruction> _header = [];
     private readonly List<SpirVInstruction> _entryPoints = [];
     private readonly List<SpirVInstruction> _debug = [];
     private readonly List<SpirVInstruction> _annotations = [];
     private readonly List<SpirVInstruction> _types = [];
     private readonly List<SpirVInstruction> _constants = [];
     private readonly List<SpirVInstruction> _globals = [];
     private readonly List<FunctionSection> _functions = [];
     
     private FunctionSection? _currentFunction;

     private sealed class FunctionSection
     {
          public List<SpirVInstruction> Header { get; } = [];
          public List<SpirVInstruction> Variables { get; } = [];
          public List<SpirVInstruction> Body { get; } = [];
          
          public bool HasEntryLabel { get; set; }
     }
     
     private readonly HashSet<string> _definedIds = [];
     private readonly Dictionary<string, string> _definitions = [];
     private readonly Dictionary<Type, string> _declaredTypes = [];
     private readonly Dictionary<(Storage, Stage), string> _declaredTypesStorages = [];

     
     public void BeginFunction()
     {
          if (_currentFunction is not null)
          {
               throw new InvalidOperationException("Ya hay una función abierta.");
          }

          _currentFunction = new FunctionSection();
     }

     public void EndFunction()
     {
          if (_currentFunction is null)
          {
               throw new InvalidOperationException("No hay una función abierta.");
          }

          _functions.Add(_currentFunction);
          _currentFunction = null;
     }
     
     private void AddInstruction(SpirVInstruction instruction)
     {
          SpirVOperation operation = instruction.Operation;
          
          
          if (operation.Name.StartsWith("OpType", StringComparison.Ordinal))
          {
               _types.Add(instruction);
               return;
          }

          if (operation.Name.StartsWith("OpConstant", StringComparison.Ordinal) ||
              operation.Name.StartsWith("OpSpecConstant", StringComparison.Ordinal) ||
              operation.Name == "OpUndef")
          {
               _types.Add(instruction);
               return;
          }
           
          if (operation.Name is "OpName" or "OpMemberName")
          {
               _debug.Add(instruction);
               return;
          }
          
          if (_currentFunction is not null)
          {
               if (operation.Name == "OpVariable")
               {
                    _currentFunction.Variables.Add(instruction);
               }
               else if (operation.Name is "OpFunction" or "OpFunctionParameter")
               {
                    _currentFunction.Header.Add(instruction);
               }
               else if (operation.Name == "OpLabel")
               {
                    // Solo el label de entrada va antes de las variables; el resto son bloques del cuerpo.
                    if (!_currentFunction.HasEntryLabel)
                    {
                         _currentFunction.Header.Add(instruction);
                         _currentFunction.HasEntryLabel = true;
                    }
                    else
                    {
                         _currentFunction.Body.Add(instruction);
                    }
               }
               else
               {
                    _currentFunction.Body.Add(instruction);
               }

               return;
          }

          switch (operation.Name)
          {
               case "OpCapability":
               case "OpExtension":
               case "OpExtInstImport":
               case "OpMemoryModel":
                    _header.Add(instruction);
                    break;

               case "OpEntryPoint":
               case "OpExecutionMode":
                    _entryPoints.Add(instruction);
                    break;

               case "OpDecorate":
               case "OpMemberDecorate":
               case "OpDecorateString":
               case "OpMemberDecorateString":
                    _annotations.Add(instruction);
                    break;

               case "OpVariable":
                    _globals.Add(instruction);
                    break;

               
               default:
                    throw new InvalidOperationException(
                         $"No sé en qué sección colocar '{operation.Name}'.");
          }
     }
     
     
     public void Name(string id, string name)
     {
          ArgumentException.ThrowIfNullOrWhiteSpace(id);
          ArgumentException.ThrowIfNullOrWhiteSpace(name);

          AddLine(
               SpirVOpTable.OpName,
               SpirVModuleHelpers.Id(id),
               new SpirVInstructionParameter(
                    name,
                    SpirVInstructionType.StringLiteral));
     }

     public void MemberName(string structTypeId, int memberIndex, string name)
     {
          ArgumentException.ThrowIfNullOrWhiteSpace(structTypeId);
          ArgumentOutOfRangeException.ThrowIfNegative(memberIndex);
          ArgumentException.ThrowIfNullOrWhiteSpace(name);

          AddLine(
               SpirVOpTable.OpMemberName,
               SpirVModuleHelpers.Id(structTypeId),
               new SpirVInstructionParameter(
                    memberIndex.ToString(),
                    SpirVInstructionType.UIntValue),
               new SpirVInstructionParameter(
                    name,
                    SpirVInstructionType.StringLiteral));
     }
     
     /// <summary>Instrucción sin resultado (OpStore, OpReturn, OpDecorate...).</summary>
     public void AddLine(SpirVOperation operation, params SpirVInstructionParameter[] parameters)
     {
          if (operation.HasResult)
          {
               throw new InvalidOperationException($"{operation.Name} produce un resultado; usa AddResultLine.");
          }

          AddInstruction(new SpirVInstruction(operation, parameters));
     }

     /// <summary>Instrucción con resultado: <c>%result = Op ...</c>. Cada ID se define una sola vez.</summary>
     public string AddResultLine(string result, SpirVOperation operation, params SpirVInstructionParameter[] parameters)
     {
          if (!operation.HasResult)
          {
               throw new InvalidOperationException($"{operation.Name} no produce resultado; usa AddLine.");
          }

          if (!_definedIds.Add(result))
          {
               throw new InvalidOperationException(
                    $"El ID {result} ya está definido en el módulo como '{_definitions.GetValueOrDefault(result)}'; " +
                    $"otra entidad intentó definirlo como '{Describe(operation, parameters)}'.");
          }

          _definitions[result] = Describe(operation, parameters);

          // Quien llama pasa el tipo como primer parámetro (como en el texto); aquí se separa,
          // porque en binario va antes del Result <id>.
          string? resultType = null;
          if (operation.HasResultType)
          {
               if (parameters.Length == 0 || parameters[0].Type != SpirVInstructionType.IdRef)
               {
                    throw new InvalidOperationException(
                         $"{operation.Name} necesita su Result Type como primer parámetro (un IdRef).");
               }

               resultType = parameters[0].Instruction;
               parameters = parameters[1..];
          }

          SpirVInstruction instruction =
               new(operation, parameters, result, resultType);

          AddInstruction(instruction);
          return result;
     }

     public bool IsDefined(string id) => _definedIds.Contains(id);

     internal static string Describe(SpirVOperation operation, SpirVInstructionParameter[] parameters) =>
          operation.Name + " " + string.Join(" ", parameters.Select(p => p.Instruction));

     /// <summary>
     /// Redeclarar un ID es válido solo si es exactamente la misma definición (los tipos y constantes se reutilizan).
     /// Si dos entidades distintas producen el mismo ID, falla en vez de fusionarlas en silencio.
     /// </summary>
     internal void EnsureSameDefinition(string id, SpirVOperation operation, SpirVInstructionParameter[] parameters)
     {
          string requested = Describe(operation, parameters);

          if (_definitions.TryGetValue(id, out string? existing) && existing != requested)
          {
               throw new InvalidOperationException(
                    $"Colisión de ID {id}: ya está definido como '{existing}', pero otra entidad necesita '{requested}'.");
          }
     }

     internal bool TryGetDeclaredType(Type type, out string id) => _declaredTypes.TryGetValue(type, out id!);
     internal bool TryGetDeclaredType(Storage type, Stage stage,  out string id) => _declaredTypesStorages.TryGetValue((type, stage), out id!);

     internal void RegisterType(Type type, string id) => _declaredTypes[type] = id;
     internal void RegisterType(Storage type, Stage stage,  string id) => _declaredTypesStorages[(type, stage)] = id;

     public string ToSpirVAs => ToText();
     /// <summary>Texto en formato spirv-as (para depurar y validar con spirv-val).</summary>
     public string ToText()
     {
          var sb = new StringBuilder();
          foreach (SpirVInstruction ins in Instructions)
          {
               sb.Append(FormatInstruction(ins)).Append('\n');
          }

          return sb.ToString();
     }

     /// <summary>Una instrucción como una línea de texto spirv-as: <c>%result = OpX %type operandos</c>.</summary>
     internal static string FormatInstruction(SpirVInstruction ins)
     {
          var sb = new StringBuilder();

          if (ins.Result is not null)
          {
               sb.Append(ins.Result.PadLeft(14)).Append(" = ");
          }
          else
          {
               sb.Append(' ', 17);
          }

          sb.Append(ins.Operation.Name);
          if (ins.ResultType is not null)
          {
               sb.Append(' ').Append(ins.ResultType);
          }

          foreach (SpirVInstructionParameter p in ins.Parameters)
          {
               sb.Append(' ');
               sb.Append(p.Type == SpirVInstructionType.StringLiteral ? $"\"{p.Instruction}\"" : p.Instruction);
          }

          return sb.ToString();
     }

     private static readonly HashSet<string> Terminators =
     [
          "OpBranch", "OpBranchConditional", "OpSwitch", "OpKill", "OpReturn",
          "OpReturnValue", "OpUnreachable", "OpTerminateInvocation"
     ];

     /// <summary>
     /// Comprueba el módulo antes de serializarlo: cada ID usado está definido, ningún ID se define dos veces
     /// y cada función es una secuencia válida de bloques. Un error aquí es un fallo del compilador de Yotsuba,
     /// y el mensaje nombra la instrucción y el ID responsables.
     /// </summary>
     internal void Validate()
     {
          List<SpirVInstruction> instructions = [.. Instructions];

          var defined = new Dictionary<string, SpirVInstruction>();
          foreach (SpirVInstruction instruction in instructions)
          {
               if (instruction.Result is null) continue;

               if (!defined.TryAdd(instruction.Result, instruction))
               {
                    throw new InvalidOperationException(
                         $"Invalid SPIR-V module: the result ID {instruction.Result} is defined twice.\n" +
                         $"  first:  {FormatInstruction(defined[instruction.Result]).Trim()}\n" +
                         $"  second: {FormatInstruction(instruction).Trim()}");
               }
          }

          foreach (SpirVInstruction instruction in instructions)
          {
               if (instruction.ResultType is not null && !defined.ContainsKey(instruction.ResultType))
               {
                    throw UndefinedId(instruction, instruction.ResultType, "result type");
               }

               foreach (SpirVInstructionParameter parameter in instruction.Parameters)
               {
                    if (parameter.Type == SpirVInstructionType.IdRef && !defined.ContainsKey(parameter.Instruction))
                    {
                         throw UndefinedId(instruction, parameter.Instruction, "operand");
                    }
               }
          }

          foreach (FunctionSection function in _functions)
          {
               ValidateFunction(function);
          }
     }

     private static InvalidOperationException UndefinedId(SpirVInstruction instruction, string id, string role) =>
          new($"Invalid SPIR-V module: {id} is used as the {role} of an instruction but is never defined.\n" +
              $"  instruction: {FormatInstruction(instruction).Trim()}");

     private static void ValidateFunction(FunctionSection function)
     {
          List<SpirVInstruction> instructions = [.. function.Header, .. function.Variables, .. function.Body];

          string name = instructions.FirstOrDefault().Result ?? "<unnamed>";

          if (instructions.Count == 0 || instructions[0].Operation.Name != "OpFunction")
          {
               throw new InvalidOperationException(
                    $"Invalid SPIR-V module: function {name} does not start with OpFunction.");
          }

          bool inBlock = false;
          bool sawBlock = false;
          bool ended = false;

          foreach (SpirVInstruction instruction in instructions.Skip(1))
          {
               string operation = instruction.Operation.Name;

               if (ended)
               {
                    throw new InvalidOperationException(
                         $"Invalid SPIR-V module: function {name} has instructions after OpFunctionEnd.\n" +
                         $"  instruction: {FormatInstruction(instruction).Trim()}");
               }

               switch (operation)
               {
                    case "OpFunctionParameter" when !sawBlock:
                         break;

                    case "OpLabel":
                         if (inBlock)
                         {
                              throw new InvalidOperationException(
                                   $"Invalid SPIR-V module: in function {name}, the block before " +
                                   $"{instruction.Result} does not end with a terminator.");
                         }

                         inBlock = sawBlock = true;
                         break;

                    case "OpFunctionEnd":
                         if (inBlock)
                         {
                              throw new InvalidOperationException(
                                   $"Invalid SPIR-V module: the last block of function {name} " +
                                   "does not end with a terminator.");
                         }

                         ended = true;
                         break;

                    default:
                         if (!inBlock)
                         {
                              throw new InvalidOperationException(
                                   $"Invalid SPIR-V module: in function {name}, an instruction is outside any block.\n" +
                                   $"  instruction: {FormatInstruction(instruction).Trim()}");
                         }

                         if (Terminators.Contains(operation)) inBlock = false;
                         break;
               }
          }

          if (!ended)
          {
               throw new InvalidOperationException(
                    $"Invalid SPIR-V module: function {name} does not end with OpFunctionEnd.");
          }
     }
}



public enum SpirVInstructionType
{
     IdRef,
     Enumerant,
     StringLiteral,
     FloatValue,
     DoubleValue,
     IntValue,
     UIntValue,
     LongValue,
     UlongValue
}

public readonly record struct SpirVInstructionParameter(
     string Instruction,
     SpirVInstructionType Type,
     uint Value = 0)
{
     /// <summary>Un enumerante: el texto sale del nombre y el binario del valor numérico.</summary>
     public static SpirVInstructionParameter Enumerant<T>(T value) where T : struct, Enum =>
          new(value.ToString(), SpirVInstructionType.Enumerant, Convert.ToUInt32(value));
}


public readonly record struct SpirVInstruction(
     SpirVOperation Operation,
     SpirVInstructionParameter[] Parameters,
     string? Result = null,
     string? ResultType = null
     );
