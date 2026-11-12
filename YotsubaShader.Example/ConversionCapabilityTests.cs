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

namespace YotsubaShader.Example;

/// <summary>#20: la capacidad que pide una conversión numérica se declara aunque nada más la pida.</summary>
[Collection("ShaderProcessor")]
public class ConversionCapabilityTests
{
    /// <summary>Las capacidades del módulo para un fragment shader con este cuerpo. También lo serializa:
    /// si algún tipo de la conversión no estuviera declarado, la validación previa lo rechazaría.</summary>
    private static string[] Capabilities(string body)
    {
        var module = new IRToSpirvModule().Compile(ShaderIr.Process($$"""
            using YotsubaShader.Language;

            namespace Game;

            public class S
            {
                [ShaderStage(Stage.Fragment)]
                public float FS()
                {
                    {{body}}
                }
            }
            """));

        Assert.NotEmpty(new SpirVCompiler().Compile(module));

        return [.. module.ToText().Split('\n')
            .Where(line => line.Contains("OpCapability"))
            .Select(line => line.Trim().Replace("OpCapability ", ""))];
    }

    [Fact]
    public void A_double_literal_converted_to_float_needs_Float64()
    {
        Assert.Equal(["Shader", "Float64"], Capabilities("float f = (float)1.5; return f;"));
    }

    [Fact]
    public void A_conversion_to_double_needs_Float64_even_when_no_double_is_declared()
    {
        Assert.Equal(["Shader", "Float64"], Capabilities("int i = 3; float f = (float)(double)i; return f;"));
    }

    [Fact]
    public void An_unsigned_value_converted_through_double_needs_Float64()
    {
        Assert.Equal(["Shader", "Float64"], Capabilities("int i = 3; uint u = (uint)(double)i; return (float)u;"));
    }

    [Fact]
    public void A_double_variable_converted_to_float_needs_Float64()
    {
        Assert.Equal(["Shader", "Float64"], Capabilities("double d = 2.0; float f = (float)d; return f;"));
    }

    [Fact]
    public void A_double_converted_to_int_needs_Float64()
    {
        Assert.Equal(["Shader", "Float64"], Capabilities("double d = 2.0; int i = (int)d; return (float)i;"));
    }

    [Fact]
    public void Conversions_between_32_bit_types_need_no_extra_capability()
    {
        Assert.Equal(["Shader"], Capabilities("int i = 3; return (float)i;"));
        Assert.Equal(["Shader"], Capabilities("int i = 3; uint u = (uint)i; return (float)u;"));
        Assert.Equal(["Shader"], Capabilities("float f = 2.5f; int i = (int)f; return (float)i;"));
        Assert.Equal(["Shader"], Capabilities("float f = 2.5f; uint u = (uint)f; return (float)u;"));
    }

    [Fact]
    public void A_double_inside_a_nested_conversion_is_found()
    {
        Assert.Equal(["Shader", "Float64"], Capabilities("int i = 3; return (float)((int)(1.5 + (double)i));"));
    }
}
