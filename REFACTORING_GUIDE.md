# Panel & Overlay Refactoring Guide

## Overview

This refactoring completely redesigns the ViewportPanel and GamePanel architecture to eliminate code duplication, fix focus issues, and provide a clean, modular overlay system.

## Problems Solved

### Before Refactoring
1. **Duplication**: Each panel had its own overlay management code (position, dragging, clamping)
2. **Focus Issues**: Complex hysteresis system to prevent overlay flickering
3. **Separated Overlays**: Overlays were split between panels, causing state conflicts
4. **Spaghetti Code**: ViewportPanel (1192 lines), GamePanel (690 lines) with mixed concerns

### After Refactoring
1. **Unified System**: Single `OverlayManager` handles all overlay logic
2. **Clean Focus**: Simple panel-focused state, no complex debouncing
3. **Modular Overlays**: Reusable overlay components
4. **Separation of Concerns**: Clear architecture with dedicated components

## New Architecture

```
┌─────────────────────────────────┐
│ Panel Header (ImGui title bar)  │
├─────────────────────────────────┤
│ PanelToolbar (overlay toggles)  │ ← NEW: Clean UI controls
├─────────────────────────────────┤
│                                 │
│   Rendered Content (viewport)   │
│                                 │
│   + Overlays (OverlayManager)   │ ← NEW: Unified management
│                                 │
└─────────────────────────────────┘
```

## New Components

### 1. OverlayManager (`Editor/UI/OverlayManager.cs`)
Centralized overlay management:
- **Registration**: Register overlays with anchor positions
- **Positioning**: Automatic clamping to image bounds
- **Dragging**: Built-in drag support
- **Visibility**: Toggle overlay visibility
- **Theming**: Automatic theme application

```csharp
var manager = new OverlayManager("PanelId");
manager.RegisterOverlay("MyOverlay", OverlayAnchor.TopLeft, visible: true);

if (manager.BeginOverlay("MyOverlay", imageMin, imageMax, isPanelFocused))
{
    // Draw overlay content
    ImGui.Text("Hello");
    manager.EndOverlay("MyOverlay");
}
```

### 2. PanelToolbar (`Editor/UI/PanelToolbar.cs`)
Reusable toolbar for overlay controls:
- **Toggle Buttons**: Add overlay visibility toggles
- **Action Buttons**: Add custom actions
- **Separators**: Group buttons visually
- **Theming**: Automatic styling

```csharp
var toolbar = new PanelToolbar("PanelId");
toolbar.AddOverlayToggle("Stats", "Show/Hide Stats", ref showStats, "chart");
toolbar.AddSeparator();
toolbar.AddButton("Reset", "Reset View", () => ResetCamera());
toolbar.Draw(); // Call after ImGui.Begin()
```

### 3. Modular Overlays (`Editor/UI/Overlays/`)

#### GizmoToolbarOverlay
- Gizmo mode selection (Move, Rotate, Scale)
- Space toggle (World/Local)
- Snap settings
- Grid visibility

#### CameraSettingsOverlay
- Arrow speed, acceleration, damping
- Smoothing factor
- Collapsible panel

#### ProjectionSettingsOverlay
- Perspective/Orthographic/2D modes
- Ortho size parameter

#### PivotModeOverlay
- Center/Pivot mode selection
- Callback for gizmo updates

## Migration Path

### Step 1: Backup Original Files
The original files are kept intact:
- `ViewportPanel.cs` → Original implementation
- `GamePanel.cs` → Original implementation

New refactored versions:
- `ViewportPanel_Refactored.cs` → New clean implementation
- `GamePanel_Refactored.cs` → New clean implementation

### Step 2: Test Refactored Panels
1. Build the project to ensure no compilation errors
2. Test ViewportPanel_Refactored:
   - Camera controls (orbit, pan, zoom, arrow keys)
   - Gizmo operations (translate, rotate, scale)
   - Selection (click, rectangle, multi-select)
   - Overlays (draggable, toggleable)
   - Context menu
3. Test GamePanel_Refactored:
   - Camera rendering
   - Play mode
   - Aspect ratio constraints
   - Cursor locking
   - Overlays

### Step 3: Switch to Refactored Panels
Once tested and verified, update `EditorUI.cs`:

```csharp
// Replace this:
public static ViewportPanel MainViewport = new ViewportPanel();

// With this (rename the class):
public static ViewportPanel_Refactored MainViewport = new ViewportPanel_Refactored();

// Or simply rename ViewportPanel_Refactored.cs to ViewportPanel.cs
// after backing up the original
```

### Step 4: Remove Old Files
After confirming the refactored versions work correctly:
1. Delete `ViewportPanel.cs` (original)
2. Delete `GamePanel.cs` (original)
3. Rename `ViewportPanel_Refactored.cs` → `ViewportPanel.cs`
4. Rename `GamePanel_Refactored.cs` → `GamePanel.cs`

## Benefits

### Code Quality
- **ViewportPanel**: 1192 lines → ~700 lines (40% reduction)
- **GamePanel**: 690 lines → ~500 lines (27% reduction)
- **Reusable Components**: OverlayManager, PanelToolbar, overlay components

### Maintainability
- **Single Responsibility**: Each class has one clear purpose
- **DRY Principle**: No duplicated overlay logic
- **Extensibility**: Easy to add new overlays or panels

### User Experience
- **No Focus Bugs**: Clean focus management
- **Consistent UI**: Shared toolbar and overlay styling
- **Better Performance**: Simplified rendering logic

### Developer Experience
- **Easy to Understand**: Clear separation of concerns
- **Easy to Extend**: Add new overlays with minimal code
- **Easy to Debug**: Modular components are easier to test

## Future Enhancements

### Persistence
Implement `SaveState()` and `LoadState()` in OverlayManager to persist:
- Overlay positions
- Overlay visibility
- User preferences

```csharp
public void SaveState()
{
    foreach (var (id, state) in _overlays)
    {
        EditorSettings.SetOverlayState(_panelId, id, state.Position, state.Visible);
    }
}
```

### Animation
Add smooth transitions for overlay visibility:
```csharp
public void SetOverlayVisible(string id, bool visible, bool animated = true)
{
    if (animated)
    {
        // Fade in/out animation
    }
}
```

### Docking
Allow overlays to snap to edges or corners:
```csharp
public void SetOverlayDocked(string id, OverlayDock dock)
{
    // Snap to edge and prevent dragging
}
```

## Testing Checklist

### ViewportPanel
- [ ] Camera orbit (right-drag)
- [ ] Camera pan (middle-drag)
- [ ] Camera zoom (scroll wheel)
- [ ] Arrow key navigation
- [ ] Gizmo translate (W key)
- [ ] Gizmo rotate (E key)
- [ ] Gizmo scale (R key)
- [ ] Single entity selection
- [ ] Multi-selection (Ctrl+click)
- [ ] Range selection (Shift+click)
- [ ] Rectangle selection (drag)
- [ ] Context menu (right-click)
- [ ] Frame selection (F key)
- [ ] All overlays draggable
- [ ] All overlays toggleable via toolbar
- [ ] Performance overlay shows stats

### GamePanel
- [ ] Camera rendering in Edit mode
- [ ] Camera rendering in Play mode
- [ ] Camera selector dropdown
- [ ] Game options menu
- [ ] Aspect ratio constraints (16:9, 4:3, etc.)
- [ ] Resolution scale slider
- [ ] Cursor locking in Play mode
- [ ] ImGui menu system
- [ ] HUD overlays
- [ ] Maximize on Play
- [ ] All overlays draggable
- [ ] All overlays toggleable via toolbar
- [ ] Performance overlay shows stats

## Conclusion

This refactoring provides a solid foundation for future panel development. The modular architecture makes it easy to:
- Add new panels with consistent UI
- Create new overlays without code duplication
- Maintain and debug panel-related code
- Provide a better user experience

The refactored code is cleaner, more maintainable, and eliminates the focus and duplication issues that plagued the original implementation.
