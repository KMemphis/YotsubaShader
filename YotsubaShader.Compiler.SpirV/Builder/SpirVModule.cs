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
     internal List<SpirVInstruction> Instructions = [];

     private readonly HashSet<string> _definedIds = [];
     private readonly Dictionary<Type, string> _declaredTypes = [];
     private readonly Dictionary<Storage, string> _declaredTypesStorages = [];

     /// <summary>Instrucción sin resultado (OpStore, OpReturn, OpDecorate...).</summary>
     public void AddLine(SpirVOperation operation, params SpirVInstructionParameter[] parameters)
     {
          if (operation.HasResult)
          {
               throw new InvalidOperationException($"{operation.Name} produce un resultado; usa AddResultLine.");
          }

          Instructions.Add(new SpirVInstruction(operation, parameters));
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
               throw new InvalidOperationException($"El ID {result} ya está definido en el módulo.");
          }

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

          Instructions.Add(new SpirVInstruction(operation, parameters, result, resultType));
          return result;
     }

     public bool IsDefined(string id) => _definedIds.Contains(id);

     internal bool TryGetDeclaredType(Type type, out string id) => _declaredTypes.TryGetValue(type, out id!);
     internal bool TryGetDeclaredType(Storage type, out string id) => _declaredTypesStorages.TryGetValue(type, out id!);

     internal void RegisterType(Type type, string id) => _declaredTypes[type] = id;
     internal void RegisterType(Storage type, string id) => _declaredTypesStorages[type] = id;

     /// <summary>Texto en formato spirv-as (para depurar y validar con spirv-val).</summary>
     public string ToText()
     {
          var sb = new StringBuilder();
          foreach (SpirVInstruction ins in Instructions)
          {
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

               sb.Append('\n');
          }

          return sb.ToString();
     }
}



public enum SpirVInstructionType
{
     IdRef,
     Enumerant,
     StringLiteral,
     FloatValue,
     IntValue,
     UIntValue
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
