using System;
using System.Numerics;
using ImGuiNET;

namespace Engine.UI.ImGuiMenu
{
    /// <summary>
    /// Panel Paramètres - Options de jeu
    /// </summary>
    public class SettingsPanel
    {
        // Test settings
        private float _masterVolume = 0.8f;
        private float _musicVolume = 0.6f;
        private float _sfxVolume = 0.7f;
        private int _graphicsQuality = 2; // 0=Low, 1=Medium, 2=High, 3=Ultra
        private bool _vsync = true;
        private bool _fullscreen = false;
        private int _resolutionIndex = 2;
        private float _mouseSensitivity = 1.0f;
        private bool _showFps = true;
        private bool _subtitles = true;
        private int _language = 0; // 0=FR, 1=EN
        
        private readonly string[] _resolutions = new[]
        {
            "1280x720",
            "1600x900",
            "1920x1080",
            "2560x1440",
            "3840x2160"
        };
        
        private readonly string[] _qualityLevels = new[]
        {
            "Bas",
            "Moyen",
            "Élevé",
            "Ultra"
        };
        
        private readonly string[] _languages = new[]
        {
            "Français",
            "English"
        };
        
        public void Render()
        {
            // Tabs for settings categories
            if (ImGui.BeginTabBar("##SettingsTabs"))
            {
                if (ImGui.BeginTabItem("🔊 Audio"))
                {
                    RenderAudioSettings();
                    ImGui.EndTabItem();
                }
                
                if (ImGui.BeginTabItem("🎨 Graphiques"))
                {
                    RenderGraphicsSettings();
                    ImGui.EndTabItem();
                }
                
                if (ImGui.BeginTabItem("🎮 Contrôles"))
                {
                    RenderControlsSettings();
                    ImGui.EndTabItem();
                }
                
                if (ImGui.BeginTabItem("⚙️ Gameplay"))
                {
                    RenderGameplaySettings();
                    ImGui.EndTabItem();
                }
                
                ImGui.EndTabBar();
            }
            
            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();
            
            // Action buttons
            if (ImGui.Button("Appliquer", new Vector2(150, 40)))
            {
                ApplySettings();
            }
            
            ImGui.SameLine();
            
            if (ImGui.Button("Par défaut", new Vector2(150, 40)))
            {
                ResetToDefaults();
            }
        }
        
        private void RenderAudioSettings()
        {
            ImGui.Spacing();
            
            if (RPGTheme.SectionHeader("🔊 Volume"))
            {
                ImGui.Indent();
                
                ImGui.Text("Volume principal");
                ImGui.SliderFloat("##MasterVolume", ref _masterVolume, 0f, 1f, $"{_masterVolume * 100:F0}%%");
                
                ImGui.Spacing();
                
                ImGui.Text("Musique");
                ImGui.SliderFloat("##MusicVolume", ref _musicVolume, 0f, 1f, $"{_musicVolume * 100:F0}%%");
                
                ImGui.Spacing();
                
                ImGui.Text("Effets sonores");
                ImGui.SliderFloat("##SfxVolume", ref _sfxVolume, 0f, 1f, $"{_sfxVolume * 100:F0}%%");
                
                ImGui.Unindent();
            }
        }
        
        private void RenderGraphicsSettings()
        {
            ImGui.Spacing();
            
            if (RPGTheme.SectionHeader("🎨 Qualité"))
            {
                ImGui.Indent();
                
                ImGui.Text("Qualité graphique");
                ImGui.Combo("##GraphicsQuality", ref _graphicsQuality, _qualityLevels, _qualityLevels.Length);
                
                ImGui.Spacing();
                
                ImGui.Text("Résolution");
                ImGui.Combo("##Resolution", ref _resolutionIndex, _resolutions, _resolutions.Length);
                
                ImGui.Spacing();
                
                ImGui.Checkbox("Synchronisation verticale (VSync)", ref _vsync);
                ImGui.Checkbox("Plein écran", ref _fullscreen);
                
                ImGui.Unindent();
            }
            
            ImGui.Spacing();
            
            if (RPGTheme.SectionHeader("📊 Affichage"))
            {
                ImGui.Indent();
                
                ImGui.Checkbox("Afficher les FPS", ref _showFps);
                ImGui.Checkbox("Sous-titres", ref _subtitles);
                
                ImGui.Unindent();
            }
        }
        
        private void RenderControlsSettings()
        {
            ImGui.Spacing();
            
            if (RPGTheme.SectionHeader("🖱️ Souris"))
            {
                ImGui.Indent();
                
                ImGui.Text("Sensibilité de la souris");
                ImGui.SliderFloat("##MouseSensitivity", ref _mouseSensitivity, 0.1f, 2.0f, $"{_mouseSensitivity:F2}");
                
                ImGui.Unindent();
            }
            
            ImGui.Spacing();
            
            if (RPGTheme.SectionHeader("⌨️ Raccourcis clavier"))
            {
                ImGui.Indent();
                
                ImGui.TextDisabled("(Configuration des touches - À implémenter)");
                
                RenderKeyBinding("Avancer", "Z");
                RenderKeyBinding("Reculer", "S");
                RenderKeyBinding("Gauche", "Q");
                RenderKeyBinding("Droite", "D");
                RenderKeyBinding("Sauter", "Espace");
                RenderKeyBinding("Attaque", "Clic gauche");
                RenderKeyBinding("Menu", "F10");
                
                ImGui.Unindent();
            }
        }
        
        private void RenderGameplaySettings()
        {
            ImGui.Spacing();
            
            if (RPGTheme.SectionHeader("🌍 Langue"))
            {
                ImGui.Indent();
                
                ImGui.Text("Langue du jeu");
                ImGui.Combo("##Language", ref _language, _languages, _languages.Length);
                
                ImGui.Unindent();
            }
            
            ImGui.Spacing();
            
            if (RPGTheme.SectionHeader("🎯 Difficulté"))
            {
                ImGui.Indent();
                
                ImGui.TextDisabled("(Paramètres de difficulté - À implémenter)");
                
                ImGui.Unindent();
            }
        }
        
        private void RenderKeyBinding(string action, string key)
        {
            ImGui.Text(action);
            ImGui.SameLine(200);
            ImGui.TextColored(RPGTheme.TabActive, key);
            
            ImGui.SameLine(ImGui.GetContentRegionAvail().X - 100);
            if (ImGui.SmallButton($"Changer##kb_{action}"))
            {
                Console.WriteLine($"[SettingsPanel] Change key binding for: {action}");
            }
        }
        
        private void ApplySettings()
        {
            Console.WriteLine("[SettingsPanel] Applying settings...");
            Console.WriteLine($"  Master Volume: {_masterVolume}");
            Console.WriteLine($"  Graphics Quality: {_qualityLevels[_graphicsQuality]}");
            Console.WriteLine($"  Resolution: {_resolutions[_resolutionIndex]}");
            Console.WriteLine($"  VSync: {_vsync}");
            Console.WriteLine($"  Fullscreen: {_fullscreen}");
            Console.WriteLine($"  Mouse Sensitivity: {_mouseSensitivity}");
            Console.WriteLine($"  Language: {_languages[_language]}");
        }
        
        private void ResetToDefaults()
        {
            Console.WriteLine("[SettingsPanel] Resetting to defaults...");
            _masterVolume = 0.8f;
            _musicVolume = 0.6f;
            _sfxVolume = 0.7f;
            _graphicsQuality = 2;
            _vsync = true;
            _fullscreen = false;
            _resolutionIndex = 2;
            _mouseSensitivity = 1.0f;
            _showFps = true;
            _subtitles = true;
            _language = 0;
        }
    }
}
