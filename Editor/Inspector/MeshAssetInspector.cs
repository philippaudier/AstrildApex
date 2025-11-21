using System;
using ImGuiNET;
using Engine.Assets;
using Editor.Tasks;
using Editor.Utils;
using Editor.Logging;
using System.IO;

namespace Editor.Inspector
{
    /// <summary>
    /// Inspector for imported mesh assets (FBX, OBJ, GLTF, etc.)
    /// Displays mesh statistics, materials, and metadata
    /// </summary>
    public static class MeshAssetInspector
    {
        // Cache to avoid reloading mesh every frame
        private static Guid _cachedGuid = Guid.Empty;
        private static MeshAsset? _cachedMesh = null;

        public static void Draw(Guid meshAssetGuid)
        {
            // Load mesh asset
            if (!AssetDatabase.TryGet(meshAssetGuid, out var assetRec))
            {
                ImGui.TextColored(new System.Numerics.Vector4(1, 0.4f, 0.4f, 1), "Mesh asset not found.");
                return;
            }

            // Display asset path
            ImGui.TextDisabled("Path:");
            ImGui.SameLine();
            ImGui.Text(assetRec.Path);

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            // Load actual mesh data (cached to avoid loading every frame)
            MeshAsset? meshAsset = null;
            if (_cachedGuid == meshAssetGuid && _cachedMesh != null)
            {
                // Use cached mesh
                meshAsset = _cachedMesh;
            }
            else
            {
                // Load from disk and cache
                meshAsset = AssetDatabase.LoadMeshAsset(meshAssetGuid);
                _cachedGuid = meshAssetGuid;
                _cachedMesh = meshAsset;
            }

            if (meshAsset == null)
            {
                ImGui.TextColored(new System.Numerics.Vector4(1, 0.6f, 0, 1), "Failed to load mesh data.");
                ImGui.TextWrapped("The mesh file may be corrupted or in an unsupported format.");
                return;
            }

            // === MESH STATISTICS ===
            if (ImGui.CollapsingHeader("Mesh Statistics", ImGuiTreeNodeFlags.DefaultOpen))
            {
                ImGui.Indent();

                ImGui.TextColored(new System.Numerics.Vector4(0.5f, 0.8f, 1.0f, 1.0f), "Geometry:");
                ImGui.Text($"  Vertices:  {meshAsset.TotalVertexCount:N0}");
                ImGui.Text($"  Triangles: {meshAsset.TotalTriangleCount:N0}");
                ImGui.Text($"  Submeshes: {meshAsset.SubMeshes.Count}");

                ImGui.Spacing();
                ImGui.TextColored(new System.Numerics.Vector4(0.5f, 0.8f, 1.0f, 1.0f), "Bounding Box:");
                var bounds = meshAsset.Bounds;
                ImGui.Text($"  Center: ({bounds.Center.X:F2}, {bounds.Center.Y:F2}, {bounds.Center.Z:F2})");
                ImGui.Text($"  Size:   ({bounds.Size.X:F2}, {bounds.Size.Y:F2}, {bounds.Size.Z:F2})");

                ImGui.Unindent();
            }

            // === SUBMESHES ===
            if (meshAsset.SubMeshes.Count > 1)
            {
                ImGui.Spacing();
                if (ImGui.CollapsingHeader($"Submeshes ({meshAsset.SubMeshes.Count})", ImGuiTreeNodeFlags.DefaultOpen))
                {
                    ImGui.Indent();

                    for (int i = 0; i < meshAsset.SubMeshes.Count; i++)
                    {
                        var submesh = meshAsset.SubMeshes[i];
                        int vertexCount = submesh.Vertices.Length / 8; // 8 floats per vertex (pos + normal + uv)
                        int triangleCount = submesh.Indices.Length / 3;

                        ImGui.PushID($"submesh_{i}");
                        if (ImGui.TreeNodeEx($"Submesh {i}", ImGuiTreeNodeFlags.DefaultOpen))
                        {
                            ImGui.Text($"  Vertices:  {vertexCount:N0}");
                            ImGui.Text($"  Triangles: {triangleCount:N0}");
                            ImGui.Text($"  Material Index: {submesh.MaterialIndex}");
                            ImGui.TreePop();
                        }
                        ImGui.PopID();
                    }

                    ImGui.Unindent();
                }
            }

            // === MATERIALS ===
            if (meshAsset.MaterialGuids.Count > 0)
            {
                ImGui.Spacing();
                if (ImGui.CollapsingHeader($"Imported Materials ({meshAsset.MaterialGuids.Count})", ImGuiTreeNodeFlags.DefaultOpen))
                {
                    ImGui.Indent();

                    for (int i = 0; i < meshAsset.MaterialGuids.Count; i++)
                    {
                        var matGuid = meshAsset.MaterialGuids[i];
                        ImGui.PushID($"mat_{i}");

                        if (matGuid.HasValue && matGuid.Value != Guid.Empty)
                        {
                            var matName = AssetDatabase.GetName(matGuid.Value);
                            ImGui.BulletText($"[{i}] {matName}");

                            // Button to select material in assets panel
                            if (ImGui.IsItemHovered() && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
                            {
                                Editor.State.Selection.SetActiveAsset(matGuid.Value, "Material");
                            }
                            if (ImGui.IsItemHovered())
                            {
                                ImGui.SetTooltip("Click to select this material");
                            }
                        }
                        else
                        {
                            ImGui.BulletText($"[{i}] <No material>");
                        }

                        ImGui.PopID();
                    }

                    ImGui.Unindent();
                }
            }

            // === USAGE IN SCENE ===
            ImGui.Spacing();
            if (ImGui.CollapsingHeader("Usage", ImGuiTreeNodeFlags.None))
            {
                ImGui.Indent();
                ImGui.TextDisabled("Drag & Drop:");
                ImGui.BulletText("Drag this mesh from Assets panel to Scene viewport");
                ImGui.BulletText("Or assign to MeshRenderer component in Inspector");
                ImGui.Unindent();
            }

            // === REIMPORT ===
            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            if (ImGui.Button("Reimport Mesh", new System.Numerics.Vector2(-1, 0)))
            {
                try
                {
                    var assetsRoot = Editor.State.ProjectPaths.AssetsDir;
                    string? modelRelativePath = meshAsset.SourcePath;
                    string sourceFile = !string.IsNullOrWhiteSpace(modelRelativePath)
                        ? System.IO.Path.Combine(assetsRoot, modelRelativePath)
                        : assetRec.Path;

                    if (!System.IO.File.Exists(sourceFile))
                    {
                        LogManager.LogWarning($"Source file not found: {sourceFile}", "MeshAssetInspector");
                        return;
                    }

                    DeferredActions.Enqueue(() =>
                    {
                            ModelImportJob.Run(sourceFile, assetsRoot, "Models", assetRec.Name, guid =>
                        {
                            _cachedGuid = Guid.Empty;
                            _cachedMesh = null;
                            LogManager.LogInfo($"Reimported: {assetRec.Name} (GUID: {guid})", "MeshAssetInspector");
                        });
                    });
                }
                catch (Exception ex)
                {
                    LogManager.LogError($"Reimport failed: {ex.Message}", "MeshAssetInspector");
                }
            }
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("Reimport this mesh from the source file (useful after external changes)");
            }
        }
    }
}
