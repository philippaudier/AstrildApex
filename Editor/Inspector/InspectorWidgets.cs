using System;
using System.Numerics;
using ImGuiNET;
using OpenTK.Mathematics;
using Editor.State;
using Engine.Scene;
using NumVector2 = System.Numerics.Vector2;
using NumVector3 = System.Numerics.Vector3;
using NumVector4 = System.Numerics.Vector4;
using OtkVector2 = OpenTK.Mathematics.Vector2;
using OtkVector3 = OpenTK.Mathematics.Vector3;
using OtkVector4 = OpenTK.Mathematics.Vector4;

namespace Editor.Inspector
{
    /// <summary>
    /// Professional Unity-style inspector widgets with automatic undo/redo,
    /// validation, tooltips, and consistent styling.
    /// </summary>
    public static class InspectorWidgets
    {
        // Undo/redo state tracking
        private static readonly System.Collections.Generic.HashSet<string> _activeEdits = new();
        private static readonly System.Collections.Generic.Dictionary<string, object?> _beforeValues = new();
        
        private static string EditKey(uint entityId, string fieldPath) => $"{entityId}:{fieldPath}";
        
        #region Core Helpers
        
        /// <summary>
        /// Begin editing a field (for undo/redo tracking)
        /// </summary>
        private static void BeginFieldEdit(uint entityId, string fieldPath, object? beforeValue, string label)
        {
            string key = EditKey(entityId, fieldPath);
            if (_activeEdits.Add(key))
            {
                _beforeValues[key] = beforeValue;
                UndoRedo.BeginComposite($"Edit {label}");
            }
        }
        
        /// <summary>
        /// End editing a field and push undo action if changed
        /// </summary>
        private static void EndFieldEdit(uint entityId, string fieldPath, object? afterValue, string label)
        {
            string key = EditKey(entityId, fieldPath);
            if (_activeEdits.Remove(key))
            {
                _beforeValues.TryGetValue(key, out var beforeValue);
                _beforeValues.Remove(key);
                
                if (!Equals(beforeValue, afterValue))
                {
                    UndoRedo.Push(new FieldEditAction($"{label}", entityId, fieldPath, beforeValue, afterValue));
                    UndoRedo.RaiseAfterChange();
                }
                
                UndoRedo.EndComposite();
            }
        }
        
        /// <summary>
        /// Draw a tooltip if provided and item is hovered
        /// </summary>
        private static void DrawTooltip(string? tooltip)
        {
            if (!string.IsNullOrEmpty(tooltip) && ImGui.IsItemHovered(ImGuiHoveredFlags.DelayShort))
            {
                ImGui.BeginTooltip();
                ImGui.TextUnformatted(tooltip);
                ImGui.EndTooltip();
            }
        }
        
        /// <summary>
        /// Draw a warning icon if validation fails
        /// </summary>
        private static void DrawValidationIcon(string? validationError)
        {
            if (!string.IsNullOrEmpty(validationError))
            {
                ImGui.SameLine();
                ImGui.TextColored(InspectorColors.Warning, "⚠");
                if (ImGui.IsItemHovered(ImGuiHoveredFlags.DelayShort))
                {
                    ImGui.BeginTooltip();
                    ImGui.TextColored(InspectorColors.Warning, validationError);
                    ImGui.EndTooltip();
                }
            }
        }
        
        /// <summary>
        /// Draw a help icon button
        /// </summary>
        private static void DrawHelpIcon(string helpText)
        {
            ImGui.SameLine();
            ImGui.TextDisabled("(?)");
            if (ImGui.IsItemHovered(ImGuiHoveredFlags.DelayShort))
            {
                ImGui.BeginTooltip();
                ImGui.TextUnformatted(helpText);
                ImGui.EndTooltip();
            }
        }
        
        #endregion
        
        #region Section Management
        
        /// <summary>
        /// Draw a collapsible section header (Unity-style)
        /// </summary>
        public static bool Section(string label, bool defaultOpen = true, string? tooltip = null, string? helpText = null)
        {
            ImGui.PushStyleColor(ImGuiCol.Header, InspectorColors.Section);
            ImGui.PushStyleColor(ImGuiCol.HeaderHovered, new NumVector4(0.4f, 0.6f, 0.9f, 1f));
            ImGui.PushStyleColor(ImGuiCol.HeaderActive, new NumVector4(0.5f, 0.7f, 1.0f, 1f));
            
            var flags = defaultOpen ? ImGuiTreeNodeFlags.DefaultOpen : ImGuiTreeNodeFlags.None;
            bool isOpen = ImGui.CollapsingHeader(label, flags);
            
            ImGui.PopStyleColor(3);
            
            if (ImGui.IsItemHovered(ImGuiHoveredFlags.DelayShort) && !string.IsNullOrEmpty(tooltip))
            {
                ImGui.BeginTooltip();
                ImGui.TextUnformatted(tooltip);
                ImGui.EndTooltip();
            }
            
            if (!string.IsNullOrEmpty(helpText))
            {
                ImGui.SameLine();
                DrawHelpIcon(helpText);
            }
            
            if (isOpen)
            {
                ImGui.Indent(InspectorLayout.IndentWidth);
            }
            
            return isOpen;
        }
        
        /// <summary>
        /// End a section (unindent)
        /// </summary>
        public static void EndSection()
        {
            ImGui.Unindent(InspectorLayout.IndentWidth);
        }
        
        /// <summary>
        /// Draw a visual separator
        /// </summary>
        public static void Separator()
        {
            ImGui.Spacing();
            ImGui.PushStyleColor(ImGuiCol.Separator, InspectorColors.Separator);
            ImGui.Separator();
            ImGui.PopStyleColor();
            ImGui.Spacing();
        }
        
        #endregion
        
        #region Basic Fields
        
        /// <summary>
        /// Float field with undo/redo, validation, and tooltips
        /// </summary>
        public static bool FloatField(string label, ref float value, 
            uint? entityId = null, string? fieldPath = null,
            float speed = 0.1f, float min = float.MinValue, float max = float.MaxValue, 
            string format = "%.3f",
            string? tooltip = null, 
            Func<float, string?>? validate = null,
            string? helpText = null)
        {
            // Generate unique ID to avoid conflicts between components
            string uniqueLabel = entityId.HasValue && !string.IsNullOrEmpty(fieldPath) 
                ? $"{label}##{entityId}_{fieldPath}" 
                : label;
            
            ImGui.PushItemWidth(InspectorLayout.ControlWidth);
            
            float before = value;
            bool isActive = ImGui.IsItemActive();
            bool wasActive = isActive;
            
            bool changed = ImGui.DragFloat(uniqueLabel, ref value, speed, min, max, format);
            isActive = ImGui.IsItemActive();
            
            ImGui.PopItemWidth();
            
            DrawTooltip(tooltip);
            
            string? error = validate?.Invoke(value);
            DrawValidationIcon(error);
            
            if (!string.IsNullOrEmpty(helpText))
                DrawHelpIcon(helpText);
            
            // Undo/redo tracking
            if (entityId.HasValue && !string.IsNullOrEmpty(fieldPath))
            {
                if (isActive && !wasActive)
                    BeginFieldEdit(entityId.Value, fieldPath, before, label);
                else if (!isActive && wasActive)
                    EndFieldEdit(entityId.Value, fieldPath, value, label);
            }
            
            return changed;
        }
        
        /// <summary>
        /// Int field with undo/redo, validation, and tooltips
        /// </summary>
        public static bool IntField(string label, ref int value,
            uint? entityId = null, string? fieldPath = null,
            int speed = 1, int min = int.MinValue, int max = int.MaxValue,
            string? tooltip = null,
            Func<int, string?>? validate = null,
            string? helpText = null)
        {
            // Generate unique ID to avoid conflicts between components
            string uniqueLabel = entityId.HasValue && !string.IsNullOrEmpty(fieldPath) 
                ? $"{label}##{entityId}_{fieldPath}" 
                : label;
            
            ImGui.PushItemWidth(InspectorLayout.ControlWidth);
            
            int before = value;
            bool isActive = ImGui.IsItemActive();
            bool wasActive = isActive;
            
            bool changed = ImGui.DragInt(uniqueLabel, ref value, speed, min, max);
            isActive = ImGui.IsItemActive();
            
            ImGui.PopItemWidth();
            
            DrawTooltip(tooltip);
            
            string? error = validate?.Invoke(value);
            DrawValidationIcon(error);
            
            if (!string.IsNullOrEmpty(helpText))
                DrawHelpIcon(helpText);
            
            // Undo/redo tracking
            if (entityId.HasValue && !string.IsNullOrEmpty(fieldPath))
            {
                if (isActive && !wasActive)
                    BeginFieldEdit(entityId.Value, fieldPath, before, label);
                else if (!isActive && wasActive)
                    EndFieldEdit(entityId.Value, fieldPath, value, label);
            }
            
            return changed;
        }
        
        /// <summary>
        /// Slider float field (for ranges like 0-1, angles, etc.)
        /// </summary>
        public static bool SliderFloat(string label, ref float value,
            float min, float max, string format = "%.2f",
            uint? entityId = null, string? fieldPath = null,
            string? tooltip = null,
            Func<float, string?>? validate = null,
            string? helpText = null)
        {
            // Generate unique ID to avoid conflicts between components
            string uniqueLabel = entityId.HasValue && !string.IsNullOrEmpty(fieldPath) 
                ? $"{label}##{entityId}_{fieldPath}" 
                : label;
            
            ImGui.PushItemWidth(InspectorLayout.ControlWidth);
            
            float before = value;
            bool isActive = ImGui.IsItemActive();
            bool wasActive = isActive;
            
            bool changed = ImGui.SliderFloat(uniqueLabel, ref value, min, max, format);
            isActive = ImGui.IsItemActive();
            
            ImGui.PopItemWidth();
            
            DrawTooltip(tooltip);
            
            string? error = validate?.Invoke(value);
            DrawValidationIcon(error);
            
            if (!string.IsNullOrEmpty(helpText))
                DrawHelpIcon(helpText);
            
            // Undo/redo tracking
            if (entityId.HasValue && !string.IsNullOrEmpty(fieldPath))
            {
                if (isActive && !wasActive)
                    BeginFieldEdit(entityId.Value, fieldPath, before, label);
                else if (!isActive && wasActive)
                    EndFieldEdit(entityId.Value, fieldPath, value, label);
            }
            
            return changed;
        }
        
        /// <summary>
        /// Angle slider (degrees) with proper formatting
        /// </summary>
        public static bool SliderAngle(string label, ref float valueDegrees,
            float minDegrees = 0f, float maxDegrees = 360f,
            uint? entityId = null, string? fieldPath = null,
            string? tooltip = null,
            string? helpText = null)
        {
            return SliderFloat(label, ref valueDegrees, minDegrees, maxDegrees, "%.1f°",
                entityId, fieldPath, tooltip, null, helpText);
        }
        
        /// <summary>
        /// Checkbox field with undo/redo
        /// </summary>
        public static bool Checkbox(string label, ref bool value,
            uint? entityId = null, string? fieldPath = null,
            string? tooltip = null,
            string? helpText = null)
        {
            // Generate unique ID to avoid conflicts between components
            string uniqueLabel = entityId.HasValue && !string.IsNullOrEmpty(fieldPath) 
                ? $"{label}##{entityId}_{fieldPath}" 
                : label;
            
            bool before = value;
            bool changed = ImGui.Checkbox(uniqueLabel, ref value);
            
            DrawTooltip(tooltip);
            
            if (!string.IsNullOrEmpty(helpText))
                DrawHelpIcon(helpText);
            
            // Undo/redo (immediate for checkbox)
            if (changed && entityId.HasValue && !string.IsNullOrEmpty(fieldPath))
            {
                BeginFieldEdit(entityId.Value, fieldPath, before, label);
                EndFieldEdit(entityId.Value, fieldPath, value, label);
            }
            
            return changed;
        }
        
        /// <summary>
        /// Text input field with undo/redo
        /// </summary>
        public static bool TextField(string label, ref string value, uint maxLength = 256,
            uint? entityId = null, string? fieldPath = null,
            string? tooltip = null,
            Func<string, string?>? validate = null,
            string? helpText = null)
        {
            // Generate unique ID to avoid conflicts between components
            string uniqueLabel = entityId.HasValue && !string.IsNullOrEmpty(fieldPath) 
                ? $"{label}##{entityId}_{fieldPath}" 
                : label;
            
            ImGui.PushItemWidth(InspectorLayout.ControlWidth);
            
            string before = value;
            bool isActive = ImGui.IsItemActive();
            bool wasActive = isActive;
            
            bool changed = ImGui.InputText(uniqueLabel, ref value, maxLength);
            isActive = ImGui.IsItemActive();
            
            ImGui.PopItemWidth();
            
            DrawTooltip(tooltip);
            
            string? error = validate?.Invoke(value);
            DrawValidationIcon(error);
            
            if (!string.IsNullOrEmpty(helpText))
                DrawHelpIcon(helpText);
            
            // Undo/redo tracking
            if (entityId.HasValue && !string.IsNullOrEmpty(fieldPath))
            {
                if (isActive && !wasActive)
                    BeginFieldEdit(entityId.Value, fieldPath, before, label);
                else if (!isActive && wasActive)
                    EndFieldEdit(entityId.Value, fieldPath, value, label);
            }
            
            return changed;
        }
        
        #endregion
        
        #region Vector Fields
        
        /// <summary>
        /// Vector4 field with undo/redo and validation (System.Numerics - for ImGui)
        /// </summary>
        public static bool Vector4Field(string label, ref NumVector4 value,
            float speed = 0.1f,
            uint? entityId = null, string? fieldPath = null,
            string? tooltip = null,
            Func<NumVector4, string?>? validate = null,
            string? helpText = null)
        {
            // Generate unique ID to avoid conflicts between components
            string uniqueLabel = entityId.HasValue && !string.IsNullOrEmpty(fieldPath) 
                ? $"{label}##{entityId}_{fieldPath}" 
                : label;
            
            ImGui.PushItemWidth(InspectorLayout.ControlWidth);
            
            NumVector4 before = value;
            bool isActive = ImGui.IsItemActive();
            bool wasActive = isActive;
            
            bool changed = ImGui.DragFloat4(uniqueLabel, ref value, speed);
            isActive = ImGui.IsItemActive();
            
            ImGui.PopItemWidth();
            
            DrawTooltip(tooltip);
            
            string? error = validate?.Invoke(value);
            DrawValidationIcon(error);
            
            if (!string.IsNullOrEmpty(helpText))
                DrawHelpIcon(helpText);
            
            // Undo/redo tracking
            if (entityId.HasValue && !string.IsNullOrEmpty(fieldPath))
            {
                if (isActive && !wasActive)
                    BeginFieldEdit(entityId.Value, fieldPath, before, label);
                else if (!isActive && wasActive)
                    EndFieldEdit(entityId.Value, fieldPath, value, label);
            }
            
            return changed;
        }
        
        /// <summary>
        /// Vector3 field with undo/redo and validation (System.Numerics - for ImGui)
        /// </summary>
        public static bool Vector3Field(string label, ref NumVector3 value,
            float speed = 0.1f,
            uint? entityId = null, string? fieldPath = null,
            string? tooltip = null,
            Func<NumVector3, string?>? validate = null,
            string? helpText = null)
        {
            // Generate unique ID to avoid conflicts between components
            string uniqueLabel = entityId.HasValue && !string.IsNullOrEmpty(fieldPath) 
                ? $"{label}##{entityId}_{fieldPath}" 
                : label;
            
            ImGui.PushItemWidth(InspectorLayout.ControlWidth);
            
            NumVector3 before = value;
            bool isActive = ImGui.IsItemActive();
            bool wasActive = isActive;
            
            bool changed = ImGui.DragFloat3(uniqueLabel, ref value, speed);
            isActive = ImGui.IsItemActive();
            
            ImGui.PopItemWidth();
            
            DrawTooltip(tooltip);
            
            string? error = validate?.Invoke(value);
            DrawValidationIcon(error);
            
            if (!string.IsNullOrEmpty(helpText))
                DrawHelpIcon(helpText);
            
            // Undo/redo tracking
            if (entityId.HasValue && !string.IsNullOrEmpty(fieldPath))
            {
                if (isActive && !wasActive)
                    BeginFieldEdit(entityId.Value, fieldPath, before, label);
                else if (!isActive && wasActive)
                    EndFieldEdit(entityId.Value, fieldPath, value, label);
            }
            
            return changed;
        }
        
        /// <summary>
        /// Vector3 field for OpenTK.Mathematics.Vector3
        /// </summary>
        public static bool Vector3FieldOTK(string label, ref OtkVector3 value,
            float speed = 0.1f,
            uint? entityId = null, string? fieldPath = null,
            string? tooltip = null,
            Func<OtkVector3, string?>? validate = null,
            string? helpText = null)
        {
            var v = new NumVector3(value.X, value.Y, value.Z);
            bool changed = Vector3Field(label, ref v, speed, entityId, fieldPath, tooltip, 
                validate != null ? (vec) => validate(new OtkVector3(vec.X, vec.Y, vec.Z)) : null, 
                helpText);
            
            if (changed)
                value = new OtkVector3(v.X, v.Y, v.Z);
            
            return changed;
        }
        
        #endregion
        
        #region Color Fields
        
        /// <summary>
        /// RGB color picker (no alpha) - System.Numerics
        /// </summary>
        public static bool ColorField(string label, ref NumVector3 color,
            uint? entityId = null, string? fieldPath = null,
            string? tooltip = null,
            string? helpText = null)
        {
            // Generate unique ID to avoid conflicts between components
            string uniqueLabel = entityId.HasValue && !string.IsNullOrEmpty(fieldPath) 
                ? $"{label}##{entityId}_{fieldPath}" 
                : label;
            
            ImGui.PushItemWidth(InspectorLayout.ControlWidth);
            
            NumVector3 before = color;
            bool changed = ImGui.ColorEdit3(uniqueLabel, ref color);
            
            ImGui.PopItemWidth();
            
            DrawTooltip(tooltip);
            
            if (!string.IsNullOrEmpty(helpText))
                DrawHelpIcon(helpText);
            
            // Undo/redo (immediate for color picker)
            if (changed && entityId.HasValue && !string.IsNullOrEmpty(fieldPath))
            {
                BeginFieldEdit(entityId.Value, fieldPath, before, label);
                EndFieldEdit(entityId.Value, fieldPath, color, label);
            }
            
            return changed;
        }
        
        /// <summary>
        /// RGBA color picker (with alpha) - System.Numerics
        /// </summary>
        public static bool ColorFieldAlpha(string label, ref NumVector4 color,
            uint? entityId = null, string? fieldPath = null,
            string? tooltip = null,
            string? helpText = null)
        {
            // Generate unique ID to avoid conflicts between components
            string uniqueLabel = entityId.HasValue && !string.IsNullOrEmpty(fieldPath) 
                ? $"{label}##{entityId}_{fieldPath}" 
                : label;
            
            ImGui.PushItemWidth(InspectorLayout.ControlWidth);
            
            NumVector4 before = color;
            bool changed = ImGui.ColorEdit4(uniqueLabel, ref color);
            
            ImGui.PopItemWidth();
            
            DrawTooltip(tooltip);
            
            if (!string.IsNullOrEmpty(helpText))
                DrawHelpIcon(helpText);
            
            // Undo/redo (immediate for color picker)
            if (changed && entityId.HasValue && !string.IsNullOrEmpty(fieldPath))
            {
                BeginFieldEdit(entityId.Value, fieldPath, before, label);
                EndFieldEdit(entityId.Value, fieldPath, color, label);
            }
            
            return changed;
        }
        
        /// <summary>
        /// Color field for OpenTK.Mathematics.Vector3
        /// </summary>
        public static bool ColorFieldOTK(string label, ref OtkVector3 color,
            uint? entityId = null, string? fieldPath = null,
            string? tooltip = null,
            string? helpText = null)
        {
            var c = new NumVector3(color.X, color.Y, color.Z);
            bool changed = ColorField(label, ref c, entityId, fieldPath, tooltip, helpText);
            
            if (changed)
                color = new OtkVector3(c.X, c.Y, c.Z);
            
            return changed;
        }
        
        #endregion
        
        #region Enum Fields
        
        /// <summary>
        /// Enum dropdown with undo/redo
        /// </summary>
        public static bool EnumField<T>(string label, ref T value,
            uint? entityId = null, string? fieldPath = null,
            string? tooltip = null,
            string? helpText = null) where T : struct, Enum
        {
            // Generate unique ID to avoid conflicts between components
            string uniqueLabel = entityId.HasValue && !string.IsNullOrEmpty(fieldPath) 
                ? $"{label}##{entityId}_{fieldPath}" 
                : label;
            
            ImGui.PushItemWidth(InspectorLayout.ControlWidth);
            
            T before = value;
            var names = Enum.GetNames(typeof(T));
            int current = Convert.ToInt32(value);
            
            bool changed = ImGui.Combo(uniqueLabel, ref current, names, names.Length);
            
            if (changed)
                value = (T)Enum.ToObject(typeof(T), current);
            
            ImGui.PopItemWidth();
            
            DrawTooltip(tooltip);
            
            if (!string.IsNullOrEmpty(helpText))
                DrawHelpIcon(helpText);
            
            // Undo/redo (immediate for dropdown)
            if (changed && entityId.HasValue && !string.IsNullOrEmpty(fieldPath))
            {
                BeginFieldEdit(entityId.Value, fieldPath, before, label);
                EndFieldEdit(entityId.Value, fieldPath, value, label);
            }
            
            return changed;
        }
        
        #endregion
        
        #region Preset Buttons
        
        /// <summary>
        /// Draw a preset/quick action button
        /// </summary>
        public static bool PresetButton(string label, string? tooltip = null, NumVector2 size = default)
        {
            if (size == default)
                size = new NumVector2(0, InspectorLayout.ButtonHeight);
            
            ImGui.PushStyleColor(ImGuiCol.Button, InspectorColors.Button);
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, InspectorColors.ButtonHovered);
            ImGui.PushStyleColor(ImGuiCol.ButtonActive, InspectorColors.ButtonActive);
            
            bool clicked = ImGui.Button(label, size);
            
            ImGui.PopStyleColor(3);
            
            DrawTooltip(tooltip);
            
            return clicked;
        }
        
        /// <summary>
        /// Draw preset buttons in a horizontal row
        /// </summary>
        public static int PresetButtonRow(params (string label, string? tooltip)[] buttons)
        {
            int clicked = -1;
            
            for (int i = 0; i < buttons.Length; i++)
            {
                if (i > 0)
                    ImGui.SameLine();
                
                if (PresetButton(buttons[i].label, buttons[i].tooltip))
                    clicked = i;
            }
            
            return clicked;
        }
        
        #endregion
        
        #region Info/Warning/Error Messages
        
        /// <summary>
        /// Display an info message box
        /// </summary>
        public static void InfoBox(string message)
        {
            ImGui.PushStyleColor(ImGuiCol.Text, InspectorColors.Info);
            ImGui.TextWrapped($"ℹ {message}");
            ImGui.PopStyleColor();
        }
        
        /// <summary>
        /// Display a warning message box
        /// </summary>
        public static void WarningBox(string message)
        {
            ImGui.PushStyleColor(ImGuiCol.Text, InspectorColors.Warning);
            ImGui.TextWrapped($"⚠ {message}");
            ImGui.PopStyleColor();
        }
        
        /// <summary>
        /// Display an error message box
        /// </summary>
        public static void ErrorBox(string message)
        {
            ImGui.PushStyleColor(ImGuiCol.Text, InspectorColors.Error);
            ImGui.TextWrapped($"✖ {message}");
            ImGui.PopStyleColor();
        }
        
        /// <summary>
        /// Display a success message box
        /// </summary>
        public static void SuccessBox(string message)
        {
            ImGui.PushStyleColor(ImGuiCol.Text, InspectorColors.Success);
            ImGui.TextWrapped($"✓ {message}");
            ImGui.PopStyleColor();
        }
        
        #endregion
        
        #region Helper Labels
        
        /// <summary>
        /// Display a disabled/grayed-out text label
        /// </summary>
        public static void DisabledLabel(string text)
        {
            ImGui.TextDisabled(text);
        }
        
        /// <summary>
        /// Display a readonly value (label + value on same line)
        /// </summary>
        public static void ReadOnlyField(string label, string value)
        {
            ImGui.Text(label);
            ImGui.SameLine(InspectorLayout.LabelWidth);
            ImGui.TextDisabled(value);
        }
        
        #endregion
    }
}
