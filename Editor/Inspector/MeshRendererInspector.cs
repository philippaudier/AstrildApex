using ImGuiNET;
using Engine.Components;
using Engine.Assets;
using Editor.Inspector;
using Editor.UI;
using Editor.Themes;
using System;
using System.Linq;

namespace Editor.Inspector
{
    /// <summary>
    /// Modern Unity-style inspector for MeshRendererComponent
    /// Uses unified EditorWidgets system for consistent UX
    /// </summary>
    public static class MeshRendererInspector
    {
        // Quick access to theme
        private static UITheme UI => ThemeManager.UI;
        
        // Cache pour éviter de charger le MeshAsset à chaque frame (cause des 6 FPS!)
        private static Guid? _cachedMeshGuid = null;
        private static MeshAsset? _cachedMeshAsset = null;
        
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
                
                // DEBUG: Afficher les valeurs brutes
                if (ImGui.IsKeyDown(ImGuiKey.LeftShift))
                {
                    ImGui.TextDisabled($"[DEBUG] CustomMeshGuid: {meshRenderer.CustomMeshGuid}");
                    ImGui.TextDisabled($"[DEBUG] IsUsingCustomMesh: {usingCustomMesh}");
                    ImGui.TextDisabled($"[DEBUG] Mesh: {meshRenderer.Mesh}");
                }

                if (usingCustomMesh)
                {
                    // Display custom mesh info
                    if (meshRenderer.CustomMeshGuid.HasValue)
                    {
                        var meshName = AssetDatabase.GetName(meshRenderer.CustomMeshGuid.Value);
                        
                        ImGui.Text("Mesh Type:");
                        ImGui.SameLine();
                        ImGui.TextColored(UI.Success, "Custom (Imported)");
                        
                        ImGui.Text("Mesh Asset:");
                        ImGui.SameLine();
                        ImGui.TextColored(UI.Info, meshName);
                        
                        ImGui.SameLine();
                        if (ImGui.SmallButton("Clear##CustomMesh"))
                        {
                            meshRenderer.ClearCustomMesh();
                            _cachedMeshGuid = null; // Invalider le cache
                            _cachedMeshAsset = null;
                        }
                        if (ImGui.IsItemHovered())
                        {
                            ImGui.SetTooltip("Click to remove custom mesh and use primitive");
                        }
                    }
                }
                else
                {
                    ImGui.Text("Mesh Type:");
                    ImGui.SameLine();
                    ImGui.TextColored(UI.Warning, "Primitive");
                    
                    // Display primitive mesh selector
                    var mesh = meshRenderer.Mesh;
                    InspectorWidgets.EnumField("Shape", ref mesh, entityId, "Mesh",
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
                            _cachedMeshGuid = null; // Invalider le cache
                            _cachedMeshAsset = null;
                        }

                        // List all available mesh assets
                        foreach (var asset in meshAssets)
                        {
                            bool isSelected = asset.Guid == currentMeshGuid;
                            if (ImGui.Selectable($"{asset.Name} ({asset.Type})", isSelected))
                            {
                                meshRenderer.SetCustomMesh(asset.Guid);
                                _cachedMeshGuid = null; // Invalider le cache
                                _cachedMeshAsset = null;
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
                // Modern Material Asset Field with drag-drop + click-to-select
                // Pass null (not Guid.Empty) when no material to show "Drag & Drop" button
                Guid? currentMaterial = meshRenderer.MaterialGuid.HasValue && meshRenderer.MaterialGuid.Value != Guid.Empty 
                    ? meshRenderer.MaterialGuid.Value 
                    : (Guid?)null;
                    
                Guid? newMaterial = EditorWidgets.AssetField(
                    "Material",
                    currentMaterial,
                    "Material",
                    description: "Material to apply visual properties (PBR, textures, colors)",
                    showPreview: false);
                
                if (newMaterial != meshRenderer.MaterialGuid)
                {
                    if (newMaterial.HasValue && newMaterial.Value != Guid.Empty)
                    {
                        meshRenderer.SetMaterial(newMaterial.Value);
                    }
                    else
                    {
                        // Clear material assignment (set to null)
                        meshRenderer.MaterialGuid = null;
                    }
                }

                ImGui.Spacing();
                EditorWidgets.Separator();

                // Multiple materials support (not yet implemented)
                ImGui.TextDisabled("Multiple Materials:");
                ImGui.Indent();
                ImGui.TextDisabled("Feature not implemented yet");
                ImGui.TextWrapped("When a mesh has multiple submeshes with different materials, you'll be able to assign different materials to each submesh here.");
                ImGui.Unindent();

                InspectorWidgets.EndSection();
            }

            // === RENDERING SECTION ===
            if (InspectorWidgets.Section("Rendering", defaultOpen: true,
                tooltip: "Rendering configuration"))
            {
                ImGui.Text("Culling Mode:");
                ImGui.SameLine();
                EditorWidgets.HelpIcon("Which faces to cull during rendering");

                var cullingMode = meshRenderer.Culling;
                string[] cullingLabels = { "Back", "Front", "None (Both)" };
                string[] cullingTooltips = {
                    "Cull back faces (default for most objects)",
                    "Cull front faces (for inside-out geometry)",
                    "No culling - render both sides (for thin objects like leaves)"
                };

                int cullingIndex = (int)cullingMode;
                
                if (ImGui.BeginCombo("##CullingMode", cullingLabels[cullingIndex]))
                {
                    for (int i = 0; i < cullingLabels.Length; i++)
                    {
                        bool isSelected = (cullingIndex == i);
                        if (ImGui.Selectable(cullingLabels[i], isSelected))
                        {
                            meshRenderer.Culling = (CullingMode)i;
                        }
                        
                        if (ImGui.IsItemHovered())
                        {
                            ImGui.SetTooltip(cullingTooltips[i]);
                        }
                        
                        if (isSelected)
                            ImGui.SetItemDefaultFocus();
                    }
                    ImGui.EndCombo();
                }

                ImGui.Spacing();
                
                // Visual indicator
                ImGui.Indent();
                switch (meshRenderer.Culling)
                {
                    case CullingMode.Back:
                        ImGui.TextColored(UI.Success, "✓ Standard rendering (recommended)");
                        break;
                    case CullingMode.Front:
                        ImGui.TextColored(UI.Warning, "⚠ Special use case (inside-out)");
                        break;
                    case CullingMode.None:
                        ImGui.TextColored(UI.Info, "ℹ Double-sided (performance cost)");
                        break;
                }
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
                    // Cache le MeshAsset pour éviter de le charger CHAQUE FRAME (cause des 6 FPS!)
                    if (_cachedMeshGuid != meshRenderer.CustomMeshGuid.Value || _cachedMeshAsset == null)
                    {
                        _cachedMeshGuid = meshRenderer.CustomMeshGuid.Value;
                        _cachedMeshAsset = AssetDatabase.LoadMeshAsset(_cachedMeshGuid.Value);
                        Console.WriteLine($"[MeshRendererInspector] Loading mesh asset for inspection (cached)");
                    }
                    
                    var meshAsset = _cachedMeshAsset;
                    
                    if (meshAsset != null)
                    {
                        // Statistics
                        ImGui.TextColored(UI.Primary, "Statistics:");
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

                        ImGui.TextColored(UI.Primary, "Bounding Box:");
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

                            ImGui.TextColored(UI.Primary, "Imported Materials:");
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
