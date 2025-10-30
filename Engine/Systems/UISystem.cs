using System.Collections.Generic;
using System.Linq;
using Engine.Scene;
using Engine.Components.UI;

namespace Engine.Systems
{
    /// <summary>
    /// System that synchronizes UI components with runtime UI elements
    /// </summary>
    public static class UISystem
    {
        /// <summary>
        /// Rebuild the UI hierarchy for all canvases in the scene
        /// </summary>
        public static void RebuildCanvases(Scene.Scene scene)
        {
            if (scene == null) return;

            // Find all canvas components
            var canvasEntities = scene.Entities
                .Where(e => e.HasComponent<CanvasComponent>())
                .ToList();

            // Clean up orphaned canvases (canvases whose entities no longer exist)
            var validCanvases = new System.Collections.Generic.HashSet<UI.Canvas>();
            foreach (var canvasEntity in canvasEntities)
            {
                var canvasComp = canvasEntity.GetComponent<CanvasComponent>();
                if (canvasComp?.RuntimeCanvas != null)
                {
                    validCanvases.Add(canvasComp.RuntimeCanvas);
                }
            }

            // Remove orphaned canvases from EventSystem
            var allCanvases = UI.EventSystem.Instance.Canvases.ToList();
            foreach (var canvas in allCanvases)
            {
                if (!validCanvases.Contains(canvas))
                {
                    UI.EventSystem.Instance.UnregisterCanvas(canvas);
                }
            }

            foreach (var canvasEntity in canvasEntities)
            {
                var canvasComp = canvasEntity.GetComponent<CanvasComponent>();
                if (canvasComp == null) continue;

                // Ensure runtime canvas exists
                if (canvasComp.RuntimeCanvas == null)
                {
                    canvasComp.OnAttached();
                }

                var canvas = canvasComp.RuntimeCanvas;
                if (canvas == null) continue;

                // Clear existing roots
                var existingRoots = canvas.Roots.ToList();
                foreach (var root in existingRoots)
                {
                    canvas.RemoveRoot(root);
                }

                // Set canvas size (default for editor viewport)
                canvas.Size = new System.Numerics.Vector2(1920, 1080);

                // Sync canvas properties
                canvas.RenderMode = canvasComp.RenderMode;
                canvas.SortOrder = canvasComp.SortOrder;

                // Build UI hierarchy from child entities
                BuildUIHierarchy(canvasEntity, canvas, scene);
            }
        }

        private static void BuildUIHierarchy(Entity canvasEntity, UI.Canvas canvas, Scene.Scene scene)
        {
            // Find direct children of the canvas entity
            var children = scene.Entities
                .Where(e => e.Parent?.Id == canvasEntity.Id)
                .OrderBy(e => e.Id) // Consistent ordering
                .ToList();

            foreach (var child in children)
            {
                var uiElement = CreateUIElement(child, scene);
                if (uiElement != null)
                {
                    canvas.AddRoot(uiElement);
                }
            }
        }

        private static UI.UIElement? CreateUIElement(Entity entity, Scene.Scene scene)
        {
            UI.UIElement? element = null;

            // Check for UI components
            var textComp = entity.GetComponent<UITextComponent>();
            var imageComp = entity.GetComponent<UIImageComponent>();
            var buttonComp = entity.GetComponent<UIButtonComponent>();

            if (textComp != null)
            {
                // Ensure runtime element exists
                if (textComp.RuntimeElement == null)
                {
                    textComp.OnAttached();
                }
                element = textComp.RuntimeElement;

                // Sync properties
                if (element is UI.UIText uiText)
                {
                    uiText.Text = textComp.Text;
                    uiText.Color = textComp.Color;
                    uiText.FontSize = textComp.FontSize;
                    uiText.FontAssetGuid = textComp.FontAssetGuid;
                    uiText.Bold = textComp.Bold;
                    uiText.Italic = textComp.Italic;
                    uiText.Alignment = textComp.Alignment;
                }
            }
            else if (imageComp != null)
            {
                if (imageComp.RuntimeElement == null)
                {
                    imageComp.OnAttached();
                }
                element = imageComp.RuntimeElement;

                // Sync properties
                if (element is UI.UIImage uiImage)
                {
                    uiImage.TextureGuid = imageComp.TextureGuid;
                    uiImage.Color = imageComp.Color;
                }
            }
            else if (buttonComp != null)
            {
                if (buttonComp.RuntimeElement == null)
                {
                    buttonComp.OnAttached();
                }
                element = buttonComp.RuntimeElement;

                // Sync button properties if needed
            }

            if (element != null)
            {
                // Sync common UIComponent properties
                var uiComp = textComp ?? imageComp ?? (UIComponent?)buttonComp;
                if (uiComp != null)
                {
                    element.Name = entity.Name;
                    element.Visible = uiComp.Visible;
                    element.Interactable = uiComp.Interactable;

                    // Sync RectTransform
                    element.Rect.AnchorMin = uiComp.RectTransform.AnchorMin;
                    element.Rect.AnchorMax = uiComp.RectTransform.AnchorMax;
                    element.Rect.AnchoredPosition = uiComp.RectTransform.AnchoredPosition;
                    element.Rect.SizeDelta = uiComp.RectTransform.SizeDelta;
                    element.Rect.Pivot = uiComp.RectTransform.Pivot;

                    // Sync FlexLayout
                    element.UseFlexLayout = uiComp.UseFlexLayout;
                    if (element.UseFlexLayout)
                    {
                        element.FlexLayout.Direction = uiComp.FlexLayout.Direction;
                        element.FlexLayout.JustifyContent = uiComp.FlexLayout.JustifyContent;
                        element.FlexLayout.AlignItems = uiComp.FlexLayout.AlignItems;
                        element.FlexLayout.Wrap = uiComp.FlexLayout.Wrap;
                        element.FlexLayout.Gap = uiComp.FlexLayout.Gap;
                        element.FlexLayout.FlexGrow = uiComp.FlexLayout.FlexGrow;
                        element.FlexLayout.FlexShrink = uiComp.FlexLayout.FlexShrink;
                        element.FlexLayout.FlexBasis = uiComp.FlexLayout.FlexBasis;
                        element.FlexLayout.AlignSelf = uiComp.FlexLayout.AlignSelf;
                        element.FlexLayout.Order = uiComp.FlexLayout.Order;
                        element.FlexLayout.PaddingLeft = uiComp.FlexLayout.PaddingLeft;
                        element.FlexLayout.PaddingRight = uiComp.FlexLayout.PaddingRight;
                        element.FlexLayout.PaddingTop = uiComp.FlexLayout.PaddingTop;
                        element.FlexLayout.PaddingBottom = uiComp.FlexLayout.PaddingBottom;
                    }
                }

                // Recursively build children
                var children = scene.Entities
                    .Where(e => e.Parent?.Id == entity.Id)
                    .OrderBy(e => e.Id)
                    .ToList();

                foreach (var child in children)
                {
                    var childElement = CreateUIElement(child, scene);
                    if (childElement != null)
                    {
                        element.AddChild(childElement);
                    }
                }
            }

            return element;
        }
    }
}
