using System;
using ImGuiNET;
using Editor.Rendering;
using OpenTK.Mathematics;
using Vector2 = System.Numerics.Vector2;

namespace Editor.Panels;

/// <summary>
/// Panel for camera settings (FOV, clipping planes, ortho size, etc.)
/// </summary>
public class CameraSettingsPanel
{
    private static bool _isOpen = false;

    public static void Open()
    {
        _isOpen = true;
    }

    public static void Draw(ViewportRenderer? renderer, ref float orthoSize, ref CameraMode cameraMode,
        ref float yaw, ref float pitch, ref float targetYaw, ref float targetPitch)
    {
        if (!_isOpen) return;

        ImGui.SetNextWindowSize(new Vector2(400, 500), ImGuiCond.FirstUseEver);

        if (ImGui.Begin("Camera Settings", ref _isOpen))
        {
            if (renderer == null)
            {
                ImGui.TextDisabled("No renderer available");
                ImGui.End();
                return;
            }

            // === General Settings ===
            if (ImGui.CollapsingHeader("General", ImGuiTreeNodeFlags.DefaultOpen))
            {
                // Camera Mode
                ImGui.Text("Projection Mode:");
                int currentMode = (int)cameraMode;
                string[] modes = { "Perspective", "Orthographic", "2D" };

                if (ImGui.Combo("##CameraMode", ref currentMode, modes, modes.Length))
                {
                    cameraMode = (CameraMode)currentMode;

                    // Apply projection mode and camera position
                    switch (cameraMode)
                    {
                        case CameraMode.Perspective:
                            // Restaurer les clips par défaut AVANT de changer le mode
                            renderer.NearClip = 0.1f;
                            renderer.FarClip = 5000f;
                            renderer.SetProjectionMode(0, orthoSize);
                            // Perspective par défaut: vue 3D libre
                            // Ne rien changer à la position de caméra
                            break;

                        case CameraMode.Orthographic:
                            // Ajuster les clips pour l'ortho AVANT de changer le mode
                            renderer.NearClip = 0.1f;
                            float orthoFarClip = Math.Max(5000f, orthoSize * 200f);
                            renderer.FarClip = orthoFarClip;
                            renderer.SetProjectionMode(1, orthoSize);
                            // Vue isométrique classique (comme dans les jeux): 45° yaw, -30° pitch
                            targetYaw = MathHelper.DegreesToRadians(45f);
                            targetPitch = MathHelper.DegreesToRadians(-30f);
                            yaw = targetYaw;
                            pitch = targetPitch;
                            break;

                        case CameraMode.TwoD:
                            // Ajuster les clips pour le 2D AVANT de changer le mode
                            renderer.NearClip = 0.1f;
                            float twoDFarClip = Math.Max(5000f, orthoSize * 200f);
                            renderer.FarClip = twoDFarClip;
                            renderer.SetProjectionMode(2, orthoSize);
                            // Vue 2D Unity-style: top-down (vue du dessus, Y vers le haut)
                            targetYaw = 0f;
                            targetPitch = MathHelper.DegreesToRadians(-90f);
                            yaw = targetYaw;
                            pitch = targetPitch;
                            break;
                    }
                }

                ImGui.Spacing();
            }

            // === Perspective Settings ===
            if (cameraMode == CameraMode.Perspective)
            {
                if (ImGui.CollapsingHeader("Perspective Settings", ImGuiTreeNodeFlags.DefaultOpen))
                {
                    // Near/Far Clip
                    float nearClip = renderer.NearClip;
                    if (ImGui.DragFloat("Near Clip", ref nearClip, 0.001f, 0.0001f, 10f, "%.4f"))
                    {
                        renderer.NearClip = nearClip;
                    }

                    float farClip = renderer.FarClip;
                    if (ImGui.DragFloat("Far Clip", ref farClip, 10f, 10f, 100000f, "%.1f"))
                    {
                        renderer.FarClip = farClip;
                    }

                    ImGui.Spacing();
                }
            }

            // === Orthographic Settings ===
            if (cameraMode == CameraMode.Orthographic || cameraMode == CameraMode.TwoD)
            {
                string headerName = cameraMode == CameraMode.TwoD ? "2D Settings" : "Orthographic Settings";

                if (ImGui.CollapsingHeader(headerName, ImGuiTreeNodeFlags.DefaultOpen))
                {
                    // Ortho Size
                    float tempOrthoSize = orthoSize;
                    if (ImGui.SliderFloat("Orthographic Size", ref tempOrthoSize, 0.1f, 500f, "%.1f"))
                    {
                        orthoSize = tempOrthoSize;

                        // Auto-adjust far clip AVANT de changer la taille
                        renderer.FarClip = Math.Max(5000f, orthoSize * 200f);

                        renderer.SetProjectionMode(cameraMode == CameraMode.TwoD ? 2 : 1, orthoSize);
                    }

                    ImGui.Spacing();

                    // Near/Far Clip
                    float nearClip = renderer.NearClip;
                    if (ImGui.DragFloat("Near Clip", ref nearClip, 0.001f, 0.0001f, 10f, "%.4f"))
                    {
                        renderer.NearClip = nearClip;
                    }

                    float farClip = renderer.FarClip;
                    if (ImGui.DragFloat("Far Clip", ref farClip, 10f, 10f, 100000f, "%.1f"))
                    {
                        renderer.FarClip = farClip;
                    }

                    ImGui.Spacing();

                    if (ImGui.Button("Auto-Adjust Far Clip"))
                    {
                        renderer.FarClip = Math.Max(5000f, orthoSize * 200f);
                    }

                    ImGui.Spacing();
                }
            }

            // === Info ===
            if (ImGui.CollapsingHeader("Info"))
            {
                ImGui.TextDisabled("Current Settings:");
                ImGui.Text($"Mode: {cameraMode}");
                ImGui.Text($"Near Clip: {renderer.NearClip:F4}");
                ImGui.Text($"Far Clip: {renderer.FarClip:F1}");

                if (cameraMode == CameraMode.Orthographic || cameraMode == CameraMode.TwoD)
                {
                    ImGui.Text($"Ortho Size: {orthoSize:F1}");
                }
            }

            ImGui.End();
        }
    }
}
