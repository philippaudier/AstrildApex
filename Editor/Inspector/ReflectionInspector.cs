using System;
using System.Linq;
using System.Reflection;
using System.Numerics;
using ImGuiNET;
using Editor.State;
using Engine.Inspector;
using Engine.Scene;
using OpenTK.Mathematics;
using NumericsVec2 = System.Numerics.Vector2;
using NumericsVec3 = System.Numerics.Vector3;
using NumericsVec4 = System.Numerics.Vector4;

namespace Editor.Inspector
{
    public static class ReflectionInspector
    {
        private static int _refreshCounter = 0;
        
        /// <summary>Force refresh of all widgets by incrementing counter (breaks ImGui internal state)</summary>
        public static void ForceRefresh()
        {
            _refreshCounter++;
        }
        public static void DrawForEntity(Entity ent)
        {
            if (ent == null) return;
            if (ImGui.CollapsingHeader("Components & Data", ImGuiTreeNodeFlags.DefaultOpen))
            {
                DrawObject(ent, "", "Entity");
            }
            
            // Process any component removals at the end of the frame
            ProcessComponentRemovals();
        }

        public static void DrawTransformComponent(Engine.Scene.Transform transform)
        {
            if (transform == null) return;
            
            // Create custom header with controls embedded in the collapsing header
            bool isOpen = DrawComponentHeaderWithControls("🔧 Transform", "transform", true, false, null);
            
            if (isOpen)
            {
                ImGui.PushID($"Transform_{_refreshCounter}");
                
                var pos = new System.Numerics.Vector3(transform.Position.X, transform.Position.Y, transform.Position.Z);
                var scale = new System.Numerics.Vector3(transform.Scale.X, transform.Scale.Y, transform.Scale.Z);
                
                // Convert quaternion to Euler angles for display
                var eulerRad = QuaternionToEuler(transform.Rotation);
                var eulerDeg = new System.Numerics.Vector3(
                    MathHelper.RadiansToDegrees(eulerRad.X),
                    MathHelper.RadiansToDegrees(eulerRad.Y),
                    MathHelper.RadiansToDegrees(eulerRad.Z)
                );
                
                // Position
                DrawXYZFields("Position", ref pos, 0.01f);
                if (pos != new System.Numerics.Vector3(transform.Position.X, transform.Position.Y, transform.Position.Z))
                {
                    transform.Position = new OpenTK.Mathematics.Vector3(pos.X, pos.Y, pos.Z);
                    Editor.Panels.EditorUI.MainViewport.UpdateGizmoPivot();
                }
                
                // Rotation (in degrees)
                var oldEulerDeg = eulerDeg;
                DrawXYZFields("Rotation", ref eulerDeg, 1.0f);
                if (eulerDeg != oldEulerDeg)
                {
                    var eulerRadNew = new System.Numerics.Vector3(
                        MathHelper.DegreesToRadians(eulerDeg.X),
                        MathHelper.DegreesToRadians(eulerDeg.Y),
                        MathHelper.DegreesToRadians(eulerDeg.Z)
                    );
                    transform.Rotation = EulerToQuaternion(eulerRadNew);
                    Editor.Panels.EditorUI.MainViewport.UpdateGizmoPivot();
                }
                
                // Scale
                DrawXYZFields("Scale", ref scale, 0.01f);
                if (scale != new System.Numerics.Vector3(transform.Scale.X, transform.Scale.Y, transform.Scale.Z))
                {
                    transform.Scale = new OpenTK.Mathematics.Vector3(scale.X, scale.Y, scale.Z);
                    Editor.Panels.EditorUI.MainViewport.UpdateGizmoPivot();
                }
                
                ImGui.PopID();
            }
        }

        // Simple header with blue color like before, controls below when expanded
        public static bool DrawComponentHeaderWithControls(string name, string iconName, bool enabled, bool canRemove, Engine.Components.Component? component)
        {
            ImGui.Spacing();
            
            // Blue header color like before
            var headerBgColor = new System.Numerics.Vector4(0.2f, 0.4f, 0.8f, 1.0f); // Blue like before
            
            ImGui.PushStyleColor(ImGuiCol.Header, headerBgColor);
            ImGui.PushStyleColor(ImGuiCol.HeaderHovered, headerBgColor * 1.1f);
            ImGui.PushStyleColor(ImGuiCol.HeaderActive, headerBgColor * 1.2f);
            
            // Simple CollapsingHeader like before
            bool isOpen = ImGui.CollapsingHeader($"{name}##{name}_header", ImGuiTreeNodeFlags.DefaultOpen);
            
            ImGui.PopStyleColor(3);
            
            // If component is open, show controls below
            if (isOpen)
            {
                ImGui.Indent();
                
                // Controls on same line below the header
                // Enabled checkbox
                if (component != null)
                {
                    bool componentEnabled = component.Enabled;
                    if (ImGui.Checkbox($"Enabled##{name}_enabled", ref componentEnabled))
                    {
                        component.Enabled = componentEnabled;
                    }
                    if (ImGui.IsItemHovered())
                        ImGui.SetTooltip("Enable/Disable Component");
                }
                else if (!canRemove) // Transform case - always enabled but grayed out
                {
                    bool dummyEnabled = true;
                    ImGui.BeginDisabled(true);
                    ImGui.Checkbox($"Enabled##{name}_enabled", ref dummyEnabled);
                    ImGui.EndDisabled();
                    if (ImGui.IsItemHovered())
                        ImGui.SetTooltip("Transform is always enabled");
                }
                
                // Remove button on same line
                if (canRemove && component != null)
                {
                    ImGui.SameLine();
                    if (ImGui.Button($"Remove Component##{name}_remove"))
                    {
                        // Mark component for removal
                        MarkComponentForRemoval(component);
                    }
                    if (ImGui.IsItemHovered())
                        ImGui.SetTooltip("Remove this component");
                }
                
                ImGui.Unindent();
            }
            
            return isOpen;
        }

        // Component removal system
        private static readonly System.Collections.Generic.List<Engine.Components.Component> _componentsToRemove = new();
        
        static void MarkComponentForRemoval(Engine.Components.Component component)
        {
            if (!_componentsToRemove.Contains(component))
            {
                _componentsToRemove.Add(component);
            }
        }
        
        public static void ProcessComponentRemovals()
        {
            foreach (var component in _componentsToRemove)
            {
                if (component.Entity != null)
                {
                    // Use reflection to call RemoveComponent<T>() with the correct type
                    var entityType = component.Entity.GetType();
                    var componentType = component.GetType();
                    var removeMethod = entityType.GetMethod("RemoveComponent", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                    
                    if (removeMethod != null)
                    {
                        var genericMethod = removeMethod.MakeGenericMethod(componentType);
                        genericMethod.Invoke(component.Entity, null);
                    }
                }
            }
            _componentsToRemove.Clear();
        }

        static void DrawXYZFields(string label, ref System.Numerics.Vector3 value, float step)
        {
            ImGui.Text(label);
            ImGui.SameLine();
            
            float labelWidth = 15f;
            float fieldWidth = (ImGui.GetContentRegionAvail().X - labelWidth * 3 - 10f) / 3f;
            
            ImGui.PushItemWidth(fieldWidth);
            
            ImGui.Text("X"); ImGui.SameLine();
            ImGui.DragFloat($"##X{label}", ref value.X, step, float.MinValue, float.MaxValue, "%.2f");
            ImGui.SameLine();
            
            ImGui.Text("Y"); ImGui.SameLine();
            ImGui.DragFloat($"##Y{label}", ref value.Y, step, float.MinValue, float.MaxValue, "%.2f");
            ImGui.SameLine();
            
            ImGui.Text("Z"); ImGui.SameLine();
            ImGui.DragFloat($"##Z{label}", ref value.Z, step, float.MinValue, float.MaxValue, "%.2f");
            
            ImGui.PopItemWidth();
        }

        static System.Numerics.Vector3 QuaternionToEuler(OpenTK.Mathematics.Quaternion q)
        {
            // Convert OpenTK Quaternion to Euler angles (in radians)
            var sinr_cosp = 2 * (q.W * q.X + q.Y * q.Z);
            var cosr_cosp = 1 - 2 * (q.X * q.X + q.Y * q.Y);
            var roll = MathF.Atan2(sinr_cosp, cosr_cosp);

            var sinp = 2 * (q.W * q.Y - q.Z * q.X);
            var pitch = MathF.Abs(sinp) >= 1 ? MathF.CopySign(MathF.PI / 2, sinp) : MathF.Asin(sinp);

            var siny_cosp = 2 * (q.W * q.Z + q.X * q.Y);
            var cosy_cosp = 1 - 2 * (q.Y * q.Y + q.Z * q.Z);
            var yaw = MathF.Atan2(siny_cosp, cosy_cosp);

            return new System.Numerics.Vector3(roll, pitch, yaw);
        }

        static OpenTK.Mathematics.Quaternion EulerToQuaternion(System.Numerics.Vector3 euler)
        {
            // Convert Euler angles (in radians) to OpenTK Quaternion
            var cr = MathF.Cos(euler.X * 0.5f);
            var sr = MathF.Sin(euler.X * 0.5f);
            var cp = MathF.Cos(euler.Y * 0.5f);
            var sp = MathF.Sin(euler.Y * 0.5f);
            var cy = MathF.Cos(euler.Z * 0.5f);
            var sy = MathF.Sin(euler.Z * 0.5f);

            return new OpenTK.Mathematics.Quaternion(
                sr * cp * cy - cr * sp * sy, // X
                cr * sp * cy + sr * cp * sy, // Y
                cr * cp * sy - sr * sp * cy, // Z
                cr * cp * cy + sr * sp * sy  // W
            );
        }

        public static void DrawObject(object obj, string basePath, string title)
        {
            if (obj == null) return;
            var t = obj.GetType();
            if (ImGui.TreeNode($"{title} [{t.Name}]"))
            {
                DrawMembers(obj, t, basePath);
                ImGui.TreePop();
            }
        }

        public static void DrawMembers(object obj, Type t, string basePath)
        {
            var scene = Editor.Panels.EditorUI.MainViewport.Renderer?.Scene;
            if (scene == null) return;
            var ent = obj as Entity;
            uint entId = (ent != null) ? ent.Id 
                    : TryGetEntityIdFromAny(obj) ?? Editor.State.Selection.ActiveEntityId;

            foreach (var m in t.GetMembers(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            {
                var f = m as FieldInfo;
                var p = m as PropertyInfo;
                Type? mt = f != null ? f.FieldType : p != null && p.CanRead ? p.PropertyType : null;
                if (mt == null) continue;

                // Montre uniquement les membres [Editable]
                var editable = (EditableAttribute?)m.GetCustomAttributes(typeof(EditableAttribute), true).FirstOrDefault();
                if (editable == null) continue;
                
                // Skip Transform properties since we have a dedicated display
                if (obj is Engine.Scene.Transform && (m.Name == "Position" || m.Name == "Rotation" || m.Name == "Scale"))
                    continue;
                
                var label = editable.DisplayName ?? m.Name;

                bool readOnly = m.GetCustomAttribute<ReadOnlyAttribute>() != null;
                var rng   = m.GetCustomAttribute<RangeAttribute>();
                var stepA = m.GetCustomAttribute<StepAttribute>();
                var color = m.GetCustomAttribute<ColorAttribute>();
                var multi = m.GetCustomAttribute<MultilineAttribute>();
                var tip   = m.GetCustomAttribute<TooltipAttribute>();

                object? cur = f != null ? f.GetValue(obj) : p!.GetValue(obj);
                string path = string.IsNullOrEmpty(basePath) ? m.Name : $"{basePath}.{m.Name}";

                if (tip != null && !string.IsNullOrEmpty(tip.Text))
                {
                    ImGui.TextDisabled($"ℹ {label}");
                    if (ImGui.IsItemHovered()) ImGui.SetTooltip(tip.Text);
                }

                ImGui.PushID($"{path}_{_refreshCounter}");
                bool anyChange = false;

                if (readOnly) ImGui.BeginDisabled(true);

                // --- Rendu selon type ---
                // Entity reference (drag & drop from hierarchy supported)
                if (mt == typeof(Engine.Scene.Entity) || typeof(Engine.Scene.Entity).IsAssignableFrom(mt))
                {
                    var entObj = scene.GetById(entId);
                    if (entObj != null)
                    {
                        var result = FieldWidgets.ComponentRefObj(mt, label, scene, cur);
                        if (result.changed)
                        {
                            cur = result.newValue;
                            anyChange = true;
                        }
                    }
                }
                else if (typeof(Engine.Components.Component).IsAssignableFrom(mt))
                {
                    // Référence de composant, attaché à l'entity courante
                    var entObj = scene.GetById(entId);
                    if (entObj != null)
                    {
                        if (mt == typeof(Engine.Components.Component))
                        {
                            ImGui.TextDisabled($"{label} (Component) — assign a specific type.");
                        }
                        else
                        {
                            var result = FieldWidgets.ComponentRefObj(mt, label, scene, cur);
                            if (result.changed)
                            {
                                cur = result.newValue;
                                anyChange = true;
                            }
                        }
                    }
                }
                else if (mt == typeof(string))
                {
                    anyChange = EditString(label, ref cur!, multi);
                }
                else if (mt == typeof(bool))
                {
                    anyChange = EditBool(label, ref cur!);
                }
                else if (mt.IsEnum)
                {
                    anyChange = EditEnum(label, mt, ref cur!);
                }
                else if (mt == typeof(int))
                {
                    anyChange = EditInt(label, ref cur!, rng, stepA);
                }
                else if (mt == typeof(float))
                {
                    anyChange = EditFloat(label, ref cur!, rng, stepA);
                }
                else if (mt == typeof(NumericsVec2))
                {
                    anyChange = EditVec2(label, ref cur!, rng, stepA);
                }
                else if (mt == typeof(NumericsVec3))
                {
                    anyChange = (color != null) ? EditColor3(label, ref cur!) : EditVec3(label, ref cur!, rng, stepA);
                }
                else if (mt == typeof(NumericsVec4))
                {
                    anyChange = (color != null) ? EditColor4(label, ref cur!) : EditVec4(label, ref cur!, rng, stepA);
                }
                else if (mt == typeof(OpenTK.Mathematics.Vector2))
                {
                    object tmp = new NumericsVec2(((OpenTK.Mathematics.Vector2)cur!).X, ((OpenTK.Mathematics.Vector2)cur!).Y);
                    anyChange = EditVec2(label, ref tmp!, rng, stepA);
                    if (anyChange) { var v = (NumericsVec2)tmp; cur = new OpenTK.Mathematics.Vector2(v.X, v.Y); }
                }
                else if (mt == typeof(OpenTK.Mathematics.Vector3))
                {
                    object tmp = new NumericsVec3(((OpenTK.Mathematics.Vector3)cur!).X, ((OpenTK.Mathematics.Vector3)cur!).Y, ((OpenTK.Mathematics.Vector3)cur!).Z);
                    anyChange = (color != null) ? EditColor3(label, ref tmp!) : EditVec3(label, ref tmp!, rng, stepA);
                    if (anyChange) { var v = (NumericsVec3)tmp; cur = new OpenTK.Mathematics.Vector3(v.X, v.Y, v.Z); }
                }
                else if (mt == typeof(OpenTK.Mathematics.Vector4))
                {
                    object tmp = new NumericsVec4(((OpenTK.Mathematics.Vector4)cur!).X, ((OpenTK.Mathematics.Vector4)cur!).Y, ((OpenTK.Mathematics.Vector4)cur!).Z, ((OpenTK.Mathematics.Vector4)cur!).W);
                    anyChange = (color != null) ? EditColor4(label, ref tmp!) : EditVec4(label, ref tmp!, rng, stepA);
                    if (anyChange) { var v = (NumericsVec4)tmp; cur = new OpenTK.Mathematics.Vector4(v.X, v.Y, v.Z, v.W); }
                }
                else if (mt == typeof(System.Numerics.Quaternion))
                {
                    ImGui.TextDisabled($"{label} (Quaternion) — non édité ici");
                    anyChange = false;
                }
                else
                {
                    if (cur != null && ImGui.TreeNode($"{label}"))
                    {
                        DrawObject(cur, path, label);
                        ImGui.TreePop();
                    }
                }

                // Apply live-set quand modifié (dans la même frame)
                if (anyChange)
                {
                    if (f != null) f.SetValue(obj, cur);
                    else p!.SetValue(obj, cur);

                    // Recalcule le pivot/gizmo quand on bouge Position/Rotation/Scale
                    Editor.Panels.EditorUI.MainViewport.UpdateGizmoPivot();
                }

                if (readOnly) ImGui.EndDisabled();
                ImGui.PopID();

                // Gestion Begin/End composite + push
                CachePushIfEnded(scene, entId, path, label, obj, m, f, p);
                ImGui.Separator();
            }
        }

        static readonly System.Collections.Generic.Dictionary<string, object?> _beforeCache = new();

        static void CachePushIfEnded(Scene scene, uint entId, string path, string label,
                                     object owner, MemberInfo m, FieldInfo? f, PropertyInfo? p)
        {
            string key = $"{entId}:{path}";

            if (ImGui.IsItemActivated())
            {
                object? before = f != null ? f.GetValue(owner) : p!.GetValue(owner);
                _beforeCache[key] = CloneValue(before);
                // Ouvrir la composite pile au début de l’édition
                UndoRedo.BeginComposite($"Edit {label}");
            }

            if (ImGui.IsItemDeactivatedAfterEdit())
            {
                _beforeCache.TryGetValue(key, out var beforeSnap);
                object? after = f != null ? f.GetValue(owner) : p!.GetValue(owner);

                if (!Equals(beforeSnap, after))
                {
                    UndoRedo.Push(new FieldEditAction($"{label} ({path})", entId, path, beforeSnap, after));
                }

                _beforeCache.Remove(key);
                // >>> Fermer la composite à la fin, qu’il y ait eu changement ou pas
                UndoRedo.EndComposite();
                Editor.Panels.EditorUI.MainViewport.UpdateGizmoPivot();
            }
        }

        static object? CloneValue(object? v)
        {
            if (v == null) return null;
            var t = v.GetType();
            if (t.IsValueType || v is string) return v;
            if (v is NumericsVec2 v2) return new NumericsVec2(v2.X, v2.Y);
            if (v is NumericsVec3 v3) return new NumericsVec3(v3.X, v3.Y, v3.Z);
            if (v is NumericsVec4 v4) return new NumericsVec4(v4.X, v4.Y, v4.Z, v4.W);
            if (v is OpenTK.Mathematics.Vector2 ov2) return new OpenTK.Mathematics.Vector2(ov2.X, ov2.Y);
            if (v is OpenTK.Mathematics.Vector3 ov3) return new OpenTK.Mathematics.Vector3(ov3.X, ov3.Y, ov3.Z);
            if (v is OpenTK.Mathematics.Vector4 ov4) return new OpenTK.Mathematics.Vector4(ov4.X, ov4.Y, ov4.Z, ov4.W);
            return v;
        }

        static uint? TryGetEntityIdFromAny(object o)
        {
            var t = o.GetType();
            var pf = t.GetField("Id", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (pf != null && pf.FieldType == typeof(uint)) return (uint)(pf.GetValue(o) ?? 0u);
            var pp = t.GetProperty("Id", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (pp != null && pp.PropertyType == typeof(uint)) return (uint)(pp.GetValue(o) ?? 0u);
            return null;
        }

        // ==== Widgets typés ====
        static bool EditString(string label, ref object val, MultilineAttribute? multi)
        {
            string s = (string)val;
            if (multi != null && multi.Lines > 1)
            {
                var text = s ?? string.Empty;
                var buf = text;
                var size = new NumericsVec2(0, 18f * multi.Lines);
                bool edited = ImGui.InputTextMultiline(label, ref buf, 1024, size);
                if (edited) val = buf;
                return edited;
            }
            else
            {
                var text = s ?? string.Empty;
                var bytes = System.Text.Encoding.UTF8.GetBytes(text);
                var buf = new byte[Math.Max(bytes.Length + 64, 256)];
                Array.Copy(bytes, buf, Math.Min(bytes.Length, buf.Length - 1));
                bool edited = ImGui.InputText(label, buf, (uint)buf.Length);
                if (edited) val = System.Text.Encoding.UTF8.GetString(buf).TrimEnd('\0');
                return edited;
            }
        }

        static bool EditBool(string label, ref object val)
        {
            bool b = (bool)val;
            bool edited = ImGui.Checkbox(label, ref b);
            if (edited) val = b;
            return edited;
        }

        static bool EditEnum(string label, Type enumType, ref object val)
        {
            var names = Enum.GetNames(enumType);
            int idx = Array.IndexOf(names, Enum.GetName(enumType, val)); if (idx < 0) idx = 0;
            bool edited = ImGui.Combo(label, ref idx, names, names.Length);
            if (edited) val = Enum.Parse(enumType, names[idx]);
            return edited;
        }

        static bool EditInt(string label, ref object val, RangeAttribute? r, StepAttribute? s)
        {
            int v = (int)val;
            float step = s?.Step ?? 1f;
            int vmin = r != null ? (int)r.Min : int.MinValue / 4;
            int vmax = r != null ? (int)r.Max : int.MaxValue / 4;
            bool edited = ImGui.DragInt(label, ref v, step, vmin, vmax);
            if (edited) val = v; return edited;
        }

        static bool EditFloat(string label, ref object val, RangeAttribute? r, StepAttribute? s)
        {
            float v = (float)val;
            float step = s?.Step ?? 0.01f;
            float vmin = r?.Min ?? float.NegativeInfinity;
            float vmax = r?.Max ?? float.PositiveInfinity;
            bool edited = ImGui.DragFloat(label, ref v, step, vmin, vmax, "%.4f");
            if (edited) val = v; return edited;
        }

        static bool EditVec2(string label, ref object val, RangeAttribute? r, StepAttribute? s)
        {
            var v = (NumericsVec2)val;
            float step = s?.Step ?? 0.01f;
            
            ImGui.SetNextItemWidth(-100f);
            bool edited = ImGui.DragFloat2(label, ref v, step);
            if (edited) val = v; return edited;
        }

        static bool EditVec3(string label, ref object val, RangeAttribute? r, StepAttribute? s)
        {
            var v = (NumericsVec3)val;
            float step = s?.Step ?? 0.01f;
            
            // Set wider width for Vector3 fields to prevent label truncation
            ImGui.SetNextItemWidth(-100f);
            bool edited = ImGui.DragFloat3(label, ref v, step);
            if (edited) val = v; return edited;
        }

        static bool EditVec4(string label, ref object val, RangeAttribute? r, StepAttribute? s)
        {
            var v = (NumericsVec4)val;
            float step = s?.Step ?? 0.01f;
            
            ImGui.SetNextItemWidth(-100f);
            bool edited = ImGui.DragFloat4(label, ref v, step);
            if (edited) val = v; return edited;
        }

        static bool EditColor3(string label, ref object val)
        {
            var v = (NumericsVec3)val;
            var v4 = new NumericsVec4(v.X, v.Y, v.Z, 1f);
            bool edited = ImGui.ColorEdit4(label, ref v4, ImGuiColorEditFlags.DisplayRGB);
            if (edited) val = new NumericsVec3(v4.X, v4.Y, v4.Z);
            return edited;
        }

        static bool EditColor4(string label, ref object val)
        {
            var v = (NumericsVec4)val;
            bool edited = ImGui.ColorEdit4(label, ref v, ImGuiColorEditFlags.DisplayRGB | ImGuiColorEditFlags.AlphaBar);
            if (edited) val = v; return edited;
        }
    }
}
