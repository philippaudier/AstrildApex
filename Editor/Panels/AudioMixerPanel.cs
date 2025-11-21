using System;
using System.Numerics;
using ImGuiNET;
using Engine.Audio.Mixing;
using Engine.Audio.Core;

namespace Editor.Panels
{
    /// <summary>
    /// Panneau de mixage audio visuel pour l'éditeur
    /// Layout horizontal: VU meter | Nom | Slider | Valeur | Mute/Solo
    /// </summary>
    public class AudioMixerPanel
    {
        private AudioMixer? _mixer;
        private bool _isOpen = true;

        // Track actual audio levels (would need to be fed from audio engine)
        private System.Collections.Generic.Dictionary<string, float> _currentLevels = new();

        public AudioMixerPanel()
        {
            // Créer un mixer par défaut et le connecter à AudioEngine
            _mixer = new AudioMixer("Main Mixer");

            // Créer les groupes de base
            _mixer.CreateGroup("Music");
            _mixer.CreateGroup("SFX");
            _mixer.CreateGroup("Voice");
            _mixer.CreateGroup("Ambient");
            
            // Connecter le mixer à AudioEngine
            Engine.Audio.Core.AudioEngine.Instance.Mixer = _mixer;

            // Initialize levels
            _currentLevels["Master"] = 0f;
            _currentLevels["Music"] = 0f;
            _currentLevels["SFX"] = 0f;
            _currentLevels["Voice"] = 0f;
            _currentLevels["Ambient"] = 0f;
        }

        public void Draw()
        {
            if (!_isOpen)
                return;

            ImGui.SetNextWindowSize(new Vector2(900, 400), ImGuiCond.FirstUseEver);

            if (ImGui.Begin("Audio Mixer", ref _isOpen))
            {
                if (_mixer == null)
                {
                    ImGui.Text("No mixer loaded");
                    ImGui.End();
                    return;
                }
                
                // Mettre à jour les niveaux en temps réel
                UpdateLevelsFromEngine();

                // Toolbar with Master Volume
                DrawToolbar();

                ImGui.Separator();
                ImGui.Spacing();

                // Mixer view - horizontal layout
                ImGui.BeginChild("MixerView", new Vector2(0, -30));
                DrawMixerGroupsHorizontal();
                ImGui.EndChild();

                // Status bar
                DrawStatusBar();
            }

            ImGui.End();
        }

        private void DrawToolbar()
        {
            if (ImGui.Button("Add Group"))
            {
                ImGui.OpenPopup("AddGroupPopup");
            }

            if (ImGui.BeginPopup("AddGroupPopup"))
            {
                ImGui.Text("Group Name:");
                // TODO: Input text pour le nom
                ImGui.EndPopup();
            }

            ImGui.SameLine();
            ImGui.Spacing();
            ImGui.SameLine();

            // Volume principal (use AudioEngine property so value is consistent)
            float masterVolume = AudioEngine.Instance.MasterVolume;
            ImGui.Text("Master Volume:");
            ImGui.SameLine();
            ImGui.SetNextItemWidth(200);
            // Allow master volume up to 10x
            if (ImGui.SliderFloat("##MasterVolume", ref masterVolume, 0f, 10f, $"{masterVolume:F2}x"))
            {
                AudioEngine.Instance.MasterVolume = masterVolume;
                Serilog.Log.Debug($"[AudioMixerPanel] Toolbar MasterVolume changed -> {masterVolume}");
            }
        }

        private void DrawMixerGroupsHorizontal()
        {
            if (_mixer == null)
                return;

            var groups = _mixer.GetAllGroups();

            // Header row
            ImGui.Columns(7, "MixerHeader", true);
            ImGui.SetColumnWidth(0, 60);  // VU Meter
            ImGui.SetColumnWidth(1, 100); // Name
            ImGui.SetColumnWidth(2, 200); // Slider
            ImGui.SetColumnWidth(3, 60);  // Value
            ImGui.SetColumnWidth(4, 40);  // Mute
            ImGui.SetColumnWidth(5, 40);  // Solo
            ImGui.SetColumnWidth(6, 60);  // Effects

            ImGui.Text("Level");
            ImGui.NextColumn();
            ImGui.Text("Group");
            ImGui.NextColumn();
            ImGui.Text("Volume");
            ImGui.NextColumn();
            ImGui.Text("Value");
            ImGui.NextColumn();
            ImGui.Text("M");
            ImGui.NextColumn();
            ImGui.Text("S");
            ImGui.NextColumn();
            ImGui.Text("FX");
            ImGui.NextColumn();

            ImGui.Separator();

            // Master group first
            DrawGroupRowHorizontal("MasterGlobal", "Master", AudioEngine.Instance.MasterVolume, AudioEngine.Instance.MasterMuted,
                (vol) => { AudioEngine.Instance.MasterVolume = vol; },
                (mute) => { AudioEngine.Instance.MasterMuted = mute; },
                (solo) => {
                    // Toggle solo on master - if solo on master, other groups muted
                    var mg = _mixer?.GetGroup("Master");
                    if (mg != null) mg.Solo = solo;
                },
                _mixer?.GetGroup("Master"),
                10f);

            // Then all other groups (skip "Master" group if it exists to avoid duplication)
            foreach (var group in groups)
            {
                if (group.Name.Equals("Master", System.StringComparison.OrdinalIgnoreCase))
                    continue; // Skip Master group as we already drew the global master volume

                DrawGroupRowHorizontal(
                    $"Group_{group.Name}",
                    group.Name,
                    group.Volume,
                    group.Mute,
                    (vol) =>
                    {
                        group.Volume = vol;
                        // Synchroniser avec AudioSettings
                        SyncGroupToSettings(group.Name, vol);
                    },
                    (mute) => { group.Mute = mute; },
                    (solo) => { group.Solo = solo; },
                    group
                );
            }

            ImGui.Columns(1);
        }

        private void DrawGroupRowHorizontal(
            string uniqueId,
            string displayName,
            float volume,
            bool mute,
            Action<float>? setVolume,
            Action<bool>? setMute,
            Action<bool>? setSolo,
            AudioMixerGroup? mixerGroup,
            float maxVolume = 1f)

        {
            ImGui.PushID(uniqueId);

            // Get current audio level for this group (0 if nothing playing)
            float currentLevel = _currentLevels.TryGetValue(displayName, out var lvl) ? lvl : 0f;

            // Column 1: Horizontal VU Meter
            DrawHorizontalVUMeter(currentLevel, volume, mute);
            ImGui.NextColumn();

            // Column 2: Group Name
            ImGui.AlignTextToFramePadding();
            ImGui.Text(displayName);
            ImGui.NextColumn();

            // Column 3: Volume Slider (horizontal)
            ImGui.SetNextItemWidth(-1);
            float vol = volume;
            // Use a unique slider ID per row to avoid ImGui ID collisions
            string sliderId = $"##vol_{uniqueId}";
            if (ImGui.SliderFloat(sliderId, ref vol, 0f, maxVolume, ""))
            {
                setVolume?.Invoke(vol);
                Serilog.Log.Debug($"[AudioMixerPanel] Group '{displayName}' volume changed -> {vol}");
            }
            ImGui.NextColumn();

            // Column 4: Volume Value
            ImGui.AlignTextToFramePadding();
            // Display percent for typical group volumes, but show multiplier for master when >1
            if (volume > 1f)
            {
                ImGui.Text($"{volume:F2}x");
            }
            else
            {
                ImGui.Text($"{(volume * 100):F0}%");
            }
            ImGui.NextColumn();

            // Column 5: Mute Button
            bool muteVal = mute;
            if (setMute != null)
            {
                if (ImGui.Checkbox("##mute", ref muteVal))
                {
                    setMute(muteVal);
                }
            }
            else
            {
                ImGui.BeginDisabled();
                ImGui.Checkbox("##mute", ref muteVal);
                ImGui.EndDisabled();
            }
            ImGui.NextColumn();

            // Column 6: Solo Button
            bool soloVal = false;
            if (setSolo != null)
            {
                // Initialize checkbox from current mixer group state if available
                var group = _mixer?.GetGroup(displayName);
                soloVal = group?.Solo ?? false;
                if (ImGui.Checkbox("##solo", ref soloVal))
                {
                    setSolo(soloVal);
                }
            }
            else
            {
                ImGui.BeginDisabled();
                ImGui.Checkbox("##solo", ref soloVal);
                ImGui.EndDisabled();
            }
            ImGui.NextColumn();

            // Column 7: Effects Button
            if (mixerGroup != null)
            {
                if (ImGui.SmallButton("FX"))
                {
                    ImGui.OpenPopup($"GroupEffects_{uniqueId}");
                }

                if (ImGui.BeginPopup($"GroupEffects_{uniqueId}"))
                {
                    DrawGroupEffectsPopup(mixerGroup);
                    ImGui.EndPopup();
                }
            }
            else
            {
                ImGui.BeginDisabled();
                ImGui.SmallButton("FX");
                ImGui.EndDisabled();
            }
            ImGui.NextColumn();

            ImGui.PopID();
        }

        private void DrawHorizontalVUMeter(float currentLevel, float volume, bool mute)
        {
            var drawList = ImGui.GetWindowDrawList();
            var pos = ImGui.GetCursorScreenPos();
            var size = new Vector2(50, 20); // Horizontal bar: 50px wide, 20px tall

            // Background
            drawList.AddRectFilled(pos, new Vector2(pos.X + size.X, pos.Y + size.Y),
                ImGui.GetColorU32(new Vector4(0.1f, 0.1f, 0.1f, 1f)));

            // Calculate effective level considering volume and mute
            float effectiveLevel = mute ? 0f : currentLevel * volume;

            if (effectiveLevel > 0.01f)
            {
                float barWidth = size.X * effectiveLevel;

                // Color gradient: green -> yellow -> red
                uint barColor;
                if (effectiveLevel < 0.7f)
                    barColor = ImGui.GetColorU32(new Vector4(0.2f, 0.8f, 0.2f, 1f)); // Green
                else if (effectiveLevel < 0.9f)
                    barColor = ImGui.GetColorU32(new Vector4(0.9f, 0.9f, 0.2f, 1f)); // Yellow
                else
                    barColor = ImGui.GetColorU32(new Vector4(0.9f, 0.2f, 0.2f, 1f)); // Red

                drawList.AddRectFilled(pos, new Vector2(pos.X + barWidth, pos.Y + size.Y), barColor);
            }

            // Border
            drawList.AddRect(pos, new Vector2(pos.X + size.X, pos.Y + size.Y),
                ImGui.GetColorU32(new Vector4(0.5f, 0.5f, 0.5f, 1f)));

            // Level markers (vertical lines at 25%, 50%, 75%)
            for (int i = 1; i <= 3; i++)
            {
                float markerX = pos.X + (size.X * i / 4);
                drawList.AddLine(
                    new Vector2(markerX, pos.Y),
                    new Vector2(markerX, pos.Y + size.Y),
                    ImGui.GetColorU32(new Vector4(0.3f, 0.3f, 0.3f, 1f)),
                    1.0f
                );
            }

            ImGui.Dummy(size);
        }

        private void DrawStatusBar()
        {
            ImGui.Separator();

            var settings = AudioEngine.Instance.Settings;

            // Display master as percent when <=1, otherwise show multiplier
            var masterVal = AudioEngine.Instance.MasterVolume;
            if (masterVal > 1f)
                ImGui.Text($"Master: {masterVal:F2}x ({masterVal * 100:F0}%)");
            else
                ImGui.Text($"Master: {masterVal * 100:F0}%");
            ImGui.SameLine();

            ImGui.Text($"| Music: {settings.MusicVolume * 100:F0}%");
            ImGui.SameLine();

            ImGui.Text($"| SFX: {settings.SFXVolume * 100:F0}%");
            ImGui.SameLine();

            ImGui.Text($"| Voice: {settings.VoiceVolume * 100:F0}%");
        }

        /// <summary>
        /// Update current audio levels from audio engine (call this from update loop)
        /// For now, levels are 0 unless audio is actually playing
        /// </summary>
        public void UpdateLevels(string groupName, float level)
        {
            _currentLevels[groupName] = Math.Clamp(level, 0f, 1f);
        }
        
        /// <summary>
        /// Synchronise les changements de volume d'un groupe avec AudioSettings
        /// </summary>
        private void SyncGroupToSettings(string groupName, float volume)
        {
            var settings = AudioEngine.Instance.Settings;
            switch (groupName)
            {
                case "Music":
                    settings.MusicVolume = volume;
                    break;
                case "SFX":
                    settings.SFXVolume = volume;
                    break;
                case "Voice":
                    settings.VoiceVolume = volume;
                    break;
                case "Ambient":
                    settings.MusicVolume = volume; // Utiliser le même que Music
                    break;
            }
        }
        
        /// <summary>
        /// Met à jour les niveaux audio depuis AudioEngine en temps réel
        /// </summary>
        private void UpdateLevelsFromEngine()
        {
            // Compute per-category levels
            _currentLevels["Music"] = AudioEngine.Instance.GetCategoryLevel(Engine.Audio.Components.AudioCategory.Music);
            _currentLevels["SFX"] = AudioEngine.Instance.GetCategoryLevel(Engine.Audio.Components.AudioCategory.SFX);
            _currentLevels["Voice"] = AudioEngine.Instance.GetCategoryLevel(Engine.Audio.Components.AudioCategory.Voice);
            _currentLevels["Ambient"] = AudioEngine.Instance.GetCategoryLevel(Engine.Audio.Components.AudioCategory.Ambient);

            // Master level: use simple average of all categories (could be improved)
            float sum = 0f;
            int count = 0;
            foreach (var key in new[] { "Music", "SFX", "Voice", "Ambient" })
            {
                if (_currentLevels.TryGetValue(key, out var v))
                {
                    sum += v;
                    count++;
                }
            }
            _currentLevels["Master"] = count > 0 ? Math.Clamp(sum / count, 0f, 1f) : 0f;
        }

        /// <summary>
        /// Draws the effects popup for a mixer group
        /// </summary>
        private void DrawGroupEffectsPopup(AudioMixerGroup group)
        {
            ImGui.Text($"Effects for {group.Name}");
            ImGui.Separator();

            // Check if EFX is supported
            if (!Engine.Audio.Effects.AudioEfxBackend.Instance.IsEFXSupported)
            {
                ImGui.TextDisabled("EFX not supported");
                return;
            }

            // Button to add effect
            if (ImGui.Button("Add Effect"))
            {
                ImGui.OpenPopup("AddGroupEffectPopup");
            }

            if (ImGui.BeginPopup("AddGroupEffectPopup"))
            {
                ImGui.Text("Select Effect Type:");
                ImGui.Separator();

                if (ImGui.MenuItem("Reverb"))
                {
                    group.AddEffect(Engine.Audio.Mixing.AudioMixerGroupEffectType.Reverb);
                    ImGui.CloseCurrentPopup();
                }

                if (ImGui.MenuItem("Echo"))
                {
                    group.AddEffect(Engine.Audio.Mixing.AudioMixerGroupEffectType.Echo);
                    ImGui.CloseCurrentPopup();
                }

                if (ImGui.MenuItem("Low-Pass Filter"))
                {
                    group.AddEffect(Engine.Audio.Mixing.AudioMixerGroupEffectType.LowPass);
                    ImGui.CloseCurrentPopup();
                }

                if (ImGui.MenuItem("High-Pass Filter"))
                {
                    group.AddEffect(Engine.Audio.Mixing.AudioMixerGroupEffectType.HighPass);
                    ImGui.CloseCurrentPopup();
                }

                ImGui.EndPopup();
            }

            ImGui.SameLine();
            ImGui.TextDisabled($"({group.Effects.Count} effect(s))");

            ImGui.Spacing();

            // Display existing effects
            if (group.Effects.Count > 0)
            {
                Engine.Audio.Mixing.AudioMixerGroupEffect? effectToRemove = null;

                for (int i = 0; i < group.Effects.Count; i++)
                {
                    var effect = group.Effects[i];
                    ImGui.PushID($"GroupEffect_{i}");

                    bool isOpen = ImGui.TreeNode($"{effect.Type}###{i}");

                    ImGui.SameLine();
                    if (ImGui.SmallButton("Remove"))
                    {
                        effectToRemove = effect;
                    }

                    if (isOpen)
                    {
                        bool enabled = effect.Enabled;
                        if (ImGui.Checkbox("Enabled", ref enabled))
                        {
                            effect.Enabled = enabled;
                        }

                        // Draw effect-specific parameters
                        DrawGroupEffectParameters(group, effect);

                        ImGui.TreePop();
                    }

                    ImGui.PopID();
                }

                if (effectToRemove != null)
                {
                    group.RemoveEffect(effectToRemove);
                }
            }
            else
            {
                ImGui.TextDisabled("No effects");
            }
        }

        /// <summary>
        /// Draw parameters for a group effect
        /// </summary>
        private void DrawGroupEffectParameters(AudioMixerGroup group, Engine.Audio.Mixing.AudioMixerGroupEffect effect)
        {
            switch (effect.Type)
            {
                case Engine.Audio.Mixing.AudioMixerGroupEffectType.Reverb:
                    if (effect.Settings is Engine.Audio.Effects.ReverbSettings reverb)
                    {
                        float density = reverb.Density;
                        if (ImGui.SliderFloat("Density", ref density, 0f, 1f))
                        {
                            reverb.Density = density;
                            group.UpdateEffect(effect);
                        }

                        float diffusion = reverb.Diffusion;
                        if (ImGui.SliderFloat("Diffusion", ref diffusion, 0f, 1f))
                        {
                            reverb.Diffusion = diffusion;
                            group.UpdateEffect(effect);
                        }

                        float gain = reverb.Gain;
                        if (ImGui.SliderFloat("Gain", ref gain, 0f, 1f))
                        {
                            reverb.Gain = gain;
                            group.UpdateEffect(effect);
                        }

                        float decayTime = reverb.DecayTime;
                        if (ImGui.SliderFloat("Decay Time", ref decayTime, 0.1f, 20f))
                        {
                            reverb.DecayTime = decayTime;
                            group.UpdateEffect(effect);
                        }
                    }
                    break;

                case Engine.Audio.Mixing.AudioMixerGroupEffectType.LowPass:
                    if (effect.Settings is Engine.Audio.Effects.LowPassSettings lowPass)
                    {
                        float gain = lowPass.Gain;
                        if (ImGui.SliderFloat("Gain", ref gain, 0f, 1f))
                        {
                            lowPass.Gain = gain;
                            group.UpdateEffect(effect);
                        }

                        float gainHF = lowPass.GainHF;
                        if (ImGui.SliderFloat("Gain HF", ref gainHF, 0f, 1f))
                        {
                            lowPass.GainHF = gainHF;
                            group.UpdateEffect(effect);
                        }
                    }
                    break;

                case Engine.Audio.Mixing.AudioMixerGroupEffectType.HighPass:
                    if (effect.Settings is Engine.Audio.Effects.HighPassSettings highPass)
                    {
                        float gain = highPass.Gain;
                        if (ImGui.SliderFloat("Gain", ref gain, 0f, 1f))
                        {
                            highPass.Gain = gain;
                            group.UpdateEffect(effect);
                        }

                        float gainLF = highPass.GainLF;
                        if (ImGui.SliderFloat("Gain LF", ref gainLF, 0f, 1f))
                        {
                            highPass.GainLF = gainLF;
                            group.UpdateEffect(effect);
                        }
                    }
                    break;
            }
        }
    }
}
