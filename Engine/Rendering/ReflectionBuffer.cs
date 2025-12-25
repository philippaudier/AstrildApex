using OpenTK.Mathematics;

namespace Engine.Rendering
{
    // Simple global holder for planar reflection resources updated by renderers
    public static class ReflectionBuffer
    {
        // GL texture handle for the planar reflection color texture (0 = none)
        public static int ReflectionTexture = 0;

        // ViewProj matrix used to sample the reflection texture
        public static Matrix4 ReflectionViewProj = Matrix4.Identity;
    }
}
