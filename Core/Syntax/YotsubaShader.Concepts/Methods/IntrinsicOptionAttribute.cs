using System;

namespace YotsubaShader.Concepts.Methods
{
    /// <summary>
    /// Marca un struct que es solo un envoltorio de argumentos de una intrínseca (por ejemplo level(2.0) en MSL).
    /// El compilador sustituye <c>new level(x)</c> por sus argumentos.
    /// </summary>
    [AttributeUsage(AttributeTargets.Struct)]
    public class IntrinsicOptionAttribute : Attribute
    {
    }
}
