using System;
using System.Linq;
using System.Collections.Generic;
using ImGuiNET;
using OpenTK.Mathematics;
using Vector2 = System.Numerics.Vector2; // alias unique pour System.Numerics.Vector2
using Editor.State;
using Engine.Scene;
using Engine.Components;
using Editor.Icons;
// Résolution d'ambiguïté pour Vector3/Vector4 d'OpenTK :
using Vec3 = OpenTK.Mathematics.Vector3;
using Vec4 = OpenTK.Mathematics.Vector4;

namespace Editor.Panels
{
    public static class HierarchyPanel
    {
        // --- Déféré (corrige shift-click bas→haut) ---
        private static bool _deferredRangeSelect = false;
        private static uint _deferredRangeToId = 0;

        // --- Drag & Drop (reparent) ---
        private static uint _draggedEntityId = 0;
        // Groupe d'IDs actuellement dragués depuis la Hiérarchie
        private static List<uint>? _draggedGroup = null;
        // Pour empêcher la sélection de bouger pendant un drag
        private static bool _isDragging;

        // --- Multi-sélection / feedback ---
        private static readonly Dictionary<uint, (Vector2 min, Vector2 max)> _itemRects = new(); // rect par item
        private static readonly List<uint> _visibleOrder = new();                                 // ordre visible
        private static uint _lastAnchorId = 0;                                                     // ancre Shift
        private static bool _rectSelecting = false;                                                // rectangle actif ?
        private static Vector2 _rectStart, _rectEnd;                                               // coords rectangle

        // --- Clic vs drag (items) ---
        private static bool _clickArmed = false;
        private static uint _clickCandidateId = 0;
        private static Vector2 _clickStart;
        private const float CLICK_DRAG_THRESHOLD = 4f;

        // --- Clic sur fond (vide) vs rectangle ---
        private static bool _mouseDownOnEmpty = false;
        private static Vector2 _windowMouseDownStart;

        public static void Draw()
        {
            if (!ImGui.Begin("Hierarchy")) { ImGui.End(); return; }

            var scene = EditorUI.MainViewport.Renderer?.Scene;
            if (scene == null)
            {
                ImGui.TextDisabled("Scene not available.");
                ImGui.End();
                return;
            }

            // Reset state for this frame
            _itemRects.Clear();
            _visibleOrder.Clear();

            // Toolbar supprimée (Create Cube, Duplicate, Delete)
            ImGui.Separator();


            // Reset frame (pour rectangle & indexs visibles)
            _itemRects.Clear();
            _visibleOrder.Clear();

            // Tree (racines -> récursif)
            foreach (var e in scene.Entities.ToArray())
                if (e.Parent == null)
                    DrawEntityNode(scene, e);

            // Traite un shift-click différé une fois que _visibleOrder est complet
            if (_deferredRangeSelect && _deferredRangeToId != 0 && _lastAnchorId != 0)
            {
                _deferredRangeSelect = false;

                uint from = _lastAnchorId;
                uint to   = _deferredRangeToId;

                int a = _visibleOrder.IndexOf(from);
                int b = _visibleOrder.IndexOf(to);

                if (a == -1)
                {
                    // Fallback : prendre le premier item sélectionné qui est visible
                    foreach (var id in _visibleOrder)
                    {
                        if (Selection.Contains(id)) { from = id; a = _visibleOrder.IndexOf(from); break; }
                    }
                }

                if (a != -1 && b != -1)
                {
                    ApplyRangeSelection(from, to, additive: false, toggle: false);
                    FinalizeSelection();
                }
            }

            var io = ImGui.GetIO();

            // Clear sélection sur fond (clic court) — seulement si le clic a démarré sur le vide
            if (ImGui.IsWindowHovered() &&
                _mouseDownOnEmpty &&
                ImGui.IsMouseReleased(ImGuiMouseButton.Left) &&
                !_rectSelecting && !io.KeyCtrl && !io.KeyShift)
            {
                Selection.Clear();
                FinalizeSelection();
                _lastAnchorId = 0;
            }

            // ===================== CLIC/FOND/RECTANGLE =====================

            // MouseDown : a-t-on cliqué sur du vide ? (On ne démarre PAS le rectangle ici)
            if (ImGui.IsWindowHovered() && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
            {
                _mouseDownOnEmpty = !ImGui.IsAnyItemHovered();
                var localMouse = ImGui.GetMousePos() - ImGui.GetWindowPos();
                _windowMouseDownStart = new Vector2(localMouse.X, localMouse.Y);
            }

            // Drag sur fond : si dépasse le seuil -> démarrer rectangle
            if (ImGui.IsMouseDown(ImGuiMouseButton.Left) && _mouseDownOnEmpty)
            {
                var localMouse = ImGui.GetMousePos() - ImGui.GetWindowPos();
                var delta = new Vector2(localMouse.X, localMouse.Y) - _windowMouseDownStart;

                if (!_rectSelecting && delta.Length() >= CLICK_DRAG_THRESHOLD)
                {
                    _rectSelecting = true;
                    _rectStart = _windowMouseDownStart;
                    // annule un clic item armé : c’est un rectangle maintenant
                    _clickArmed = false;
                    _clickCandidateId = 0;
                }

                if (_rectSelecting)
                {
                    _rectEnd = new Vector2(localMouse.X, localMouse.Y);

                    // 1) Dessin rectangle
                    var draw = ImGui.GetForegroundDrawList();
                    var a = new Vector2(MathF.Min(_rectStart.X, _rectEnd.X), MathF.Min(_rectStart.Y, _rectEnd.Y));
                    var b = new Vector2(MathF.Max(_rectStart.X, _rectEnd.X), MathF.Max(_rectStart.Y, _rectEnd.Y));
                    var p0 = ImGui.GetWindowPos() + a;
                    var p1 = ImGui.GetWindowPos() + b;
                    draw.AddRect(p0, p1, 0x66FFFFFF);
                    draw.AddRectFilled(p0, p1, 0x2266AAFF);

                    // 2) Feedback live
                    var selectionRect = (min: p0, max: p1);

                    // Couleurs (ImGui ABGR)
                    const uint PREVIEW_FILL  = 0x223388FF;
                    const uint PREVIEW_EDGE  = 0x663388FF;
                    const uint RANGE_FILL    = 0x3344AAFF;
                    const uint RANGE_EDGE    = 0x7744AAFF;
                    const uint ANCHOR_EDGE   = 0xFFAA22FF; // liseré ancre (orange)

                    // Items touchés
                    var hitIds = new List<uint>();
                    foreach (var kv in _itemRects)
                        if (Intersects(kv.Value, selectionRect)) hitIds.Add(kv.Key);

                    bool shift = io.KeyShift;

                    if (shift && _lastAnchorId != 0 && hitIds.Count > 0)
                    {
                        // Multi-range preview : anchor ↔ nearest (verticalement)
                        uint nearest = hitIds[0];
                        float best = float.MaxValue;
                        float rectCenterY = 0.5f * (selectionRect.min.Y + selectionRect.max.Y);

                        foreach (var id in hitIds)
                        {
                            var r = _itemRects[id];
                            float cy = 0.5f * (r.min.Y + r.max.Y);
                            float d = MathF.Abs(cy - rectCenterY);
                            if (d < best) { best = d; nearest = id; }
                        }

                        int aIdx = _visibleOrder.IndexOf(_lastAnchorId);
                        int bIdx = _visibleOrder.IndexOf(nearest);
                        if (aIdx != -1 && bIdx != -1)
                        {
                            if (aIdx > bIdx) (aIdx, bIdx) = (bIdx, aIdx);
                            for (int i = aIdx; i <= bIdx; i++)
                                DrawItemHighlight(_visibleOrder[i], RANGE_FILL, RANGE_EDGE);
                        }
                        else
                        {
                            foreach (var id in hitIds) DrawItemHighlight(id, PREVIEW_FILL, PREVIEW_EDGE);
                        }
                    }
                    else
                    {
                        foreach (var id in hitIds) DrawItemHighlight(id, PREVIEW_FILL, PREVIEW_EDGE);
                    }

                    // 3) Liseré spécial pour l’ancre (✔ présent)
                    if (_lastAnchorId != 0 && _itemRects.TryGetValue(_lastAnchorId, out var rectAnchor))
                    {
                        var p0a = new Vector2(rectAnchor.min.X, rectAnchor.min.Y);
                        var p1a = new Vector2(rectAnchor.max.X, rectAnchor.max.Y);
                        draw.AddRect(p0a, p1a, ANCHOR_EDGE, 0f, ImDrawFlags.None, 2.0f);
                    }
                }
            }

            // MouseUp : rectangle OU clic court sur fond
            if (ImGui.IsMouseReleased(ImGuiMouseButton.Left))
            {
                bool ctrl = io.KeyCtrl;
                bool shift = io.KeyShift;

                if (_rectSelecting)
                {
                    _rectSelecting = false;
                    var a = new Vector2(MathF.Min(_rectStart.X, _rectEnd.X), MathF.Min(_rectStart.Y, _rectEnd.Y));
                    var b = new Vector2(MathF.Max(_rectStart.X, _rectEnd.X), MathF.Max(_rectStart.Y, _rectEnd.Y));
                    var selectionRect = (min: ImGui.GetWindowPos() + a, max: ImGui.GetWindowPos() + b);

                    var hitIds = new List<uint>();
                    foreach (var kv in _itemRects)
                        if (Intersects(kv.Value, selectionRect)) hitIds.Add(kv.Key);

                    if (hitIds.Count > 0)
                    {
                        if (ctrl)
                        {
                            foreach (var id in hitIds) Selection.Toggle(id);
                        }
                        else if (shift && _lastAnchorId != 0)
                        {
                            // Multi-range final : anchor ↔ nearest
                            uint nearest = hitIds[0];
                            float best = float.MaxValue;
                            float rectCenterY = 0.5f * (selectionRect.min.Y + selectionRect.max.Y);
                            foreach (var id in hitIds)
                            {
                                var r = _itemRects[id];
                                float cy = 0.5f * (r.min.Y + r.max.Y);
                                float d = MathF.Abs(cy - rectCenterY);
                                if (d < best) { best = d; nearest = id; }
                            }
                            ApplyRangeSelection(_lastAnchorId, nearest, additive: false, toggle: false);
                        }
                        else
                        {
                            Selection.ReplaceMany(hitIds);
                            // ancre = item le plus haut
                            uint topMost = hitIds[0];
                            float topY = float.MaxValue;
                            foreach (var id in hitIds)
                            {
                                var r = _itemRects[id];
                                if (r.min.Y < topY) { topY = r.min.Y; topMost = id; }
                            }
                            _lastAnchorId = topMost;
                        }
                        FinalizeSelection();
                    }
                }
                else if (_mouseDownOnEmpty)
                {
                    // Clic court sur fond : clear
                    var localMouse = ImGui.GetMousePos() - ImGui.GetWindowPos();
                    var delta = new Vector2(localMouse.X, localMouse.Y) - _windowMouseDownStart;
                    if (delta.Length() < CLICK_DRAG_THRESHOLD && !ctrl && !shift)
                    {
                        Selection.Clear();
                        FinalizeSelection();
                        _lastAnchorId = 0;
                    }
                }

                _mouseDownOnEmpty = false; // reset fin de cycle
            }

            // Zone de drop invisible pour déparenter vers zone vide
            var windowSize = ImGui.GetWindowSize();
            var currentCursor = ImGui.GetCursorPos();
            var availableSpace = windowSize.Y - currentCursor.Y - 40f; // Laisser un peu d'espace
            
            // Ajouter une zone invisible pour capturer les drops vers l'espace vide
            if (availableSpace > 20f)
            {
                ImGui.PushID("empty-drop-zone");
                var dummyHeight = Math.Max(20f, availableSpace);
                ImGui.Dummy(new Vector2(windowSize.X - 20f, dummyHeight));
                
                if (ImGui.BeginDragDropTarget())
                {
                    unsafe
                    {
                        var payload = ImGui.AcceptDragDropPayload("ENT");
                        if (payload.NativePtr == null || (payload.Data == IntPtr.Zero && payload.DataSize == 0))
                        {
                            // Support aussi un drop venant de "ENTITY_ID" pour harmoniser
                            payload = ImGui.AcceptDragDropPayload("ENTITY_ID");
                        }
                        if (payload.NativePtr != null)
                        {
                            var ids = (_draggedGroup != null && _draggedGroup.Count > 0)
                                        ? _draggedGroup
                                        : (_draggedEntityId != 0 ? new List<uint> { _draggedEntityId } : new List<uint>());


                            foreach (var id in ids)
                            {
                                var who = scene.GetById(id);
                                if (who != null && who.Parent != null)
                                {
                                    var old = who.Parent;
                                    who.SetParent(null, keepWorld: true); // Garder keepWorld=true pour unparent
                                    UndoRedo.Push(new ReparentAction("Unparent", who.Id, old?.Id, null));
                                }
                            }
                            _draggedEntityId = 0;
                            _draggedGroup = null;
                        }
                    }
                    ImGui.EndDragDropTarget();
                }
                ImGui.PopID();
            }

            // Right-click menu for creating objects
            if (ImGui.BeginPopupContextWindow("HierarchyContextMenu", ImGuiPopupFlags.MouseButtonRight | ImGuiPopupFlags.NoOpenOverExistingPopup))
            {
                if (ImGui.BeginMenu("Create"))
                {
                    if (ImGui.MenuItem("Empty GameObject"))
                    {
                        var entity = new Engine.Scene.Entity 
                        { 
                            Id = scene.GetNextEntityId(),
                            Name = "GameObject" 
                        };
                        scene.Entities.Add(entity);
                        Selection.SetSingle(entity.Id);
                        EditorUI.MainViewport.UpdateGizmoPivot();
                    }
                    if (ImGui.MenuItem("Camera"))
                    {
                        var entity = new Engine.Scene.Entity 
                        { 
                            Id = scene.GetNextEntityId(),
                            Name = "Camera" 
                        };
                        scene.Entities.Add(entity);
                        entity.AddComponent<Engine.Components.CameraComponent>();
                        Selection.SetSingle(entity.Id);
                        EditorUI.MainViewport.UpdateGizmoPivot();
                    }
                    if (ImGui.BeginMenu("3D"))
                    {
                        if (ImGui.MenuItem("Cube"))
                        {
                            var go = scene.CreateCube("Cube", new Vec3(0, 0.5f, 0), Vec3.One, new Vec4(0.8f, 0.85f, 0.98f, 1));
                            // Default collider
                            if (!go.HasComponent<Engine.Components.BoxCollider>())
                                go.AddComponent<Engine.Components.BoxCollider>();
                            Selection.SetSingle(go.Id);
                            EditorUI.MainViewport.UpdateGizmoPivot();
                        }
                        if (ImGui.MenuItem("Capsule"))
                        {
                            var go = scene.CreateCapsule("Capsule", new Vec3(0, 1.0f, 0), 2.0f, 0.5f, new Vec4(0.9f, 0.9f, 0.9f, 1));
                            Selection.SetSingle(go.Id);
                            EditorUI.MainViewport.UpdateGizmoPivot();
                        }
                        if (ImGui.MenuItem("Sphere"))
                        {
                            var go = scene.CreateSphere("Sphere", new Vec3(0, 0.5f, 0), 0.5f, new Vec4(0.95f, 0.95f, 0.95f, 1));
                            Selection.SetSingle(go.Id);
                            EditorUI.MainViewport.UpdateGizmoPivot();
                        }
                        if (ImGui.MenuItem("Plane"))
                        {
                            var go = scene.CreatePlane("Plane", new Vec3(0, 0f, 0), new OpenTK.Mathematics.Vector2(10f, 10f), new Vec4(1,1,1,1));
                            Selection.SetSingle(go.Id);
                            EditorUI.MainViewport.UpdateGizmoPivot();
                        }
                        if (ImGui.MenuItem("Quad"))
                        {
                            var go = scene.CreateQuad("Quad", new Vec3(0, 0.5f, 0), new OpenTK.Mathematics.Vector2(1f, 1f), new Vec4(1,1,1,1));
                            Selection.SetSingle(go.Id);
                            EditorUI.MainViewport.UpdateGizmoPivot();
                        }
                        ImGui.EndMenu();
                    }
                    
                    if (ImGui.BeginMenu("Generation"))
                    {
                        if (ImGui.MenuItem("Terrain Generator"))
                        {
                            var entity = new Engine.Scene.Entity
                            {
                                Id = scene.GetNextEntityId(),
                                Name = "Terrain"
                            };
                            scene.Entities.Add(entity);
                            entity.AddComponent<Engine.Components.Terrain>();
                            Selection.SetSingle(entity.Id);
                            EditorUI.MainViewport.UpdateGizmoPivot();
                        }
                        ImGui.EndMenu();
                    }
                    if (ImGui.MenuItem("Water"))
                    {
                        var water = scene.CreateWater("Water", new Vec3(0, 0, 0), 100f, 100f, 32);
                        Selection.SetSingle(water.Id);
                        EditorUI.MainViewport.UpdateGizmoPivot();
                    }
                    // UX menu supprimé (Separator)
                    if (ImGui.BeginMenu("Light"))
                    {
                        if (ImGui.MenuItem("Directional Light"))
                        {
                            var light = scene.CreateDirectionalLight("Directional Light", 
                                new Vec3(-0.3f, -0.8f, -0.5f), // Default sun-like direction
                                new Vec3(1f, 0.956f, 0.839f), // Warm white color
                                1.0f);
                            Selection.SetSingle(light.Id);
                            EditorUI.MainViewport.UpdateGizmoPivot();
                        }
                        if (ImGui.MenuItem("Point Light"))
                        {
                            var light = scene.CreatePointLight("Point Light", 
                                new Vec3(0f, 2f, 0f), // Default position above origin
                                new Vec3(1f, 1f, 1f), // White color
                                1.0f, 10.0f);
                            Selection.SetSingle(light.Id);
                            EditorUI.MainViewport.UpdateGizmoPivot();
                        }
                        if (ImGui.MenuItem("Spot Light"))
                        {
                            var light = scene.CreateSpotLight("Spot Light", 
                                new Vec3(0f, 3f, 0f), // Default position above origin
                                new Vec3(0f, -1f, 0f), // Pointing down
                                new Vec3(1f, 1f, 1f), // White color
                                1.0f, 10.0f, 30.0f);
                            Selection.SetSingle(light.Id);
                            EditorUI.MainViewport.UpdateGizmoPivot();
                        }
                        ImGui.EndMenu();
                    }
                    if (ImGui.BeginMenu("Effects"))
                    {
                        if (ImGui.MenuItem("Global Effects"))
                        {
                            var entity = new Engine.Scene.Entity
                            {
                                Id = scene.GetNextEntityId(),
                                Name = "Global Effects"
                            };
                            scene.Entities.Add(entity);
                            entity.AddComponent<Engine.Components.GlobalEffects>();
                            Selection.SetSingle(entity.Id);
                            EditorUI.MainViewport.UpdateGizmoPivot();
                        }
                        ImGui.EndMenu();
                    }
                    ImGui.EndMenu();
                }
                ImGui.EndPopup();
            }

            // Déverrouiller l'inspecteur si aucun drag n'est actif ET si on était en train de draguer
            if (_isDragging && !ImGui.IsMouseDragging(ImGuiMouseButton.Left) && !ImGui.IsMouseDown(ImGuiMouseButton.Left))
            {
                _isDragging = false;
                _draggedGroup = null;
                _draggedEntityId = 0;
                Editor.Panels.InspectorPanel.AutoUnlockFromDrag();
            }

            ImGui.End();
        }

        private static void DrawEntityNode(Scene scene, Entity e)
        {

            // Affichage spécial pour Separator : trait horizontal
            // Suppression de la logique Separator

            // --- Zone de drop AVANT l’item (pour réordonner) ---
            ImGui.PushID($"drop-before-{e.Id}");
            var cursor2 = ImGui.GetCursorScreenPos();
            ImGui.InvisibleButton($"drop-before-btn##{e.Id}", new Vector2(ImGui.GetWindowWidth(), 4f));
            bool dropBeforeHovered2 = ImGui.IsItemHovered();
            if (dropBeforeHovered2)
            {
                var draw2 = ImGui.GetWindowDrawList();
                draw2.AddRectFilled(cursor2, new Vector2(cursor2.X + ImGui.GetWindowWidth(), cursor2.Y + 4f), 0x4488FFFF);
            }
            if (ImGui.BeginDragDropTarget())
            {
                // On n'accepte que le payload unique
                var payload = ImGui.AcceptDragDropPayload("ENTITY_ID");
                unsafe
                {
                    if (payload.NativePtr != null && payload.DataSize == sizeof(int))
                    {
                        int dropped = *(int*)payload.Data;
                        var ids = (_draggedGroup != null && _draggedGroup.Count > 0)
                                  ? _draggedGroup
                                  : new List<uint> { (uint)dropped };

                        foreach (var id in ids)
                        {
                            var dragged = scene?.GetById(id);
                            if (dragged != null && dragged != e && dragged.Parent == e.Parent)
                            {
                                var siblings = e.Parent == null ? scene!.Entities : e.Parent.Children;
                            siblings.Remove(dragged);
                            int idx = siblings.IndexOf(e);
                            if (idx >= 0)
                                siblings.Insert(idx, dragged);
                        }
                    }
                    _draggedEntityId = 0;
                    _draggedGroup = null;
                    Editor.Panels.InspectorPanel.AutoUnlockFromDrag();
                    _isDragging = false;
                    }
                }
                ImGui.EndDragDropTarget();
            }
            ImGui.PopID();

            bool selected = Selection.Contains(e.Id);

            var flags = ImGuiTreeNodeFlags.OpenOnArrow | ImGuiTreeNodeFlags.SpanFullWidth |
                        ImGuiTreeNodeFlags.FramePadding |
                        (e.Children.Count == 0 ? ImGuiTreeNodeFlags.Leaf : 0) |
                        (selected ? ImGuiTreeNodeFlags.Selected : 0);

            string label = string.IsNullOrEmpty(e.Name) ? $"Entity {e.Id}" : e.Name;
            bool open = ImGui.TreeNodeEx($"{label}##{e.Id}", flags);

            // Icône Light en overlay (facultatif)
            if (e.HasComponent<Engine.Components.LightComponent>())
            {
                var drawList = ImGui.GetWindowDrawList();
                var itemRect = ImGui.GetItemRectMin();
                float iconSize = 16f;
                var iconPos = new System.Numerics.Vector2(itemRect.X + ImGui.GetTreeNodeToLabelSpacing() - iconSize - 4f, itemRect.Y + 2f);
                var tex = Editor.Icons.IconManager.GetIconTexture("light_component", (int)iconSize);
                if (tex != nint.Zero)
                    drawList.AddImage(tex, iconPos, new System.Numerics.Vector2(iconPos.X + iconSize, iconPos.Y + iconSize),
                                      new System.Numerics.Vector2(0, 0), new System.Numerics.Vector2(1, 1), 0xFFFFFFFF);
            }

            // Enregistre le rect écran et l’ordre visible (utile au rectangle et au Shift-range)
            var min = ImGui.GetItemRectMin();
            var max = ImGui.GetItemRectMax();
            _itemRects[e.Id] = new(min, max);
            _visibleOrder.Add(e.Id);

            // ---- SÉLECTION AVEC DETECTION DRAG & DROP ----
            if (ImGui.IsItemHovered() && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
            {
                // Démarrer le processus de sélection différée
                _clickArmed = true;
                _clickCandidateId = e.Id;
                _clickStart = ImGui.GetMousePos();
            }

            // Confirmer la sélection si pas de drag détecté
            if (_clickArmed && _clickCandidateId == e.Id)
            {
                var currentMousePos = ImGui.GetMousePos();
                var dragDelta = new Vector2(currentMousePos.X - _clickStart.X, currentMousePos.Y - _clickStart.Y);
                bool isDragging = dragDelta.Length() >= CLICK_DRAG_THRESHOLD;

                if (!ImGui.IsMouseDown(ImGuiMouseButton.Left))
                {
                    // Souris relâchée sans drag - confirmer la sélection
                    if (!isDragging)
                    {
                        var io = ImGui.GetIO();
                        bool ctrl = io.KeyCtrl;
                        bool shift = io.KeyShift;

                        if (!_isDragging && shift && _lastAnchorId != 0 && _lastAnchorId != e.Id)
                        {
                            _deferredRangeSelect = true;
                            _deferredRangeToId = e.Id;
                        }
                        else if (!_isDragging && ctrl)
                        {
                            Selection.Toggle(e.Id);
                            if (Selection.Contains(e.Id)) _lastAnchorId = e.Id;
                            FinalizeSelection();
                        }
                        else if (!_isDragging)
                        {
                            if (Selection.Selected.Count > 1 && Selection.Contains(e.Id))
                            {
                                Selection.ActiveEntityId = e.Id;
                            }
                            else
                            {
                                Selection.SetSingle(e.Id);
                                _lastAnchorId = e.Id;
                            }
                            FinalizeSelection();
                        }
                    }
                    _clickArmed = false;
                    _clickCandidateId = 0;
                }
                else if (isDragging)
                {
                    // Drag détecté — ne pas modifier la sélection pour éviter que l'inspecteur change de focus.
                    // On se contente d'auto-verrouiller l'inspecteur pour la durée du drag.
                    Editor.Panels.InspectorPanel.AutoLockForDrag();
                    _isDragging = true;
                    _clickArmed = false;
                    _clickCandidateId = 0;
                }
            }


            // Menu contextuel (attaché à l’item courant)
            if (ImGui.BeginPopupContextItem())
            {
                if (ImGui.MenuItem("Select")) { Selection.SetSingle(e.Id); FinalizeSelection(); }
                bool canUnparent = e.Parent != null;
                if (ImGui.MenuItem("Unparent", null, false, canUnparent))
                {
                    var old = e.Parent;
                    e.SetParent(null, keepWorld: true); // Garder keepWorld=true pour unparent
                    UndoRedo.Push(new ReparentAction("Unparent", e.Id, old?.Id, null));
                }
                if (ImGui.MenuItem("Delete"))
                {
                    if (scene != null)
                    {
                        DeleteRecursive(scene, e);
                        Selection.Clear();
                        FinalizeSelection();
                        ImGui.EndPopup();
                        if (open) ImGui.TreePop();
                        return;
                    }
                }
                if (ImGui.MenuItem("Duplicate"))
                {
                    if (scene != null)
                    {
                        var d = scene.CreateCube(e.Name + " (Copy)",
                                                 e.Transform.Position + new Vec3(0.2f, 0, 0.2f),
                                                 e.Transform.Scale, new Vec4(1, 1, 1, 1)); // Default white color
                        d.Transform.Rotation = e.Transform.Rotation;
                        d.SetParent(e.Parent, keepWorld: false);
                        Selection.SetSingle(d.Id);
                        FinalizeSelection();
                    }
                }
                ImGui.EndPopup();
            }

            // Drag & Drop (reparent ou reorder)  
            // Utiliser des flags moins restrictifs pour permettre le drag avec notre logique custom
            if (ImGui.BeginDragDropSource())
            {
                // Auto-lock l'inspecteur dès le début du drag pour éviter le changement de focus
                Editor.Panels.InspectorPanel.AutoLockForDrag();
                _isDragging = true;
                
                // Toujours préparer le groupe basé sur la sélection actuelle
                if (Selection.Selected.Contains(e.Id) && Selection.Selected.Count > 1)
                {
                    _draggedGroup = new List<uint> { e.Id };
                    _draggedGroup.AddRange(Selection.Selected.Where(id => id != e.Id));
                    _draggedEntityId = e.Id;
                }
                else
                {
                    _draggedGroup = new List<uint> { e.Id };
                    _draggedEntityId = e.Id;
                }
                // Payload unique, universel
                unsafe
                {
                    int id = (int)e.Id;
                    ImGui.SetDragDropPayload("ENTITY_ID", new IntPtr(&id), sizeof(int));
                }
                ImGui.Text(_draggedGroup.Count > 1 ? $"Move {_draggedGroup.Count} items" : $"Move {e.Name}");
                ImGui.EndDragDropSource();
            }
            if (ImGui.BeginDragDropTarget())
            {
                // On n'accepte que le payload unique
                var payload2 = ImGui.AcceptDragDropPayload("ENTITY_ID");
                unsafe
                {
                    if (payload2.NativePtr != null && payload2.DataSize == sizeof(int))
                    {
                        int dropped2 = *(int*)payload2.Data;
                        var ids = (_draggedGroup != null && _draggedGroup.Count > 0)
                                  ? _draggedGroup
                                  : new List<uint> { (uint)dropped2 };

                        foreach (var id in ids)
                        {
                            var child = scene?.GetById(id);
                            if (child != null && child != e && !IsAncestorOf(child, e))
                            {
                                var oldParent = child.Parent;
                                child.SetParent(e);
                                UndoRedo.Push(new ReparentAction("Reparent", child.Id, oldParent?.Id, e.Id));
                            }
                        }
                        _draggedGroup = null;
                        Editor.Panels.InspectorPanel.AutoUnlockFromDrag();
                        _isDragging = false;
                    }
                }
                ImGui.EndDragDropTarget();
            }

            if (open)
            {
                foreach (var child in e.Children.ToList())
                {
                    if (scene != null)
                        DrawEntityNode(scene, child);
                }
                ImGui.TreePop();
            }
        }

        private static void DeleteRecursive(Scene scene, Entity e)
        {
            foreach (var c in e.Children.ToArray())
                DeleteRecursive(scene, c);
            if (e.Parent != null) e.Parent.Children.Remove(e);
            scene.Entities.Remove(e);
        }

        private static bool Intersects((Vector2 min, Vector2 max) a, (Vector2 min, Vector2 max) b)
            => !(a.max.X < b.min.X || a.min.X > b.max.X || a.max.Y < b.min.Y || a.min.Y > b.max.Y);

        private static void ApplyRangeSelection(uint fromId, uint toId, bool additive, bool toggle)
        {
            if (_visibleOrder.Count == 0) return;
            int a = _visibleOrder.IndexOf(fromId);
            int b = _visibleOrder.IndexOf(toId);

            if (a == -1 || b == -1) return;

            // 👉 NE PLUS réordonner a et b : garder le sens
            int start = a;
            int end = b;

            // Corriger pour que le GetRange marche même si end < start
            int len = Math.Abs(end - start) + 1;
            int realStart = Math.Min(start, end);

            var range = _visibleOrder.GetRange(realStart, len);

            if (toggle)
            {
                foreach (var id in range) Selection.Toggle(id);
            }
            else if (additive)
            {
                Selection.AddMany(range);
            }
            else
            {
                Selection.ReplaceMany(range);
                Selection.ActiveEntityId = fromId; // garder l’ancre
            }
        }    

        private static void FinalizeSelection()
            => EditorUI.MainViewport.UpdateGizmoPivot();

        // Méthode utilitaire pour vérifier si un entity est un ancêtre d'un autre
        private static bool IsAncestorOf(Entity child, Entity potentialAncestor)
        {
            var parent = child.Parent;
            while (parent != null)
            {
                if (parent == potentialAncestor)
                    return true;
                parent = parent.Parent;
            }
            return false;
        }

        private static void DrawItemHighlight(uint id, uint fillCol, uint borderCol)
        {
            if (!_itemRects.TryGetValue(id, out var r)) return;
            var draw = ImGui.GetForegroundDrawList();
            draw.AddRectFilled(r.min, r.max, fillCol);
            draw.AddRect(r.min, r.max, borderCol);
        }
    }
}
