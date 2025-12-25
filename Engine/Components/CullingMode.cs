namespace Engine.Components
{
    /// <summary>
    /// Specifies which face(s) to cull during rendering.
    /// </summary>
    public enum CullingMode
    {
        /// <summary>No culling - render both front and back faces</summary>
        None = 0,

        /// <summary>Cull back faces (default - most common)</summary>
        Back = 1,

        /// <summary>Cull front faces</summary>
        Front = 2
    }
}
