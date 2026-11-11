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

using System.Buffers.Binary;
using System.Globalization;
using YotsubaShader.Compiler.SpirV;
using YotsubaShader.Compiler.SpirV.Builder;

namespace YotsubaShader.Example;

/// <summary>#27: el binario SPIR-V se escribe en little-endian, palabra a palabra, sea cual sea el equipo.</summary>
[Collection("ShaderProcessor")]
public class SpirVSerializationTests
{
    private static byte[] Serialize(SpirVModule module) => new SpirVCompiler().Compile(module);

    /// <summary>¿Aparece la secuencia de bytes <paramref name="needle"/> en <paramref name="haystack"/>?</summary>
    private static bool ContainsBytes(byte[] haystack, params byte[] needle) =>
        haystack.AsSpan().IndexOf(needle) >= 0;

    [Fact]
    public void Module_header_is_five_little_endian_words()
    {
        byte[] binary = Serialize(new SpirVModule());

        Assert.Equal(
            [
                0x03, 0x02, 0x23, 0x07, // número mágico 0x07230203
                0x00, 0x06, 0x01, 0x00, // versión 1.6
                0x00, 0x00, 0x00, 0x00, // generador
                0x01, 0x00, 0x00, 0x00, // bound
                0x00, 0x00, 0x00, 0x00  // esquema
            ],
            binary);
    }

    [Fact]
    public void Float_literal_is_one_little_endian_word()
    {
        var module = new SpirVModule();
        module.DeclareFloatConstant(1.0f);

        Assert.True(ContainsBytes(Serialize(module), 0x00, 0x00, 0x80, 0x3F)); // 1.0f = 0x3F800000
    }

    [Fact]
    public void Double_literal_is_two_words_with_the_low_word_first()
    {
        var module = new SpirVModule();
        module.DeclareDoubleConstant(1.0);

        // 1.0 = 0x3FF0000000000000: palabra baja 0x00000000, palabra alta 0x3FF00000.
        Assert.True(ContainsBytes(Serialize(module), 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xF0, 0x3F));
    }

    [Fact]
    public void Unsigned_64_bit_literal_is_two_words_with_the_low_word_first()
    {
        var module = new SpirVModule();
        module.DeclareIntConstant("4294967298", 64, signed: false); // 0x0000000100000002

        Assert.True(ContainsBytes(Serialize(module),
            0x02, 0x00, 0x00, 0x00,  // palabra baja
            0x01, 0x00, 0x00, 0x00)); // palabra alta
    }

    [Fact]
    public void Signed_64_bit_literal_keeps_its_two_complement_words()
    {
        var module = new SpirVModule();
        module.DeclareIntConstant("-2", 64, signed: true); // 0xFFFFFFFFFFFFFFFE

        Assert.True(ContainsBytes(Serialize(module),
            0xFE, 0xFF, 0xFF, 0xFF,
            0xFF, 0xFF, 0xFF, 0xFF));
    }

    [Fact]
    public void Negative_32_bit_literal_is_written_as_its_two_complement_word()
    {
        var module = new SpirVModule();
        module.DeclareIntConstant("-2", 32, signed: true);

        Assert.True(ContainsBytes(Serialize(module), 0xFE, 0xFF, 0xFF, 0xFF));
    }

    [Fact]
    public void String_literal_packs_its_characters_in_order_and_pads_to_whole_words()
    {
        var module = new SpirVModule();
        module.DeclareType(typeof(void));
        module.Name("%void", "main"); // "main" + NUL = 5 bytes = 2 palabras

        Assert.True(ContainsBytes(Serialize(module), 0x6D, 0x61, 0x69, 0x6E, 0x00, 0x00, 0x00, 0x00));
    }

    [Fact]
    public void Number_formatting_of_the_current_culture_does_not_change_the_output()
    {
        byte[] Build()
        {
            var module = new SpirVModule();
            module.DeclareIntConstant("-5", 32, signed: true);
            module.DeclareIntConstant("-5", 64, signed: true);
            module.DeclareIntConstant("7", 32, signed: false);
            return Serialize(module);
        }

        byte[] expected = Build();

        CultureInfo original = CultureInfo.CurrentCulture;
        try
        {
            // Una cultura que no escribe el signo menos como '-': el binario no debe depender de ella.
            var odd = (CultureInfo)CultureInfo.InvariantCulture.Clone();
            odd.NumberFormat.NegativeSign = "~";
            CultureInfo.CurrentCulture = odd;

            Assert.Equal(expected, Build());
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [Fact]
    public void A_whole_compiled_shader_is_a_sequence_of_well_formed_instructions()
    {
        SpirVModule module = new IRToSpirvModule().Compile(ShaderIr.Process("""
            using YotsubaShader.Language;

            namespace Game;

            public class S
            {
                [ShaderStage(Stage.Fragment)]
                public float FS()
                {
                    int[] a = new int[3];
                    double d = 1.5;
                    return a[1] + (float)d;
                }
            }
            """));

        byte[] binary = Serialize(module);

        Assert.Equal(0, binary.Length % 4);
        Assert.Equal(0x07230203u, BinaryPrimitives.ReadUInt32LittleEndian(binary));

        // Tras las cinco palabras de la cabecera, cada instrucción trae su longitud en la mitad alta
        // de la primera palabra: las longitudes deben sumar exactamente el resto del binario.
        int word = 5;
        int totalWords = binary.Length / 4;
        int instructions = 0;

        while (word < totalWords)
        {
            uint first = BinaryPrimitives.ReadUInt32LittleEndian(binary.AsSpan(word * 4));
            int length = (int)(first >> 16);

            Assert.True(length >= 1, $"Instruction {instructions} has a word count of 0.");
            word += length;
            instructions++;
        }

        Assert.Equal(totalWords, word);
        Assert.True(instructions > 10);
    }
}
