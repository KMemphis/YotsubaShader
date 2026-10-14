namespace YotsubaShader.Language.Native.Types.Vector.Exceptions;

public class VectorComponentTypeNotValidException : Exception
{
    public VectorComponentTypeNotValidException(
        string type) : base($"Vector type is declared but doesnt have a valid component type. " +
                            $"Type incompatible '{type}' is founded")

    {

    }
}