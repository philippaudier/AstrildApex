using System;
using System.Numerics;
using ImGuiNET;
using Engine.Audio.Assets;

namespace Editor.Panels
{
    /// <summary>
    /// Visualiseur de forme d'onde audio pour l'éditeur
    /// Affiche la waveform d'un AudioClip
    /// </summary>
    public static class WaveformViewer
    {
        private static float[] _waveformData = Array.Empty<float>();
        // private static int _samplesPerPixel = 512; // TODO: Use this for real waveform rendering

        /// <summary>
        /// Dessine la waveform d'un AudioClip
        /// </summary>
        public static void DrawWaveform(AudioClip? clip, Vector2 size)
        {
            if (clip == null || !clip.IsLoaded)
            {
                DrawEmptyWaveform(size);
                return;
            }

            var drawList = ImGui.GetWindowDrawList();
            var pos = ImGui.GetCursorScreenPos();

            // Fond noir
            drawList.AddRectFilled(pos, new Vector2(pos.X + size.X, pos.Y + size.Y),
                ImGui.GetColorU32(new Vector4(0.1f, 0.1f, 0.1f, 1f)));

            // Ligne centrale
            float centerY = pos.Y + size.Y * 0.5f;
            drawList.AddLine(
                new Vector2(pos.X, centerY),
                new Vector2(pos.X + size.X, centerY),
                ImGui.GetColorU32(new Vector4(0.3f, 0.3f, 0.3f, 1f)),
                1f
            );

            // TODO: Extraire et dessiner les vraies données audio
            // Pour l'instant, dessiner une waveform simulée
            DrawSimulatedWaveform(drawList, pos, size);

            ImGui.Dummy(size);

            // Infos
            ImGui.Text($"Duration: {clip.Length:F2}s");
            ImGui.Text($"Format: {clip.Format}");
            ImGui.Text($"Frequency: {clip.Frequency} Hz");
            ImGui.Text($"Channels: {clip.Channels}");
        }

        /// <summary>
        /// Dessine une waveform vide (placeholder)
        /// </summary>
        private static void DrawEmptyWaveform(Vector2 size)
        {
            var drawList = ImGui.GetWindowDrawList();
            var pos = ImGui.GetCursorScreenPos();

            // Fond gris
            drawList.AddRectFilled(pos, new Vector2(pos.X + size.X, pos.Y + size.Y),
                ImGui.GetColorU32(new Vector4(0.15f, 0.15f, 0.15f, 1f)));

            // Texte centré
            var text = "No audio clip loaded";
            var textSize = ImGui.CalcTextSize(text);
            var textPos = new Vector2(
                pos.X + (size.X - textSize.X) * 0.5f,
                pos.Y + (size.Y - textSize.Y) * 0.5f
            );

            drawList.AddText(textPos, ImGui.GetColorU32(new Vector4(0.5f, 0.5f, 0.5f, 1f)), text);

            ImGui.Dummy(size);
        }

        /// <summary>
        /// Dessine une waveform simulée (placeholder)
        /// </summary>
        private static void DrawSimulatedWaveform(ImDrawListPtr drawList, Vector2 pos, Vector2 size)
        {
            int numPoints = (int)size.X;
            float centerY = pos.Y + size.Y * 0.5f;
            float amplitude = size.Y * 0.4f;

            uint color = ImGui.GetColorU32(new Vector4(0.2f, 0.8f, 0.3f, 1f));

            for (int i = 0; i < numPoints - 1; i++)
            {
                float t1 = (float)i / numPoints;
                float t2 = (float)(i + 1) / numPoints;

                // Simuler une waveform sinusoïdale avec du bruit
                float wave1 = (float)Math.Sin(t1 * 50) * 0.5f + (float)(Math.Sin(t1 * 200) * 0.3f);
                float wave2 = (float)Math.Sin(t2 * 50) * 0.5f + (float)(Math.Sin(t2 * 200) * 0.3f);

                float x1 = pos.X + i;
                float y1 = centerY + wave1 * amplitude;
                float x2 = pos.X + i + 1;
                float y2 = centerY + wave2 * amplitude;

                drawList.AddLine(new Vector2(x1, y1), new Vector2(x2, y2), color, 1.5f);
            }
        }

        /// <summary>
        /// Dessine un spectrogramme (TODO: à implémenter avec FFT)
        /// </summary>
        public static void DrawSpectrogram(AudioClip? clip, Vector2 size)
        {
            if (clip == null || !clip.IsLoaded)
            {
                DrawEmptyWaveform(size);
                return;
            }

            var drawList = ImGui.GetWindowDrawList();
            var pos = ImGui.GetCursorScreenPos();

            // Fond noir
            drawList.AddRectFilled(pos, new Vector2(pos.X + size.X, pos.Y + size.Y),
                ImGui.GetColorU32(new Vector4(0.05f, 0.05f, 0.1f, 1f)));

            // TODO: Implémenter l'analyse FFT et le spectrogramme

            var text = "Spectrogram (TODO)";
            var textSize = ImGui.CalcTextSize(text);
            var textPos = new Vector2(
                pos.X + (size.X - textSize.X) * 0.5f,
                pos.Y + (size.Y - textSize.Y) * 0.5f
            );

            drawList.AddText(textPos, ImGui.GetColorU32(new Vector4(0.6f, 0.6f, 0.8f, 1f)), text);

            ImGui.Dummy(size);
        }
    }
}
