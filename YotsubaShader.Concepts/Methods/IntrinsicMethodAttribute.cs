using System;

namespace YotsubaShader.Concepts.Methods
{
    [AttributeUsage(AttributeTargets.Method)]
    public class IntrinsicMethodAttribute : Attribute
    {
        public Intrinsic IntrinsicType;

        public IntrinsicMethodAttribute(Intrinsic intrinsicType)
        {
            IntrinsicType = intrinsicType;
        }
    }
}