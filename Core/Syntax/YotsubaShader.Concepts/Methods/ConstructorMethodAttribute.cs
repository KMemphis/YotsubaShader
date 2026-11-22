using System;

namespace YotsubaShader.Concepts.Methods
{
    /// <summary>
    /// Builds the vector or matrix the method returns (or the type that declares the constructor) from the components
    /// of its arguments, in order. For a vector, a single scalar argument fills every component. For a matrix, a single
    /// scalar fills the diagonal, a single matrix is copied (cut or completed with the identity), and otherwise the
    /// components fill the matrix column by column, or row by row when <see cref="RowMajor"/> is set (HLSL).
    /// The compiler turns the call into a composite construction (OpCompositeConstruct); the C# body is only used when
    /// the code runs on the CPU.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Constructor)]
    public class ConstructorMethodAttribute : Attribute
    {
        /// <summary>The arguments of a matrix constructor fill it row by row (float2x2(m00, m01, m10, m11) in HLSL).</summary>
        public bool RowMajor { get; set; }
    }
}
