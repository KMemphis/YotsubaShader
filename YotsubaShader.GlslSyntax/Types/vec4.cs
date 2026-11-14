using YotsubaShader.Language.Native.Types.Vector;

namespace YotsubaShader.GlslSyntax.Types
{
    [Vector]
    public struct vec4
    {
        [VectorComponent(0)]
        public float X;
    
        [VectorComponent(1)]
        public float Y;
    
        [VectorComponent(2)]
        public float Z;
    
        [VectorComponent(3)]
        public float W;

        // public void Saturate<[Vector] T>() where T : unmanaged
        // {
        //     
        // }
    }
}