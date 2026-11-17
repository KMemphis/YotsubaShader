namespace YotsubaShader.Extensions;

public interface IShaderTargetBackend
{
    string Name { get; }

    byte[] Compile(
        CompiledCode shader,
        uint options);
}