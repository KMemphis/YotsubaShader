using System;

namespace YotsubaShader.Concepts.Methods
{
    /// <summary>
    /// An operator of a vector type that works component by component, as in the shading languages. A scalar operand
    /// is repeated for every component. The compiler applies the C# operator to each pair of components; the C# body
    /// is only used when the code runs on the CPU.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public class ComponentwiseOperatorAttribute : Attribute
    {
    }
}
