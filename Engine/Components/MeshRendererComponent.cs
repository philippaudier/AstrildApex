using System;
using Engine.Scene;

namespace Engine.Components
{
    /// <summary>
    /// MeshRenderer component - handles mesh rendering and material assignment
    /// </summary>
    public sealed class MeshRendererComponent : Component
    {
        [Engine.Serialization.Serializable("mesh")]
        public MeshKind Mesh { get; set; } = MeshKind.Cube;

        /// <summary>
        /// Custom mesh asset GUID (if using imported mesh instead of primitive)
        /// When set, this takes precedence over the Mesh property
        /// </summary>
        [Engine.Serialization.Serializable("customMeshGuid")]
        public Guid? CustomMeshGuid { get; set; } = null;

        /// <summary>
        /// Submesh index to render (for models with multiple submeshes)
        /// Default is 0 (first submesh)
        /// </summary>
        [Engine.Serialization.Serializable("submeshIndex")]
        public int SubmeshIndex { get; set; } = 0;

        [Engine.Serialization.Serializable("materialGuid")]
        public Guid? MaterialGuid { get; set; } = null;
        
        public override void OnAttached()
        {
            base.OnAttached();
            
            // If entity has no material, assign default white material
            if (MaterialGuid == null || MaterialGuid == Guid.Empty)
            {
                MaterialGuid = Engine.Assets.AssetDatabase.EnsureDefaultWhiteMaterial();
            }
            
            // Sync with entity's material for backward compatibility
            if (Entity != null)
            {
                Entity.MaterialGuid = MaterialGuid;
            }
        }
        
        public override void OnDetached()
        {
            base.OnDetached();
            
            // Clear entity's material reference
            if (Entity != null)
            {
                Entity.MaterialGuid = null;
            }
        }
        
        /// <summary>
        /// Set the material for this mesh renderer
        /// </summary>
        public void SetMaterial(Guid materialGuid)
        {
            MaterialGuid = materialGuid;
            
            // Keep entity in sync for backward compatibility
            if (Entity != null)
            {
                Entity.MaterialGuid = materialGuid;
            }
        }
        
        /// <summary>
        /// Get the current material GUID
        /// </summary>
        public Guid GetMaterialGuid()
        {
            return MaterialGuid ?? Guid.Empty;
        }
        
        /// <summary>
        /// Check if this mesh renderer has a valid mesh to render
        /// </summary>
        public bool HasMeshToRender()
        {
            return CustomMeshGuid.HasValue || Mesh != MeshKind.None;
        }

        /// <summary>
        /// Check if using a custom imported mesh
        /// </summary>
        public bool IsUsingCustomMesh()
        {
            return CustomMeshGuid.HasValue && CustomMeshGuid.Value != Guid.Empty;
        }

        /// <summary>
        /// Set a custom mesh by GUID
        /// </summary>
        public void SetCustomMesh(Guid meshGuid, int submeshIndex = 0)
        {
            CustomMeshGuid = meshGuid;
            SubmeshIndex = submeshIndex;
        }

        /// <summary>
        /// Clear custom mesh and revert to primitive
        /// </summary>
        public void ClearCustomMesh()
        {
            CustomMeshGuid = null;
            SubmeshIndex = 0;
        }
    }
}