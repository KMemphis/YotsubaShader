using System.Collections.Generic;

namespace YotsubaShader.Concepts.Methods
{
    public class CSharpIntrinsicToIntrinsicGPU
    {
        // Nombres de metadata. Se escriben a mano porque MathF no existe en netstandard2.0
        // y varios miembros de Math tampoco (Asinh, Acosh, Atanh, Log2, Clamp, FusedMultiplyAdd...).
        const string M = "System.Math";
        const string F = "System.MathF";

        // Los mismos métodos existen con el mismo nombre en Math y en MathF.
        static readonly (string Name, Intrinsic Intrinsic)[] Shared =
        {
            ("Sin", Intrinsic.Sin),
            ("Cos", Intrinsic.Cos),
            ("Tan", Intrinsic.Tan),
            ("Asin", Intrinsic.Asin),
            ("Acos", Intrinsic.Acos),
            ("Atan", Intrinsic.Atan),
            ("Atan2", Intrinsic.Atan2),
            ("Sinh", Intrinsic.Sinh),
            ("Cosh", Intrinsic.Cosh),
            ("Tanh", Intrinsic.Tanh),
            ("Asinh", Intrinsic.Asinh),
            ("Acosh", Intrinsic.Acosh),
            ("Atanh", Intrinsic.Atanh),
            ("Pow", Intrinsic.Pow),
            ("Exp", Intrinsic.Exp),
            ("Log", Intrinsic.Log),
            ("Log2", Intrinsic.Log2),
            ("Log10", Intrinsic.Log10),
            ("Sqrt", Intrinsic.Sqrt),
            ("ReciprocalSqrtEstimate", Intrinsic.InverseSqrt),
            ("Abs", Intrinsic.Abs),
            ("Sign", Intrinsic.Sign),
            ("Floor", Intrinsic.Floor),
            ("Ceiling", Intrinsic.Ceiling),
            ("Round", Intrinsic.Round),
            ("Truncate", Intrinsic.Truncate),
            ("Min", Intrinsic.Min),
            ("Max", Intrinsic.Max),
            ("Clamp", Intrinsic.Clamp),
            ("FusedMultiplyAdd", Intrinsic.FusedMultiplyAdd),
        };

        public static readonly Dictionary<string, Intrinsic> Map = Build();

        static Dictionary<string, Intrinsic> Build()
        {
            var map = new Dictionary<string, Intrinsic>();
            foreach (var (name, intrinsic) in Shared)
            {
                map[$"{M}.{name}"] = intrinsic;
                map[$"{F}.{name}"] = intrinsic;
            }
            return map;
        }
    }
}