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

using System.Globalization;
using System.Text;
using YotsubaShader.Compiler.SpirV.Builder;

namespace YotsubaShader.Compiler.SpirV;

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
