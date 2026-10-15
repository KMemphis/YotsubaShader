using System.Globalization;
using System.Text;
using YotsubaShader.Compiler.SpirV.Builder;

namespace YotsubaShader.Compiler.SpirV.Compiler;

public class SpirVCompiler
{
    private int nextID = 1;
    private Dictionary<string, int> StringToID = new Dictionary<string, int>();

    public byte[] Compile(SpirVModule module)
    {
        ArgumentNullException.ThrowIfNull(module);

        nextID = 1;
        StringToID.Clear();

        return ParseModuleToSpirV(module);
    }

    private byte[] ParseModuleToSpirV(SpirVModule module)
    {
        List<byte> spirV = new List<byte>();
        
        foreach (var instruction in module.Instructions)
        {
            // Palabra 0 reservada: el word count solo se conoce cuando ya se escribieron todos los operandos.
            int instructionStart = spirV.Count;
            spirV.AddRange(new byte[4]);
            
            // En las instrucciones que tienen Result Type, este va antes del Result ID.
            if (instruction.Operation.HasResultType)
            {
                if (instruction.ResultType is null)
                {
                    throw new InvalidOperationException(
                        $"{instruction.Operation.Name} necesita un Result Type.");
                }

                WriteId(spirV, instruction.ResultType);

            }

            if (instruction.Result is not null)
            {
                WriteId(spirV, instruction.Result);
            }

            foreach (var parameter in instruction.Parameters)
            {
                WriteParameter(spirV, parameter);
            }

            // Word count = palabras escritas (cabecera, resultado y operandos; una cadena ocupa varias).
            int totalWords = (spirV.Count - instructionStart) / 4;
            uint header = instruction.Operation.Header(totalWords);
            byte[] headerBytes = BitConverter.GetBytes(header);
            for (int i = 0; i < headerBytes.Length; i++)
            {
                spirV[instructionStart + i] = headerBytes[i];
            }
        }
        
        var moduleHeader = new List<byte>();
        moduleHeader.AddRange(BitConverter.GetBytes(0x07230203u));   // número mágico
        moduleHeader.AddRange(BitConverter.GetBytes(0x00010600u));   // versión 1.6
        moduleHeader.AddRange(BitConverter.GetBytes(0u));            // generador
        moduleHeader.AddRange(BitConverter.GetBytes((uint)nextID));  // bound
        moduleHeader.AddRange(BitConverter.GetBytes(0u));            // esquema
        spirV.InsertRange(0, moduleHeader);
        
        return [.. spirV];
    }

    private void WriteParameter(List<byte> spirV, SpirVInstructionParameter parameter)
    {
        switch (parameter.Type)
        {
            case SpirVInstructionType.FloatValue:
                float floatValue = float.Parse(parameter.Instruction, CultureInfo.InvariantCulture);
                spirV.AddRange(BitConverter.GetBytes(floatValue));
                break;
            case SpirVInstructionType.IntValue:
                int intValue = int.Parse(parameter.Instruction);
                spirV.AddRange(BitConverter.GetBytes(intValue));
                break;
            case SpirVInstructionType.Enumerant:
                spirV.AddRange(BitConverter.GetBytes(parameter.Value));
                break;
            case SpirVInstructionType.UIntValue:
                uint uintValue = uint.Parse(parameter.Instruction);
                spirV.AddRange(BitConverter.GetBytes(uintValue));
                break;
            case SpirVInstructionType.StringLiteral:
                byte[] utf8 = Encoding.UTF8.GetBytes(parameter.Instruction);
                int byteCount = utf8.Length + 1;
                int wordCount = (byteCount + 3) / 4;
                byte[] buffer = new byte[wordCount * 4];
                utf8.CopyTo(buffer, 0);
                spirV.AddRange(buffer);
                break;
            case SpirVInstructionType.IdRef:
                WriteId(spirV, parameter.Instruction);
                break;
            default:
                throw new ArgumentOutOfRangeException(
                    nameof(parameter), $"Tipo de parámetro desconocido: {parameter.Type}");
        }
    }

    private int GetOrCreateId(string name)
    {
        if (!StringToID.TryGetValue(name, out int id))
        {
            id = nextID++;
            StringToID[name] = id;
        }

        return id;
    }
    
    private void WriteId(List<byte> spirV, string symbolicId)
    {
        int numericId = GetOrCreateId(symbolicId);
        spirV.AddRange(BitConverter.GetBytes(numericId));
    }
}
