using System.Collections.Generic;
using System.Numerics;
using System.Linq;

namespace Engine.UI
{
    public class UIElement
    {
        public string Name { get; set; } = "UIElement";
        public RectTransform Rect { get; } = new RectTransform();
        public FlexLayout FlexLayout { get; set; } = new FlexLayout();
        public UIElement? Parent { get; private set; }
        private readonly List<UIElement> _children = new List<UIElement>();

        public IReadOnlyList<UIElement> Children => _children;

        public bool Visible { get; set; } = true;
        public bool Interactable { get; set; } = true;
        public bool UseFlexLayout { get; set; } = false; // Active/désactive le flex layout

        public void AddChild(UIElement child)
        {
            if (child.Parent != null) child.Parent.RemoveChild(child);
            child.Parent = this;
            _children.Add(child);
        }

        public void RemoveChild(UIElement child)
        {
            if (_children.Remove(child)) child.Parent = null;
        }

        // Virtual render hook: canvas will call this
        public virtual void OnPopulateMesh(UIMeshBuilder mb, Vector2 canvasSize)
        {
            // Base class draws nothing
        }

        // Event hooks
        public virtual void OnPointerEnter() { }
        public virtual void OnPointerExit() { }
        public virtual void OnPointerDown() { }
        public virtual void OnPointerUp() { }
        public virtual void OnClick() { }
    }
}
