namespace YotsubaShader.Compiler.YS_Types;

public class SpirVTableTypesIds
{
    public const string FloatID = "%float";

    public const string IntID = "%int";

    public const string UIntID = "%uint";

    public const string BoolID = "%bool";

    public const string Mat4ID = "%mat4";

    public const string Image2DID = "%img2d";

    public const string SamplerID = "%sampler";

    public static string IntDinamicId(int large) => "%int_" + large;

    public static string UIntDinamicId(int large) => "%uint_" + large;

    public static string FloatDinamicId(int large) => "%float_" + large;

    /// <summary>%v2float, %v3float, %v4float.</summary>
    public static string VectorFloatID(int components) => "%v" + components + "float";

    public const string VoidID = "%void";

    public const string VoidFunctionID = VoidID + "_fn";

    public const string FragmentMainID = "%fs_main";
    public const string VertexMainID = "%vs_main";
    public const string ComputeMainID = "%cs_main";


    public const string PtrTypeIDLastConcatenation = "_ptr";

}
