using YotsubaShader.Compiler.SpirV;
using YotsubaShader.IR.Intermediate;
using YotsubaShader.IR.Intermediate.Slot;
using YotsubaShader.IR.Intermediate.Types;

namespace YotsubaShader.Example;

/// <summary>#25: los recursos Uniform y StorageBuffer llegan al IR y a SPIR-V, o fallan con un mensaje claro.</summary>
[Collection("ShaderProcessor")]
public class BufferResourceTests
{
    private static Shader Process(string members, string body = "return 0f;") =>
        ShaderIr.Process($$"""
            using YotsubaShader.Language;
            using YotsubaShader.SpirvSyntax.Resources;

            namespace Game;

            public struct Params
            {
                public float Time;
                public float Scale;
            }

            public class S
            {
                {{members}}

                [ShaderStage(Stage.Fragment)]
                public float FS()
                {
                    {{body}}
                }
            }
            """);

    private static void Rejects(string members, string expected)
    {
        var exception = Assert.ThrowsAny<Exception>(() => Process(members));
        Assert.Contains(expected, exception.Message);
    }

    [Fact]
    public void A_uniform_field_is_kept_in_the_ir_with_its_binding()
    {
        Shader shader = Process("[Uniform] [DescriptorSet(1)] [Binding(2)] public Params Globals;");

        Storage resource = Assert.Single(shader.GlobalResources);
        Assert.Equal("Globals", resource.HumanName);
        Assert.Equal(StorageKind.Uniform, resource.Kind);
        Assert.Equal(new DescriptorSlot(1, 2), resource.Slot);
    }

    [Fact]
    public void A_storage_buffer_field_is_kept_in_the_ir_with_its_binding()
    {
        Shader shader = Process("[StorageBuffer] [DescriptorSet(0)] [Binding(3)] public Params Items;");

        Storage resource = Assert.Single(shader.GlobalResources);
        Assert.Equal("Items", resource.HumanName);
        Assert.Equal(StorageKind.StorageBuffer, resource.Kind);
        Assert.Equal(new DescriptorSlot(0, 3), resource.Slot);
    }

    [Fact]
    public void A_uniform_reaches_spirv_as_a_decorated_global_variable()
    {
        string text = new IRToSpirvModule().Compile(
            Process("[Uniform] [DescriptorSet(1)] [Binding(2)] public Params Globals;", "return Globals.Time;")).ToText();

        Assert.Contains("%resource_Globals = OpVariable", text);
        Assert.Contains("Uniform", text);
        Assert.Contains("OpDecorate %resource_Globals DescriptorSet 1", text);
        Assert.Contains("OpDecorate %resource_Globals Binding 2", text);
        Assert.Contains("OpAccessChain", text);
    }

    [Fact]
    public void A_storage_buffer_reaches_spirv_as_a_decorated_global_variable()
    {
        string text = new IRToSpirvModule().Compile(
            Process("[StorageBuffer] [DescriptorSet(0)] [Binding(3)] public Params Items;", "return Items.Scale;")).ToText();

        Assert.Contains("%resource_Items = OpVariable", text);
        Assert.Contains("StorageBuffer", text);
        Assert.Contains("OpDecorate %resource_Items DescriptorSet 0", text);
        Assert.Contains("OpDecorate %resource_Items Binding 3", text);
    }

    [Fact]
    public void The_entry_point_lists_the_resources_it_can_reach()
    {
        string text = new IRToSpirvModule().Compile(
            Process("[Uniform] [DescriptorSet(0)] [Binding(0)] public Params Globals;", "return Globals.Time;")).ToText();

        string entryPoint = text.Split('\n').Single(line => line.Contains("OpEntryPoint"));
        Assert.Contains("%resource_Globals", entryPoint);
    }

    [Fact]
    public void A_buffer_without_a_binding_is_rejected()
    {
        Rejects("[Uniform] public Params Globals;", "has no descriptor binding");
    }

    [Fact]
    public void A_buffer_declared_as_a_property_is_rejected()
    {
        Rejects("[Uniform] [DescriptorSet(0)] [Binding(0)] public Params Globals { get; set; }", "Declare it as a field");
    }

    [Fact]
    public void A_buffer_of_an_array_type_is_rejected()
    {
        Rejects("[Uniform] [DescriptorSet(0)] [Binding(0)] public float[] Values;",
            "Only scalars, vectors and structs are supported");
    }

    [Fact]
    public void A_buffer_that_is_both_uniform_and_storage_is_rejected()
    {
        Rejects("[Uniform] [StorageBuffer] [DescriptorSet(0)] [Binding(0)] public Params Both;",
            "cannot be both a uniform and a storage buffer");
    }

    [Fact]
    public void A_plain_field_is_still_not_a_resource()
    {
        Assert.Empty(Process("public float Plain;").GlobalResources);
    }
}