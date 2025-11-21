using System;
using Engine.Assets;
using Engine.Scene;

namespace Editor.State
{
    public sealed class MaterialEditAction : IEditorAction
    {
        public string Label { get; }
        public DateTime Timestamp { get; }
        public long MemoryFootprint { get; }
        
        readonly Guid _guid;
        readonly MaterialAsset _before, _after;

        public MaterialEditAction(string label, Guid guid, MaterialAsset before, MaterialAsset after)
        {
            Label = label; 
            Timestamp = DateTime.UtcNow;
            _guid = guid; 
            _before = Clone(before); 
            _after = Clone(after);
            
            // Estimate memory footprint (rough estimation)
            MemoryFootprint = 200 + (before.Name?.Length * 2 ?? 0) + (after.Name?.Length * 2 ?? 0);
        }

        public void Undo(Scene scene)
        {
            AssetDatabase.SaveMaterial(_before);
            UndoRedo.RaiseAfterChange(); // rafraîchir l’UI/viewport
        }

        public void Redo(Scene scene)
        {
            AssetDatabase.SaveMaterial(_after);
            UndoRedo.RaiseAfterChange();
        }
        
        public bool CanMergeWith(IEditorAction other)
        {
            if (other is not MaterialEditAction otherMaterial) return false;
            if (_guid != otherMaterial._guid) return false;
            if ((other.Timestamp - Timestamp).Duration() > TimeSpan.FromSeconds(2)) return false;
            
            return Label.Contains("Material") && otherMaterial.Label.Contains("Material");
        }
        
        public IEditorAction? TryMergeWith(IEditorAction other)
        {
            if (!CanMergeWith(other) || other is not MaterialEditAction otherMaterial)
                return null;
                
            // Create merged action that goes from our "before" to other's "after"
            return new MaterialEditAction(
                $"Material Merged ({Label})",
                _guid,
                _before, // Keep original starting state
                otherMaterial._after // Use final ending state
            );
        }

        static MaterialAsset Clone(MaterialAsset m) => new MaterialAsset
        {
            Guid = m.Guid,
            Name = m.Name,
            AlbedoTexture = m.AlbedoTexture,
            AlbedoColor = (float[])m.AlbedoColor.Clone(),
            NormalTexture = m.NormalTexture,
            NormalStrength = m.NormalStrength,
            Metallic = m.Metallic,
            Roughness = m.Roughness,
            TextureTiling = (float[])m.TextureTiling.Clone(),
            TextureOffset = (float[])m.TextureOffset.Clone(),
            Saturation = m.Saturation,
            Brightness = m.Brightness,
            Contrast = m.Contrast,
            Hue = m.Hue,
            Emission = m.Emission,
            TransparencyMode = m.TransparencyMode,
            Opacity = m.Opacity,
            Shader = m.Shader
        };
    }
}
