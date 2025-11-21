using System;
using System.Numerics;
using ImGuiNET;
using Engine.Audio.Components;
using Engine.Audio.Assets;
using Serilog;

namespace Editor.Inspector
{
    /// <summary>
    /// Inspecteur pour le composant AudioSource
    /// </summary>
    public static class AudioSourceInspector
    {
        public static void Draw(AudioSource audioSource)
        {
            if (audioSource == null) return;

            ImGui.PushID("AudioSource");

            // Section : Audio Clip
            ImGui.SeparatorText("Audio Clip");

            string clipName = audioSource.Clip?.Name ?? "None (Audio Clip)";

            // Create a button that looks like an assignment field (similar to Material field)
            var buttonColor = audioSource.Clip != null
                ? new Vector4(0.3f, 0.7f, 0.4f, 1.0f)  // Green for assigned clip
                : new Vector4(0.4f, 0.4f, 0.4f, 1.0f);  // Gray for none

            ImGui.PushStyleColor(ImGuiCol.Button, buttonColor);
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, buttonColor * 1.2f);
            ImGui.PushStyleColor(ImGuiCol.ButtonActive, buttonColor * 0.8f);

            bool clipClicked = ImGui.Button($"{clipName}##ClipField", new Vector2(-1, 24));

            ImGui.PopStyleColor(3);

            // Handle drag & drop from Assets panel
            if (ImGui.BeginDragDropTarget())
            {
                var payload = ImGui.AcceptDragDropPayload("ASSET_MULTI");
                unsafe
                {
                    if (payload.NativePtr != null && payload.Data != IntPtr.Zero && payload.DataSize >= 16)
                    {
                        try
                        {
                            // Extract first GUID from payload
                            var span = new ReadOnlySpan<byte>((void*)payload.Data, 16);
                            var droppedGuid = new Guid(span);

                            // Check if it's an audio file
                            if (Engine.Assets.AssetDatabase.TryGet(droppedGuid, out var record))
                            {
                                string ext = System.IO.Path.GetExtension(record.Path).ToLowerInvariant();
                                if (ext == ".mp3" || ext == ".ogg" || ext == ".wav")
                                {
                                    // Load the audio clip
                                    var clip = AudioImporter.LoadClip(record.Path);
                                    if (clip != null)
                                    {
                                        audioSource.Clip = clip;
                                        Log.Information($"[AudioSourceInspector] Assigned audio clip: {record.Path}");
                                    }
                                }
                            }
                        }
                        catch (System.Exception ex)
                        {
                            Log.Error(ex, "[AudioSourceInspector] Error in drag & drop");
                        }
                    }
                }
                ImGui.EndDragDropTarget();
            }

            // Right-click context menu
            if (ImGui.BeginPopupContextItem("ClipContextMenu"))
            {
                if (ImGui.MenuItem("Clear Clip"))
                {
                    audioSource.Clip = null;
                }
                ImGui.EndPopup();
            }

            if (ImGui.Button("Load Clip from File..."))
            {
                // Open file dialog to select an audio clip
                try
                {
                    var result = NativeFileDialogSharp.Dialog.FileOpen("wav,mp3,ogg");

                    if (result.IsOk)
                    {
                        string filePath = result.Path;

                        // Load the clip (AudioImporter handles streaming vs non-streaming automatically)
                        var clip = AudioImporter.LoadClip(filePath);

                        if (clip != null)
                        {
                            audioSource.Clip = clip;
                            if (clip.IsStreaming)
                            {
                                Log.Information($"[AudioSourceInspector] Loaded streaming audio clip: {filePath}");
                            }
                            else
                            {
                                Log.Information($"[AudioSourceInspector] Loaded audio clip: {filePath}");
                            }
                        }
                        else
                        {
                            Log.Warning($"[AudioSourceInspector] Failed to load audio clip: {filePath}");
                        }
                    }
                }
                catch (System.Exception ex)
                {
                    Log.Error(ex, "[AudioSourceInspector] Error loading audio clip");
                }
            }

            ImGui.Spacing();

            // Section : Playback Controls
            ImGui.SeparatorText("Playback");

            if (ImGui.Button(audioSource.IsPlaying ? "Stop" : "Play"))
            {
                if (audioSource.IsPlaying)
                    audioSource.Stop();
                else
                    audioSource.Play(resetPosition: false); // Preview without resetting live stream
            }

            ImGui.SameLine();

            if (ImGui.Button(audioSource.IsPaused ? "Resume" : "Pause"))
            {
                if (audioSource.IsPaused)
                    audioSource.UnPause();
                else
                    audioSource.Pause();
            }

            // Afficher le temps de lecture
            if (audioSource.Clip != null)
            {
                float time = audioSource.Time;
                float length = audioSource.Clip.Length;
                
                // Format time as MM:SS
                string FormatTime(float seconds)
                {
                    int minutes = (int)(seconds / 60);
                    int secs = (int)(seconds % 60);
                    return $"{minutes:D2}:{secs:D2}";
                }
                
                ImGui.Text($"Time: {FormatTime(time)} / {FormatTime(length)}");

                // Progress bar
                float progress = length > 0 ? time / length : 0f;
                var cursorPos = ImGui.GetCursorScreenPos();
                var availWidth = ImGui.GetContentRegionAvail().X;
                var barHeight = 20f;
                
                // Draw custom progress bar
                var drawList = ImGui.GetWindowDrawList();
                var barMin = cursorPos;
                var barMax = new Vector2(cursorPos.X + availWidth, cursorPos.Y + barHeight);
                
                // Background
                drawList.AddRectFilled(barMin, barMax, ImGui.GetColorU32(new Vector4(0.2f, 0.2f, 0.2f, 1f)));
                
                // Progress fill
                if (progress > 0)
                {
                    var fillMax = new Vector2(barMin.X + availWidth * progress, barMax.Y);
                    drawList.AddRectFilled(barMin, fillMax, ImGui.GetColorU32(new Vector4(0.3f, 0.6f, 0.9f, 1f)));
                }
                
                // Border
                drawList.AddRect(barMin, barMax, ImGui.GetColorU32(new Vector4(0.5f, 0.5f, 0.5f, 1f)), 0f, 0, 1.5f);
                
                // Time indicator text on top
                var textSize = ImGui.CalcTextSize($"{(progress * 100):F1}%");
                var textPos = new Vector2(
                    barMin.X + (availWidth - textSize.X) / 2,
                    barMin.Y + (barHeight - textSize.Y) / 2
                );
                drawList.AddText(textPos, ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 1f)), $"{(progress * 100):F1}%");
                
                // Make it a dummy to take up space
                ImGui.SetCursorScreenPos(barMin);
                ImGui.Dummy(new Vector2(availWidth, barHeight));
            }

            ImGui.Spacing();

            // Section : Volume & Pitch
            ImGui.SeparatorText("Audio Settings");

            float volume = audioSource.Volume;
            if (ImGui.SliderFloat("Volume", ref volume, 0f, 1f))
            {
                audioSource.Volume = volume;
            }

            float pitch = audioSource.Pitch;
            if (ImGui.SliderFloat("Pitch", ref pitch, 0.5f, 2.0f))
            {
                audioSource.Pitch = pitch;
            }

            bool mute = audioSource.Mute;
            if (ImGui.Checkbox("Mute", ref mute))
            {
                audioSource.Mute = mute;
            }

            bool loop = audioSource.Loop;
            if (ImGui.Checkbox("Loop", ref loop))
            {
                audioSource.Loop = loop;
            }

            bool playOnAwake = audioSource.PlayOnAwake;
            if (ImGui.Checkbox("Play On Awake", ref playOnAwake))
            {
                audioSource.PlayOnAwake = playOnAwake;
            }

            ImGui.Spacing();

            // Section : Spatial Settings
            ImGui.SeparatorText("3D Sound Settings");

            float spatialBlend = audioSource.SpatialBlend;
            if (ImGui.SliderFloat("Spatial Blend", ref spatialBlend, 0f, 1f))
            {
                audioSource.SpatialBlend = spatialBlend;
            }
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("0 = 2D (no attenuation), 1 = 3D (full spatial audio)");
            }

            if (spatialBlend > 0f)
            {
                float minDistance = audioSource.MinDistance;
                if (ImGui.DragFloat("Min Distance", ref minDistance, 0.1f, 0.1f, 1000f))
                {
                    audioSource.MinDistance = Math.Max(0.1f, minDistance);
                }
                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip("Distance where volume is at maximum");
                }

                float maxDistance = audioSource.MaxDistance;
                if (ImGui.DragFloat("Max Distance", ref maxDistance, 1f, minDistance, 10000f))
                {
                    audioSource.MaxDistance = Math.Max(minDistance, maxDistance);
                }
                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip("Distance where volume reaches minimum");
                }

                float rolloffFactor = audioSource.RolloffFactor;
                if (ImGui.SliderFloat("Rolloff Factor", ref rolloffFactor, 0f, 5f))
                {
                    audioSource.RolloffFactor = rolloffFactor;
                }
                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip("Controls how quickly volume decreases with distance");
                }

                float dopplerLevel = audioSource.DopplerLevel;
                if (ImGui.SliderFloat("Doppler Level", ref dopplerLevel, 0f, 5f))
                {
                    audioSource.DopplerLevel = dopplerLevel;
                }
                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip("Strength of the Doppler effect (pitch change based on velocity)");
                }
            }

            ImGui.Spacing();

            // Section : Category & Priority
            ImGui.SeparatorText("Mixing");

            int category = (int)audioSource.Category;
            string[] categoryNames = Enum.GetNames(typeof(AudioCategory));
            if (ImGui.Combo("Category", ref category, categoryNames, categoryNames.Length))
            {
                audioSource.Category = (AudioCategory)category;
            }

            int priority = audioSource.Priority;
            if (ImGui.SliderInt("Priority", ref priority, 0, 256))
            {
                audioSource.Priority = priority;
            }
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("0 = highest priority, 256 = lowest");
            }

            ImGui.Spacing();

            // Section : Audio Filters (EFX)
            ImGui.SeparatorText("Audio Filters");

            DrawAudioFilters(audioSource);

            ImGui.Spacing();

            // Section : Info
            ImGui.SeparatorText("Info");

            if (audioSource.Clip != null)
            {
                ImGui.Text($"Format: {audioSource.Clip.Format}");
                ImGui.Text($"Frequency: {audioSource.Clip.Frequency} Hz");
                ImGui.Text($"Channels: {audioSource.Clip.Channels}");
                ImGui.Text($"Size: {audioSource.Clip.SizeInBytes / 1024} KB");
            }

            ImGui.Text($"Is Playing: {audioSource.IsPlaying}");
            ImGui.Text($"Is Paused: {audioSource.IsPaused}");

            ImGui.PopID();
        }

        // Legacy audio effects UI removed: old per-source "Audio Effects" support
        // was superseded by the EFX-based "Audio Filters" and mixer EFX system.
        // DrawAudioEffects and per-effect parameter drawers were removed.

        private static void DrawAudioFilters(AudioSource audioSource)
        {
            // Check if EFX is supported
            if (!Engine.Audio.Effects.AudioEfxBackend.Instance.IsEFXSupported)
            {
                ImGui.TextDisabled("EFX not supported - filters unavailable");
                return;
            }

            // Button to add a filter
            if (ImGui.Button("Add Filter"))
            {
                ImGui.OpenPopup("AddFilterPopup");
            }

            if (ImGui.BeginPopup("AddFilterPopup"))
            {
                ImGui.Text("Select Filter Type:");
                ImGui.Separator();

                if (ImGui.MenuItem("Low-Pass Filter"))
                {
                    audioSource.AddLowPassFilter();
                    ImGui.CloseCurrentPopup();
                }

                if (ImGui.MenuItem("High-Pass Filter"))
                {
                    audioSource.AddHighPassFilter();
                    ImGui.CloseCurrentPopup();
                }

                ImGui.EndPopup();
            }

            ImGui.SameLine();
            ImGui.TextDisabled($"({audioSource.Filters.Count} filter(s))");

            // Display list of filters
            if (audioSource.Filters.Count > 0)
            {
                ImGui.Spacing();
                ImGui.TextWrapped("Note: Only one direct filter can be active at a time (OpenAL limitation)");
                ImGui.Spacing();

                Engine.Audio.Components.AudioSourceFilter? filterToRemove = null;

                for (int i = 0; i < audioSource.Filters.Count; i++)
                {
                    var filter = audioSource.Filters[i];
                    ImGui.PushID($"Filter_{i}");

                    // Header
                    bool isOpen = ImGui.CollapsingHeader($"{filter.Type} Filter ###{i}");

                    // Remove button on the same line
                    ImGui.SameLine(ImGui.GetContentRegionAvail().X - 60);
                    if (ImGui.Button("Remove"))
                    {
                        filterToRemove = filter;
                    }

                    // Filter content (if open)
                    if (isOpen)
                    {
                        ImGui.Indent();

                        bool enabled = filter.Enabled;
                        if (ImGui.Checkbox("Enabled", ref enabled))
                        {
                            filter.Enabled = enabled;
                        }

                        // Draw filter-specific parameters
                        DrawFilterParameters(filter);

                        ImGui.Unindent();
                    }

                    ImGui.PopID();
                }

                // Remove marked filter
                if (filterToRemove != null)
                {
                    Engine.Audio.Components.AudioSourceFilterExtensions.DestroyFilterHandle(filterToRemove);
                    audioSource.Filters.Remove(filterToRemove);
                }
            }
            else
            {
                ImGui.TextDisabled("No filters added");
                ImGui.TextWrapped("Filters affect the sound directly. Use Low-Pass to muffle sounds, High-Pass to remove low frequencies.");
            }
        }

        private static void DrawFilterParameters(Engine.Audio.Components.AudioSourceFilter filter)
        {
            switch (filter.Type)
            {
                case Engine.Audio.Components.AudioSourceFilterType.LowPass:
                    if (filter.Settings is Engine.Audio.Effects.LowPassSettings lowPass)
                    {
                        float gain = lowPass.Gain;
                        if (ImGui.SliderFloat("Gain", ref gain, 0f, 1f))
                        {
                            lowPass.Gain = gain;
                            filter.UpdateFilter();
                        }
                        if (ImGui.IsItemHovered())
                        {
                            ImGui.SetTooltip("Overall volume multiplier");
                        }

                        float gainHF = lowPass.GainHF;
                        if (ImGui.SliderFloat("Gain HF", ref gainHF, 0f, 1f))
                        {
                            lowPass.GainHF = gainHF;
                            filter.UpdateFilter();
                        }
                        if (ImGui.IsItemHovered())
                        {
                            ImGui.SetTooltip("High frequency attenuation (0 = muffled, 1 = clear)");
                        }
                    }
                    break;

                case Engine.Audio.Components.AudioSourceFilterType.HighPass:
                    if (filter.Settings is Engine.Audio.Effects.HighPassSettings highPass)
                    {
                        float gain = highPass.Gain;
                        if (ImGui.SliderFloat("Gain", ref gain, 0f, 1f))
                        {
                            highPass.Gain = gain;
                            filter.UpdateFilter();
                        }
                        if (ImGui.IsItemHovered())
                        {
                            ImGui.SetTooltip("Overall volume multiplier");
                        }

                        float gainLF = highPass.GainLF;
                        if (ImGui.SliderFloat("Gain LF", ref gainLF, 0f, 1f))
                        {
                            highPass.GainLF = gainLF;
                            filter.UpdateFilter();
                        }
                        if (ImGui.IsItemHovered())
                        {
                            ImGui.SetTooltip("Low frequency attenuation (0 = thin, 1 = full)");
                        }
                    }
                    break;

                case Engine.Audio.Components.AudioSourceFilterType.BandPass:
                    if (filter.Settings is Engine.Audio.Effects.BandPassSettings bandPass)
                    {
                        float gain = bandPass.Gain;
                        if (ImGui.SliderFloat("Gain", ref gain, 0f, 1f))
                        {
                            bandPass.Gain = gain;
                            filter.UpdateFilter();
                        }

                        float gainLF = bandPass.GainLF;
                        if (ImGui.SliderFloat("Gain LF", ref gainLF, 0f, 1f))
                        {
                            bandPass.GainLF = gainLF;
                            filter.UpdateFilter();
                        }

                        float gainHF = bandPass.GainHF;
                        if (ImGui.SliderFloat("Gain HF", ref gainHF, 0f, 1f))
                        {
                            bandPass.GainHF = gainHF;
                            filter.UpdateFilter();
                        }
                    }
                    break;
            }
        }
    }
}
