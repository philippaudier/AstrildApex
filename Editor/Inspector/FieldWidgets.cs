using System;
using System.Collections.Generic;
using System.Numerics;
using ImGuiNET;
using Editor.State;
using Engine.Scene;
using OpenTK.Mathematics;
using NumericsVec4 = System.Numerics.Vector4;

namespace Editor.Inspector
{
    /// <summary>Widgets génériques + logiques d'édition undoables par champ.</summary>
    public static partial class FieldWidgets
    {

        /// <summary>
        /// Widget pour éditer une référence à un Component (ou dérivé) depuis n'importe quelle entité de la scène.
        /// Supporte le drag & drop depuis la hiérarchie.
        /// </summary>
        public static (bool changed, object? newValue) ComponentRefObj(Type t, string label, Entity ent, object? current)
        {
            // Pour l'instant, on utilise l'ancienne méthode mais on devrait passer la scène ici
            // TODO: Refactoriser pour passer la scène en paramètre
            var all = ent.GetAllComponents().Where(c => t.IsInstanceOfType(c)).ToList();
            string[] names = all.Select(c => c.GetType().Name).ToArray();
            int curIdx = -1;
            if (current != null)
            {
                for (int i = 0; i < all.Count; i++)
                    if (ReferenceEquals(all[i], current)) curIdx = i;
            }
            int sel = curIdx;
            bool changed = ImGui.Combo(label, ref sel, names, names.Length);
            object? newVal = (sel >= 0 && sel < all.Count) ? all[sel] : null;
            return (changed, newVal);
        }
        
        /// <summary>
        /// Widget pour éditer une référence à un Component (ou dérivé) depuis n'importe quelle entité de la scène.
        /// Supporte le drag & drop depuis la hiérarchie.
        /// </summary>
        public static unsafe (bool changed, object? newValue) ComponentRefObj(Type t, string label, Scene scene, object? current)
        {
            bool changed = false;
            object? newValue = current;
            
            var currentComp = current as Engine.Components.Component;
            var currentEnt  = current as Engine.Scene.Entity;
            string displayText;
            if (currentEnt != null)
                displayText = currentEnt.Name ?? $"Entity#{currentEnt.Id}";
            else if (currentComp != null)
                displayText = $"{currentComp.Entity?.Name}.{currentComp.GetType().Name}";
            else
                displayText = "<none>";
            
            ImGui.PushItemWidth(220f);
            
            // Bouton qui sert de zone de drop
            if (ImGui.Button($"{displayText}###{label}", new System.Numerics.Vector2(220f, 0)))
            {
                // Ouvrir le popup au clic
                ImGui.OpenPopup($"select_{label}");
            }
            
            // Drag & Drop Target
            if (ImGui.BeginDragDropTarget())
            {
                // IMPORTANT : accepter *avant* la livraison ne sert à rien ici,
                // on ne s'intéresse qu'à la livraison effective.
                var payload = ImGui.AcceptDragDropPayload("ENTITY_ID");
                unsafe
                {
                    if (payload.NativePtr != null && payload.DataSize == sizeof(int))
                    {
                        int dropped = *(int*)payload.Data;
                        var go = scene?.GetById((uint)dropped);
                        if (go != null)
                        {
                            object? targetValue = null;
                            // Si le champ attend une Entity, on assigne l'Entity
                            if (t == typeof(Engine.Scene.Entity) || t.IsAssignableFrom(typeof(Engine.Scene.Entity)))
                                targetValue = go;
                            // Si le champ attend un Component, on va chercher ce type sur l'entité droppée
                            else if (typeof(Engine.Components.Component).IsAssignableFrom(t))
                                targetValue = go.GetAllComponents().FirstOrDefault(c => t.IsInstanceOfType(c));

                            if (targetValue != null && !ReferenceEquals(current, targetValue))
                            {
                                newValue = targetValue;
                                changed = true;
                                // Si on était en auto-lock (drag depuis la hiérarchie), on peut déverrouiller maintenant
                                if (Editor.Panels.InspectorPanel.IsAutoLockedForDrag())
                                    Editor.Panels.InspectorPanel.AutoUnlockFromDrag();
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
                if (ImGui.Selectable("<none>", current == null))
                {
                    newValue = null;
                    changed = true;
                }
                
                // Tous les components compatibles dans toute la scène
                if (scene != null)
                {
                    // Si le type attendu est Entity, lister les entités
                    if (t == typeof(Engine.Scene.Entity) || t.IsAssignableFrom(typeof(Engine.Scene.Entity)))
                    {
                        foreach (var entity in scene.Entities)
                        {
                            bool isSelected = ReferenceEquals(current, entity);
                            var itemText = entity.Name ?? $"Entity#{entity.Id}";
                            if (ImGui.Selectable(itemText, isSelected))
                            {
                                newValue = entity;
                                changed = true;
                            }
                        }
                    }
                    else
                    {
                        foreach (var entity in scene.Entities)
                        {
                            foreach (var component in entity.GetAllComponents().Where(c => t.IsInstanceOfType(c)))
                            {
                                bool isSelected = ReferenceEquals(current, component);
                                var itemText = $"{entity.Name}.{component.GetType().Name}";
                                
                                if (ImGui.Selectable(itemText, isSelected))
                                {
                                    newValue = component;
                                    changed = true;
                                }
                            }
                        }
                    }
                }
                
                ImGui.EndPopup();
            }
            
            ImGui.PopItemWidth();
            return (changed, newValue);
        }
    
        // Etat d'édition par champ (clef = "entityId:path")
        private static readonly HashSet<string> _editing = new();
        private static readonly Dictionary<string, object?> _before = new();

        private static string Key(uint id, string path) => $"{id}:{path}";

        private static void BeginEdit(uint id, string path, object? snapshotBefore, string label)
        {
            string k = Key(id, path);
            if (_editing.Add(k))
            {
                _before[k] = snapshotBefore;
                UndoRedo.BeginComposite($"Edit {label}");
            }
        }

        private static void EndEdit(Scene scene, uint id, string path, object? current, string label)
        {
            string k = Key(id, path);
            if (_editing.Remove(k))
            {
                _before.TryGetValue(k, out var prev);
                _before.Remove(k);

                if (!Equals(prev, current))
                {
                    UndoRedo.Push(new FieldEditAction($"{label} ({path})", id, path, prev, current));
                    UndoRedo.RaiseAfterChange();
                }

                // Toujours fermer la transaction, changement ou non.
                UndoRedo.EndComposite();
            }
        }

        // --------- Widgets de base (Name / Mesh / Color) ----------------------

        public static void DrawEntityBasic(Entity ent)
        {
            var scene = Editor.Panels.EditorUI.MainViewport.Renderer?.Scene;
            if (scene == null) return;

            // Name (string)
            {
                string path  = "Name";
                string label = "Name";
                string cur   = ent.Name ?? string.Empty;

                // buffer UTF8 256 octets
                byte[] buf = new byte[256];
                var curBytes = System.Text.Encoding.UTF8.GetBytes(cur);
                Array.Copy(curBytes, buf, Math.Min(buf.Length - 1, curBytes.Length));

                bool edited = ImGui.InputText(label, buf, (uint)buf.Length);
                if (ImGui.IsItemActivated()) BeginEdit(ent.Id, path, cur, label);

                if (edited)
                {
                    var s = System.Text.Encoding.UTF8.GetString(buf).TrimEnd('\0');
                    ent.Name = s;
                }

                if (ImGui.IsItemDeactivatedAfterEdit())
                {
                    EndEdit(scene!, ent.Id, path, ent.Name ?? string.Empty, label);
                }
            }

            // Note: Mesh and Color are now handled by component system
            // These legacy fields are no longer part of Entity base class
            // They should be managed through MeshRendererComponent in the Inspector
        }
        }
    }

