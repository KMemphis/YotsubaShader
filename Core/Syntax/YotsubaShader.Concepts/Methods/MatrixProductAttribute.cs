using System;

namespace YotsubaShader.Concepts.Methods
{
    /// <summary>
    /// The * operator of a matrix as linear algebra, as in GLSL, MSL and WGSL: matrix * vector, vector * matrix and
    /// matrix * matrix. The compiler turns it into OpMatrixTimesVector, OpVectorTimesMatrix or OpMatrixTimesMatrix;
    /// the C# body is only used when the code runs on the CPU.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public class MatrixProductAttribute : Attribute
    {
    }
}
