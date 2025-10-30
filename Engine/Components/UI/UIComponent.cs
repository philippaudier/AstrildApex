using System.Numerics;
using Engine.Serialization;

namespace Engine.Components.UI
{
    /// <summary>
    /// Base class for UI elements exposed as Components so they serialize and appear in the Editor.
    /// Wraps the runtime Engine.UI.RectTransform for layout.
    /// </summary>
    public class UIComponent : Component
    {
        [Engine.Serialization.SerializableAttribute("rectTransform")]
        public Engine.UI.RectTransform RectTransform { get; set; } = new Engine.UI.RectTransform();

        [Engine.Serialization.SerializableAttribute("flexLayout")]
        public Engine.UI.FlexLayout FlexLayout { get; set; } = new Engine.UI.FlexLayout();

        [Engine.Serialization.SerializableAttribute("useFlexLayout")]
        public bool UseFlexLayout { get; set; } = false;

        [Engine.Serialization.SerializableAttribute("visible")]
        public bool Visible { get; set; } = true;

        [Engine.Serialization.SerializableAttribute("interactable")]
        public bool Interactable { get; set; } = true;

        // Backing runtime element (not serialized)
        [System.Text.Json.Serialization.JsonIgnore]
        public Engine.UI.UIElement? RuntimeElement { get; set; }

        public UIComponent()
        {
        }
    }
}
