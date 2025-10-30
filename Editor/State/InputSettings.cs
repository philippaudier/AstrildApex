using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using OpenTK.Windowing.GraphicsLibraryFramework;
using Engine.Input;

namespace Editor.State
{
    /// <summary>
    /// Persistent input settings for key bindings
    /// </summary>
    public static class InputSettings
    {
        private static readonly string _settingsPath = Path.Combine(ProjectPaths.ProjectRoot, "ProjectSettings", "InputSettings.json");
        
        private class Settings
        {
            public Dictionary<string, List<KeyBindingData>> ActionBindings { get; set; } = new();
            public Dictionary<string, Dictionary<string, List<KeyBindingData>>> ActionMaps { get; set; } = new();
        }
        
        public class KeyBindingData
        {
            public string Path { get; set; } = "";
            public string Type { get; set; } = "";
            public string Key { get; set; } = "";
            public string MouseButton { get; set; } = "";
            public string MouseAxis { get; set; } = "";
            public float Scale { get; set; } = 1f;
            public List<string> Modifiers { get; set; } = new(); // Support des modificateurs
        }
        
        private static Settings? _currentSettings;
        
        public static void LoadSettings()
        {
            try
            {
                if (File.Exists(_settingsPath))
                {
                    var json = File.ReadAllText(_settingsPath);
                    _currentSettings = JsonSerializer.Deserialize<Settings>(json) ?? new Settings();
                    
                    // Migration: si on a l'ancien format ActionBindings mais pas ActionMaps, migrer
                    if (_currentSettings.ActionBindings.Count > 0 && _currentSettings.ActionMaps.Count == 0)
                    {
                        // Migrating settings to new multi-map structure
                        _currentSettings.ActionMaps["Player"] = new Dictionary<string, List<KeyBindingData>>(_currentSettings.ActionBindings);
                        _currentSettings.ActionBindings.Clear(); // Nettoyer l'ancien format
                        SaveSettings(); // Sauvegarder le nouveau format
                    }
                }
                else
                {
                    _currentSettings = new Settings();
                    SetDefaultBindings();
                    SaveSettings();
                }
            }
            catch
            {
                _currentSettings = new Settings();
                SetDefaultBindings();
            }
        }
        
        public static void SaveSettings()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_settingsPath) ?? "");
                var json = JsonSerializer.Serialize(_currentSettings, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_settingsPath, json);
            }
            catch
            {
                // Silent fail for settings save
            }
        }
        
        private static void SetDefaultBindings()
        {
            if (_currentSettings == null) return;
            
            _currentSettings.ActionBindings.Clear();
            _currentSettings.ActionMaps.Clear();
            
            // Créer les bindings par défaut pour la map "Player"
            var playerBindings = new Dictionary<string, List<KeyBindingData>>();
            
            // Mouvement
            playerBindings["MoveForward"] = new List<KeyBindingData>
            {
                new() { Type = "Key", Key = "W", Path = "<Keyboard>/W" }
            };
            playerBindings["MoveBackward"] = new List<KeyBindingData>
            {
                new() { Type = "Key", Key = "S", Path = "<Keyboard>/S" }
            };
            playerBindings["MoveLeft"] = new List<KeyBindingData>
            {
                new() { Type = "Key", Key = "A", Path = "<Keyboard>/A" }
            };
            playerBindings["MoveRight"] = new List<KeyBindingData>
            {
                new() { Type = "Key", Key = "D", Path = "<Keyboard>/D" }
            };
            
            // Saut
            playerBindings["Jump"] = new List<KeyBindingData>
            {
                new() { Type = "Key", Key = "Space", Path = "<Keyboard>/Space" }
            };
            
            // Caméra - Increased scale for more responsive rotation
            playerBindings["LookX"] = new List<KeyBindingData>
            {
                new() { Type = "MouseAxis", MouseAxis = "X", Scale = 1.0f, Path = "<Mouse>/X" }
            };
            playerBindings["LookY"] = new List<KeyBindingData>
            {
                new() { Type = "MouseAxis", MouseAxis = "Y", Scale = 1.0f, Path = "<Mouse>/Y" }
            };
            
            _currentSettings.ActionMaps["Player"] = playerBindings;
            
            // Créer d'autres ActionMaps par défaut
            SetDefaultVehicleBindings();
            SetDefaultMenuBindings();
        }
        
        private static void SetDefaultVehicleBindings()
        {
            if (_currentSettings == null) return;
            
            var vehicleBindings = new Dictionary<string, List<KeyBindingData>>();
            
            vehicleBindings["Accelerate"] = new List<KeyBindingData>
            {
                new() { Type = "Key", Key = "Up", Path = "<Keyboard>/Up" }
            };
            vehicleBindings["Brake"] = new List<KeyBindingData>
            {
                new() { Type = "Key", Key = "Down", Path = "<Keyboard>/Down" }
            };
            vehicleBindings["SteerLeft"] = new List<KeyBindingData>
            {
                new() { Type = "Key", Key = "Left", Path = "<Keyboard>/Left" }
            };
            vehicleBindings["SteerRight"] = new List<KeyBindingData>
            {
                new() { Type = "Key", Key = "Right", Path = "<Keyboard>/Right" }
            };
            vehicleBindings["ExitVehicle"] = new List<KeyBindingData>
            {
                new() { Type = "Key", Key = "F", Path = "<Keyboard>/F" }
            };
            
            _currentSettings.ActionMaps["Vehicle"] = vehicleBindings;
        }
        
        private static void SetDefaultMenuBindings()
        {
            if (_currentSettings == null) return;
            
            var menuBindings = new Dictionary<string, List<KeyBindingData>>();
            
            menuBindings["Navigate"] = new List<KeyBindingData>
            {
                new() { Type = "Key", Key = "Up", Path = "<Keyboard>/Up" },
                new() { Type = "Key", Key = "Down", Path = "<Keyboard>/Down" },
                new() { Type = "Key", Key = "Left", Path = "<Keyboard>/Left" },
                new() { Type = "Key", Key = "Right", Path = "<Keyboard>/Right" }
            };
            menuBindings["Confirm"] = new List<KeyBindingData>
            {
                new() { Type = "Key", Key = "Enter", Path = "<Keyboard>/Enter" }
            };
            menuBindings["Cancel"] = new List<KeyBindingData>
            {
                new() { Type = "Key", Key = "Escape", Path = "<Keyboard>/Escape" }
            };
            menuBindings["Pause"] = new List<KeyBindingData>
            {
                new() { Type = "Key", Key = "Escape", Path = "<Keyboard>/Escape" }
            };
            
            _currentSettings.ActionMaps["Menu"] = menuBindings;
        }
        
        public static void ApplySettingsToInputManager()
        {
            LoadSettings();
            if (_currentSettings == null || InputManager.Instance == null) return;
            
            // Appliquer les bindings pour toutes les ActionMaps
            foreach (var mapEntry in _currentSettings.ActionMaps)
            {
                string mapName = mapEntry.Key;
                var bindingsDict = mapEntry.Value;
                
                var actionMap = InputManager.Instance.FindActionMap(mapName);
                if (actionMap == null)
                {
                    actionMap = InputManager.Instance.CreateActionMap(mapName);
                    // Created new ActionMap: {mapName}
                }
                
                foreach (var actionEntry in bindingsDict)
                {
                    string actionName = actionEntry.Key;
                    var bindingList = actionEntry.Value;
                    
                    var action = actionMap.FindAction(actionName);
                    if (action == null)
                    {
                        action = actionMap.CreateAction(actionName);
                    }
                    
                    action.ClearBindings();
                    foreach (var bindData in bindingList)
                    {
                        var binding = CreateBindingFromData(bindData);
                        if (binding != null)
                        {
                            action.AddBinding(binding);
                        }
                    }
                }
                
                // Applied {bindingsDict.Count} actions to ActionMap '{mapName}'
            }
            
            // Support pour l'ancien format (backward compatibility)
            if (_currentSettings.ActionBindings.Count > 0)
            {
                var playerMap = InputManager.Instance.FindActionMap("Player");
                if (playerMap == null)
                {
                    playerMap = InputManager.Instance.CreateActionMap("Player");
                }
                
                foreach (var actionBinding in _currentSettings.ActionBindings)
                {
                    var action = playerMap.FindAction(actionBinding.Key);
                    if (action == null)
                    {
                        action = playerMap.CreateAction(actionBinding.Key);
                    }
                    
                    action.ClearBindings();
                    foreach (var bindingData in actionBinding.Value)
                    {
                        var binding = CreateBindingFromData(bindingData);
                        if (binding != null)
                        {
                            action.AddBinding(binding);
                        }
                    }
                }
            }
        }
        
    internal static InputBinding? CreateBindingFromData(KeyBindingData data)
        {
            // Normalize fields
            string keyStr = (data.Key ?? string.Empty).Trim();
            string btnStr = (data.MouseButton ?? string.Empty).Trim();
            string axisStr = (data.MouseAxis ?? string.Empty).Trim();
            string path = (data.Path ?? string.Empty).Trim();

            // Try to infer type from Path if Type is empty
            string type = (data.Type ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(type) && !string.IsNullOrEmpty(path))
            {
                if (path.StartsWith("<Keyboard>", StringComparison.OrdinalIgnoreCase)) type = "Key";
                else if (path.StartsWith("<Mouse>", StringComparison.OrdinalIgnoreCase))
                {
                    // Axis vs Button: look at token after /
                    var token = ExtractLastToken(path);
                    if (IsMouseAxisToken(token)) type = "MouseAxis"; else type = "MouseButton";
                }
            }

            // Parse modifiers from data or extract from key/button string
            var modifiers = new List<Keys>();
            if (data.Modifiers?.Any() == true)
            {
                foreach (var modStr in data.Modifiers)
                {
                    if (TryParseKey(modStr, out var modKey))
                    {
                        modifiers.Add(modKey);
                    }
                }
            }
            else
            {
                // Try parsing from composite key string (legacy support)
                var compositeKey = keyStr.Contains('+') ? keyStr : btnStr.Contains('+') ? btnStr : "";
                if (!string.IsNullOrEmpty(compositeKey))
                {
                    var parts = compositeKey.Split('+');
                    if (parts.Length > 1)
                    {
                        for (int i = 0; i < parts.Length - 1; i++)
                        {
                            if (TryParseKey(parts[i].Trim(), out var modKey))
                            {
                                modifiers.Add(modKey);
                            }
                        }
                        // Le dernier élément est la touche principale
                        keyStr = parts[parts.Length - 1].Trim();
                        btnStr = parts[parts.Length - 1].Trim();
                    }
                }
            }

            switch (type)
            {
                case "Key":
                {
                    if (string.IsNullOrEmpty(keyStr) && !string.IsNullOrEmpty(path)) 
                    {
                        var token = ExtractLastToken(path);
                        if (token.Contains('+'))
                        {
                            var parts = token.Split('+');
                            keyStr = parts[parts.Length - 1];
                        }
                        else
                        {
                            keyStr = token;
                        }
                    }
                    if (TryParseKey(keyStr, out var key)) 
                        return InputBinding.FromKey(key, modifiers);
                    return null;
                }
                case "MouseButton":
                {
                    if (string.IsNullOrEmpty(btnStr) && !string.IsNullOrEmpty(path))
                    {
                        var token = ExtractLastToken(path);
                        if (token.Contains('+'))
                        {
                            var parts = token.Split('+');
                            btnStr = parts[parts.Length - 1];
                        }
                        else
                        {
                            btnStr = token;
                        }
                    }
                    if (TryParseMouseButton(btnStr, out var mb)) 
                        return InputBinding.FromMouseButton(mb, modifiers);
                    return null;
                }
                case "MouseAxis":
                {
                    if (string.IsNullOrEmpty(axisStr) && !string.IsNullOrEmpty(path)) axisStr = ExtractLastToken(path);
                    if (TryParseMouseAxis(axisStr, out var ma)) return InputBinding.FromMouseAxis(ma, data.Scale);
                    return null;
                }
                default:
                    return null;
            }
        }

        private static string ExtractLastToken(string path)
        {
            int idx = path.LastIndexOf('/') + 1;
            if (idx <= 0 || idx >= path.Length) return path;
            return path.Substring(idx);
        }

        private static bool IsMouseAxisToken(string token)
        {
            if (string.IsNullOrEmpty(token)) return false;
            token = token.Trim();
            return token.Equals("X", StringComparison.OrdinalIgnoreCase)
                || token.Equals("Y", StringComparison.OrdinalIgnoreCase)
                || token.Equals("ScrollX", StringComparison.OrdinalIgnoreCase)
                || token.Equals("ScrollY", StringComparison.OrdinalIgnoreCase);
        }

        private static bool TryParseKey(string s, out Keys key)
        {
            key = Keys.Unknown;
            if (string.IsNullOrWhiteSpace(s)) return false;
            s = s.Trim();

            // Common synonyms normalization
            switch (s.ToLowerInvariant())
            {
                case "spacebar": s = "Space"; break;
                case "return": s = "Enter"; break;
                case "escape": s = "Escape"; break;
                case "ctl":
                case "ctrl": s = "LeftControl"; break; // default to left
                case "alt": s = "LeftAlt"; break;
                case "shift": s = "LeftShift"; break;
                case "pageup": s = "PageUp"; break;
                case "pagedown": s = "PageDown"; break;
                case "plus": s = "Equal"; break; // usually Shift+'='
                case "minus": s = "Minus"; break;
                case "tilde":
                case "grave":
                case "backquote": s = "GraveAccent"; break;
                case "apostrophe": s = "Apostrophe"; break;
                case "semicolon": s = "Semicolon"; break;
                case "backspace": s = "Backspace"; break;
                case "tab": s = "Tab"; break;
                case "capslock": s = "CapsLock"; break;
                case "insert": s = "Insert"; break;
                case "del": s = "Delete"; break;
                case "prtsc": s = "PrintScreen"; break;
                case "num+": s = "KeyPadAdd"; break;
                case "num-": s = "KeyPadSubtract"; break;
            }

            // Digits normalization (e.g., "1" -> Number1)
            if (s.Length == 1 && char.IsDigit(s[0]))
            {
                s = "Number" + s;
            }

            return Enum.TryParse<Keys>(s, ignoreCase: true, out key);
        }

        private static bool TryParseMouseButton(string s, out MouseButton button)
        {
            button = MouseButton.Left;
            if (string.IsNullOrWhiteSpace(s)) return false;
            s = s.Trim();

            // Synonyms
            switch (s.ToLowerInvariant())
            {
                case "lmb":
                case "left": s = "Left"; break;
                case "rmb":
                case "right": s = "Right"; break;
                case "mmb":
                case "middle": s = "Middle"; break;
            }
            return Enum.TryParse<MouseButton>(s, ignoreCase: true, out button);
        }

        private static bool TryParseMouseAxis(string s, out MouseAxis axis)
        {
            axis = MouseAxis.X;
            if (string.IsNullOrWhiteSpace(s)) return false;
            s = s.Trim();
            // Accept lowercase
            return Enum.TryParse<MouseAxis>(s, ignoreCase: true, out axis);
        }
        
        public static Dictionary<string, List<KeyBindingData>> GetAllBindings()
        {
            // Backward compatibility - return Player map bindings
            return GetActionMap("Player") ?? new Dictionary<string, List<KeyBindingData>>();
        }
        
        public static Dictionary<string, Dictionary<string, List<KeyBindingData>>> GetAllActionMaps()
        {
            LoadSettings();
            return _currentSettings?.ActionMaps ?? new Dictionary<string, Dictionary<string, List<KeyBindingData>>>();
        }
        
        public static void SetActionBinding(string actionName, List<KeyBindingData> bindings)
        {
            // Backward compatibility - set in Player map
            SetActionBinding("Player", actionName, bindings);
        }
        
        public static void SetActionBinding(string mapName, string actionName, List<KeyBindingData> bindings)
        {
            LoadSettings();
            if (_currentSettings == null) return;
            
            if (!_currentSettings.ActionMaps.ContainsKey(mapName))
            {
                _currentSettings.ActionMaps[mapName] = new Dictionary<string, List<KeyBindingData>>();
            }
            
            _currentSettings.ActionMaps[mapName][actionName] = bindings;
            SaveSettings();
            ApplySettingsToInputManager();
        }
        
        public static List<KeyBindingData>? GetActionBinding(string mapName, string actionName)
        {
            LoadSettings();
            if (_currentSettings == null) return null;
            
            if (_currentSettings.ActionMaps.TryGetValue(mapName, out var map) &&
                map.TryGetValue(actionName, out var bindings))
            {
                return bindings;
            }
            
            return null;
        }
        
        public static Dictionary<string, List<KeyBindingData>>? GetActionMap(string mapName)
        {
            LoadSettings();
            if (_currentSettings == null) return null;
            
            return _currentSettings.ActionMaps.TryGetValue(mapName, out var map) ? map : null;
        }
        
        public static string[] GetAvailableActionMaps()
        {
            LoadSettings();
            if (_currentSettings == null) return new string[0];
            
            return _currentSettings.ActionMaps.Keys.ToArray();
        }
    }
}