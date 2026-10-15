using YotsubaShader.Compiler.SpirV.YS_Types.SpirVFunctions;

namespace YotsubaShader.Compiler.SpirV.Builder;

internal sealed class SpirVFunctionBuilder
{
    private readonly List<SpirVBasicBlock> blocks = [];

    public string Id { get; }
    public string ReturnTypeId { get; }
    public string FunctionTypeId { get; }

    public SpirVFunctionBuilder(
        string id,
        string returnTypeId,
        string functionTypeId)
    {
        Id = id;
        ReturnTypeId = returnTypeId;
        FunctionTypeId = functionTypeId;
    }

    public void AddBlock(SpirVBasicBlock block)
    {
        blocks.Add(block);
    }

    public SpirVFunction Build()
    {
        return new SpirVFunction(
            Id,
            ReturnTypeId,
            FunctionTypeId,
            [],
            [.. blocks]);
    }
}