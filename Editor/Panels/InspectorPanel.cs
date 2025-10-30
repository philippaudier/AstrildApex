using System;
using System.Linq;
using System.Text;
using ImGuiNET;
using OpenTK.Mathematics;
using Editor.State;
using Engine.Scene;
using Engine.Components;
using MeshRendererComponent = Engine.Components.MeshRendererComponent;
using Engine.Mathx;
using Editor.Inspector;
using Editor.Icons;
using Numerics = System.Numerics;

namespace Editor.Panels
{
    public static class InspectorPanel
    {
        // --- State édition transform (composite Undo/Redo) ---
        static bool _trEditing = false;
        static Xform _trBefore;

        // -- Resync flags after Undo/Redo --
        static bool _forceRefreshUI = false;

        // --- Cache Euler pour l'Inspecteur (en degrés) ---
        static Numerics.Vector3 _eulerCacheDeg = new Numerics.Vector3(0, 0, 0);
        static uint _eulerCacheEntity = 0;

        // --- Lock (Unity-like) ---
        static bool _lockInspector = false;   // état du cadenas
        static bool _lockedIsAsset = false;   // sinon entité
        static Guid _lockedAssetGuid = Guid.Empty;
        static uint _lockedEntityId = 0;
        
        // --- Auto-lock pendant drag & drop ---
        static bool _autoLockedForDrag = false;
        static bool _pendingSelectionRefresh = false;

        static InspectorPanel()
        {
            Editor.State.UndoRedo.AfterChange += () =>
            {
                // Casser toute édition en cours et invalider les caches
                _trEditing = false;
                _eulerCacheEntity = 0; // force recalcul du cache euler depuis le quaternion
                _forceRefreshUI = true;

                // ► recaler le gizmo après Undo/Redo ou toute action
                EditorUI.MainViewport.UpdateGizmoPivot();
            };
        }

        /// <summary>Abort current edit and force UI to reload values from the entity.</summary>
        static void AbortAndReload(Engine.Scene.Entity ent)
        {
            _trEditing = false;
            // Invalidate Euler cache so next Draw() recomputes from quaternion
            _eulerCacheEntity = 0;
            _forceRefreshUI = true;
        }

        static void BeginEditIfNeeded(Engine.Scene.Entity ent)
        {
            if (!_trEditing && ImGui.IsItemActivated())
            {
                _trEditing = true;
                _trBefore = new Xform
                {
                    Pos = ent.Transform.Position,
                    Rot = ent.Transform.Rotation,
                    Scl = ent.Transform.Scale
                };
                // Ouvre une transaction composite pour regrouper tout le drag
                UndoRedo.BeginComposite("Transform (Inspector)");
            }
        }

        static void ApplyTransformActionFromInspector(Engine.Scene.Entity ent)
        {
            if (_trEditing && ImGui.IsItemDeactivatedAfterEdit())
            {
                var after = new Xform
                {
                    Pos = ent.Transform.Position,
                    Rot = ent.Transform.Rotation,
                    Scl = ent.Transform.Scale
                };

                if (!after.Pos.Equals(_trBefore.Pos) ||
                    !after.Rot.Equals(_trBefore.Rot) ||
                    !after.Scl.Equals(_trBefore.Scl))
                {
                    // Enregistre l’action dans la composite ouverte
                    UndoRedo.Push(new TransformAction("Transform (Inspector)", ent.Id, _trBefore, after));
                }

                _trEditing = false;
                // >>> Ferme la composite (indispensable)
                UndoRedo.EndComposite();
            }
        }

        public static void Draw()
        {
            // Inspector window with lock button in title bar
            ImGui.Begin("Inspector");
            
            // Draw lock icon in title bar area
            DrawLockInTitleBar();

            if (_forceRefreshUI)
            {
                // Cette frame, on n'essaie pas de poursuivre une édition : on laisse
                // simplement les champs UI relire les valeurs actuelles du modèle.
                // (Le cache Euler ayant été invalidé, la rotation sera correcte.)
                _forceRefreshUI = false;
            }

            // petit refresh une fois le drag terminé pour réaligner la vue si besoin
            if (_pendingSelectionRefresh)
            {
                _pendingSelectionRefresh = false;
                // Forcer un redraw propre : aucun changement d'objet sélectionné ici,
                // juste une réévaluation des sections repliées/états de widgets si tu en as besoin.
            }

            var scene = EditorUI.MainViewport.Renderer?.Scene;
            if (scene == null) { ImGui.TextDisabled("Scene not available."); ImGui.End(); return; }

            // Déterminer la cible à afficher selon lock/selection
            bool showAssetInspector = false;
            Guid assetGuidToShow = Guid.Empty;
            Engine.Scene.Entity? entToShow = null;

            if (_lockInspector || _autoLockedForDrag)
            {
                if (_lockedIsAsset)
                {
                    showAssetInspector = true;
                    assetGuidToShow = _lockedAssetGuid;
                }
                else
                {
                    entToShow = scene.GetById(_lockedEntityId);
                }
            }
            else
            {
                if (Selection.HasAsset)
                {
                    showAssetInspector = true;
                    assetGuidToShow = Selection.ActiveAssetGuid;
                }
                else
                {
                    entToShow = scene.GetById(Selection.ActiveEntityId);
                }
            }

            // ===== Inspector d’asset (Material/Texture/…) =====
            if (showAssetInspector)
            {
                if (assetGuidToShow == Guid.Empty)
                {
                    ImGui.TextDisabled((_lockInspector || _autoLockedForDrag)
                        ? "Locked asset missing."
                        : "No asset selected.");
                    if (_lockInspector)
                    {
                        ImGui.SameLine();
                        if (ImGui.SmallButton("Unlock")) { UnlockInspector(); }
                    }
                    ImGui.End(); return;
                }

                // Récupérer type/nom de l’asset
                string type = Engine.Assets.AssetDatabase.GetTypeName(assetGuidToShow);
                string name = Engine.Assets.AssetDatabase.GetName(assetGuidToShow);

                if (string.IsNullOrEmpty(type))
                {
                    ImGui.TextDisabled(_lockInspector
                        ? "Locked asset not found in database."
                        : "Asset not found.");
                    if (_lockInspector)
                    {
                        ImGui.SameLine();
                        if (ImGui.SmallButton("Unlock")) { UnlockInspector(); }
                    }
                    ImGui.End(); return;
                }

                // En-tête
                ImGui.Text($"Asset: {name}");
                ImGui.SameLine();
                ImGui.TextDisabled($"[{type}]");
                if (_lockInspector)
                {
                    ImGui.SameLine();
                    ImGui.TextColored(new Numerics.Vector4(0.9f, 0.8f, 0.2f, 1f), " (Locked)");
                }

                ImGui.Separator();

                // Inspecteurs spécialisés
                if (string.Equals(type, "Material", StringComparison.OrdinalIgnoreCase))
                {
                    Editor.Inspector.MaterialAssetInspector.Draw(assetGuidToShow);
                }
                else if (string.Equals(type, "SkyboxMaterial", StringComparison.OrdinalIgnoreCase))
                {
                    Editor.Inspector.SkyboxMaterialInspector.Draw(assetGuidToShow);
                }
                else if (string.Equals(type, "TextureHDR", StringComparison.OrdinalIgnoreCase))
                {
                    // HDR textures get their specialized inspector
                    Editor.Inspector.HDRTextureInspector.Draw(assetGuidToShow);
                }
                else if (string.Equals(type, "Texture2D", StringComparison.OrdinalIgnoreCase))
                {
                    // Check if it's an HDR texture by file extension
                    if (Engine.Assets.AssetDatabase.TryGet(assetGuidToShow, out var textureRecord) &&
                        textureRecord.Path.EndsWith(".hdr", StringComparison.OrdinalIgnoreCase))
                    {
                        Editor.Inspector.HDRTextureInspector.Draw(assetGuidToShow);
                    }
                    else
                    {
                        Editor.Inspector.TextureInspector.Draw(assetGuidToShow);
                    }
                }
                else if (string.Equals(type, "FontAsset", StringComparison.OrdinalIgnoreCase))
                {
                    Editor.Inspector.FontAssetInspector.Draw(assetGuidToShow);
                }
                else if (string.Equals(type, "TrueTypeFont", StringComparison.OrdinalIgnoreCase))
                {
                    Editor.Inspector.TrueTypeFontInspector.Draw(assetGuidToShow);
                }
                else if (string.Equals(type, "MeshAsset", StringComparison.OrdinalIgnoreCase) ||
                         type.StartsWith("Model", StringComparison.OrdinalIgnoreCase))
                {
                    Editor.Inspector.MeshAssetInspector.Draw(assetGuidToShow);
                }
                else
                {
                    ImGui.TextDisabled("No custom inspector for this asset type.");
                    ImGui.TextWrapped(Engine.Assets.AssetDatabase.TryGet(assetGuidToShow, out var rec)
                        ? rec.Path
                        : "(unindexed)");
                }

                ImGui.End();
                return; // pas d’inspector d’entité quand un asset est affiché
            }

            // ===== Inspector d’entité =====
            if (entToShow == null)
            {
                ImGui.TextDisabled((_lockInspector || _autoLockedForDrag) ? "Locked entity missing." : "No selection.");
                if (_lockInspector)
                {
                    ImGui.SameLine();
                    if (ImGui.SmallButton("Unlock")) { UnlockInspector(); }
                }
                ImGui.End();
                return;
            }

            // --- En-tête & propriétés de base ---
            FieldWidgets.DrawEntityBasic(entToShow);
            ReflectionInspector.DrawForEntity(entToShow);


            // ================= Unity-like Component Inspector =================
            DrawUnityLikeInspector(entToShow);

            ImGui.End();
        }

        
        private static void DrawLightComponent(LightComponent light)
        {
            ImGui.PushItemWidth(120f);
            
            // Light Type
            var lightTypeNames = new[] { "Directional", "Point", "Spot" };
            var currentType = (int)light.Type;
            if (ImGui.Combo("Type", ref currentType, lightTypeNames, lightTypeNames.Length))
            {
                light.Type = (LightType)currentType;
            }
            
            // Color
            var color = new Numerics.Vector3(light.Color.X, light.Color.Y, light.Color.Z);
            if (ImGui.ColorEdit3("Color", ref color))
            {
                light.Color = new Vector3(color.X, color.Y, color.Z);
            }
            
            // Intensity
            var intensity = light.Intensity;
            if (ImGui.DragFloat("Intensity", ref intensity, 0.01f, 0f, 100f))
            {
                light.Intensity = intensity;
            }
            
            // Range (for Point and Spot lights)
            if (light.Type == LightType.Point || light.Type == LightType.Spot)
            {
                var range = light.Range;
                if (ImGui.DragFloat("Range", ref range, 0.1f, 0.1f, 1000f))
                {
                    light.Range = range;
                }
            }
            
            // Spot Angle (for Spot lights only)
            if (light.Type == LightType.Spot)
            {
                var spotAngle = light.SpotAngle;
                if (ImGui.SliderFloat("Spot Angle", ref spotAngle, 1f, 179f, "%.1f°"))
                {
                    light.SpotAngle = spotAngle;
                }
            }
            
            // Cast Shadows
            var castShadows = light.CastShadows;
            if (ImGui.Checkbox("Cast Shadows", ref castShadows))
            {
                light.CastShadows = castShadows;
            }
            
            ImGui.PopItemWidth();
        }
        
        private static void DrawEnvironmentSettingsComponent(EnvironmentSettings env)
        {
            ImGui.PushItemWidth(120f);
            ImGui.TextDisabled("Tip: Use the Environment panel for full control");
            ImGui.Spacing();
            
            // Time of day - most important setting to show in inspector
            var timeOfDay = env.TimeOfDay;
            if (ImGui.SliderFloat("Time of Day", ref timeOfDay, 0.0f, 24.0f, "%.1f"))
            {
                env.TimeOfDay = timeOfDay;
            }
            
            // Skybox tint
            var tint = new Numerics.Vector3(env.SkyboxTint.X, env.SkyboxTint.Y, env.SkyboxTint.Z);
            if (ImGui.ColorEdit3("Skybox Tint", ref tint))
            {
                env.SkyboxTint = new Vector3(tint.X, tint.Y, tint.Z);
            }
            
            // Ambient intensity
            var ambientIntensity = env.AmbientIntensity;
            if (ImGui.DragFloat("Ambient Intensity", ref ambientIntensity, 0.01f, 0.0f, 5.0f))
            {
                env.AmbientIntensity = ambientIntensity;
            }
            
            ImGui.PopItemWidth();
        }
        
        private static void DrawMeshRendererComponent(Engine.Scene.Entity entity, MeshRendererComponent meshRenderer)
        {
            ImGui.PushItemWidth(120f);
            
            // Mesh dropdown
            var meshNames = new[] { "None", "Cube", "Sphere", "Capsule", "Plane", "Quad" };
            int currentMesh = (int)meshRenderer.Mesh;
            if (ImGui.Combo("Mesh", ref currentMesh, meshNames, meshNames.Length))
            {
                meshRenderer.Mesh = (Engine.Scene.MeshKind)currentMesh;
            }
            
            // Materials section (Unity-like)
            ImGui.Spacing();
            ImGui.Text("Materials");
            ImGui.Indent();
            
            // Material field with drag & drop support
            ImGui.Text("Element 0");
            ImGui.SameLine();
            
            var materialGuid = meshRenderer.GetMaterialGuid();
            string materialName = "None (Material)";
            
            if (materialGuid != Guid.Empty)
            {
                materialName = Engine.Assets.AssetDatabase.GetName(materialGuid);
                if (string.IsNullOrEmpty(materialName))
                    materialName = "Unknown Material";
            }
            
            ImGui.SetNextItemWidth(-1);
            
            // Create a button that looks like a material field
            var buttonColor = materialGuid != Guid.Empty 
                ? new Numerics.Vector4(0.3f, 0.6f, 1.0f, 1.0f)  // Blue for assigned material
                : new Numerics.Vector4(0.4f, 0.4f, 0.4f, 1.0f);  // Gray for none
                
            ImGui.PushStyleColor(ImGuiCol.Button, buttonColor);
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, buttonColor * 1.2f);
            ImGui.PushStyleColor(ImGuiCol.ButtonActive, buttonColor * 0.8f);
            
            bool materialClicked = ImGui.Button($"{materialName}##MaterialField", new Numerics.Vector2(-1, 20));
            
            // Pop style colors immediately after button to avoid imbalance
            ImGui.PopStyleColor(3);
            
            // Handle drag & drop
            if (ImGui.BeginDragDropTarget())
            {
                var payload = ImGui.AcceptDragDropPayload("ASSET_MULTI");
                unsafe
                {
                    if (payload.NativePtr != null && payload.Data != IntPtr.Zero && payload.DataSize >= 16) // At least one GUID (16 bytes)
                    {
                        try
                        {
                            // Extract first GUID from the payload (materials are typically dropped one at a time)
                            var span = new ReadOnlySpan<byte>((void*)payload.Data, 16); // First GUID only
                            var droppedMaterialGuid = new Guid(span);
                            
                            // Check if it's actually a material asset
                            if (Engine.Assets.AssetDatabase.TryGet(droppedMaterialGuid, out var record) && 
                                string.Equals(record.Type, "Material", StringComparison.OrdinalIgnoreCase))
                            {
                                meshRenderer.SetMaterial(droppedMaterialGuid);
                                // Ne pas changer le focus pendant le drag depuis la hiérarchie
                                if (!_autoLockedForDrag)
                                {
                                    _pendingSelectionRefresh = true;
                                }
                            }
                        }
                        catch (System.Exception)
                        {
                            // Error handled silently - avoid console pollution
                        }
                    }
                }
                ImGui.EndDragDropTarget();
            }
            
            // Right-click context menu
            if (ImGui.BeginPopupContextItem("MaterialContextMenu"))
            {
                if (ImGui.MenuItem("Clear Material"))
                {
                    meshRenderer.SetMaterial(Engine.Assets.AssetDatabase.EnsureDefaultWhiteMaterial());
                }
                ImGui.EndPopup();
            }
            
            ImGui.Unindent();
            ImGui.PopItemWidth();
        }

        private static void DrawUnityLikeInspector(Engine.Scene.Entity entity)
        {
            ImGui.Spacing();
            
            // Handle drag & drop over entire inspector area (Unity-like)
            if (ImGui.BeginDragDropTarget())
            {
                // Accept material asset drag & drop
                var payload = ImGui.AcceptDragDropPayload("ASSET_MULTI");
                unsafe
                {
                    if (payload.NativePtr != null && payload.Data != IntPtr.Zero && payload.DataSize >= 16) // At least one GUID (16 bytes)
                    {
                        try
                        {
                            // Try to assign to MeshRenderer if entity has one
                            var meshRenderer = entity.GetComponent<MeshRendererComponent>();
                            if (meshRenderer != null)
                            {
                                // Extract first GUID from the payload
                                var span = new ReadOnlySpan<byte>((void*)payload.Data, 16); // First GUID only
                                var droppedMaterialGuid = new Guid(span);
                                // Check if it's actually a material asset
                                if (Engine.Assets.AssetDatabase.TryGet(droppedMaterialGuid, out var record) && 
                                    string.Equals(record.Type, "Material", StringComparison.OrdinalIgnoreCase))
                                {
                                    meshRenderer.SetMaterial(droppedMaterialGuid);
                                    // Ne pas changer le focus pendant le drag depuis la hiérarchie
                                    if (!_autoLockedForDrag)
                                    {
                                        _pendingSelectionRefresh = true;
                                    }
                                }
                            }
                        }
                        catch (System.Exception)
                        {
                            // Error handled silently - avoid console pollution
                        }
                    }
                }
                ImGui.EndDragDropTarget();
            }

            // Handle drag & drop of script files (.cs) over inspector
            if (ImGui.BeginDragDropTarget())
            {
                var payload = ImGui.AcceptDragDropPayload("SCRIPT_FILE");
                unsafe
                {
                    if (payload.NativePtr != null && payload.Data != IntPtr.Zero && payload.DataSize > 0)
                    {
                        try
                        {
                            // On ne lit pas le path pour l’instant: le ScriptCompiler a un watcher: il va recompiler.
                            // Après recompile, l’utilisateur peut choisir le script dans "Scripts".
                            // Option: tu peux décoder le path et forcer un “attach first type” si tu veux.
                        }
                        catch { }
                    }
                }
                ImGui.EndDragDropTarget();
            }
            
            // Get all components, with Transform always first
            var allComponents = entity.GetAllComponents().ToList();
            var transformComponent = allComponents.OfType<TransformComponent>().FirstOrDefault();
            var otherComponents = allComponents.Where(c => !(c is TransformComponent)).ToList();
            
            // Always draw Transform first (mandatory)
            if (transformComponent != null)
            {
                // Use the new Unity-style Transform display
                Inspector.ReflectionInspector.DrawTransformComponent(entity.Transform);
            }
            
            // Draw other components
            foreach (var component in otherComponents)
            {
                var componentName = component.GetType().Name;
                var iconName = GetComponentIconName(component);
                bool canRemove = !IsComponentMandatory(component);
                
                // Use the new header style with embedded controls
                if (Inspector.ReflectionInspector.DrawComponentHeaderWithControls($"🔧 {componentName}", iconName, component.Enabled, canRemove, component))
                {
                    ImGui.Indent();
                    DrawComponentContent(entity, component);
                    ImGui.Unindent();
                }
            }
            
            // Unity-style Add Component button
            ImGui.Spacing();
            ImGui.Spacing();
            
            float buttonWidth = ImGui.GetContentRegionAvail().X;
            if (ImGui.Button("Add Component", new Numerics.Vector2(buttonWidth, 25)))
            {
                ImGui.OpenPopup("AddComponentPopup");
            }
            
            DrawAddComponentPopup(entity);
            
            // Process component removals
            Inspector.ReflectionInspector.ProcessComponentRemovals();
        }
        
        private static void DrawUnityComponentHeader(string name, string iconName, bool enabled, bool canRemove)
        {
            ImGui.Spacing();
            
            // Component header background (like Unity)
            var drawList = ImGui.GetWindowDrawList();
            var cursorPos = ImGui.GetCursorScreenPos();
            var availWidth = ImGui.GetContentRegionAvail().X;
            var headerColor = ImGui.ColorConvertFloat4ToU32(new Numerics.Vector4(0.24f, 0.24f, 0.24f, 1.0f));
            
            drawList.AddRectFilled(
                cursorPos,
                new Numerics.Vector2(cursorPos.X + availWidth, cursorPos.Y + 20),
                headerColor,
                2.0f
            );
            
            // Icon
            var iconTex = IconManager.GetIconTexture(iconName, 16);
            if (iconTex != IntPtr.Zero)
            {
                drawList.AddImage(iconTex, 
                    new Numerics.Vector2(cursorPos.X + 4, cursorPos.Y + 2),
                    new Numerics.Vector2(cursorPos.X + 20, cursorPos.Y + 18),
                    new Numerics.Vector2(0, 0), new Numerics.Vector2(1, 1), 0xFFFFFFFF);
            }
            
            // Component name
            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + 24);
            ImGui.SetCursorPosY(ImGui.GetCursorPosY() + 2);
            ImGui.TextUnformatted(name);
            
            // Enabled checkbox and settings on the right
            ImGui.SameLine(availWidth - 60);
            ImGui.SetCursorPosY(ImGui.GetCursorPosY() - 2);
            ImGui.Checkbox($"##{name}_enabled", ref enabled);
            
            ImGui.SameLine();
            if (ImGui.SmallButton($"⚙##{name}_settings"))
            {
                ImGui.OpenPopup($"ComponentSettings_{name}");
            }
            
            if (canRemove && ImGui.BeginPopup($"ComponentSettings_{name}"))
            {
                if (ImGui.MenuItem("Remove Component"))
                {
                    // TODO: Remove component
                }
                ImGui.EndPopup();
            }
            
            ImGui.Spacing();
        }
        
        private static void DrawTransformComponent(Engine.Scene.Entity entity, TransformComponent transform)
        {
            ImGui.PushItemWidth(60f);
            
            // Position
            ImGui.Text("Position");
            ImGui.SameLine(80);
            ImGui.Text("X"); ImGui.SameLine();
            var pos = transform.Position;
            var posVec = new Numerics.Vector3(pos.X, pos.Y, pos.Z);
            if (ImGui.DragFloat3("##pos", ref posVec, 0.01f))
            {
                BeginEditIfNeeded(entity);
                transform.Position = new Vector3(posVec.X, posVec.Y, posVec.Z);
                // Update legacy transform for compatibility
                entity.Transform.Position = transform.Position;
                EditorUI.MainViewport.UpdateGizmoPivot();
            }
            BeginEditIfNeeded(entity);
            ApplyTransformActionFromInspector(entity);
            
            // Rotation (Euler)
            ImGui.Text("Rotation");
            ImGui.SameLine(80);
            ImGui.Text("X"); ImGui.SameLine();
            
            // Use cache for stable Euler display
            if (_eulerCacheEntity != entity.Id)
            {
                _eulerCacheDeg = ToEulerXYZStable(transform.Rotation, _eulerCacheDeg);
                _eulerCacheEntity = entity.Id;
            }
            
            var eulerUI = _eulerCacheDeg;
            if (ImGui.DragFloat3("##rot", ref eulerUI, 0.5f, -9999f, 9999f, "%.1f°"))
            {
                BeginEditIfNeeded(entity);
                
                eulerUI.X = Wrap180(eulerUI.X); 
                eulerUI.Y = Wrap180(eulerUI.Y); 
                eulerUI.Z = Wrap180(eulerUI.Z);
                
                float dx = MathHelper.DegreesToRadians(eulerUI.X - _eulerCacheDeg.X);
                float dy = MathHelper.DegreesToRadians(eulerUI.Y - _eulerCacheDeg.Y);
                float dz = MathHelper.DegreesToRadians(eulerUI.Z - _eulerCacheDeg.Z);
                
                var q = transform.Rotation;
                if (MathF.Abs(dx) > 1e-6f) q *= Quaternion.FromAxisAngle(Vector3.UnitX, dx);
                if (MathF.Abs(dy) > 1e-6f) q *= Quaternion.FromAxisAngle(Vector3.UnitY, dy);
                if (MathF.Abs(dz) > 1e-6f) q *= Quaternion.FromAxisAngle(Vector3.UnitZ, dz);
                
                transform.Rotation = Quaternion.Normalize(q);
                entity.Transform.Rotation = transform.Rotation;
                
                _eulerCacheDeg = new Numerics.Vector3(
                    WrapNearest(eulerUI.X, _eulerCacheDeg.X),
                    WrapNearest(eulerUI.Y, _eulerCacheDeg.Y),
                    WrapNearest(eulerUI.Z, _eulerCacheDeg.Z)
                );
                _eulerCacheEntity = entity.Id;
                
                EditorUI.MainViewport.UpdateGizmoPivot();
            }
            BeginEditIfNeeded(entity);
            ApplyTransformActionFromInspector(entity);
            
            // Scale
            ImGui.Text("Scale");
            ImGui.SameLine(80);
            ImGui.Text("X"); ImGui.SameLine();
            var scale = transform.Scale;
            var scaleVec = new Numerics.Vector3(scale.X, scale.Y, scale.Z);
            if (ImGui.DragFloat3("##scale", ref scaleVec, 0.01f, 0.01f, 100f))
            {
                BeginEditIfNeeded(entity);
                transform.Scale = new Vector3(scaleVec.X, scaleVec.Y, scaleVec.Z);
                entity.Transform.Scale = transform.Scale;
                EditorUI.MainViewport.UpdateGizmoPivot();
            }
            BeginEditIfNeeded(entity);
            ApplyTransformActionFromInspector(entity);
            
            ImGui.PopItemWidth();
        }
        
        private static void DrawComponentContent(Engine.Scene.Entity entity, Component component)
        {
            switch (component)
            {
                case LightComponent light:
                    DrawLightComponent(light);
                    break;
                case MeshRendererComponent meshRenderer:
                    DrawMeshRendererComponent(entity, meshRenderer);
                    break;
                case CameraComponent cam:
                    Editor.Inspector.CameraInspector.Draw(cam);
                    break;
                case CharacterController cc:
                    Editor.Inspector.CharacterControllerInspector.Draw(cc);
                    break;
                case BoxCollider bc:
                    Editor.Inspector.BoxColliderInspector.Draw(bc);
                    break;
                case SphereCollider sc:
                    Editor.Inspector.SphereColliderInspector.Draw(sc);
                    break;
                case CapsuleCollider capsule:
                    Editor.Inspector.CapsuleColliderInspector.Draw(capsule);
                    break;
                case HeightfieldCollider heightfield:
                    Editor.Inspector.HeightfieldColliderInspector.Draw(heightfield);
                    break;
                case EnvironmentSettings env:
                    DrawEnvironmentSettingsComponent(env);
                    break;
                case Engine.Components.Terrain tg:
                    // Use new terrain inspector
                    Editor.Inspector.TerrainInspector.Draw(entity, tg);
                    break;
                case Engine.Components.WaterComponent water:
                    // Use water component inspector
                    Editor.Inspector.WaterComponentInspector.Draw(entity, water);
                    break;
                case Engine.Components.GlobalEffects globalEffects:
                    Editor.Inspector.GlobalEffectsInspector.DrawInspector(globalEffects);
                    break;
                case Engine.Components.UI.CanvasComponent canvas:
                    Editor.Inspector.CanvasInspector.Draw(canvas);
                    break;
                case Engine.Components.UI.UITextComponent uiText:
                    Editor.Inspector.UITextInspector.Draw(uiText);
                    break;
                case Engine.Components.UI.UIImageComponent uiImage:
                    Editor.Inspector.UIImageInspector.Draw(uiImage);
                    break;
                case Engine.Components.UI.UIButtonComponent uiButton:
                    Editor.Inspector.UIButtonInspector.Draw(uiButton);
                    break;
                case Engine.Scripting.MonoBehaviour script:
                    Editor.Inspector.ReflectionInspector.DrawMembers(script, script.GetType(), "");
                    break;
                default:
                    ImGui.TextDisabled("No custom inspector for this component type.");
                    break;
            }
        }
        
        private static string GetComponentIconName(Component component)
        {
            return component switch
            {
                LightComponent => "light_component",
                TransformComponent => "transform", 
                MeshRendererComponent => "mesh_renderer",
                _ => "component"
            };
        }
        
        private static bool IsComponentMandatory(Component component)
        {
            return component is TransformComponent;
        }
        
        private static void DrawAddComponentPopup(Engine.Scene.Entity entity)
        {
            if (ImGui.BeginPopup("AddComponentPopup"))
            {
                ImGui.Text("Add Component");
                ImGui.Separator();
                
                // Lighting category
                if (ImGui.BeginMenu("Lighting"))
                {
                    if (ImGui.MenuItem("Light") && !entity.HasComponent<LightComponent>())
                    {
                        entity.AddComponent<LightComponent>();
                        ImGui.CloseCurrentPopup();
                    }
                    ImGui.EndMenu();
                }
                
                // Physics category
                if (ImGui.BeginMenu("Physics"))
                {
                    if (ImGui.MenuItem("Box Collider") && !entity.HasComponent<BoxCollider>())
                    {
                        entity.AddComponent<BoxCollider>();
                        ImGui.CloseCurrentPopup();
                    }
                    if (ImGui.MenuItem("Sphere Collider") && !entity.HasComponent<SphereCollider>())
                    {
                        entity.AddComponent<SphereCollider>();
                        ImGui.CloseCurrentPopup();
                    }
                    if (ImGui.MenuItem("Capsule Collider") && !entity.HasComponent<CapsuleCollider>())
                    {
                        entity.AddComponent<CapsuleCollider>();
                        ImGui.CloseCurrentPopup();
                    }
                    if (ImGui.MenuItem("Character Controller") && !entity.HasComponent<CharacterController>())
                    {
                        entity.AddComponent<CharacterController>();
                        ImGui.CloseCurrentPopup();
                    }
                    if (ImGui.MenuItem("Heightfield Collider") && !entity.HasComponent<HeightfieldCollider>())
                    {
                        entity.AddComponent<HeightfieldCollider>();
                        ImGui.CloseCurrentPopup();
                    }
                    ImGui.EndMenu();
                }
                
                // Rendering category
                if (ImGui.BeginMenu("Rendering"))
                {
                    if (ImGui.MenuItem("Mesh Renderer") && !entity.HasComponent<MeshRendererComponent>())
                    {
                        entity.AddComponent<MeshRendererComponent>();
                        ImGui.CloseCurrentPopup();
                    }
                    ImGui.EndMenu();
                }

                // Camera
                if (ImGui.BeginMenu("Camera"))
                {
                    if (ImGui.MenuItem("Camera") && !entity.HasComponent<CameraComponent>())
                    {
                        entity.AddComponent<CameraComponent>();
                        ImGui.CloseCurrentPopup();
                    }
                    ImGui.EndMenu();
                }
                
                // Environment
                if (ImGui.BeginMenu("Environment"))
                {
                    if (ImGui.MenuItem("Environment Settings") && !entity.HasComponent<EnvironmentSettings>())
                    {
                        entity.AddComponent<EnvironmentSettings>();
                        ImGui.CloseCurrentPopup();
                    }
                    ImGui.EndMenu();
                }

                // Generation category
                if (ImGui.BeginMenu("Generation"))
                {
                    if (ImGui.MenuItem("Terrain") && !entity.HasComponent<Engine.Components.Terrain>())
                    {
                        entity.AddComponent<Engine.Components.Terrain>();
                        ImGui.CloseCurrentPopup();
                    }
                    ImGui.EndMenu();
                }

                // Post-Processing category
                if (ImGui.BeginMenu("Post-Processing"))
                {
                    if (ImGui.MenuItem("Global Effects") && !entity.HasComponent<Engine.Components.GlobalEffects>())
                    {
                        entity.AddComponent<Engine.Components.GlobalEffects>();
                        ImGui.CloseCurrentPopup();
                    }
                    ImGui.EndMenu();
                }

                // UI category
                if (ImGui.BeginMenu("UI"))
                {
                    if (ImGui.MenuItem("Canvas") && !entity.HasComponent<Engine.Components.UI.CanvasComponent>())
                    {
                        entity.AddComponent<Engine.Components.UI.CanvasComponent>();
                        ImGui.CloseCurrentPopup();
                    }
                    if (ImGui.MenuItem("UI Text") && !entity.HasComponent<Engine.Components.UI.UITextComponent>())
                    {
                        entity.AddComponent<Engine.Components.UI.UITextComponent>();
                        ImGui.CloseCurrentPopup();
                    }
                    if (ImGui.MenuItem("UI Image") && !entity.HasComponent<Engine.Components.UI.UIImageComponent>())
                    {
                        entity.AddComponent<Engine.Components.UI.UIImageComponent>();
                        ImGui.CloseCurrentPopup();
                    }
                    if (ImGui.MenuItem("UI Button") && !entity.HasComponent<Engine.Components.UI.UIButtonComponent>())
                    {
                        entity.AddComponent<Engine.Components.UI.UIButtonComponent>();
                        ImGui.CloseCurrentPopup();
                    }
                    ImGui.EndMenu();
                }

                // Scripts (MonoBehaviour)
                if (ImGui.BeginMenu("Scripts"))
                {
                    var host = Editor.Program.ScriptHost; // expose _staticScriptHost via une prop statique
                    if (host != null)
                    {
                        var types = host.AvailableScripts;
                        if (types.Length == 0) ImGui.TextDisabled("No scripts compiled yet.");
                        foreach (var t in types.OrderBy(t => t.Name))
                        {
                            if (ImGui.MenuItem(t.Name))
                            {
                                host.AddScriptToEntity(entity, t);
                                ImGui.CloseCurrentPopup();
                            }
                        }
                    }
                    else ImGui.TextDisabled("Script system not ready.");
                    ImGui.EndMenu();
                }
                
                ImGui.EndPopup();
            }
        }

        // ---------- Lock UI ----------
        
        /// <summary>
        /// Calcule la largeur optimale d'un bouton basée sur son contenu texte
        /// </summary>
        /// <param name="text">Le texte du bouton</param>
        /// <returns>Largeur calculée incluant le padding</returns>
        static float CalculateButtonWidth(string text)
        {
            var textSize = ImGui.CalcTextSize(text);
            return textSize.X + ImGui.GetStyle().FramePadding.X * 2.0f;
        }
        
        static void DrawLockInTitleBar()
        {
            // Draw lock icon in the top-right corner of the window, within content area
            float frameH = ImGui.GetFrameHeight();
            string lockIconKey = _lockInspector ? "lock" : "unlock";
            
            // Position the lock button in top-right corner
            float cursorX = ImGui.GetCursorPosX();
            float availX = ImGui.GetContentRegionAvail().X;
            float iconSize = 20f; // Smaller size for title bar
            
            // Save current cursor position
            var savedCursor = ImGui.GetCursorPos();
            
            // Move to top-right position
            ImGui.SetCursorPosX(cursorX + MathF.Max(0, availX - iconSize - 4));
            ImGui.SetCursorPosY(ImGui.GetCursorPosY() - 2); // Slightly up to align better
            
            // Use IconManager button with proper tooltip
            if (IconManager.IconButton(lockIconKey, _lockInspector ? "Unlock Inspector" : "Lock Inspector", (int)iconSize))
            {
                ToggleLockWithCurrentSelection();
            }
            
            // Restore cursor position for rest of content
            ImGui.SetCursorPos(savedCursor);
            ImGui.Spacing();
        }

        static void ToggleLockWithCurrentSelection()
        {
            if (_lockInspector)
            {
                UnlockInspector();
                return;
            }

            // Lock sur la cible affichée/actuelle
            if (Selection.HasAsset)
            {
                _lockedIsAsset = true;
                _lockedAssetGuid = Selection.ActiveAssetGuid;
                _lockedEntityId = 0;
            }
            else
            {
                _lockedIsAsset = false;
                _lockedAssetGuid = Guid.Empty;
                _lockedEntityId = Selection.ActiveEntityId;
            }
            _lockInspector = true;
        }

        static void UnlockInspector()
        {
            _lockInspector = false;
            _lockedIsAsset = false;
            _lockedAssetGuid = Guid.Empty;
            _lockedEntityId = 0;
        }
        
        /// <summary>
        /// Auto-verrouille l'inspecteur pendant les opérations de drag & drop
        /// </summary>
        public static void AutoLockForDrag()
        {
            if (!_lockInspector && !_autoLockedForDrag)
            {
                _autoLockedForDrag = true;
                // Sauvegarder l'état actuel
                if (Selection.HasAsset)
                {
                    _lockedIsAsset = true;
                    _lockedAssetGuid = Selection.ActiveAssetGuid;
                    _lockedEntityId = 0;
                }
                else
                {
                    _lockedIsAsset = false;
                    _lockedAssetGuid = Guid.Empty;
                    _lockedEntityId = Selection.ActiveEntityId;
                }
                // Mémoriser aussi l'active entity pour pouvoir la restaurer après drop
                _preDragActiveEntityId = _lockedEntityId;
            }
        }
        
        /// <summary>
        /// Déverrouille l'inspecteur après la fin du drag & drop
        /// </summary>
        public static void AutoUnlockFromDrag()
        {
            if (_autoLockedForDrag)
            {
                _autoLockedForDrag = false;
                _pendingSelectionRefresh = true;
                // Restaurer la sélection active si elle a été modifiée pendant le drag
                if (_preDragActiveEntityId != 0 && Selection.ActiveEntityId != _preDragActiveEntityId)
                {
                    Selection.ActiveEntityId = _preDragActiveEntityId;
                }
                // Ne pas toucher au lock manuel
            }
        }

        // Sélection active à l'entrée du drag (pour restauration post-drop)
        static uint _preDragActiveEntityId = 0;

        /// <summary>
        /// Vérifie si l'inspecteur est automatiquement verrouillé pour un drag & drop
        /// </summary>
        public static bool IsAutoLockedForDrag()
        {
            return _autoLockedForDrag;
        }

        private static float Wrap180(float d)
        {
            while (d > 180f) d -= 360f;
            while (d < -180f) d += 360f;
            return d;
        }

        // Choisit l'équivalent (±360) le plus proche d'une référence
        static float WrapNearest(float angle, float reference)
        {
            angle = Wrap180(angle);
            reference = Wrap180(reference);
            float delta = angle - reference;
            if (delta > 180f) angle -= 360f;
            if (delta < -180f) angle += 360f;
            return angle;
        }

        // Recalcule des Euler XYZ (en degrés) stables, proches d'un affichage précédent
        static Numerics.Vector3 ToEulerXYZStable(Quaternion q, Numerics.Vector3 prevDeg)
        {
            // part de ta fonction utilitaire existante
            var eRad = QuatUtil.ToEulerXYZ(q); // radians
            var deg = new Numerics.Vector3(
                MathHelper.RadiansToDegrees(eRad.X),
                MathHelper.RadiansToDegrees(eRad.Y),
                MathHelper.RadiansToDegrees(eRad.Z)
            );

            deg.X = WrapNearest(deg.X, prevDeg.X);
            deg.Y = WrapNearest(deg.Y, prevDeg.Y);
            deg.Z = WrapNearest(deg.Z, prevDeg.Z);

            // Gimbal guard : si |Y| ~ 90°, yaw/roll deviennent couplés -> garde Z proche
            if (MathF.Abs(deg.Y) > 89.9f)
                deg.Z = prevDeg.Z;

            // Enfin, on garde l’affichage dans [-180,180]
            deg.X = Wrap180(deg.X);
            deg.Y = Wrap180(deg.Y);
            deg.Z = Wrap180(deg.Z);
            return deg;
        }

        public static void InvalidateEulerCache()
        {
            _eulerCacheEntity = 0; // forcera le recalcul au prochain Draw()
        }

        /// <summary>Force UI refresh to reload values from entities (used after gizmo transforms).</summary>
        public static void ForceUIRefresh()
        {
            _trEditing = false;
            _eulerCacheEntity = 0; // force recalcul du cache euler depuis le quaternion
            _forceRefreshUI = true;
            Inspector.ReflectionInspector.ForceRefresh();
        }
    }
}
