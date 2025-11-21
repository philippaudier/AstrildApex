using System.Numerics;
using ImGuiNET;
using Engine.Audio.Components;

namespace Editor.Inspector
{
    /// <summary>
    /// Inspecteur pour le composant AudioListenerComponent
    /// </summary>
    public static class AudioListenerInspector
    {
        public static void Draw(AudioListenerComponent listener)
        {
            if (listener == null) return;

            ImGui.PushID("AudioListener");

            ImGui.SeparatorText("Audio Listener");

            // Indiquer si c'est le listener actif
            bool isActive = AudioListenerComponent.ActiveListener == listener;
            bool isEnabled = listener.Enabled;

            if (isActive && isEnabled)
            {
                ImGui.TextColored(new Vector4(0.2f, 1.0f, 0.2f, 1.0f), "● ACTIVE LISTENER");
            }
            else if (!isEnabled)
            {
                ImGui.TextColored(new Vector4(0.7f, 0.7f, 0.7f, 1.0f), "○ Disabled (Enable the component)");
            }
            else
            {
                ImGui.TextColored(new Vector4(1.0f, 0.5f, 0.0f, 1.0f), "○ Inactive (Another listener is active)");
            }

            ImGui.Spacing();

            // Velocity Update Mode
            int velocityMode = (int)listener.VelocityMode;
            string[] modeNames = { "Auto", "Manual" };
            if (ImGui.Combo("Velocity Mode", ref velocityMode, modeNames, modeNames.Length))
            {
                listener.VelocityMode = (AudioListenerComponent.VelocityUpdateMode)velocityMode;
            }

            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("Auto: Calculate velocity from position\nManual: Set velocity manually for Doppler effect");
            }

            ImGui.Spacing();
            ImGui.TextDisabled("Only one listener can be active at a time.");
            ImGui.TextDisabled("This represents the player's ear in the 3D world.");

            // Small helper to let user activate this listener without disabling the previous component
            ImGui.Spacing();
            if (ImGui.Button("Make Active"))
            {
                listener.Activate();
            }

            ImGui.PopID();
        }
    }
}
