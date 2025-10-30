using Engine.Serialization;
using System.Numerics;

namespace Engine.Components.UI
{
    public class UITextComponent : UIComponent
    {
        [Engine.Serialization.SerializableAttribute("text")]
        public string Text { get; set; } = "Text";

        [Engine.Serialization.SerializableAttribute("color")]
        public uint Color { get; set; } = 0xFF000000;

        [Engine.Serialization.SerializableAttribute("fontSize")]
        public float FontSize { get; set; } = 14f;

        [Engine.Serialization.SerializableAttribute("fontAssetGuid")]
        public System.Guid? FontAssetGuid { get; set; }

        [Engine.Serialization.SerializableAttribute("bold")]
        public bool Bold { get; set; } = false;

        [Engine.Serialization.SerializableAttribute("italic")]
        public bool Italic { get; set; } = false;

        [Engine.Serialization.SerializableAttribute("alignment")]
        public Engine.UI.TextAlignment Alignment { get; set; } = Engine.UI.TextAlignment.Left;

        public override void OnAttached()
        {
            base.OnAttached();
            var t = new Engine.UI.UIText();
            t.Text = Text;
            t.Color = Color;
            t.FontSize = FontSize;
            t.FontAssetGuid = FontAssetGuid;
            t.Bold = Bold;
            t.Italic = Italic;
            t.Alignment = Alignment;
            RuntimeElement = t;
        }
    }
}
