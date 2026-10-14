namespace YotsubaShader.Language.Native.Types.Vector.Exceptions;

public class InconsistentVectorComponentTypeException :
    Exception
{
    public InconsistentVectorComponentTypeException(Type type1, Type type2) :
        base($"Vector is declared with a components type {type1.FullName} and {type2.FullName} at the same time")
    {
        
    }
    
    public InconsistentVectorComponentTypeException(string type1, string type2) :
        base($"Vector is declared with a components type {type1} and {type2} at the same time")
    {
        
    }
}