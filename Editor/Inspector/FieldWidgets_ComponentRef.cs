using System;
using System.Linq;
using ImGuiNET;
using Engine.Scene;
using Engine.Components;

namespace Editor.Inspector
{
    public static partial class FieldWidgets
    {
        // Affiche une référence vers un Component T depuis n'importe quelle Entity de la scène.
        // Supporte le drag & drop depuis la hiérarchie + combo box pour sélection manuelle.
        public static bool ComponentRef<T>(string label, Scene scene, ref T? value) where T : Component
        {
            if (scene == null) return false;
            bool changed = false;
            
            var displayText = value != null ? $"{value.Entity?.Name}.{value.GetType().Name}" : "<none>";
            
            ImGui.PushItemWidth(220f);
            
            // Zone de drop
            var cursorPos = ImGui.GetCursorPos();
            var availableWidth = ImGui.GetContentRegionAvail().X;
            
            // Bouton qui sert de zone de drop
            if (ImGui.Button($"{displayText}###{label}", new System.Numerics.Vector2(220f, 0)))
            {
                // Ouvrir le combo au clic
                ImGui.OpenPopup($"select_{label}");
            }
            
            // Drag & Drop Target
            if (ImGui.BeginDragDropTarget())
            {
                unsafe
                {
                    var payload = ImGui.AcceptDragDropPayload("ENTITY_ID");
                    if (payload.NativePtr != (void*)IntPtr.Zero)
                    {
                        var entityId = *(uint*)payload.Data;
                        var entity = scene.GetById(entityId);
                        if (entity != null)
                        {
                            var component = entity.GetComponent<T>();
                            if (component != null && !ReferenceEquals(value, component))
                            {
                                value = component;
                                changed = true;
                            }
                        }
                    }
                }
                ImGui.EndDragDropTarget();
            }
            
            // Popup de sélection manuelle
            if (ImGui.BeginPopup($"select_{label}"))
            {
                // Option none
                if (ImGui.Selectable("<none>", value == null))
                {
                    value = null;
                    changed = true;
                }
                
                // Tous les components compatibles dans toute la scène
                foreach (var entity in scene.Entities)
                {
                    foreach (var component in entity.GetAllComponents().OfType<T>())
                    {
                        bool isSelected = ReferenceEquals(value, component);
                        var itemText = $"{entity.Name}.{component.GetType().Name}";
                        
                        if (ImGui.Selectable(itemText, isSelected))
                        {
                            value = component;
                            changed = true;
                        }
                    }
                }
                
                ImGui.EndPopup();
            }
            
            ImGui.PopItemWidth();
            return changed;
        }
        
        // Version de compatibilité pour l'entité courante seulement
        public static bool ComponentRef<T>(string label, Entity ent, ref T? value) where T : Component
        {
            // Trouve la scène depuis l'entité
            if (ent?.GetComponent<TransformComponent>()?.Entity?.Transform?.GetType().Assembly
                ?.GetType("Engine.Scene.Scene") != null)
            {
                // Pour l'instant, utilise l'ancienne méthode
                if (ent == null) return false;
                bool changed = false;
                ImGui.PushItemWidth(220f);
                if (ImGui.BeginCombo(label, value != null ? value.GetType().Name : "<none>"))
                {
                    // Option none
                    if (ImGui.Selectable("<none>", value == null))
                    {
                        value = null;
                        changed = true;
                    }
                    // Tous les components compatibles
                    foreach (var c in ent.GetAllComponents())
                    {
                        if (c is T tC)
                        {
                            bool sel = ReferenceEquals(value, tC);
                            if (ImGui.Selectable(c.GetType().Name, sel))
                            {
                                value = tC;
                                changed = true;
                            }
                        }
                    }
                    ImGui.EndCombo();
                }
                ImGui.PopItemWidth();
                return changed;
            }
            return false;
        }
    }
}
