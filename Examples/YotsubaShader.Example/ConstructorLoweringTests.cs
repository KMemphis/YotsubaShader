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

using YotsubaShader.IR.Intermediate;
using YotsubaShader.IR.Intermediate.FunctionBody.Expressions;
using YotsubaShader.IR.Intermediate.FunctionBody.Expressions.AssignmentTargets;
using YotsubaShader.IR.Intermediate.FunctionBody.Expressions.FunctionCalls;
using YotsubaShader.IR.Intermediate.FunctionBody.Expressions.Literals;
using YotsubaShader.IR.Intermediate.FunctionBody.Statements;
using YotsubaShader.IR.Intermediate.Types;

namespace YotsubaShader.Example;

/// <summary>#12: un constructor es una función que devuelve el objeto creado (con su propio `this`).</summary>
[Collection("ShaderProcessor")] // ShaderProcessor guarda estado estático: estos tests no pueden correr en paralelo.
public class ConstructorLoweringTests
{
    private const string Header = """
        using YotsubaShader.Language;
        using YotsubaShader.Language.Native.Types.Vector;

        namespace Game;
        """;

    private static Shader Compile(string body) => ShaderIr.Process(Header + "\n" + body);

    [Fact]
    public void Struct_constructor_becomes_a_function_that_returns_the_created_value()
    {
        Shader shader = Compile("""
            public struct Light
            {
                public float Intensity;
                public float Radius;
                public Light(float intensity, float radius) { Intensity = intensity; Radius = radius; }
            }

            public class S
            {
                [ShaderStage(Stage.Fragment)]
                public float FS() { Light l = new Light(0.5f, 2f); return l.Intensity; }
            }
            """);

        Function ctor = shader.Single("NewLight");

        Assert.Equal(["intensity", "radius"], ctor.Inputs.Select(i => i.HumanName));
        Assert.IsType<CustomStruct>(Assert.Single(ctor.Outputs));

        // this = valor cero; asignaciones en orden; return this.
        VariableDeclaration self = Assert.IsType<VariableDeclaration>(ctor.Body[0]);
        Assert.Equal("this", self.Storage.HumanName);

        var assignments = ctor.Body.OfType<Assignment>().ToArray();
        Assert.Equal(["Intensity", "Radius"],
            assignments.Select(a => Assert.IsType<StorageStructPropertyReference>(a.Target).PropertyName));

        Return ret = Assert.IsType<Return>(ctor.Body[^1]);
        Assert.Equal("this", Assert.IsType<StorageReference>(ret.Value).Storage.HumanName);

        // y el shader lo llama con sus argumentos.
        FunctionCallExpression call = shader.Single("FS").CallInitializerOf("l");
        Assert.Equal(ctor.Id, call.Target);
        Assert.Equal(2, call.Arguments.Count);
    }

    [Fact]
    public void Constructor_overload_is_selected_by_the_bound_constructor()
    {
        Shader shader = Compile("""
            public struct Light
            {
                public float Intensity;
                public float Radius;
                public Light(float intensity) { Intensity = intensity; Radius = 1f; }
                public Light(float intensity, float radius) { Intensity = intensity; Radius = radius; }
            }

            public class S
            {
                [ShaderStage(Stage.Fragment)]
                public float FS()
                {
                    Light a = new Light(1f);
                    Light b = new Light(1f, 2f);
                    return a.Radius + b.Radius;
                }
            }
            """);

        Function[] ctors = shader.Functions.Where(f => f.HumanName == "NewLight").ToArray();
        Assert.Equal([1, 2], ctors.Select(c => c.Inputs.Count).Order());

        Function fs = shader.Single("FS");
        Function oneArg = ctors.Single(c => c.Inputs.Count == 1);
        Function twoArgs = ctors.Single(c => c.Inputs.Count == 2);
        Assert.Equal(oneArg.Id, fs.CallInitializerOf("a").Target);
        Assert.Equal(twoArgs.Id, fs.CallInitializerOf("b").Target);
    }

    [Fact]
    public void Nested_constructors_compile_each_argument_as_its_own_call()
    {
        Shader shader = Compile("""
            public struct Inner { public float V; public Inner(float v) { V = v; } }
            public struct Outer { public Inner I; public Outer(Inner i) { I = i; } }

            public class S
            {
                [ShaderStage(Stage.Fragment)]
                public float FS() { Outer o = new Outer(new Inner(3f)); return o.I.V; }
            }
            """);

        FunctionCallExpression outer = shader.Single("FS").CallInitializerOf("o");
        Assert.Equal(shader.Single("NewOuter").Id, outer.Target);

        FunctionCallExpression inner = Assert.IsType<FunctionCallExpression>(Assert.Single(outer.Arguments));
        Assert.Equal(shader.Single("NewInner").Id, inner.Target);
    }

    [Fact]
    public void Object_initializer_runs_after_the_constructor_in_source_order()
    {
        Shader shader = Compile("""
            public struct Light
            {
                public float Intensity;
                public float Radius;
                public Light(float intensity, float radius) { Intensity = intensity; Radius = radius; }
            }

            public class S
            {
                [ShaderStage(Stage.Fragment)]
                public float FS() { Light l = new Light(1f, 2f) { Radius = 9f, Intensity = 4f }; return l.Radius; }
            }
            """);

        SequenceExpression sequence = Assert.IsType<SequenceExpression>(shader.Single("FS").InitializerOf("l"));

        // temp = NewLight(1, 2); temp.Radius = 9; temp.Intensity = 4;
        VariableDeclaration temp = Assert.IsType<VariableDeclaration>(sequence.Prelude[0]);
        Assert.IsType<FunctionCallExpression>(temp.Initializer);

        var assigned = sequence.Prelude.OfType<Assignment>()
            .Select(a => Assert.IsType<StorageStructPropertyReference>(a.Target).PropertyName);
        Assert.Equal(["Radius", "Intensity"], assigned);
    }

    [Fact]
    public void Default_constructor_is_the_zero_value_and_needs_no_function()
    {
        Shader shader = Compile("""
            public struct Light
            {
                public float Intensity;
                public Light(float intensity) { Intensity = intensity; }
            }

            public class S
            {
                [ShaderStage(Stage.Fragment)]
                public float FS() { Light l = new Light(); return l.Intensity; }
            }
            """);

        Assert.IsType<CustomStructExpression>(shader.Single("FS").InitializerOf("l"));
        Assert.DoesNotContain(shader.Functions, f => f.HumanName == "NewLight");
    }

    [Fact]
    public void Vector_constructor_may_assign_only_some_components_by_itself()
    {
        Shader shader = Compile("""
            [Vector]
            public struct V4
            {
                [VectorComponent(0)] public float X;
                [VectorComponent(1)] public float Y;
                [VectorComponent(2)] public float Z;
                [VectorComponent(3)] public float W;
                public V4(float x, float y, float z) { X = x; Y = y; Z = z; W = 1; }
            }

            public class S
            {
                [ShaderStage(Stage.Fragment)]
                public float FS() { V4 v = new V4(0f, 0f, 3f); return v.Z; }
            }
            """);

        Function ctor = shader.Single("NewV4");
        Assert.Equal(3, ctor.Inputs.Count);
        Assert.Equal(["X", "Y", "Z", "W"],
            ctor.Body.OfType<Assignment>()
                .Select(a => Assert.IsType<StorageStructPropertyReference>(a.Target).PropertyName));
    }

    [Fact]
    public void Constructor_chained_with_this_is_rejected_with_a_clear_message()
    {
        const string code = """
            public struct Light
            {
                public float Intensity;
                public float Radius;
                public Light(float both) : this(both, both) { }
                public Light(float intensity, float radius) { Intensity = intensity; Radius = radius; }
            }

            public class S
            {
                [ShaderStage(Stage.Fragment)]
                public float FS() { Light l = new Light(1f); return l.Radius; }
            }
            """;

        var error = Assert.Throws<NotSupportedException>(() => Compile(code));
        Assert.Contains("this(...)", error.Message);
    }

    [Fact]
    public void Host_only_constructor_is_rejected()
    {
        const string code = """
            public class S
            {
                [ShaderStage(Stage.Fragment)]
                public float FS() { var v = new System.Numerics.Vector2(1f, 2f); return v.X; }
            }
            """;

        Assert.Throws<NotSupportedException>(() => Compile(code));
    }
}
