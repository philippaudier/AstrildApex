using ImGuiNET;
using Engine.Components;
using Engine.Assets;
using Editor.Inspector;
using System;
using System.Linq;

namespace Editor.Inspector
{
    /// <summary>
    /// Unity-style inspector for MeshRendererComponent with comprehensive UI
    /// </summary>
    public static class MeshRendererInspector
    {
        public static void Draw(MeshRendererComponent meshRenderer)
        {
            if (meshRenderer?.Entity == null) return;
            uint entityId = meshRenderer.Entity.Id;

            // === MESH SECTION ===
            if (InspectorWidgets.Section("Mesh", defaultOpen: true,
                tooltip: "Mesh geometry to render"))
            {
                // Check if using custom mesh
                bool usingCustomMesh = meshRenderer.IsUsingCustomMesh();

                if (usingCustomMesh)
                {
                    // Display custom mesh info
                    if (meshRenderer.CustomMeshGuid.HasValue)
                    {
                        var meshName = AssetDatabase.GetName(meshRenderer.CustomMeshGuid.Value);
                        ImGui.Text($"Custom Mesh: {meshName}");
                        ImGui.SameLine();

                        if (ImGui.SmallButton("Clear##CustomMesh"))
                        {
                            meshRenderer.ClearCustomMesh();
                        }
                        if (ImGui.IsItemHovered())
                        {
                            ImGui.SetTooltip("Click to remove custom mesh and use primitive");
                        }
                    }
                }
                else
                {
                    // Display primitive mesh selector
                    var mesh = meshRenderer.Mesh;
                    InspectorWidgets.EnumField("Primitive", ref mesh, entityId, "Mesh",
                        tooltip: "Built-in primitive mesh shape");
                    meshRenderer.Mesh = mesh;
                }

                // Mesh Asset Picker
                ImGui.Spacing();
                ImGui.Separator();
                ImGui.Spacing();

                ImGui.Text("Custom Mesh Asset:");

                // Get all mesh assets from AssetDatabase
                var meshAssets = AssetDatabase.All()
                    .Where(r => AssetDatabase.IsMeshAsset(r.Guid))
                    .ToList();

                if (meshAssets.Count == 0)
                {
                    ImGui.TextDisabled("  (No mesh assets imported)");
                    ImGui.TextDisabled("  Use File -> Import 3D Model... to import meshes");
                }
                else
                {
                    // Display mesh asset picker as a combo box
                    var currentMeshGuid = meshRenderer.CustomMeshGuid ?? Guid.Empty;
                    var currentMeshName = currentMeshGuid != Guid.Empty
                        ? AssetDatabase.GetName(currentMeshGuid)
                        : "<None>";

                    if (ImGui.BeginCombo("##MeshAsset", currentMeshName))
                    {
                        // Option to clear custom mesh
                        if (ImGui.Selectable("<None>", currentMeshGuid == Guid.Empty))
                        {
                            meshRenderer.ClearCustomMesh();
                        }

                        // List all available mesh assets
                        foreach (var asset in meshAssets)
                        {
                            bool isSelected = asset.Guid == currentMeshGuid;
                            if (ImGui.Selectable($"{asset.Name} ({asset.Type})", isSelected))
                            {
                                meshRenderer.SetCustomMesh(asset.Guid);
                            }

                            if (isSelected)
                            {
                                ImGui.SetItemDefaultFocus();
                            }
                        }

                        ImGui.EndCombo();
                    }
                    if (ImGui.IsItemHovered())
                    {
                        ImGui.SetTooltip("Select a custom imported 3D mesh");
                    }
                }

                InspectorWidgets.EndSection();
            }

            // === MATERIALS SECTION ===
            if (InspectorWidgets.Section("Materials", defaultOpen: true,
                tooltip: "Material(s) to apply to the mesh"))
            {
                // Material Asset Picker
                var currentMaterialGuid = meshRenderer.MaterialGuid ?? Guid.Empty;
                var currentMaterialName = currentMaterialGuid != Guid.Empty
                    ? AssetDatabase.GetName(currentMaterialGuid)
                    : "<Default White>";

                ImGui.Text("Material:");
                ImGui.SameLine();

                var materials = AssetDatabase.All()
                    .Where(r => r.Type == "Material")
                    .ToList();

                if (ImGui.BeginCombo("##MaterialAsset", currentMaterialName))
                {
                    // Option to use default material
                    if (ImGui.Selectable("<Default White>", currentMaterialGuid == Guid.Empty))
                    {
                        meshRenderer.SetMaterial(AssetDatabase.EnsureDefaultWhiteMaterial());
                    }

                    // List all available materials
                    foreach (var mat in materials)
                    {
                        bool isSelected = mat.Guid == currentMaterialGuid;
                        if (ImGui.Selectable(mat.Name, isSelected))
                        {
                            meshRenderer.SetMaterial(mat.Guid);
                        }

                        if (isSelected)
                        {
                            ImGui.SetItemDefaultFocus();
                        }
                    }

                    ImGui.EndCombo();
                }
                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip("Select a material to apply visual properties");
                }

                // Quick Material Actions
                ImGui.Spacing();
                if (currentMaterialGuid != Guid.Empty)
                {
                    if (ImGui.Button("Edit Material", new System.Numerics.Vector2(-1, 0)))
                    {
                        // TODO: Select the material in the AssetsPanel
                        Engine.Utils.DebugLogger.Log($"[MeshRendererInspector] Edit material: {currentMaterialGuid}");
                    }
                    if (ImGui.IsItemHovered())
                    {
                        ImGui.SetTooltip("Open material in inspector");
                    }
                }

                ImGui.Spacing();
                ImGui.Separator();
                ImGui.Spacing();

                // Multiple materials support (not yet implemented)
                ImGui.TextDisabled("Multiple Materials:");
                ImGui.Indent();
                ImGui.TextDisabled("Feature not implemented yet");
                ImGui.TextWrapped("When a mesh has multiple submeshes with different materials, you'll be able to assign different materials to each submesh here.");
                ImGui.Unindent();

                InspectorWidgets.EndSection();
            }

            // === LIGHTING SECTION ===
            if (InspectorWidgets.Section("Lighting", defaultOpen: false,
                tooltip: "Configure how this mesh receives and casts light"))
            {
                ImGui.TextDisabled("Cast Shadows:");
                ImGui.Indent();
                ImGui.TextDisabled("Feature not implemented yet");
                ImGui.Unindent();

                ImGui.Spacing();

                ImGui.TextDisabled("Receive Shadows:");
                ImGui.Indent();
                ImGui.TextDisabled("Feature not implemented yet");
                ImGui.Unindent();

                ImGui.Spacing();

                ImGui.TextDisabled("Light Probes:");
                ImGui.Indent();
                ImGui.TextDisabled("Feature not implemented yet");
                ImGui.Unindent();

                ImGui.Spacing();

                ImGui.TextDisabled("Reflection Probes:");
                ImGui.Indent();
                ImGui.TextDisabled("Feature not implemented yet");
                ImGui.Unindent();

                InspectorWidgets.EndSection();
            }

            // === PROBES SECTION ===
            if (InspectorWidgets.Section("Probes", defaultOpen: false,
                tooltip: "Light and reflection probe settings"))
            {
                ImGui.TextDisabled("Anchor Override:");
                ImGui.Indent();
                ImGui.TextDisabled("Feature not implemented yet");
                ImGui.TextWrapped("Override the transform that specifies the interpolation position for light and reflection probes.");
                ImGui.Unindent();

                InspectorWidgets.EndSection();
            }

            // === ADDITIONAL SETTINGS SECTION ===
            if (InspectorWidgets.Section("Additional Settings", defaultOpen: false,
                tooltip: "Advanced rendering settings"))
            {
                ImGui.TextDisabled("Motion Vectors:");
                ImGui.Indent();
                ImGui.TextDisabled("Feature not implemented yet");
                ImGui.Unindent();

                ImGui.Spacing();

                ImGui.TextDisabled("Dynamic Occlusion:");
                ImGui.Indent();
                ImGui.TextDisabled("Feature not implemented yet");
                ImGui.Unindent();

                InspectorWidgets.EndSection();
            }

            // === MESH INFO SECTION ===
            if (meshRenderer.IsUsingCustomMesh() && meshRenderer.CustomMeshGuid.HasValue)
            {
                if (InspectorWidgets.Section("Mesh Info", defaultOpen: true,
                    tooltip: "Information about the loaded mesh"))
                {
                    var meshAsset = AssetDatabase.LoadMeshAsset(meshRenderer.CustomMeshGuid.Value);
                    if (meshAsset != null)
                    {
                        // Statistics
                        ImGui.TextColored(new System.Numerics.Vector4(0.5f, 0.8f, 1.0f, 1.0f), "Statistics:");
                        ImGui.Indent();
                        ImGui.Text($"Vertices: {meshAsset.TotalVertexCount:N0}");
                        ImGui.Text($"Triangles: {meshAsset.TotalTriangleCount:N0}");
                        ImGui.Text($"Submeshes: {meshAsset.SubMeshes.Count}");
                        ImGui.Unindent();

                        if (meshAsset.SubMeshes.Count > 1)
                        {
                            ImGui.Spacing();
                            InspectorWidgets.WarningBox($"This mesh has {meshAsset.SubMeshes.Count} submeshes. Currently only the first submesh is rendered.");
                        }

                        // Bounding Box
                        ImGui.Spacing();
                        ImGui.Separator();
                        ImGui.Spacing();

                        ImGui.TextColored(new System.Numerics.Vector4(0.5f, 0.8f, 1.0f, 1.0f), "Bounding Box:");
                        ImGui.Indent();
                        var bounds = meshAsset.Bounds;
                        ImGui.Text($"Center: ({bounds.Center.X:F2}, {bounds.Center.Y:F2}, {bounds.Center.Z:F2})");
                        ImGui.Text($"Size:   ({bounds.Size.X:F2}, {bounds.Size.Y:F2}, {bounds.Size.Z:F2})");
                        ImGui.Unindent();

                        // Materials from model
                        if (meshAsset.MaterialGuids.Count > 0)
                        {
                            ImGui.Spacing();
                            ImGui.Separator();
                            ImGui.Spacing();

                            ImGui.TextColored(new System.Numerics.Vector4(0.5f, 0.8f, 1.0f, 1.0f), "Imported Materials:");
                            ImGui.Indent();
                            for (int i = 0; i < meshAsset.MaterialGuids.Count; i++)
                            {
                                var matGuid = meshAsset.MaterialGuids[i];
                                if (matGuid.HasValue)
                                {
                                    var matName = AssetDatabase.GetName(matGuid.Value);
                                    ImGui.Text($"[{i}] {matName}");
                                }
                                else
                                {
                                    ImGui.TextDisabled($"[{i}] <No material>");
                                }
                            }
                            ImGui.Unindent();
                        }
                    }
                    else
                    {
                        InspectorWidgets.WarningBox("Failed to load mesh asset info");
                    }

                    InspectorWidgets.EndSection();
                }
            }
        }
    }
}
