using System;
using System.IO;
using System.Numerics;
using ImGuiNET;
using Engine.Audio.Assets;
using Engine.Assets;

namespace Editor.Inspector
{
    /// <summary>
    /// Inspecteur pour les fichiers audio dans le panneau Assets
    /// </summary>
    public static class AudioClipInspector
    {
        public static void Draw(Guid assetGuid)
        {
            if (!AssetDatabase.TryGet(assetGuid, out var record))
            {
                ImGui.Text("Audio clip not found");
                return;
            }

            ImGui.PushID("AudioClipInspector");

            // Header
            ImGui.SeparatorText("Audio Clip");
            ImGui.Text($"File: {Path.GetFileName(record.Path)}");

            // Detect format from extension
            string ext = Path.GetExtension(record.Path).ToLowerInvariant();
            string format = ext switch
            {
                ".mp3" => "MP3",
                ".ogg" => "OGG Vorbis",
                ".wav" => "WAV",
                _ => "Unknown"
            };

            ImGui.Text($"Format: {format}");
            ImGui.Text($"Path: {record.Path}");

            // File info
            if (File.Exists(record.Path))
            {
                var fileInfo = new FileInfo(record.Path);
                ImGui.Text($"Size: {fileInfo.Length / 1024.0:F2} KB");
            }

            ImGui.Spacing();
            ImGui.SeparatorText("Preview");

            // Preview controls (simple)
            bool isStreaming = ext == ".mp3" || ext == ".ogg" || (ext == ".wav" && new FileInfo(record.Path).Length > 1_000_000);
            ImGui.Text($"Streaming: {(isStreaming ? "Yes" : "No")}");

            // Simple waveform visualization
            DrawSimpleWaveform();

            if (ImGui.Button("Preview in Scene"))
            {
                // TODO: Create a temporary audio source in the scene for preview
                Serilog.Log.Information("[AudioClipInspector] Preview (TODO: implement preview functionality)");
            }

            ImGui.Spacing();
            ImGui.SeparatorText("Import Settings");

            ImGui.TextDisabled("Audio files are imported automatically");
            ImGui.TextDisabled("Support for: WAV, MP3, OGG");

            // Recommendations based on format
            ImGui.Spacing();
            ImGui.TextColored(new Vector4(0.5f, 0.8f, 1f, 1f), "Tips:");
            if (ext == ".mp3" || ext == ".ogg")
            {
                ImGui.BulletText("Compressed format - good for music");
                ImGui.BulletText("Will be streamed if > 1MB");
            }
            else if (ext == ".wav")
            {
                ImGui.BulletText("Uncompressed format - good for SFX");
                ImGui.BulletText("Will be streamed if > 1MB");
            }

            ImGui.Spacing();
            ImGui.SeparatorText("Usage");
            ImGui.TextWrapped("Drag this audio file onto an AudioSource component's 'Clip' field, or use the 'Load Clip...' button in the AudioSource inspector.");

            ImGui.PopID();
        }

        /// <summary>
        /// Draw a simple placeholder waveform (sine wave for demonstration)
        /// In a real implementation, this would read actual audio data from the clip
        /// </summary>
        private static void DrawSimpleWaveform()
        {
            var drawList = ImGui.GetWindowDrawList();
            var pos = ImGui.GetCursorScreenPos();
            var size = new Vector2(ImGui.GetContentRegionAvail().X, 80);

            // Background
            drawList.AddRectFilled(pos, new Vector2(pos.X + size.X, pos.Y + size.Y),
                ImGui.GetColorU32(new Vector4(0.1f, 0.1f, 0.1f, 1f)));

            // Center line
            float centerY = pos.Y + size.Y / 2;
            drawList.AddLine(
                new Vector2(pos.X, centerY),
                new Vector2(pos.X + size.X, centerY),
                ImGui.GetColorU32(new Vector4(0.3f, 0.3f, 0.3f, 1f)),
                1.0f
            );

            // Draw a simple sine wave as placeholder
            // In a real implementation, this would sample actual audio data
            int sampleCount = (int)size.X;
            Vector2? lastPoint = null;

            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / sampleCount;
                float x = pos.X + i;

                // Simple sine wave (placeholder - would be real audio data)
                float sampleValue = MathF.Sin(t * MathF.PI * 4) * 0.8f; // 2 cycles
                float y = centerY + sampleValue * (size.Y / 2 - 4);

                Vector2 currentPoint = new Vector2(x, y);

                if (lastPoint.HasValue)
                {
                    drawList.AddLine(
                        lastPoint.Value,
                        currentPoint,
                        ImGui.GetColorU32(new Vector4(0.2f, 0.7f, 0.9f, 1f)),
                        1.5f
                    );
                }

                lastPoint = currentPoint;
            }

            // Border
            drawList.AddRect(pos, new Vector2(pos.X + size.X, pos.Y + size.Y),
                ImGui.GetColorU32(new Vector4(0.5f, 0.5f, 0.5f, 1f)));

            // Label
            ImGui.SetCursorScreenPos(new Vector2(pos.X + 4, pos.Y + 4));
            ImGui.TextColored(new Vector4(0.7f, 0.7f, 0.7f, 1f), "Waveform (placeholder)");

            ImGui.SetCursorScreenPos(new Vector2(pos.X, pos.Y + size.Y));
            ImGui.Dummy(new Vector2(size.X, 1)); // Move cursor past the waveform
        }
    }
}
