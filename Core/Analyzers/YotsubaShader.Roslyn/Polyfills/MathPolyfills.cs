// Código que YotsubaShader añade a la compilación del shader: métodos de System.Math/MathF que la GPU no tiene
// como instrucción, escritos con las que sí tiene. Se compilan como cualquier función auxiliar.
// MidpointRounding llega como int (ToEven = 0, AwayFromZero = 1, ToZero = 2, ToNegativeInfinity = 3,
// ToPositiveInfinity = 4).
namespace YotsubaShader.Generated
{
    internal static class MathPolyfills
    {
        // ── Round con dígitos y modo de redondeo ──
        public static double RoundDigits(double x, int digits) => RoundMode(x, digits, 0);
        public static double RoundWithMode(double x, int mode) => RoundMode(x, 0, mode);
        public static double RoundDigitsWithMode(double x, int digits, int mode) => RoundMode(x, digits, mode);
        public static float RoundDigits(float x, int digits) => RoundMode(x, digits, 0);
        public static float RoundWithMode(float x, int mode) => RoundMode(x, 0, mode);
        public static float RoundDigitsWithMode(float x, int digits, int mode) => RoundMode(x, digits, mode);

        private static double RoundMode(double x, int digits, int mode)
        {
            double scale = System.Math.Pow(10.0, digits);
            double v = x * scale;
            double r;
            switch (mode)
            {
                case 1: r = System.Math.Sign(v) * System.Math.Floor(System.Math.Abs(v) + 0.5); break;
                case 2: r = System.Math.Truncate(v); break;
                case 3: r = System.Math.Floor(v); break;
                case 4: r = System.Math.Ceiling(v); break;
                default: r = System.Math.Round(v); break;
            }

            return r / scale;
        }

        private static float RoundMode(float x, int digits, int mode)
        {
            float scale = System.MathF.Pow(10f, digits);
            float v = x * scale;
            float r;
            switch (mode)
            {
                case 1: r = System.MathF.Sign(v) * System.MathF.Floor(System.MathF.Abs(v) + 0.5f); break;
                case 2: r = System.MathF.Truncate(v); break;
                case 3: r = System.MathF.Floor(v); break;
                case 4: r = System.MathF.Ceiling(v); break;
                default: r = System.MathF.Round(v); break;
            }

            return r / scale;
        }

        // ── Raíz cúbica, signo, resto IEEE ──
        public static double Cbrt(double x) => System.Math.Sign(x) * System.Math.Pow(System.Math.Abs(x), 1.0 / 3.0);
        public static float Cbrt(float x) => System.MathF.Sign(x) * System.MathF.Pow(System.MathF.Abs(x), 1f / 3f);

        public static double CopySign(double x, double y) => IsNegative(y) ? -System.Math.Abs(x) : System.Math.Abs(x);
        public static float CopySign(float x, float y) => IsNegative(y) ? -System.MathF.Abs(x) : System.MathF.Abs(x);

        // -0.0 también es negativo: 1/-0 = -infinito.
        private static bool IsNegative(double y) => y < 0.0 || (y == 0.0 && 1.0 / y < 0.0);
        private static bool IsNegative(float y) => y < 0f || (y == 0f && 1f / y < 0f);

        public static double IEEERemainder(double x, double y) => x - y * System.Math.Round(x / y);
        public static float IEEERemainder(float x, float y) => x - y * System.MathF.Round(x / y);

        // ── Mayor/menor magnitud ──
        public static double MaxMagnitude(double x, double y)
        {
            double ax = System.Math.Abs(x), ay = System.Math.Abs(y);
            return ax > ay ? x : ay > ax ? y : System.Math.Max(x, y);
        }

        public static float MaxMagnitude(float x, float y)
        {
            float ax = System.MathF.Abs(x), ay = System.MathF.Abs(y);
            return ax > ay ? x : ay > ax ? y : System.MathF.Max(x, y);
        }

        public static double MinMagnitude(double x, double y)
        {
            double ax = System.Math.Abs(x), ay = System.Math.Abs(y);
            return ax < ay ? x : ay < ax ? y : System.Math.Min(x, y);
        }

        public static float MinMagnitude(float x, float y)
        {
            float ax = System.MathF.Abs(x), ay = System.MathF.Abs(y);
            return ax < ay ? x : ay < ax ? y : System.MathF.Min(x, y);
        }

        // ── Exponente y siguiente float representable ──
        public static int ILogB(double x) => (int)System.Math.Floor(System.Math.Log2(System.Math.Abs(x)));
        public static int ILogB(float x) => (int)System.MathF.Floor(System.MathF.Log2(System.MathF.Abs(x)));

        public static float BitIncrement(float x)
        {
            int bits = System.BitConverter.SingleToInt32Bits(x);
            if (bits == unchecked((int)0x80000000)) return 1.401298E-45f; // -0 → menor positivo
            return System.BitConverter.Int32BitsToSingle(x >= 0f ? bits + 1 : bits - 1);
        }

        public static float BitDecrement(float x)
        {
            int bits = System.BitConverter.SingleToInt32Bits(x);
            if (bits == 0) return -1.401298E-45f; // +0 → menor negativo
            return System.BitConverter.Int32BitsToSingle(x > 0f ? bits - 1 : bits + 1);
        }

        // ── División entera con resto ──
        public static int DivRem(int a, int b, out int remainder)
        {
            int quotient = a / b;
            remainder = a - quotient * b;
            return quotient;
        }

        public static uint DivRem(uint a, uint b, out uint remainder)
        {
            uint quotient = a / b;
            remainder = a - quotient * b;
            return quotient;
        }

        public static (int Quotient, int Remainder) DivRem(int a, int b)
        {
            int quotient = a / b;
            return (quotient, a - quotient * b);
        }

        public static (uint Quotient, uint Remainder) DivRem(uint a, uint b)
        {
            uint quotient = a / b;
            return (quotient, a - quotient * b);
        }
    }
}
