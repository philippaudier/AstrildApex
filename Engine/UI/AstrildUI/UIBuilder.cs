using System;
using System.Numerics;
using ImGuiNET;

namespace Engine.UI.AstrildUI
{
    /// <summary>
    /// Builder pattern fluide pour créer des UIs ImGui de manière déclarative
    /// Exemple : UI.Window("Menu").Panel("Content").Button("Click me", onClick)
    /// </summary>
    public class UIBuilder
    {
        private readonly UIStyleSheet _styleSheet;
        private bool _windowOpen = false;
        private string _currentWindowId = "";
        
        public UIBuilder(UIStyleSheet? styleSheet = null)
        {
            _styleSheet = styleSheet ?? UIStyleSheet.Default;
        }
        
        /// <summary>
        /// Démarre une fenêtre ImGui avec style appliqué
        /// </summary>
        public UIWindowScope Window(string title, Action content, UIWindowOptions? options = null)
        {
            options ??= new UIWindowOptions();
            
            // Apply style
            _styleSheet.Push();
            
            // Configure window
            if (options.Size.HasValue)
                ImGui.SetNextWindowSize(options.Size.Value);
            if (options.Position.HasValue)
                ImGui.SetNextWindowPos(options.Position.Value);
            if (options.BackgroundAlpha.HasValue)
                ImGui.SetNextWindowBgAlpha(options.BackgroundAlpha.Value);
            
            var flags = options.Flags;
            if (options.NoDecoration)
                flags |= ImGuiWindowFlags.NoDecoration;
            if (options.NoMove)
                flags |= ImGuiWindowFlags.NoMove;
            if (options.NoResize)
                flags |= ImGuiWindowFlags.NoResize;
            
            _windowOpen = ImGui.Begin(title, flags);
            _currentWindowId = title;
            
            if (_windowOpen)
            {
                content?.Invoke();
            }
            
            ImGui.End();
            _styleSheet.Pop();
            
            return new UIWindowScope(this);
        }
        
        /// <summary>
        /// Panel (child window) avec bordures optionnelles
        /// </summary>
        public UIPanelScope Panel(string id, Action content, UIPanelOptions? options = null)
        {
            options ??= new UIPanelOptions();
            
            var size = options.Size ?? new Vector2(0, 0);
            var flags = options.HasBorder ? ImGuiChildFlags.Borders : ImGuiChildFlags.None;
            
            if (ImGui.BeginChild(id, size, flags))
            {
                content?.Invoke();
            }
            ImGui.EndChild();
            
            return new UIPanelScope(this);
        }
        
        /// <summary>
        /// Bouton stylisé avec callback
        /// </summary>
        public UIBuilder Button(string label, Action? onClick = null, UIButtonStyle style = UIButtonStyle.Default, Vector2? size = null)
        {
            var btnSize = size ?? new Vector2(0, 0);
            
            // Apply style
            PushButtonStyle(style);
            
            bool clicked = ImGui.Button(label, btnSize);
            
            PopButtonStyle(style);
            
            if (clicked)
                onClick?.Invoke();
            
            return this;
        }
        
        /// <summary>
        /// Texte avec style optionnel
        /// </summary>
        public UIBuilder Text(string text, UITextStyle style = UITextStyle.Normal)
        {
            switch (style)
            {
                case UITextStyle.Normal:
                    ImGui.Text(text);
                    break;
                case UITextStyle.Disabled:
                    ImGui.TextDisabled(text);
                    break;
                case UITextStyle.Colored:
                    ImGui.TextColored(_styleSheet.PrimaryColor, text);
                    break;
                case UITextStyle.Warning:
                    ImGui.TextColored(new Vector4(1, 0.8f, 0, 1), text);
                    break;
                case UITextStyle.Error:
                    ImGui.TextColored(new Vector4(1, 0.2f, 0.2f, 1), text);
                    break;
            }
            return this;
        }
        
        /// <summary>
        /// Séparateur horizontal
        /// </summary>
        public UIBuilder Separator()
        {
            ImGui.Separator();
            return this;
        }
        
        /// <summary>
        /// Espacement vertical
        /// </summary>
        public UIBuilder Spacing(int count = 1)
        {
            for (int i = 0; i < count; i++)
                ImGui.Spacing();
            return this;
        }
        
        /// <summary>
        /// Layout horizontal (éléments sur la même ligne)
        /// </summary>
        public UIBuilder SameLine(float offsetFromStartX = 0f, float spacing = -1f)
        {
            ImGui.SameLine(offsetFromStartX, spacing);
            return this;
        }
        
        /// <summary>
        /// Header collapsible
        /// </summary>
        public UIBuilder CollapsingHeader(string label, Action content, bool defaultOpen = true)
        {
            var flags = defaultOpen ? ImGuiTreeNodeFlags.DefaultOpen : ImGuiTreeNodeFlags.None;
            
            if (ImGui.CollapsingHeader(label, flags))
            {
                ImGui.Indent();
                content?.Invoke();
                ImGui.Unindent();
            }
            
            return this;
        }
        
        /// <summary>
        /// Input text
        /// </summary>
        public UIBuilder InputText(string label, ref string value, int maxLength = 256)
        {
            ImGui.InputText(label, ref value, (uint)maxLength);
            return this;
        }
        
        /// <summary>
        /// Slider float
        /// </summary>
        public UIBuilder SliderFloat(string label, ref float value, float min, float max, string format = "%.2f")
        {
            ImGui.SliderFloat(label, ref value, min, max, format);
            return this;
        }
        
        /// <summary>
        /// Checkbox
        /// </summary>
        public UIBuilder Checkbox(string label, ref bool value, Action<bool>? onChange = null)
        {
            bool prev = value;
            if (ImGui.Checkbox(label, ref value))
            {
                if (value != prev)
                    onChange?.Invoke(value);
            }
            return this;
        }
        
        /// <summary>
        /// Combo (dropdown)
        /// </summary>
        public UIBuilder Combo(string label, ref int currentIndex, string[] items, Action<int>? onChange = null)
        {
            int prev = currentIndex;
            if (ImGui.Combo(label, ref currentIndex, items, items.Length))
            {
                if (currentIndex != prev)
                    onChange?.Invoke(currentIndex);
            }
            return this;
        }
        
        /// <summary>
        /// Progress bar avec style personnalisé
        /// </summary>
        public UIBuilder ProgressBar(float fraction, Vector2? size = null, string? overlay = null, Vector4? color = null)
        {
            var barSize = size ?? new Vector2(-1, 0);
            
            if (color.HasValue)
            {
                ImGui.PushStyleColor(ImGuiCol.PlotHistogram, color.Value);
            }
            
            ImGui.ProgressBar(fraction, barSize, overlay ?? "");
            
            if (color.HasValue)
            {
                ImGui.PopStyleColor();
            }
            
            return this;
        }
        
        /// <summary>
        /// Image button avec tooltip
        /// </summary>
        public UIBuilder ImageButton(string id, IntPtr textureId, Vector2 size, Action? onClick = null, string? tooltip = null)
        {
            if (ImGui.ImageButton(id, textureId, size))
            {
                onClick?.Invoke();
            }
            
            if (!string.IsNullOrEmpty(tooltip) && ImGui.IsItemHovered())
            {
                ImGui.SetTooltip(tooltip);
            }
            
            return this;
        }
        
        /// <summary>
        /// Tooltip sur le dernier item
        /// </summary>
        public UIBuilder Tooltip(string text)
        {
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip(text);
            }
            return this;
        }
        
        /// <summary>
        /// Group horizontal - commence un layout horizontal
        /// </summary>
        public UIBuilder BeginHorizontal()
        {
            ImGui.BeginGroup();
            return this;
        }
        
        /// <summary>
        /// Termine le layout horizontal
        /// </summary>
        public UIBuilder EndHorizontal()
        {
            ImGui.EndGroup();
            return this;
        }
        
        /// <summary>
        /// Aligner à droite
        /// </summary>
        public UIBuilder AlignRight(float width)
        {
            float avail = ImGui.GetContentRegionAvail().X;
            if (width < avail)
            {
                ImGui.SetCursorPosX(ImGui.GetCursorPosX() + avail - width);
            }
            return this;
        }
        
        /// <summary>
        /// Centrer horizontalement
        /// </summary>
        public UIBuilder CenterHorizontal(float width)
        {
            float avail = ImGui.GetContentRegionAvail().X;
            if (width < avail)
            {
                ImGui.SetCursorPosX(ImGui.GetCursorPosX() + (avail - width) * 0.5f);
            }
            return this;
        }
        
        /// <summary>
        /// Dummy (spacing personnalisé)
        /// </summary>
        public UIBuilder Dummy(Vector2 size)
        {
            ImGui.Dummy(size);
            return this;
        }
        
        /// <summary>
        /// Indent (indentation)
        /// </summary>
        public UIBuilder Indent(float width = 0f)
        {
            if (width > 0)
                ImGui.Indent(width);
            else
                ImGui.Indent();
            return this;
        }
        
        /// <summary>
        /// Unindent
        /// </summary>
        public UIBuilder Unindent(float width = 0f)
        {
            if (width > 0)
                ImGui.Unindent(width);
            else
                ImGui.Unindent();
            return this;
        }
        
        /// <summary>
        /// Color picker
        /// </summary>
        public UIBuilder ColorPicker(string label, ref Vector4 color, Action<Vector4>? onChange = null)
        {
            Vector4 prev = color;
            if (ImGui.ColorEdit4(label, ref color))
            {
                if (color != prev)
                    onChange?.Invoke(color);
            }
            return this;
        }
        
        /// <summary>
        /// Tree node collapsible
        /// </summary>
        public UIBuilder TreeNode(string label, Action content, bool defaultOpen = false)
        {
            var flags = defaultOpen ? ImGuiTreeNodeFlags.DefaultOpen : ImGuiTreeNodeFlags.None;
            
            if (ImGui.TreeNodeEx(label, flags))
            {
                content?.Invoke();
                ImGui.TreePop();
            }
            
            return this;
        }
        
        /// <summary>
        /// Custom drawing via DrawList
        /// </summary>
        public UIBuilder CustomDraw(Action<ImDrawListPtr> drawCallback)
        {
            var drawList = ImGui.GetWindowDrawList();
            drawCallback?.Invoke(drawList);
            return this;
        }
        
        /// <summary>
        /// Draw a line using ImGui DrawList
        /// </summary>
        public UIBuilder DrawLine(Vector2 p1, Vector2 p2, Vector4 color, float thickness = 1.0f)
        {
            var drawList = ImGui.GetWindowDrawList();
            uint col = ImGui.ColorConvertFloat4ToU32(color);
            drawList.AddLine(p1, p2, col, thickness);
            return this;
        }
        
        /// <summary>
        /// Draw a circle using ImGui DrawList
        /// </summary>
        public UIBuilder DrawCircle(Vector2 center, float radius, Vector4 color, float thickness = 1.0f, int segments = 0)
        {
            var drawList = ImGui.GetWindowDrawList();
            uint col = ImGui.ColorConvertFloat4ToU32(color);
            drawList.AddCircle(center, radius, col, segments, thickness);
            return this;
        }
        
        /// <summary>
        /// Draw a filled circle using ImGui DrawList
        /// </summary>
        public UIBuilder DrawCircleFilled(Vector2 center, float radius, Vector4 color, int segments = 0)
        {
            var drawList = ImGui.GetWindowDrawList();
            uint col = ImGui.ColorConvertFloat4ToU32(color);
            drawList.AddCircleFilled(center, radius, col, segments);
            return this;
        }
        
        /// <summary>
        /// Draw a rectangle using ImGui DrawList
        /// </summary>
        public UIBuilder DrawRect(Vector2 min, Vector2 max, Vector4 color, float thickness = 1.0f, float rounding = 0f)
        {
            var drawList = ImGui.GetWindowDrawList();
            uint col = ImGui.ColorConvertFloat4ToU32(color);
            drawList.AddRect(min, max, col, rounding, ImDrawFlags.None, thickness);
            return this;
        }
        
        /// <summary>
        /// Draw a filled rectangle using ImGui DrawList
        /// </summary>
        public UIBuilder DrawRectFilled(Vector2 min, Vector2 max, Vector4 color, float rounding = 0f)
        {
            var drawList = ImGui.GetWindowDrawList();
            uint col = ImGui.ColorConvertFloat4ToU32(color);
            drawList.AddRectFilled(min, max, col, rounding);
            return this;
        }
        
        private void PushButtonStyle(UIButtonStyle style)
        {
            switch (style)
            {
                case UIButtonStyle.Primary:
                    ImGui.PushStyleColor(ImGuiCol.Button, _styleSheet.PrimaryColor);
                    ImGui.PushStyleColor(ImGuiCol.ButtonHovered, _styleSheet.PrimaryHoverColor);
                    ImGui.PushStyleColor(ImGuiCol.ButtonActive, _styleSheet.PrimaryActiveColor);
                    break;
                case UIButtonStyle.Danger:
                    ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.8f, 0.2f, 0.2f, 1));
                    ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.9f, 0.3f, 0.3f, 1));
                    ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(0.7f, 0.1f, 0.1f, 1));
                    break;
                case UIButtonStyle.Success:
                    ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.2f, 0.8f, 0.2f, 1));
                    ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.3f, 0.9f, 0.3f, 1));
                    ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(0.1f, 0.7f, 0.1f, 1));
                    break;
            }
        }
        
        private void PopButtonStyle(UIButtonStyle style)
        {
            if (style != UIButtonStyle.Default)
            {
                ImGui.PopStyleColor(3);
            }
        }
    }
    
    // ============================================
    // Options & Enums
    // ============================================
    
    public class UIWindowOptions
    {
        public Vector2? Size { get; set; }
        public Vector2? Position { get; set; }
        public float? BackgroundAlpha { get; set; }
        public ImGuiWindowFlags Flags { get; set; } = ImGuiWindowFlags.None;
        public bool NoDecoration { get; set; } = false;
        public bool NoMove { get; set; } = false;
        public bool NoResize { get; set; } = false;
    }
    
    public class UIPanelOptions
    {
        public Vector2? Size { get; set; }
        public bool HasBorder { get; set; } = true;
    }
    
    public enum UIButtonStyle
    {
        Default,
        Primary,
        Danger,
        Success
    }
    
    public enum UITextStyle
    {
        Normal,
        Disabled,
        Colored,
        Warning,
        Error
    }
    
    // ============================================
    // Scopes (for using statement)
    // ============================================
    
    public struct UIWindowScope : IDisposable
    {
        private readonly UIBuilder _builder;
        
        public UIWindowScope(UIBuilder builder)
        {
            _builder = builder;
        }
        
        public void Dispose() { }
    }
    
    public struct UIPanelScope : IDisposable
    {
        private readonly UIBuilder _builder;
        
        public UIPanelScope(UIBuilder builder)
        {
            _builder = builder;
        }
        
        public void Dispose() { }
    }
}
