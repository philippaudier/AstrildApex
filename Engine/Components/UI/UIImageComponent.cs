using Engine.Serialization;
using System.Numerics;

namespace Engine.Components.UI
{
    public class UIImageComponent : UIComponent
    {
        [Engine.Serialization.SerializableAttribute("textureGuid")]
        public System.Guid? TextureGuid { get; set; }

        [Engine.Serialization.SerializableAttribute("color")]
        public uint Color { get; set; } = 0xFFFFFFFF;

        public override void OnAttached()
        {
            base.OnAttached();
            // Create runtime element and assign basic rect
            RuntimeElement = new Engine.UI.UIImage();
            RuntimeElement.Name = $"UIImage_{(TextureGuid.HasValue ? TextureGuid.Value.ToString() : "nil")}";
            // Copy rect
            if (RuntimeElement is Engine.UI.UIImage img)
            {
                img.Color = Color;
                img.TextureGuid = TextureGuid;
            }
        }
    }
}
