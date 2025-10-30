using Engine.Serialization;
using System.Numerics;

namespace Engine.Components.UI
{
    public class CanvasComponent : UIComponent
    {
        [Engine.Serialization.SerializableAttribute("renderMode")]
        public Engine.UI.RenderMode RenderMode { get; set; } = Engine.UI.RenderMode.ScreenSpaceOverlay;

        [Engine.Serialization.SerializableAttribute("sortOrder")]
        public int SortOrder { get; set; } = 0;

        [Engine.Serialization.SerializableAttribute("width")]
        public float Width { get; set; } = 1920;

        [Engine.Serialization.SerializableAttribute("height")]
        public float Height { get; set; } = 1080;

        // Runtime canvas instance (deprecated, kept for backward compatibility)
        [System.Text.Json.Serialization.JsonIgnore]
        public Engine.UI.Canvas? RuntimeCanvas { get; set; }

        public override void OnAttached()
        {
            base.OnAttached();
            // Create runtime canvas when attached to an entity (for backward compatibility)
            RuntimeCanvas = new Engine.UI.Canvas { RenderMode = RenderMode, SortOrder = SortOrder };
            Engine.UI.EventSystem.Instance.RegisterCanvas(RuntimeCanvas);
        }

        public override void OnDetached()
        {
            if (RuntimeCanvas != null) Engine.UI.EventSystem.Instance.UnregisterCanvas(RuntimeCanvas);
            base.OnDetached();
        }
    }
}
