using System.Text;
using Microsoft.Xna.Framework.Graphics;
using YotsubaShader.Compiler.SpirV.YS_Types;

namespace YotsubaShader.Compiler.SpirV.Builder;

public class SpirVModule
{
     internal List<SpirVInstruction> Instructions = [];

     private readonly HashSet<string> _definedIds = [];
     private readonly Dictionary<Type, string> _declaredTypes = [];

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

     internal void RegisterType(Type type, string id) => _declaredTypes[type] = id;

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
