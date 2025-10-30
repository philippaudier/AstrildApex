using System.Numerics;

namespace Editor.Themes
{
    /// <summary>
    /// Theme definition for the editor UI
    /// </summary>
    public class EditorTheme
    {
        public string Name { get; set; } = "Default";
        public string Description { get; set; } = "";
        
        // === WINDOW & BACKGROUND ===
        public Vector4 WindowBackground { get; set; }
        public Vector4 ChildBackground { get; set; }
        public Vector4 PopupBackground { get; set; }
        public Vector4 Border { get; set; }
        
        // === TEXT ===
        public Vector4 Text { get; set; }
        public Vector4 TextDisabled { get; set; }
        public Vector4 TextSelectedBg { get; set; }
        
        // === FRAMES (Input fields, etc.) ===
        public Vector4 FrameBg { get; set; }
        public Vector4 FrameBgHovered { get; set; }
        public Vector4 FrameBgActive { get; set; }
        
        // === TITLE BAR ===
        public Vector4 TitleBg { get; set; }
        public Vector4 TitleBgActive { get; set; }
        public Vector4 TitleBgCollapsed { get; set; }
        
        // === MENU BAR ===
        public Vector4 MenuBarBg { get; set; }
        
        // === SCROLLBAR ===
        public Vector4 ScrollbarBg { get; set; }
        public Vector4 ScrollbarGrab { get; set; }
        public Vector4 ScrollbarGrabHovered { get; set; }
        public Vector4 ScrollbarGrabActive { get; set; }
        
        // === CHECKMARK ===
        public Vector4 CheckMark { get; set; }
        
        // === SLIDER ===
        public Vector4 SliderGrab { get; set; }
        public Vector4 SliderGrabActive { get; set; }
        
        // === BUTTONS ===
        public Vector4 Button { get; set; }
        public Vector4 ButtonHovered { get; set; }
        public Vector4 ButtonActive { get; set; }
        
        // === HEADER (Collapsing, TreeNode) ===
        public Vector4 Header { get; set; }
        public Vector4 HeaderHovered { get; set; }
        public Vector4 HeaderActive { get; set; }
        
        // === SEPARATOR ===
        public Vector4 Separator { get; set; }
        public Vector4 SeparatorHovered { get; set; }
        public Vector4 SeparatorActive { get; set; }
        
        // === RESIZE GRIP ===
        public Vector4 ResizeGrip { get; set; }
        public Vector4 ResizeGripHovered { get; set; }
        public Vector4 ResizeGripActive { get; set; }
        
        // === TABS ===
        public Vector4 Tab { get; set; }
        public Vector4 TabHovered { get; set; }
        public Vector4 TabActive { get; set; }
        public Vector4 TabUnfocused { get; set; }
        public Vector4 TabUnfocusedActive { get; set; }
        
        // === DOCKING ===
        public Vector4 DockingPreview { get; set; }
        public Vector4 DockingEmptyBg { get; set; }
        
        // === TABLE ===
        public Vector4 TableHeaderBg { get; set; }
        public Vector4 TableBorderStrong { get; set; }
        public Vector4 TableBorderLight { get; set; }
        public Vector4 TableRowBg { get; set; }
        public Vector4 TableRowBgAlt { get; set; }
        
        // === DRAG DROP ===
        public Vector4 DragDropTarget { get; set; }
        
        // === NAV HIGHLIGHT ===
        public Vector4 NavHighlight { get; set; }
        public Vector4 NavWindowingHighlight { get; set; }
        public Vector4 NavWindowingDimBg { get; set; }
        
        // === MODAL ===
        public Vector4 ModalWindowDimBg { get; set; }
        
        // === CUSTOM COLORS (Inspector specific) ===
        public Vector4 InspectorLabel { get; set; }
        public Vector4 InspectorValue { get; set; }
        public Vector4 InspectorWarning { get; set; }
        public Vector4 InspectorError { get; set; }
        public Vector4 InspectorSuccess { get; set; }
        public Vector4 InspectorInfo { get; set; }
        public Vector4 InspectorSection { get; set; }
        
        // === GRADIENTS (for special effects) ===
        public Vector4 GradientStart { get; set; }
        public Vector4 GradientEnd { get; set; }
        public Vector4 AccentColor { get; set; }
        
        // === STYLE VALUES ===
        public float Alpha { get; set; } = 1.0f;
        public float DisabledAlpha { get; set; } = 0.6f;
        public float WindowRounding { get; set; } = 12.0f;
        public float ChildRounding { get; set; } = 8.0f;
        public float FrameRounding { get; set; } = 6.0f;
        public float PopupRounding { get; set; } = 8.0f;
        public float ScrollbarRounding { get; set; } = 9.0f;
        public float GrabRounding { get; set; } = 6.0f;
        public float TabRounding { get; set; } = 8.0f;
    }
}
