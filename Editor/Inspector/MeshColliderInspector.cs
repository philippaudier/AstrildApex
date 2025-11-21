using System;
using System.Numerics;
using ImGuiNET;
using Engine.Components;
using Engine.Assets;

namespace Editor.Inspector
{
    public static class MeshColliderInspector
    {
        public static void Draw(MeshCollider collider)
        {
            if (collider == null) return;

            ImGui.PushID($"MeshCollider_{collider.GetHashCode()}");

            // IsTrigger
            bool isTrigger = collider.IsTrigger;
            if (ImGui.Checkbox("Is Trigger", ref isTrigger))
            {
                collider.IsTrigger = isTrigger;
            }

            // Layer
            int layer = collider.Layer;
            if (ImGui.InputInt("Layer", ref layer))
            {
                collider.Layer = System.Math.Clamp(layer, 0, 31);
            }

            // Center
            var center = new Vector3(collider.Center.X, collider.Center.Y, collider.Center.Z);
            if (ImGui.DragFloat3("Center", ref center, 0.1f))
            {
                collider.Center = new OpenTK.Mathematics.Vector3(center.X, center.Y, center.Z);
            }

            ImGui.Separator();

            // Use MeshRenderer Mesh
            bool useMeshRendererMesh = collider.UseMeshRendererMesh;
            if (ImGui.Checkbox("Use MeshRenderer Mesh", ref useMeshRendererMesh))
            {
                collider.UseMeshRendererMesh = useMeshRendererMesh;
                if (useMeshRendererMesh)
                {
                    // Clear custom mesh guid when using MeshRenderer's mesh
                    collider.MeshGuid = null;
                }
                collider.RefreshMesh();
            }

            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("Automatically use the mesh from the MeshRenderer component");
            }

            // Si on n'utilise pas le mesh du MeshRenderer, permettre la sélection d'un mesh custom
            if (!useMeshRendererMesh)
            {
                ImGui.Text("Custom Mesh:");
                ImGui.SameLine();

                Guid? currentGuid = collider.MeshGuid;
                string currentName = "None";

                if (currentGuid.HasValue && currentGuid.Value != Guid.Empty)
                {
                    if (AssetDatabase.TryGet(currentGuid.Value, out var rec))
                    {
                        currentName = rec.Name ?? "Unnamed";
                    }
                }

                if (ImGui.BeginCombo("##MeshAsset", currentName))
                {
                    // Option "None"
                    if (ImGui.Selectable("None", !currentGuid.HasValue || currentGuid.Value == Guid.Empty))
                    {
                        collider.MeshGuid = null;
                        collider.RefreshMesh();
                    }

                    // Liste des mesh assets disponibles
                    foreach (var asset in AssetDatabase.All())
                    {
                        if (asset.Type != "MeshAsset") continue;

                        bool isSelected = currentGuid.HasValue && currentGuid.Value == asset.Guid;
                        if (ImGui.Selectable(asset.Name ?? "Unnamed", isSelected))
                        {
                            collider.MeshGuid = asset.Guid;
                            collider.RefreshMesh();
                        }
                    }

                    ImGui.EndCombo();
                }
            }
            else
            {
                // Afficher le mesh utilisé depuis le MeshRenderer
                ImGui.BeginDisabled();
                string meshInfo = "Using MeshRenderer mesh";
                
                if (collider.Entity != null)
                {
                    var meshRenderer = collider.Entity.GetComponent<MeshRendererComponent>();
                    if (meshRenderer?.CustomMeshGuid.HasValue == true)
                    {
                        if (AssetDatabase.TryGet(meshRenderer.CustomMeshGuid.Value, out var rec))
                        {
                            meshInfo = $"Using: {rec.Name ?? "Unnamed"}";
                        }
                    }
                }

                ImGui.InputText("##AutoMesh", ref meshInfo, 100);
                ImGui.EndDisabled();
            }

            ImGui.Separator();

            // Convex (pour futur usage)
            bool convex = collider.Convex;
            if (ImGui.Checkbox("Convex (Future)", ref convex))
            {
                collider.Convex = convex;
            }

            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("Convex collision (simplified, faster). Currently not implemented.");
            }

            ImGui.Separator();

            // Bouton pour forcer le refresh
            if (ImGui.Button("Refresh Mesh"))
            {
                collider.RefreshMesh();
            }

            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("Force reload the mesh collision data");
            }

            // Info sur le mesh
            ImGui.Spacing();
            ImGui.TextDisabled("Collision Mesh Info:");
            
            int triangleCount = collider.CachedTriangleCount;
            bool isDirty = collider.IsTriangleCacheDirty;
            
            if (!isDirty && triangleCount > 0)
            {
                ImGui.TextColored(new System.Numerics.Vector4(0.4f, 0.8f, 0.4f, 1.0f), 
                    $"✓ {triangleCount:N0} triangles cached");
                ImGui.TextDisabled("Collision will follow mesh geometry precisely.");
            }
            else if (!isDirty && triangleCount == 0)
            {
                ImGui.TextColored(new System.Numerics.Vector4(0.8f, 0.4f, 0.4f, 1.0f), 
                    "⚠ 0 triangles - No collision!");
                ImGui.TextDisabled("The mesh is not loaded or has no geometry.");
                ImGui.TextDisabled("Check the Console for error messages.");
            }
            else
            {
                ImGui.TextColored(new System.Numerics.Vector4(0.8f, 0.8f, 0.4f, 1.0f), 
                    "⏳ Triangles not cached yet");
                ImGui.TextDisabled("Click 'Refresh Mesh' to force reload.");
            }
            
            ImGui.Text($"Bounds: {collider.WorldAABB.Extents.X:F2} x {collider.WorldAABB.Extents.Y:F2} x {collider.WorldAABB.Extents.Z:F2}");
            
            // Vérifier si le mesh source existe
            if (collider.UseMeshRendererMesh && collider.Entity != null)
            {
                var meshRenderer = collider.Entity.GetComponent<MeshRendererComponent>();
                if (meshRenderer != null)
                {
                    if (meshRenderer.CustomMeshGuid.HasValue)
                    {
                        ImGui.TextColored(new System.Numerics.Vector4(0.6f, 0.8f, 1.0f, 1.0f), 
                            $"Source: {AssetDatabase.GetName(meshRenderer.CustomMeshGuid.Value)}");
                    }
                    else
                    {
                        ImGui.TextColored(new System.Numerics.Vector4(0.8f, 0.4f, 0.4f, 1.0f), 
                            "⚠ MeshRenderer has no custom mesh!");
                        ImGui.TextDisabled("Select a mesh in the MeshRenderer component first.");
                    }
                }
                else
                {
                    ImGui.TextColored(new System.Numerics.Vector4(0.8f, 0.4f, 0.4f, 1.0f), 
                        "⚠ No MeshRenderer component found!");
                }
            }
            else if (collider.MeshGuid.HasValue && collider.MeshGuid.Value != Guid.Empty)
            {
                if (AssetDatabase.TryGet(collider.MeshGuid.Value, out var rec))
                {
                    ImGui.TextColored(new System.Numerics.Vector4(0.6f, 0.8f, 1.0f, 1.0f), 
                        $"Source: {rec.Name}");
                }
            }

            ImGui.PopID();
        }
    }
}
