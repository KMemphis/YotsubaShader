using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using YotsubaShader.Frontend.Model;
using YotsubaShader.Stages;

namespace YotsubaShader.Frontend.Tests;

public class ShaderDiscoveryTests
{
    private const string MegaShaderLike = """
        using YotsubaShader.Compiler.Stages;

        namespace Game;

        public struct VertexInput { public float X; public float Y; }
        public struct VertexOutput { public float X; public float Y; }

        public sealed partial class MegaShader
        {
            public string DebugName { get; set; } = "mega";

            [ShaderStage(ShaderStage.Vertex)]
            private VertexOutput VS(VertexInput input) => new VertexOutput { X = input.X, Y = input.Y };

            [ShaderStage(ShaderStage.Fragment)]
            private float FS(VertexOutput input, int index) => Lighting(input.X);

            private float Lighting(float x) => x;

            public void RefreshFromDatabase() { }
        }
        """;

    [Fact]
    public void Finds_the_shader_class_with_its_stages_in_declaration_order()
    {
        ImmutableArray<ShaderInfo> shaders = ShaderDiscovery.Find(TestCompilation.Create(MegaShaderLike));

        ShaderInfo shader = Assert.Single(shaders);
        Assert.Equal("Game.MegaShader", shader.FullName);
        Assert.Equal(["VS", "FS"], shader.EntryPoints.Select(e => e.Name));
        Assert.Equal([ShaderStage.Vertex, ShaderStage.Fragment], shader.EntryPoints.Select(e => e.Stage));
    }

    [Fact]
    public void Entry_points_expose_parameters_and_return_type()
    {
        ShaderInfo shader = Assert.Single(ShaderDiscovery.Find(TestCompilation.Create(MegaShaderLike)));

        EntryPointInfo vs = shader.EntryPoints[0];
        Assert.Equal("VertexOutput", vs.ReturnType.Name);
        ParameterInfo input = Assert.Single(vs.Parameters);
        Assert.Equal("input", input.Name);
        Assert.Equal("VertexInput", input.Type.Name);

        EntryPointInfo fs = shader.EntryPoints[1];
        Assert.Equal(SpecialType.System_Single, fs.ReturnType.SpecialType);
        Assert.Equal(["input", "index"], fs.Parameters.Select(p => p.Name));
        Assert.Equal(SpecialType.System_Int32, fs.Parameters[1].Type.SpecialType);
    }

    [Fact]
    public void Helpers_and_cpu_methods_are_not_entry_points()
    {
        ShaderInfo shader = Assert.Single(ShaderDiscovery.Find(TestCompilation.Create(MegaShaderLike)));

        Assert.DoesNotContain(shader.EntryPoints, e => e.Name is "Lighting" or "RefreshFromDatabase");
    }

    [Fact]
    public void Class_without_stage_methods_is_not_a_shader()
    {
        const string code = """
            public class Plain { public void Update() { } }
            """;

        Assert.Empty(ShaderDiscovery.Find(TestCompilation.Create(code)));
    }

    [Fact]
    public void Finds_shaders_in_nested_types_and_several_namespaces()
    {
        const string code = """
            using YotsubaShader.Compiler.Stages;

            namespace A.B
            {
                public class Outer
                {
                    public class Inner
                    {
                        [ShaderStage(ShaderStage.Compute)] void CS() { }
                    }
                }
            }

            public class TopLevel
            {
                [ShaderStage(ShaderStage.Fragment)] static float FS() => 1f;
            }
            """;

        ImmutableArray<ShaderInfo> shaders = ShaderDiscovery.Find(TestCompilation.Create(code));

        Assert.Equal(["A.B.Outer.Inner", "TopLevel"], shaders.Select(s => s.FullName).OrderBy(n => n));
        Assert.Equal(ShaderStage.Compute, shaders.Single(s => s.Name == "Inner").EntryPoints[0].Stage);
    }

    [Theory]
    [InlineData("[ShaderStage(ShaderStage.Fragment)]")]
    [InlineData("[ShaderStageAttribute(ShaderStage.Fragment)]")]
    [InlineData("[Ys.ShaderStage(Ys.ShaderStage.Fragment)]")]
    [InlineData("[Stage(ShaderStage.Fragment)]")]
    [InlineData("[YotsubaShader.Compiler.Stages.ShaderStage(YotsubaShader.Compiler.Stages.ShaderStage.Fragment)]")]
    public void Attribute_is_matched_by_symbol_not_by_text(string attribute)
    {
        string code = $$"""
            using YotsubaShader.Compiler.Stages;
            using Ys = YotsubaShader.Compiler.Stages;
            using Stage = YotsubaShader.Compiler.Stages.ShaderStageAttribute;

            public class S
            {
                {{attribute}}
                float FS() => 1f;
            }
            """;

        ShaderInfo shader = Assert.Single(ShaderDiscovery.Find(TestCompilation.Create(code)));
        Assert.Equal(ShaderStage.Fragment, Assert.Single(shader.EntryPoints).Stage);
    }

    [Fact]
    public void A_look_alike_attribute_from_another_namespace_is_ignored()
    {
        const string code = """
            namespace Other { public class ShaderStageAttribute : System.Attribute { public ShaderStageAttribute(int s) { } } }

            public class S
            {
                [Other.ShaderStage(1)] float FS() => 1f;
            }
            """;

        Assert.Empty(ShaderDiscovery.Find(TestCompilation.Create(code)));
    }

    [Fact]
    public void Only_types_of_this_project_are_scanned_not_referenced_ones()
    {
        CSharpCompilationHolder lib = CSharpCompilationHolder.From(TestCompilation.Create(MegaShaderLike, "ShaderLibrary"));

        var game = TestCompilation.Create("public class Game { }", "Game", true, lib.Reference);

        Assert.Single(ShaderDiscovery.Find(lib.Compilation));
        Assert.Empty(ShaderDiscovery.Find(game));
    }

    [Fact]
    public void Returns_nothing_when_the_project_does_not_reference_YotsubaShader()
    {
        var compilation = TestCompilation.Create("public class S { }", referenceYotsubaShader: false);

        Assert.Empty(ShaderDiscovery.Find(compilation));
    }

    private sealed record CSharpCompilationHolder(Compilation Compilation, MetadataReference Reference)
    {
        public static CSharpCompilationHolder From(Compilation c) => new(c, c.ToMetadataReference());
    }
}
