using System.Numerics;

namespace Engine.UI
{
    public class UIButton : UIElement
    {
        // Optional callback invoked when button is clicked
        public System.Action? Clicked;
        public Engine.UI.UIImage Background { get; } = new UIImage();
        public UIText Label { get; } = new UIText();

        public uint NormalColor { get; set; } = 0xFFCCCCCC;
        public uint HoverColor { get; set; } = 0xFFDDDDDD;
        public uint PressedColor { get; set; } = 0xFFAAAAAA;

        private bool _hovered = false;
        private bool _pressed = false;

        public UIButton()
        {
            AddChild(Background);
            AddChild(Label);
            Background.Color = NormalColor;
            Label.Text = "Button";
        }

        public override void OnPointerEnter() { _hovered = true; Background.Color = HoverColor; }
        public override void OnPointerExit() { _hovered = false; _pressed = false; Background.Color = NormalColor; }
        public override void OnPointerDown() { _pressed = true; Background.Color = PressedColor; }
        public override void OnPointerUp() { if (_pressed && _hovered) OnClick(); _pressed = false; Background.Color = _hovered ? HoverColor : NormalColor; }
    public override void OnClick() { Clicked?.Invoke(); }

        public override void OnPopulateMesh(UIMeshBuilder mb, Vector2 canvasSize)
        {
            // Background draws itself via its own OnPopulateMesh when the renderer traverses children.
            // Ensure Label is positioned centered inside this button.
            var rect = Rect.GetLocalRect(canvasSize);
            // Place background at same rect
            Background.Rect.AnchoredPosition = rect.Position;
            Background.Rect.SizeDelta = rect.Size;
            // Label centered
            Label.Rect.AnchoredPosition = rect.Position + rect.Size / 2f;
            Label.Rect.SizeDelta = new Vector2(rect.Size.X * 0.8f, rect.Size.Y * 0.8f);
        }
    }
}
