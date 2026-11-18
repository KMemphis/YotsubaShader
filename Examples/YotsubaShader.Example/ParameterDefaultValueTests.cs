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
using YotsubaShader.Compiler.SpirV;
using YotsubaShader.IR.Intermediate;
using YotsubaShader.IR.Intermediate.FunctionBody;
using YotsubaShader.IR.Intermediate.FunctionBody.Expressions.Literals;
using YotsubaShader.IR.Intermediate.Types;

namespace YotsubaShader.Example;

/// <summary>#16: el valor por defecto de un parámetro conserva su tipo; un uint no pasa por Int32.</summary>
[Collection("ShaderProcessor")]
public class ParameterDefaultValueTests
{
    private static Shader Process(string parameter, string call = "return G(2f);") =>
        ShaderIr.Process($$"""
            using YotsubaShader.Language;

            namespace Game;

            public class S
            {
                float G(float x, {{parameter}}) { return x; }

                [ShaderStage(Stage.Fragment)]
                public float FS() { {{call}} }
            }
            """);

    private static Expression? DefaultOf(Shader shader) =>
        Assert.IsType<Int>(shader.Single("G").Inputs[1]).InitialValue;

    [Theory]
    [InlineData("0u", "0")]
    [InlineData("1u", "1")]
    [InlineData("2147483647u", "2147483647")]
    [InlineData("2147483648u", "2147483648")]
    [InlineData("4000000000u", "4000000000")]
    [InlineData("4294967295u", "4294967295")]
    public void A_uint_default_keeps_its_unsigned_value(string written, string expected)
    {
        Shader shader = Process($"uint n = {written}");

        Assert.Equal(new IntLiteral(expected, false), DefaultOf(shader));
    }

    [Fact]
    public void An_int_default_keeps_its_sign()
    {
        Assert.Equal(new IntLiteral("-3", true), DefaultOf(Process("int n = -3")));
    }

    [Fact]
    public void An_int_default_is_written_the_same_in_any_culture()
    {
        CultureInfo original = CultureInfo.CurrentCulture;
        try
        {
            var odd = (CultureInfo)CultureInfo.InvariantCulture.Clone();
            odd.NumberFormat.NegativeSign = "~";
            CultureInfo.CurrentCulture = odd;

            Assert.Equal(new IntLiteral("-3", true), DefaultOf(Process("int n = -3")));
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [Fact]
    public void Float_double_and_bool_defaults_are_kept()
    {
        Shader floatShader = Process("float n = 1.5f");
        Assert.Equal(new FloatLiteral(1.5f), Assert.IsType<Float>(floatShader.Single("G").Inputs[1]).InitialValue);

        Shader doubleShader = Process("double n = 2.5");
        Assert.Equal(new DoubleLiteral(2.5), Assert.IsType<YotsubaShader.IR.Intermediate.Types.Double>(doubleShader.Single("G").Inputs[1]).InitialValue);

        Shader boolShader = Process("bool n = true");
        Assert.Equal(new BoolLiteral(true), Assert.IsType<Bool>(boolShader.Single("G").Inputs[1]).InitialValue);
    }

    [Fact]
    public void A_parameter_without_a_default_has_no_initial_value()
    {
        Assert.Null(DefaultOf(Process("uint n", "return G(2f, 1u);")));
    }

    [Theory]
    [InlineData("1u", "OpConstant %uint 1")]
    [InlineData("4000000000u", "OpConstant %uint 4000000000")]
    public void A_call_that_omits_a_uint_default_passes_it_as_an_unsigned_constant(string written, string expected)
    {
        string text = new IRToSpirvModule().Compile(Process($"uint n = {written}")).ToText();

        Assert.Contains(expected, text);
    }
}
