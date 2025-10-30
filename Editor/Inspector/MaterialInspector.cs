using System;
using ImGuiNET;
using Engine.Assets;
using Editor.State;
using Engine.Scene;

namespace Editor.Inspector
{
    public static class MaterialInspector
    {
        static void EnsureUniqueMaterialForEntity(Scene scene, Entity e)
        {
            if (!e.MaterialGuid.HasValue || e.MaterialGuid.Value == Guid.Empty) return;

            var g = e.MaterialGuid.Value;

            // Si c'est le material "Default White" OU s'il est partagé par plusieurs entités -> dupliquer
            if (AssetDatabase.TryGetMaterialName(g, out var name) && string.Equals(name, "Default White", StringComparison.OrdinalIgnoreCase)
                || scene.CountEntitiesUsingMaterial(g) > 1)
            {
                var newGuid = AssetDatabase.CloneMaterial(g, e.Name + " Mat");
                e.MaterialGuid = newGuid;
            }
        }

        public unsafe static void DrawForActiveEntity(Scene scene)
        {
            if (Selection.ActiveEntityId == 0) return;
            var e = scene.GetById(Selection.ActiveEntityId);
            if (e == null) return;

            ImGui.Separator();
            ImGui.Text("Material");
            ImGui.BeginGroup();

            if (e.MaterialGuid == null || e.MaterialGuid == Guid.Empty)
            {
                // Zone de drop pour assigner un material quand il n'y en a pas
                //ImGui.Button("Drag Material Here");
                if (ImGui.IsItemHovered() && ImGui.GetDragDropPayload().NativePtr != null)
                {
                    ImGui.SetTooltip("Drop Material here to assign");
                }
                if (ImGui.BeginDragDropTarget())
                {
                    if (Editor.Panels.AssetsPanel.TryConsumeDraggedAsset(out var dropped))
                    {
                        if (AssetDatabase.GetTypeName(dropped) == "Material")
                        {
                            e.MaterialGuid = dropped;
                        }
                    }
                    ImGui.EndDragDropTarget();
                }

                ImGui.SameLine();
                if (ImGui.Button("Create & Assign Material"))
                {
                    var rec = AssetDatabase.CreateMaterial(e.Name ?? "Material");
                    e.MaterialGuid = rec.Guid;
                }
                ImGui.EndGroup();
                return;
            }

            var mat = AssetDatabase.LoadMaterial(e.MaterialGuid.Value);
            if (mat == null)
            {
                ImGui.TextColored(new System.Numerics.Vector4(1, 0.5f, 0.5f, 1), "Material introuvable ou supprimé.");
                if (ImGui.Button("Réassigner un nouveau Material"))
                {
                    var rec = AssetDatabase.CreateMaterial(e.Name ?? "Material");
                    e.MaterialGuid = rec.Guid;
                }
                ImGui.EndGroup();
                return;
            }

            // Affichage du material actuel avec possibilité de drop pour le remplacer
            ImGui.Text($"Current: {mat.Name}");
            ImGui.Button("Drop Material to Replace");
            if (ImGui.IsItemHovered() && ImGui.GetDragDropPayload().NativePtr != null)
            {
                ImGui.SetTooltip("Drop Material here to replace current");
            }
            if (ImGui.BeginDragDropTarget())
            {
                if (Editor.Panels.AssetsPanel.TryConsumeDraggedAsset(out var dropped))
                {
                    if (AssetDatabase.GetTypeName(dropped) == "Material")
                    {
                        // Option 1: Remplacer directement
                        e.MaterialGuid = dropped;

                        // Option 2: Demander confirmation (optionnel)
                        // var oldName = mat.Name;
                        // var newMat = AssetDatabase.LoadMaterial(dropped);
                    }
                }
                ImGui.EndDragDropTarget();
            }

            // Shader selection
            ImGui.Separator();
            ImGui.Text("🎨 Shader");
            var availableShaders = new[] { "ForwardBase", "TerrainForward", "Unlit", "Water" };
            var currentShader = mat.Shader ?? "ForwardBase";
            var currentIndex = Array.IndexOf(availableShaders, currentShader);
            if (currentIndex == -1) currentIndex = 0; // Default to ForwardBase if unknown shader

            if (ImGui.Combo("Shader", ref currentIndex, availableShaders, availableShaders.Length))
            {
                EnsureUniqueMaterialForEntity(scene, e);
                mat = AssetDatabase.LoadMaterial(e.MaterialGuid.Value);
                mat.Shader = availableShaders[currentIndex];
                AssetDatabase.SaveMaterial(mat);
            }
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Select the rendering shader for this material");

            // Reste du code existant (Albedo Texture, couleurs, etc.)
            ImGui.Text("Albedo Texture:"); ImGui.SameLine();
            var texName = mat.AlbedoTexture.HasValue ? AssetDatabase.GetName(mat.AlbedoTexture.Value) : "<none>";
            ImGui.Button(texName);
            if (ImGui.IsItemHovered() && ImGui.GetDragDropPayload().NativePtr != null)
            {
                ImGui.SetTooltip("Drop Texture here for Albedo");
            }
            if (ImGui.BeginDragDropTarget())
            {
                if (Editor.Panels.AssetsPanel.TryConsumeDraggedAsset(out var dropped))
                {
                    if (AssetDatabase.GetTypeName(dropped) == "Texture2D")
                    {
                        EnsureUniqueMaterialForEntity(scene, e);
                        mat = AssetDatabase.LoadMaterial(e.MaterialGuid.Value);
                        mat.AlbedoTexture = dropped;
                        AssetDatabase.SaveMaterial(mat);
                    }
                }
                ImGui.EndDragDropTarget();
            }

             // Bouton pour supprimer la normal map
            if (mat.AlbedoTexture.HasValue && mat.AlbedoTexture.Value != Guid.Empty)
            {
                ImGui.SameLine();
                if (ImGui.Button("X##ClearAlbedo"))
                {
                    EnsureUniqueMaterialForEntity(scene, e);
                    mat = AssetDatabase.LoadMaterial(e.MaterialGuid.Value);
                    mat.AlbedoTexture = null;
                    AssetDatabase.SaveMaterial(mat);
                }
                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip("Remove albedo map");
                }
            }

            // Reste du code pour les propriétés du material...
            var col = new System.Numerics.Vector4(mat.AlbedoColor[0], mat.AlbedoColor[1], mat.AlbedoColor[2], mat.AlbedoColor[3]);
            if (ImGui.ColorEdit4("Albedo Color", ref col))
            {
                EnsureUniqueMaterialForEntity(scene, e);
                mat = AssetDatabase.LoadMaterial(e.MaterialGuid.Value);
                mat.AlbedoColor = new[] { col.X, col.Y, col.Z, col.W };
                AssetDatabase.SaveMaterial(mat);
            }

            // Normal Texture
            ImGui.Text("Normal Texture:"); ImGui.SameLine();
            var normalTexName = mat.NormalTexture.HasValue && mat.NormalTexture.Value != Guid.Empty 
                ? AssetDatabase.GetName(mat.NormalTexture.Value) : "<none>";

            // Bouton avec feedback visuel
            ImGui.Button(normalTexName);
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("Drop Normal Map texture here");
            }
            if (ImGui.BeginDragDropTarget())
            {
                if (Editor.Panels.AssetsPanel.TryConsumeDraggedAsset(out var dropped))
                {
                    if (AssetDatabase.GetTypeName(dropped) == "Texture2D")
                    {
                        EnsureUniqueMaterialForEntity(scene, e);
                        mat = AssetDatabase.LoadMaterial(e.MaterialGuid.Value);
                        mat.NormalTexture = dropped;
                        AssetDatabase.SaveMaterial(mat);
                    }
                }
                ImGui.EndDragDropTarget();
            }

            // Bouton pour supprimer la normal map
            if (mat.NormalTexture.HasValue && mat.NormalTexture.Value != Guid.Empty)
            {
                ImGui.SameLine();
                if (ImGui.Button("X##ClearNormal"))
                {
                    EnsureUniqueMaterialForEntity(scene, e);
                    mat = AssetDatabase.LoadMaterial(e.MaterialGuid.Value);
                    mat.NormalTexture = null;
                    AssetDatabase.SaveMaterial(mat);
                }
                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip("Remove normal map");
                }
            }

            // Slider Normal Strength (seulement si une normal map est assignée)
            if (mat.NormalTexture.HasValue && mat.NormalTexture.Value != Guid.Empty)
            {
                float strength = mat.NormalStrength;
                if (ImGui.SliderFloat("Normal Strength", ref strength, 0.0f, 2.0f, "%.2f"))
                {
                    EnsureUniqueMaterialForEntity(scene, e);
                    mat = AssetDatabase.LoadMaterial(e.MaterialGuid.Value);
                    mat.NormalStrength = strength;
                    AssetDatabase.SaveMaterial(mat);
                }
                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip("Controls the intensity of the normal map effect");
                }
            }

            float m = mat.Metallic, r = mat.Roughness;
            if (ImGui.SliderFloat("Metallic", ref m, 0, 1))
            {
                EnsureUniqueMaterialForEntity(scene, e);
                mat = AssetDatabase.LoadMaterial(e.MaterialGuid.Value);
                mat.Metallic = m;
                AssetDatabase.SaveMaterial(mat);
            }
            if (ImGui.SliderFloat("Roughness", ref r, 0, 1))
            {
                EnsureUniqueMaterialForEntity(scene, e);
                mat = AssetDatabase.LoadMaterial(e.MaterialGuid.Value);
                mat.Roughness = r;
                AssetDatabase.SaveMaterial(mat);
            }

            ImGui.EndGroup();
        }
    }
}
