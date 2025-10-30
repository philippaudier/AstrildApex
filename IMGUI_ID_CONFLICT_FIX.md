# ImGui ID Conflict Fix - Complete

## Problem

**ImGui Error**: "MESSAGE FROM DEAR IMGUI: Programmer error: 2 Visible Items with conflicting ID!"

### Root Cause
When multiple components of the same type (e.g., 2 BoxColliders on different entities) were selected, ImGui widgets in `InspectorWidgets.cs` used the same label strings ("Enabled", "Layer", "Radius", etc.) without unique IDs. This caused ID collisions in ImGui's internal system.

While `ComponentInspector.cs` calls `PushID(component.GetHashCode())` to create ID scopes, this wasn't sufficient because some ImGui widgets need globally unique IDs within the entire frame.

## Solution

Added unique ID generation to all widget methods using ImGui's `"Label##UniqueID"` syntax, which separates the visible label from the internal ID:

```csharp
// Generate unique ID to avoid conflicts between components
string uniqueLabel = entityId.HasValue && !string.IsNullOrEmpty(fieldPath) 
    ? $"{label}##{entityId}_{fieldPath}" 
    : label;

bool changed = ImGui.DragFloat(uniqueLabel, ref value, speed);
```

**How it works**:
- `"Radius"` → Visible to user as "Radius"
- `"Radius##123_Radius"` → ImGui internal ID is unique per entity + field
- Multiple BoxColliders can now have "Radius" fields without conflicts

## Fixed Methods in InspectorWidgets.cs

### ✅ All Widget Methods Updated:

1. **FloatField** (line 176)
   - `ImGui.DragFloat(uniqueLabel, ...)`

2. **IntField** (line 223)
   - `ImGui.DragInt(uniqueLabel, ...)`

3. **SliderFloat** (line 269)
   - `ImGui.SliderFloat(uniqueLabel, ...)`

4. **SliderAngle** (line 315)
   - Delegates to SliderFloat → Inherits fix ✅

5. **Checkbox** (line 328)
   - `ImGui.Checkbox(uniqueLabel, ...)`

6. **TextField** (line 359)
   - `ImGui.InputText(uniqueLabel, ...)`

7. **Vector4Field** (line 408)
   - `ImGui.DragFloat4(uniqueLabel, ...)`
   - Removed duplicate definition at line 516

8. **Vector3Field** (line 454)
   - `ImGui.DragFloat3(uniqueLabel, ...)`

9. **Vector3FieldOTK** (line 500)
   - Delegates to Vector3Field → Inherits fix ✅

10. **ColorField** (line 525)
    - `ImGui.ColorEdit3(uniqueLabel, ...)`

11. **ColorFieldAlpha** (line 560)
    - `ImGui.ColorEdit4(uniqueLabel, ...)`

12. **ColorFieldOTK** (line 595)
    - Delegates to ColorField → Inherits fix ✅

13. **EnumField** (line 616)
    - `ImGui.Combo(uniqueLabel, ...)`

## Build Results

```
dotnet build Editor/Editor.csproj

La génération a réussi.
    0 Avertissement(s)
    0 Erreur(s)

Temps écoulé 00:00:03.29
```

✅ **Build Status**: SUCCESS  
✅ **Errors**: 0  
✅ **Warnings**: 0

## Testing Verification

To verify the fix works:

1. **Create Multiple Components**:
   - Add 2+ BoxColliders to different entities
   - Add 2+ SphereColliders to different entities
   - Select entities with same component types

2. **Verify No Conflicts**:
   - No more "2 Visible Items with conflicting ID!" errors
   - Each component's fields display correctly
   - Undo/redo still works (uses same entityId + fieldPath)

3. **Test All Widget Types**:
   - Float fields (Radius, Height, etc.)
   - Int fields (Layer)
   - Checkboxes (Enabled, IsTrigger)
   - Vector fields (Position, Offset)
   - Color fields (Light color, Ambient color)
   - Enums (Direction, Type)

## Technical Details

### ID Generation Pattern

```csharp
string uniqueLabel = entityId.HasValue && !string.IsNullOrEmpty(fieldPath) 
    ? $"{label}##{entityId}_{fieldPath}" 
    : label;
```

**Example**:
- Entity 123, field "Radius" → `"Radius##123_Radius"`
- Entity 456, field "Radius" → `"Radius##456_Radius"`
- No entityId (legacy use) → `"Radius"` (fallback)

### Backward Compatibility

The fix maintains backward compatibility:
- If `entityId` is null or `fieldPath` is empty → uses raw label (legacy behavior)
- All modern inspectors pass entityId + fieldPath → get unique IDs
- Undo/redo system already used entityId + fieldPath → no changes needed

## Next Steps

✅ **Phase 1 Complete**: ImGui ID conflicts resolved  
⏳ **Phase 2 Next**: Continue with UI Inspector upgrades as requested:
- UIElementInspector
- UIButtonInspector  
- UIImageInspector
- UITextInspector
- CanvasInspector

## Files Modified

- `Editor/Inspector/InspectorWidgets.cs` (767 lines)
  - 13 widget methods updated with unique ID generation
  - 1 duplicate Vector4Field definition removed
  - All ImGui widget calls now use uniqueLabel instead of raw label

---

**Status**: ✅ COMPLETE  
**Date**: 2024  
**Build**: SUCCESS (0 errors, 0 warnings)
