using Engine.Serialization;
using System.Numerics;

namespace Engine.Components.UI
{
    public class UIButtonComponent : UIComponent
    {
        [Engine.Serialization.SerializableAttribute("text")]
        public string Text { get; set; } = "Button";

        // Colors
        [Engine.Serialization.SerializableAttribute("normalColor")]
        public uint NormalColor { get; set; } = 0xFFCCCCCC;

        [Engine.Serialization.SerializableAttribute("hoverColor")]
        public uint HoverColor { get; set; } = 0xFFDDDDDD;

        [Engine.Serialization.SerializableAttribute("pressedColor")]
        public uint PressedColor { get; set; } = 0xFFAAAAAA;

        // Runtime
        [System.Text.Json.Serialization.JsonIgnore]
        public Engine.UI.UIButton? RuntimeButton { get; set; }

    // Event raised when the button is clicked (runtime-only)
    public event System.Action? OnClick;

        public override void OnAttached()
        {
            base.OnAttached();
            var b = new Engine.UI.UIButton();
            b.Label.Text = Text;
            b.NormalColor = NormalColor;
            b.HoverColor = HoverColor;
            b.PressedColor = PressedColor;
            // Wire runtime click to component event
            b.Clicked = () => OnClick?.Invoke();
            RuntimeElement = b;
            RuntimeButton = b;
        }
    }
}
