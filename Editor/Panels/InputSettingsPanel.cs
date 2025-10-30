using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using ImGuiNET;
using OpenTK.Windowing.GraphicsLibraryFramework;
using Editor.State;
using Engine.Input;

namespace Editor.Panels
{
    /// <summary>
    /// Panel avancé pour configurer les liaisons d'entrée (style Unity)
    /// Support des catégories, détection de conflits, validation temps réel, multiple action maps
    /// </summary>
    public static class InputSettingsPanel
    {
        private static bool _isOpen = false;
        private static Dictionary<string, Dictionary<string, List<InputSettings.KeyBindingData>>> _tempActionMaps = new();
        private static string _selectedActionMap = "Player";
        private static Dictionary<string, List<InputSettings.KeyBindingData>> _tempBindings = new();
        private static Dictionary<string, InputActionCategory> _categories = new();
        private static Dictionary<string, bool> _categoryExpanded = new();
        private static List<InputConflict> _conflicts = new();
        
        private static string _selectedAction = "";
        private static bool _isWaitingForKey = false;
        private static int _bindingIndex = 0;
        private static bool _waitingForModifiers = false;
        private static List<Keys> _capturedModifiers = new();
    // Inline editor state for full editing UX
    private static bool _isEditingBinding = false;
    private static string _editingAction = "";
    private static int _editingIndex = 0;
    private static string _editingType = "Key"; // Key | MouseButton | MouseAxis
    private static Engine.Input.KeyCode _editingKeyCode = Engine.Input.KeyCode.None;
    private static OpenTK.Windowing.GraphicsLibraryFramework.MouseButton _editingMouseButton = OpenTK.Windowing.GraphicsLibraryFramework.MouseButton.Left;
    private static string _editingMouseAxis = "X";
    private static float _editingScale = 1.0f;
    private static bool _editingModCtrl = false;
    private static bool _editingModAlt = false;
    private static bool _editingModShift = false;
        
        private static string _searchFilter = "";
        private static bool _showConflictsOnly = false;
        private static float _conflictBlinkTimer = 0f;
        
        // State for creating new actions
        private static bool _isCreatingNewAction = false;
        private static string _newActionName = "";
        private static string _newActionCategory = "General";

        public static void Open()
        {
            _isOpen = true;
            
            // Charger toutes les ActionMaps
            _tempActionMaps = new Dictionary<string, Dictionary<string, List<InputSettings.KeyBindingData>>>();
            var allActionMaps = InputSettings.GetAllActionMaps();
            foreach (var mapEntry in allActionMaps)
            {
                _tempActionMaps[mapEntry.Key] = new Dictionary<string, List<InputSettings.KeyBindingData>>();
                foreach (var actionEntry in mapEntry.Value)
                {
                    _tempActionMaps[mapEntry.Key][actionEntry.Key] = new List<InputSettings.KeyBindingData>(actionEntry.Value);
                }
            }
            
            // S'assurer qu'on a au moins une ActionMap
            if (!_tempActionMaps.ContainsKey("Player"))
            {
                _tempActionMaps["Player"] = new Dictionary<string, List<InputSettings.KeyBindingData>>();
            }
            
            // Sélectionner la première ActionMap disponible
            _selectedActionMap = _tempActionMaps.Keys.FirstOrDefault() ?? "Player";
            LoadSelectedActionMap();
            
            InitializeCategories();
            DetectConflicts();
            _searchFilter = "";
            _showConflictsOnly = false;
        }
        
        private static void LoadSelectedActionMap()
        {
            if (_tempActionMaps.TryGetValue(_selectedActionMap, out var actionMap))
            {
                _tempBindings = actionMap;
            }
            else
            {
                _tempBindings = new Dictionary<string, List<InputSettings.KeyBindingData>>();
            }
        }
        
        private static void SyncBindingsToActionMaps()
        {
            // Synchroniser les changements de _tempBindings vers _tempActionMaps
            if (_tempActionMaps.ContainsKey(_selectedActionMap))
            {
                _tempActionMaps[_selectedActionMap] = _tempBindings;
            }
        }

        public static void Draw()
        {
            if (!_isOpen) return;
            
            _conflictBlinkTimer += ImGui.GetIO().DeltaTime;

            var viewport = ImGui.GetMainViewport();
            ImGui.SetNextWindowPos(viewport.WorkPos + new Vector2(50, 50), ImGuiCond.FirstUseEver);
            ImGui.SetNextWindowSize(new Vector2(800, 600), ImGuiCond.FirstUseEver);

            if (ImGui.Begin("Input Settings", ref _isOpen, ImGuiWindowFlags.NoCollapse))
            {
                DrawHeader();
                DrawConflictWarnings();
                DrawFilterBar();
                ImGui.Separator();
                DrawInputBindings();
                DrawFooter();
            }
            ImGui.End();

            // Draw inline editor overlay after main window
            if (_isEditingBinding)
            {
                DrawInlineEditor();
            }

            // Draw new action creator overlay
            if (_isCreatingNewAction)
            {
                DrawNewActionDialog();
            }

            // Draw capture overlay after main window
            if (_isWaitingForKey)
            {
                DrawCaptureOverlay();
            }
        }

        private static void DrawHeader()
        {
            ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.9f, 0.95f, 1.0f, 1f));
            ImGui.SetWindowFontScale(1.3f);
            ImGui.Text("🎮 Input Settings");
            ImGui.SetWindowFontScale(1.0f);
            ImGui.PopStyleColor();
            
            ImGui.SameLine(0, 30);
            
            // ActionMap selector
            ImGui.AlignTextToFramePadding();
            ImGui.Text("Action Map:");
            ImGui.SameLine();
            ImGui.SetNextItemWidth(180);
            
            var actionMaps = _tempActionMaps.Keys.ToArray();
            var currentIndex = Array.IndexOf(actionMaps, _selectedActionMap);
            if (currentIndex < 0) currentIndex = 0;
            
            if (ImGui.Combo("##ActionMapCombo", ref currentIndex, actionMaps, actionMaps.Length))
            {
                _selectedActionMap = actionMaps[currentIndex];
                LoadSelectedActionMap();
                DetectConflicts(); // Recalculer les conflits pour la nouvelle map
            }
            
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip($"Switch between different input contexts:\n- Player: On-foot controls\n- Vehicle: Driving controls\n- Menu: UI navigation");
            }
            
            ImGui.SameLine(0, 20);
            if (_conflicts.Any())
            {
                var blinkColor = MathF.Sin(_conflictBlinkTimer * 3f) > 0 ? 
                    new Vector4(1, 0.2f, 0.2f, 1) : new Vector4(1, 0.6f, 0.6f, 1);
                ImGui.PushStyleColor(ImGuiCol.Text, blinkColor);
                ImGui.Text($"⚠️ {_conflicts.Count} conflict{(_conflicts.Count > 1 ? "s" : "")}");
                ImGui.PopStyleColor();
                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip($"{_conflicts.Count} binding conflict(s) detected.\nScroll down to see details.");
                }
            }
            else
            {
                ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.3f, 1f, 0.3f, 1));
                ImGui.Text("✅ No conflicts");
                ImGui.PopStyleColor();
                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip("All bindings are unique - no conflicts!");
                }
            }
        }
        
        private static void DrawConflictWarnings()
        {
            if (!_conflicts.Any()) return;
            
            ImGui.PushStyleColor(ImGuiCol.ChildBg, new Vector4(0.8f, 0.2f, 0.2f, 0.1f));
            if (ImGui.BeginChild("ConflictWarnings", new Vector2(0, 80), ImGuiChildFlags.Borders))
            {
                ImGui.Text("⚠️ Binding Conflicts:");
                foreach (var conflict in _conflicts.Take(3))
                {
                    ImGui.BulletText(conflict.GetDescription());
                }
                if (_conflicts.Count > 3)
                {
                    ImGui.Text($"... and {_conflicts.Count - 3} more");
                }
            }
            ImGui.EndChild();
            ImGui.PopStyleColor();
        }
        
        private static void DrawFilterBar()
        {
            ImGui.Text("Search:");
            ImGui.SameLine();
            ImGui.SetNextItemWidth(200);
            if (ImGui.InputText("##SearchFilter", ref _searchFilter, 100))
            {
                // Filtre en temps réel
            }
            
            ImGui.SameLine();
            ImGui.Checkbox("Show conflicts only", ref _showConflictsOnly);
            
            ImGui.SameLine();
            if (ImGui.Button("Clear"))
            {
                _searchFilter = "";
                _showConflictsOnly = false;
            }
            
            // Add New Action button
            ImGui.SameLine(ImGui.GetWindowWidth() - 180);
            ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.3f, 0.7f, 0.3f, 0.8f));
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.4f, 0.8f, 0.4f, 1.0f));
            ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(0.5f, 0.9f, 0.5f, 1.0f));
            if (ImGui.Button("➕ New Action", new Vector2(150, 0)))
            {
                _isCreatingNewAction = true;
                _newActionName = "";
                _newActionCategory = "General";
            }
            ImGui.PopStyleColor(3);
            
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("Create a new action in the current action map");
            }
        }

        private static void DrawInputBindings()
        {
            if (ImGui.BeginChild("BindingsScroll", new Vector2(0, -50)))
            {
                foreach (var category in _categories.Values.OrderBy(c => c.Name))
                {
                    // Toujours dessiner l'en-tête de catégorie, peu importe le contenu
                    DrawCategoryHeader(category);

                    // Si la catégorie est développée, calculer et afficher le contenu
                    if (_categoryExpanded.TryGetValue(category.Name, out var expanded) && expanded)
                    {
                        var actionsInCategory = category.Actions
                            .Where(action => _tempBindings.ContainsKey(action))
                            .Where(ActionPassesFilter)
                            .Where(action => !_showConflictsOnly || ActionHasConflicts(action))
                            .ToList();

                        // Ajouter les actions sans catégorie à "General"
                        if (category.Name == "General")
                        {
                            var uncategorizedActions = _tempBindings.Keys
                                .Where(action => GetActionCategory(action) == "General")
                                .Where(ActionPassesFilter)
                                .Where(action => !_showConflictsOnly || ActionHasConflicts(action))
                                .Where(action => !category.Actions.Contains(action));
                            actionsInCategory.AddRange(uncategorizedActions);
                        }

                        // Afficher les actions ou un message si aucune action
                        if (actionsInCategory.Any())
                        {
                            foreach (var actionName in actionsInCategory)
                            {
                                DrawActionBindings(actionName, _tempBindings[actionName]);
                            }
                        }
                        else
                        {
                            ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.6f, 0.6f, 0.6f, 1f));
                            ImGui.Text("(No bindings match current filter)");
                            ImGui.PopStyleColor();
                        }
                    }

                    ImGui.Spacing();
                }
            }
            ImGui.EndChild();
        }
        
        private static void DrawCategoryHeader(InputActionCategory category)
        {
            // Calculer le nombre d'actions dans cette catégorie
            var totalActionsInCategory = category.Actions.Count(action => _tempBindings.ContainsKey(action));
            
            // Ajouter les actions sans catégorie à "General"
            if (category.Name == "General")
            {
                var uncategorizedActions = _tempBindings.Keys
                    .Where(action => GetActionCategory(action) == "General")
                    .Where(action => !category.Actions.Contains(action));
                totalActionsInCategory += uncategorizedActions.Count();
            }

            var isExpanded = _categoryExpanded.TryGetValue(category.Name, out var expanded) ? expanded : true;
            
            // Afficher le nom avec le nombre d'actions
            var headerText = $"{category.Name} ({totalActionsInCategory})##{category.Name}";
            
            // CollapsingHeader retourne true quand la section est ouverte
            var headerOpen = ImGui.CollapsingHeader(headerText, ImGuiTreeNodeFlags.DefaultOpen);
            
            // Mettre à jour l'état d'expansion
            _categoryExpanded[category.Name] = headerOpen;
            
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip($"{category.Description}\n{totalActionsInCategory} action(s) in this category");
            }
        }
        
        private static void DrawFooter()
        {
            ImGui.Separator();
            ImGui.Spacing();
            
            // Statistiques avec icônes
            var totalActions = _tempBindings.Count;
            var totalBindings = _tempBindings.Values.Sum(bindings => bindings.Count);
            ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.7f, 0.7f, 0.7f, 1f));
            ImGui.Text($"📋 {totalActions} actions  |  🎯 {totalBindings} bindings  |  {(_conflicts.Any() ? "⚠️" : "✓")} {_conflicts.Count} conflicts");
            ImGui.PopStyleColor();
            
            ImGui.SameLine(ImGui.GetWindowWidth() - 380);
            
            // Boutons d'action avec couleurs
            ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.2f, 0.6f, 0.8f, 0.8f));
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.3f, 0.7f, 0.9f, 1.0f));
            ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(0.4f, 0.8f, 1.0f, 1.0f));
            if (ImGui.Button("💾 Apply", new Vector2(100, 30)))
            {
                ApplyChanges();
            }
            ImGui.PopStyleColor(3);
            
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("Save all changes and close");
            }
            
            ImGui.SameLine();
            ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.7f, 0.5f, 0.2f, 0.8f));
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.8f, 0.6f, 0.3f, 1.0f));
            ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(0.9f, 0.7f, 0.4f, 1.0f));
            if (ImGui.Button("🔄 Reset", new Vector2(100, 30)))
            {
                ResetToDefaults();
            }
            ImGui.PopStyleColor(3);
            
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("Reset current action map to default bindings");
            }
            
            ImGui.SameLine();
            ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.5f, 0.5f, 0.5f, 0.8f));
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.6f, 0.6f, 0.6f, 1.0f));
            ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(0.7f, 0.7f, 0.7f, 1.0f));
            if (ImGui.Button("✗ Cancel", new Vector2(100, 30)))
            {
                _isOpen = false;
            }
            ImGui.PopStyleColor(3);
            
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("Discard all changes and close");
            }
        }

        private static void DrawActionBindings(string actionName, List<InputSettings.KeyBindingData> bindings)
        {
            var hasConflicts = ActionHasConflicts(actionName);
            
            // Icône et nom de l'action avec meilleur style
            ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(8, 4));
            
            if (hasConflicts)
            {
                var blinkColor = MathF.Sin(_conflictBlinkTimer * 4f) > 0 ? 
                    new Vector4(1, 0.3f, 0.3f, 1) : new Vector4(1, 0.7f, 0.7f, 1);
                ImGui.PushStyleColor(ImGuiCol.Text, blinkColor);
                ImGui.Text($"⚠️ {actionName}:");
                ImGui.PopStyleColor();
                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip($"This action has binding conflicts!\nClick bindings to resolve.");
                }
            }
            else
            {
                ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.8f, 0.9f, 1.0f, 1f));
                ImGui.Text($"🎯 {actionName}:");
                ImGui.PopStyleColor();
                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip($"Click binding buttons to edit or add new bindings below.");
                }
            }
            
            // Delete action button (small, on the same line)
            ImGui.SameLine();
            ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.6f, 0.2f, 0.2f, 0.5f));
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.8f, 0.3f, 0.3f, 0.8f));
            ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(1f, 0.4f, 0.4f, 1f));
            if (ImGui.Button($"🗑️##delete_{actionName}", new Vector2(30, 20)))
            {
                // Confirm and delete action
                _tempBindings.Remove(actionName);
                // Remove from categories
                foreach (var cat in _categories.Values)
                {
                    cat.Actions.Remove(actionName);
                }
                SyncBindingsToActionMaps();
                DetectConflicts();
                ImGui.PopStyleColor(3);
                ImGui.PopStyleVar();
                return; // Exit early since we modified the list
            }
            ImGui.PopStyleColor(3);
            
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip($"Delete action '{actionName}' and all its bindings");
            }
            
            ImGui.Indent(30);

                // Afficher chaque binding
            for (int i = 0; i < bindings.Count; i++)
            {
                var binding = bindings[i];
                    // Try to parse the serialized binding into an InputBinding for a nicer display
                    var parsed = Editor.State.InputSettings.CreateBindingFromData(binding);
                    var bindingText = parsed != null ? parsed.GetDisplayName() : GetEnhancedBindingDisplayText(binding);
                var isConflicted = _conflicts.Any(c => 
                    (c.Action1 == actionName || c.Action2 == actionName) && 
                    (c.Binding1 == binding || c.Binding2 == binding));
                
                ImGui.PushID($"{actionName}_{i}");
                
                // Background coloré pour les conflits
                if (isConflicted)
                {
                    ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.8f, 0.3f, 0.3f, 0.3f));
                    ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.8f, 0.4f, 0.4f, 0.4f));
                    ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(0.8f, 0.5f, 0.5f, 0.5f));
                }
                else
                {
                    ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.2f, 0.3f, 0.8f, 0.3f));
                    ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.3f, 0.4f, 0.8f, 0.4f));
                    ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(0.4f, 0.5f, 0.8f, 0.5f));
                }
                
                // Bouton principal du binding -> opens inline editor
                if (ImGui.Button($"{bindingText}##binding", new Vector2(200, 25)))
                {
                    StartInlineEdit(actionName, i);
                }
                ImGui.PopStyleColor(3);
                
                // Tooltip informatif
                if (ImGui.IsItemHovered())
                {
                    var tooltip = isConflicted ? "⚠️ CONFLICT! Click to reassign" : "Click to reassign this binding";
                    ImGui.SetTooltip(tooltip);
                }
                
                ImGui.SameLine();
                
                // Bouton supprimer
                ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.8f, 0.2f, 0.2f, 0.6f));
                if (ImGui.Button("×##remove", new Vector2(25, 25)))
                {
                    bindings.RemoveAt(i);
                    DetectConflicts(); // Recalcule les conflits
                    ImGui.PopID();
                    ImGui.PopStyleColor();
                    break;
                }
                ImGui.PopStyleColor();
                
                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip("Remove this binding");
                }
                
                ImGui.PopID();
            }

            // Bouton ajouter nouveau binding
            ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.2f, 0.7f, 0.3f, 0.7f));
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.3f, 0.8f, 0.4f, 0.9f));
            ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(0.4f, 0.9f, 0.5f, 1.0f));
            if (ImGui.Button($"➕ Add Binding##{actionName}", new Vector2(150, 25)))
            {
                // Create new unassigned binding and immediately open editor
                bindings.Add(new InputSettings.KeyBindingData { Type = "Key", Key = "Unassigned" });
                StartInlineEdit(actionName, bindings.Count - 1);
            }
            ImGui.PopStyleColor(3);
            
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("Add a new binding (Key, Mouse Button, or Mouse Axis)");
            }

            ImGui.Unindent(30);
            ImGui.PopStyleVar(); // ItemSpacing
            ImGui.Spacing();
        }

        private static string GetBindingDisplayText(InputSettings.KeyBindingData binding)
        {
            return binding.Type switch
            {
                "Key" => $"Key: {binding.Key}",
                "MouseButton" => $"Mouse: {binding.MouseButton}",
                "MouseAxis" => $"Mouse {binding.MouseAxis} (Scale: {binding.Scale:F2})",
                _ => "Unknown"
            };
        }
        
        private static string GetEnhancedBindingDisplayText(InputSettings.KeyBindingData binding)
        {
            return binding.Type switch
            {
                "Key" => GetKeyDisplayName(binding.Key ?? "Unknown"),
                "MouseButton" => GetMouseButtonDisplayName(binding.MouseButton ?? "Unknown"),
                "MouseAxis" => $"{GetMouseAxisDisplayName(binding.MouseAxis ?? "Unknown")} ({binding.Scale:F1}x)",
                _ => "Unassigned"
            };
        }
        
        private static string GetKeyDisplayName(string key)
        {
            return key switch
            {
                "LeftControl" or "RightControl" => "Ctrl",
                "LeftShift" or "RightShift" => "Shift", 
                "LeftAlt" or "RightAlt" => "Alt",
                "Space" => "Space",
                "Enter" => "Enter",
                "Escape" => "Esc",
                "Tab" => "Tab",
                "Backspace" => "Backspace",
                "Delete" => "Delete",
                "Home" => "Home",
                "End" => "End",
                "PageUp" => "PgUp",
                "PageDown" => "PgDn",
                "Up" => "↑",
                "Down" => "↓",
                "Left" => "←",
                "Right" => "→",
                "Unknown" or "Unassigned" => "⚪ Press Key",
                _ => key
            };
        }
        
        private static string GetMouseButtonDisplayName(string button)
        {
            return button switch
            {
                "Left" => "🖱️ Left",
                "Right" => "🖱️ Right", 
                "Middle" => "🖱️ Middle",
                "Unknown" or "Unassigned" => "⚪ Click Button",
                _ => $"🖱️ {button}"
            };
        }
        
        private static string GetMouseAxisDisplayName(string axis)
        {
            return axis switch
            {
                "X" => "🖱️ Move X",
                "Y" => "🖱️ Move Y",
                "ScrollX" => "🖱️ Scroll X",
                "ScrollY" => "🖱️ Scroll Y", 
                "Unknown" or "Unassigned" => "🖱️ Mouse Axis",
                _ => $"🖱️ {axis}"
            };
        }

        private static void StartKeyRebind(string actionName, int bindingIndex)
        {
            // Legacy: keep but prefer inline editor for full control
            StartInlineEdit(actionName, bindingIndex);
        }

        private static void StartInlineEdit(string actionName, int bindingIndex)
        {
            _isEditingBinding = true;
            _editingAction = actionName;
            _editingIndex = bindingIndex;
            _editingType = "Key";
            _editingKeyCode = Engine.Input.KeyCode.None;
            _editingMouseButton = OpenTK.Windowing.GraphicsLibraryFramework.MouseButton.Left;
            _editingMouseAxis = "X";
            _editingScale = 1.0f;
            _editingModCtrl = false;
            _editingModAlt = false;
            _editingModShift = false;

            // Pre-populate fields from existing binding if present
            if (_tempBindings.TryGetValue(actionName, out var bindings) && bindingIndex < bindings.Count)
            {
                var b = bindings[bindingIndex];
                _editingType = b.Type ?? "Key";
                if (_editingType == "Key")
                {
                    // Parse simple key string possibly with modifiers
                    var keyStr = (b.Key ?? "").Trim();
                    if (keyStr.Contains('+'))
                    {
                        var parts = keyStr.Split('+');
                        for (int p = 0; p < parts.Length - 1; p++)
                        {
                            var mod = parts[p].Trim();
                            if (mod.Equals("LeftControl", StringComparison.OrdinalIgnoreCase) || mod.Equals("RightControl", StringComparison.OrdinalIgnoreCase) || mod.Equals("Ctrl", StringComparison.OrdinalIgnoreCase)) _editingModCtrl = true;
                            if (mod.Equals("LeftAlt", StringComparison.OrdinalIgnoreCase) || mod.Equals("RightAlt", StringComparison.OrdinalIgnoreCase) || mod.Equals("Alt", StringComparison.OrdinalIgnoreCase)) _editingModAlt = true;
                            if (mod.Equals("LeftShift", StringComparison.OrdinalIgnoreCase) || mod.Equals("RightShift", StringComparison.OrdinalIgnoreCase) || mod.Equals("Shift", StringComparison.OrdinalIgnoreCase)) _editingModShift = true;
                        }
                        var main = parts.Last().Trim();
                        if (Enum.TryParse<Engine.Input.KeyCode>(main, true, out var kc)) _editingKeyCode = kc;
                        else _editingKeyCode = Engine.Input.KeyCode.None;
                    }
                    else
                    {
                        var main = keyStr;
                        if (Enum.TryParse<Engine.Input.KeyCode>(main, true, out var kc)) _editingKeyCode = kc;
                        else _editingKeyCode = Engine.Input.KeyCode.None;
                    }
                }
                else if (_editingType == "MouseButton")
                {
                    var btn = (b.MouseButton ?? "Left").Trim();
                    Enum.TryParse<OpenTK.Windowing.GraphicsLibraryFramework.MouseButton>(btn, true, out _editingMouseButton);
                }
                else if (_editingType == "MouseAxis")
                {
                    _editingMouseAxis = b.MouseAxis ?? "X";
                    _editingScale = b.Scale;
                }
            }
        }

        private static void OnKeyCaptured(Keys key)
        {
            AssignKeyBinding(key);
            _isWaitingForKey = false;
            _waitingForModifiers = false;
            _capturedModifiers.Clear();
        }

        private static void OnKeyCaptured(Engine.Input.KeyCode kc)
        {
            // Convert KeyCode to OpenTK Keys for existing assignment logic
            var k = kc.ToOpenTK();
            OnKeyCaptured(k);
        }

        private static void OnMouseCaptured(MouseButton button)
        {
            AssignMouseButtonBinding(button);
            _isWaitingForKey = false;
            _waitingForModifiers = false;
            _capturedModifiers.Clear();
        }

        private static void OnCaptureCancelled()
        {
            _isWaitingForKey = false;
            _waitingForModifiers = false;
            _capturedModifiers.Clear();
        }

        // Ancienne méthode supprimée - maintenant géré par le nouveau système de contextes
        
        private static void DrawCaptureOverlay()
        {
            var viewport = ImGui.GetMainViewport();
            ImGui.SetNextWindowPos(viewport.WorkPos + viewport.WorkSize / 2 - new Vector2(200, 50));
            ImGui.SetNextWindowSize(new Vector2(400, 100));
            
            var flags = ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoMove | 
                       ImGuiWindowFlags.NoResize | ImGuiWindowFlags.AlwaysAutoResize;
                       
            ImGui.PushStyleColor(ImGuiCol.WindowBg, new Vector4(0.1f, 0.1f, 0.1f, 0.95f));
            ImGui.PushStyleColor(ImGuiCol.Border, new Vector4(0.8f, 0.6f, 0.2f, 1f));
            
            if (ImGui.Begin("Key Capture", flags))
            {
                ImGui.Text($"🎯 Assigning key for: {_selectedAction}");
                ImGui.Separator();
                
                if (_waitingForModifiers && _capturedModifiers.Any())
                {
                    var modText = string.Join(" + ", _capturedModifiers.Select(k => GetKeyDisplayName(k.ToString())));
                    ImGui.Text($"Modifiers: {modText} + ?");
                    ImGui.Text("Press the main key...");
                }
                else
                {
                    ImGui.Text("Press any key or mouse button...");
                    ImGui.Text("Hold Ctrl/Shift/Alt for combo keys");
                }
                
                ImGui.Text("(ESC to cancel)");
            }
            ImGui.End();
            ImGui.PopStyleColor(2);
        }

        // Draw the inline edit modal for a binding
        private static void DrawInlineEditor()
        {
            if (!_isEditingBinding) return;

            var viewport = ImGui.GetMainViewport();
            ImGui.SetNextWindowPos(viewport.WorkPos + viewport.WorkSize / 2 - new Vector2(250, 160));
            ImGui.SetNextWindowSize(new Vector2(500, 320));
            
            ImGui.PushStyleColor(ImGuiCol.WindowBg, new Vector4(0.12f, 0.12f, 0.15f, 0.98f));
            ImGui.PushStyleColor(ImGuiCol.Border, new Vector4(0.3f, 0.5f, 0.8f, 1f));
            ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, 8f);
            
            if (ImGui.Begin("🎮 Edit Binding", ref _isEditingBinding, ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoCollapse))
            {
                // Header with action name
                ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.7f, 0.9f, 1.0f, 1f));
                ImGui.Text($"Action: {_editingAction}");
                ImGui.PopStyleColor();
                ImGui.SameLine();
                ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.6f, 0.6f, 0.6f, 1f));
                ImGui.Text($"[Binding #{_editingIndex + 1}]");
                ImGui.PopStyleColor();
                ImGui.Separator();
                ImGui.Spacing();

                // Type selector
                ImGui.AlignTextToFramePadding();
                ImGui.Text("Input Type:"); 
                ImGui.SameLine(120);
                ImGui.SetNextItemWidth(200);
                var types = new[] { "Key", "MouseButton", "MouseAxis" };
                var typeIndex = Array.IndexOf(types, _editingType);
                if (typeIndex < 0) typeIndex = 0;
                if (ImGui.Combo("##bindingType", ref typeIndex, types, types.Length))
                {
                    _editingType = types[typeIndex];
                }
                
                ImGui.Spacing();
                ImGui.Separator();
                ImGui.Spacing();

                if (_editingType == "Key")
                {
                    // Key selection
                    ImGui.AlignTextToFramePadding();
                    ImGui.Text("Key:");
                    ImGui.SameLine(120);
                    
                    // KeyCode dropdown with friendly names
                    var keyValues = Enum.GetValues(typeof(Engine.Input.KeyCode));
                    var keyNames = new string[keyValues.Length];
                    int sel = 0;
                    for (int i = 0; i < keyValues.Length; i++)
                    {
                        var kc = (Engine.Input.KeyCode)keyValues.GetValue(i)!;
                        keyNames[i] = GetFriendlyKeyCodeName(kc);
                        if (kc == _editingKeyCode) sel = i;
                    }
                    ImGui.SetNextItemWidth(250);
                    if (ImGui.Combo("##keycombo", ref sel, keyNames, keyNames.Length))
                    {
                        _editingKeyCode = (Engine.Input.KeyCode)keyValues.GetValue(sel)!;
                    }

                    ImGui.SameLine();
                    ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.2f, 0.6f, 0.8f, 0.8f));
                    ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.3f, 0.7f, 0.9f, 1.0f));
                    ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(0.4f, 0.8f, 1.0f, 1.0f));
                    if (ImGui.Button("⌨️ Capture", new Vector2(100, 0)))
                    {
                        var im = Engine.Input.InputManager.Instance;
                        if (im != null)
                        {
                            _isWaitingForKey = true;
                            im.BeginBindingCapture(kc => {
                                _editingKeyCode = kc;
                                _isWaitingForKey = false;
                            }, mb => { _isWaitingForKey = false; }, () => { _isWaitingForKey = false; });
                        }
                    }
                    ImGui.PopStyleColor(3);
                    
                    if (ImGui.IsItemHovered())
                    {
                        ImGui.SetTooltip("Press any key to capture it");
                    }

                    ImGui.Spacing();
                    ImGui.AlignTextToFramePadding();
                    ImGui.Text("Modifiers:");
                    ImGui.SameLine(120);
                    ImGui.Checkbox("Ctrl", ref _editingModCtrl); 
                    ImGui.SameLine();
                    ImGui.Checkbox("Alt", ref _editingModAlt); 
                    ImGui.SameLine();
                    ImGui.Checkbox("Shift", ref _editingModShift);
                    
                    // Preview
                    ImGui.Spacing();
                    ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.5f, 0.8f, 0.5f, 1f));
                    var preview = BuildKeyPreview();
                    ImGui.Text($"Preview: {preview}");
                    ImGui.PopStyleColor();
                }
                else if (_editingType == "MouseButton")
                {
                    ImGui.AlignTextToFramePadding();
                    ImGui.Text("Mouse Button:");
                    ImGui.SameLine(120);
                    var btns = Enum.GetValues(typeof(OpenTK.Windowing.GraphicsLibraryFramework.MouseButton));
                    var btnNames = new string[btns.Length];
                    int sel = 0;
                    for (int i = 0; i < btns.Length; i++)
                    {
                        var mb = (OpenTK.Windowing.GraphicsLibraryFramework.MouseButton)btns.GetValue(i)!;
                        btnNames[i] = GetFriendlyMouseButtonName(mb);
                        if (mb == _editingMouseButton) sel = i;
                    }
                    ImGui.SetNextItemWidth(250);
                    if (ImGui.Combo("##mousebutton", ref sel, btnNames, btnNames.Length))
                    {
                        _editingMouseButton = (OpenTK.Windowing.GraphicsLibraryFramework.MouseButton)btns.GetValue(sel)!;
                    }
                    
                    ImGui.Spacing();
                    ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.5f, 0.8f, 0.5f, 1f));
                    ImGui.Text($"Preview: 🖱️ {GetFriendlyMouseButtonName(_editingMouseButton)}");
                    ImGui.PopStyleColor();
                }
                else if (_editingType == "MouseAxis")
                {
                    ImGui.AlignTextToFramePadding();
                    ImGui.Text("Mouse Axis:");
                    ImGui.SameLine(120);
                    var axes = new[] { "X", "Y", "ScrollX", "ScrollY" };
                    var axSel = Array.IndexOf(axes, _editingMouseAxis);
                    if (axSel < 0) axSel = 0;
                    ImGui.SetNextItemWidth(150);
                    if (ImGui.Combo("##mouseaxis", ref axSel, axes, axes.Length))
                    {
                        _editingMouseAxis = axes[axSel];
                    }
                    
                    ImGui.Spacing();
                    ImGui.AlignTextToFramePadding();
                    ImGui.Text("Sensitivity:");
                    ImGui.SameLine(120);
                    ImGui.SetNextItemWidth(250);
                    ImGui.SliderFloat("##scale", ref _editingScale, 0.1f, 10f, "%.2fx");
                    
                    ImGui.Spacing();
                    ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.5f, 0.8f, 0.5f, 1f));
                    ImGui.Text($"Preview: 🖱️ {GetMouseAxisDisplayName(_editingMouseAxis)} × {_editingScale:F2}");
                    ImGui.PopStyleColor();
                }

                ImGui.Spacing();
                ImGui.Separator();
                ImGui.Spacing();
                
                // Action buttons
                var buttonWidth = 120f;
                var spacing = ImGui.GetStyle().ItemSpacing.X;
                var totalWidth = buttonWidth * 2 + spacing;
                ImGui.SetCursorPosX((ImGui.GetWindowWidth() - totalWidth) * 0.5f);
                
                ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.2f, 0.7f, 0.2f, 0.8f));
                ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.3f, 0.8f, 0.3f, 1.0f));
                ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(0.4f, 0.9f, 0.4f, 1.0f));
                if (ImGui.Button("✓ Apply", new Vector2(buttonWidth, 35)))
                {
                    ApplyInlineEdit();
                    _isEditingBinding = false;
                }
                ImGui.PopStyleColor(3);
                
                ImGui.SameLine();
                
                ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.6f, 0.2f, 0.2f, 0.8f));
                ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.7f, 0.3f, 0.3f, 1.0f));
                ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(0.8f, 0.4f, 0.4f, 1.0f));
                if (ImGui.Button("✗ Cancel", new Vector2(buttonWidth, 35)))
                {
                    _isEditingBinding = false;
                }
                ImGui.PopStyleColor(3);
            }
            ImGui.End();
            
            ImGui.PopStyleVar();
            ImGui.PopStyleColor(2);
        }

        private static string BuildKeyPreview()
        {
            var parts = new List<string>();
            if (_editingModCtrl) parts.Add("Ctrl");
            if (_editingModAlt) parts.Add("Alt");
            if (_editingModShift) parts.Add("Shift");
            parts.Add(GetFriendlyKeyCodeName(_editingKeyCode));
            return string.Join(" + ", parts);
        }

        private static void ApplyInlineEdit()
        {
            if (!_tempBindings.TryGetValue(_editingAction, out var list) || _editingIndex >= list.Count)
                return;

            if (_editingType == "Key")
            {
                var modParts = new List<string>();
                if (_editingModCtrl) modParts.Add("LeftControl");
                if (_editingModAlt) modParts.Add("LeftAlt");
                if (_editingModShift) modParts.Add("LeftShift");
                var keyName = _editingKeyCode.ToString();
                var keyString = modParts.Any() ? string.Join("+", modParts) + "+" + keyName : keyName;
                var path = modParts.Any() ? $"<Keyboard>/{string.Join("+", modParts)}+{keyName}" : $"<Keyboard>/{keyName}";
                list[_editingIndex] = new InputSettings.KeyBindingData { Type = "Key", Key = keyString, Path = path };
            }
            else if (_editingType == "MouseButton")
            {
                var btnName = _editingMouseButton.ToString();
                var path = $"<Mouse>/{btnName}";
                list[_editingIndex] = new InputSettings.KeyBindingData { Type = "MouseButton", MouseButton = btnName, Path = path };
            }
            else if (_editingType == "MouseAxis")
            {
                var axis = _editingMouseAxis;
                var path = $"<Mouse>/{axis}";
                list[_editingIndex] = new InputSettings.KeyBindingData { Type = "MouseAxis", MouseAxis = axis, Scale = _editingScale, Path = path };
            }
            SyncBindingsToActionMaps();
            DetectConflicts();
        }

        private static string GetFriendlyKeyCodeName(Engine.Input.KeyCode kc)
        {
            return kc switch
            {
                Engine.Input.KeyCode.None => "(None)",
                Engine.Input.KeyCode.Alpha0 => "0",
                Engine.Input.KeyCode.Alpha1 => "1",
                Engine.Input.KeyCode.Alpha2 => "2",
                Engine.Input.KeyCode.Alpha3 => "3",
                Engine.Input.KeyCode.Alpha4 => "4",
                Engine.Input.KeyCode.Alpha5 => "5",
                Engine.Input.KeyCode.Alpha6 => "6",
                Engine.Input.KeyCode.Alpha7 => "7",
                Engine.Input.KeyCode.Alpha8 => "8",
                Engine.Input.KeyCode.Alpha9 => "9",
                Engine.Input.KeyCode.Keypad0 => "Numpad 0",
                Engine.Input.KeyCode.Keypad1 => "Numpad 1",
                Engine.Input.KeyCode.Keypad2 => "Numpad 2",
                Engine.Input.KeyCode.Keypad3 => "Numpad 3",
                Engine.Input.KeyCode.Keypad4 => "Numpad 4",
                Engine.Input.KeyCode.Keypad5 => "Numpad 5",
                Engine.Input.KeyCode.Keypad6 => "Numpad 6",
                Engine.Input.KeyCode.Keypad7 => "Numpad 7",
                Engine.Input.KeyCode.Keypad8 => "Numpad 8",
                Engine.Input.KeyCode.Keypad9 => "Numpad 9",
                Engine.Input.KeyCode.KeypadPeriod => "Numpad .",
                Engine.Input.KeyCode.KeypadDivide => "Numpad /",
                Engine.Input.KeyCode.KeypadMultiply => "Numpad *",
                Engine.Input.KeyCode.KeypadMinus => "Numpad -",
                Engine.Input.KeyCode.KeypadPlus => "Numpad +",
                Engine.Input.KeyCode.KeypadEnter => "Numpad Enter",
                Engine.Input.KeyCode.Return => "Return",
                Engine.Input.KeyCode.Enter => "Enter",
                Engine.Input.KeyCode.Space => "Space",
                Engine.Input.KeyCode.Backspace => "Backspace",
                Engine.Input.KeyCode.Tab => "Tab",
                Engine.Input.KeyCode.Escape => "Escape",
                Engine.Input.KeyCode.LeftShift => "Left Shift",
                Engine.Input.KeyCode.RightShift => "Right Shift",
                Engine.Input.KeyCode.LeftControl => "Left Ctrl",
                Engine.Input.KeyCode.RightControl => "Right Ctrl",
                Engine.Input.KeyCode.LeftAlt => "Left Alt",
                Engine.Input.KeyCode.RightAlt => "Right Alt",
                Engine.Input.KeyCode.CapsLock => "Caps Lock",
                Engine.Input.KeyCode.NumLock => "Num Lock",
                Engine.Input.KeyCode.ScrollLock => "Scroll Lock",
                Engine.Input.KeyCode.Insert => "Insert",
                Engine.Input.KeyCode.Delete => "Delete",
                Engine.Input.KeyCode.Home => "Home",
                Engine.Input.KeyCode.End => "End",
                Engine.Input.KeyCode.PageUp => "Page Up",
                Engine.Input.KeyCode.PageDown => "Page Down",
                Engine.Input.KeyCode.UpArrow => "↑ Up",
                Engine.Input.KeyCode.DownArrow => "↓ Down",
                Engine.Input.KeyCode.LeftArrow => "← Left",
                Engine.Input.KeyCode.RightArrow => "→ Right",
                Engine.Input.KeyCode.Pause => "Pause",
                Engine.Input.KeyCode.PrintScreen => "Print Screen",
                Engine.Input.KeyCode.Menu => "Menu",
                Engine.Input.KeyCode.Semicolon => "; (Semicolon)",
                Engine.Input.KeyCode.Equals => "= (Equals)",
                Engine.Input.KeyCode.Comma => ", (Comma)",
                Engine.Input.KeyCode.Minus => "- (Minus)",
                Engine.Input.KeyCode.Period => ". (Period)",
                Engine.Input.KeyCode.Slash => "/ (Slash)",
                Engine.Input.KeyCode.BackQuote => "` (Grave)",
                Engine.Input.KeyCode.LeftBracket => "[ (Left Bracket)",
                Engine.Input.KeyCode.Backslash => "\\ (Backslash)",
                Engine.Input.KeyCode.RightBracket => "] (Right Bracket)",
                Engine.Input.KeyCode.Quote => "' (Quote)",
                _ => kc.ToString()
            };
        }

        private static string GetFriendlyMouseButtonName(OpenTK.Windowing.GraphicsLibraryFramework.MouseButton mb)
        {
            return mb switch
            {
                OpenTK.Windowing.GraphicsLibraryFramework.MouseButton.Left => "Left Click",
                OpenTK.Windowing.GraphicsLibraryFramework.MouseButton.Right => "Right Click",
                OpenTK.Windowing.GraphicsLibraryFramework.MouseButton.Middle => "Middle Click",
                OpenTK.Windowing.GraphicsLibraryFramework.MouseButton.Button4 => "Mouse Button 4",
                OpenTK.Windowing.GraphicsLibraryFramework.MouseButton.Button5 => "Mouse Button 5",
                OpenTK.Windowing.GraphicsLibraryFramework.MouseButton.Button6 => "Mouse Button 6",
                OpenTK.Windowing.GraphicsLibraryFramework.MouseButton.Button7 => "Mouse Button 7",
                _ => mb.ToString()
            };
        }
        
        private static bool IsModifierKey(Keys key)
        {
            return key == Keys.LeftControl || key == Keys.RightControl ||
                   key == Keys.LeftShift || key == Keys.RightShift ||
                   key == Keys.LeftAlt || key == Keys.RightAlt;
        }

        // Les méthodes IsKeyPressed et ToImGuiKey ne sont plus nécessaires
        // car on utilise maintenant InputManager.TryConsumeLastKeyPressed

        private static void AssignKeyBinding(Keys key)
        {
            if (_tempBindings.TryGetValue(_selectedAction, out var bindings) && 
                _bindingIndex < bindings.Count)
            {
                string path;
                if (_capturedModifiers.Any())
                {
                    var modifierPart = string.Join("+", _capturedModifiers.Select(m => m.ToString()));
                    path = $"<Keyboard>/{modifierPart}+{key}";
                }
                else
                {
                    path = $"<Keyboard>/{key}";
                }
                    
                var keyString = _capturedModifiers.Any()
                    ? $"{string.Join("+", _capturedModifiers.Select(m => m.ToString()))}+{key}"
                    : key.ToString();
                
                bindings[_bindingIndex] = new InputSettings.KeyBindingData
                {
                    Type = "Key",
                    Key = keyString,
                    Path = path
                };
                
                SyncBindingsToActionMaps(); // Synchroniser les changements
                DetectConflicts(); // Recalcule les conflits après assignation
            }
            _isWaitingForKey = false;
            _capturedModifiers.Clear();
        }

        private static void AssignMouseButtonBinding(MouseButton button)
        {
            if (_tempBindings.TryGetValue(_selectedAction, out var bindings) && 
                _bindingIndex < bindings.Count)
            {
                string path;
                if (_capturedModifiers.Any())
                {
                    var modifierPart = string.Join("+", _capturedModifiers.Select(m => m.ToString()));
                    path = $"<Mouse>/{modifierPart}+{button}";
                }
                else
                {
                    path = $"<Mouse>/{button}";
                }
                    
                var buttonString = _capturedModifiers.Any()
                    ? $"{string.Join("+", _capturedModifiers.Select(m => m.ToString()))}+{button}"
                    : button.ToString();
                
                bindings[_bindingIndex] = new InputSettings.KeyBindingData
                {
                    Type = "MouseButton", 
                    MouseButton = buttonString,
                    Path = path
                };
                
                SyncBindingsToActionMaps(); // Synchroniser les changements
                DetectConflicts(); // Recalcule les conflits après assignation
            }
            _isWaitingForKey = false;
            _capturedModifiers.Clear();
        }

        private static void ApplyChanges()
        {
            // Sauvegarder toutes les ActionMaps
            foreach (var mapEntry in _tempActionMaps)
            {
                string mapName = mapEntry.Key;
                var actionBindings = mapEntry.Value;
                
                foreach (var actionEntry in actionBindings)
                {
                    InputSettings.SetActionBinding(mapName, actionEntry.Key, actionEntry.Value);
                }
            }
            _isOpen = false;
        }

        private static void ResetToDefaults()
        {
            // Create new default bindings
            _tempBindings.Clear();
            _tempBindings["MoveForward"] = new List<InputSettings.KeyBindingData>
            {
                new() { Type = "Key", Key = "W", Path = "<Keyboard>/W" }
            };
            _tempBindings["MoveBackward"] = new List<InputSettings.KeyBindingData>
            {
                new() { Type = "Key", Key = "S", Path = "<Keyboard>/S" }
            };
            _tempBindings["MoveLeft"] = new List<InputSettings.KeyBindingData>
            {
                new() { Type = "Key", Key = "A", Path = "<Keyboard>/A" }
            };
            _tempBindings["MoveRight"] = new List<InputSettings.KeyBindingData>
            {
                new() { Type = "Key", Key = "D", Path = "<Keyboard>/D" }
            };
            _tempBindings["Jump"] = new List<InputSettings.KeyBindingData>
            {
                new() { Type = "Key", Key = "Space", Path = "<Keyboard>/Space" }
            };
            _tempBindings["LookX"] = new List<InputSettings.KeyBindingData>
            {
                new() { Type = "MouseAxis", MouseAxis = "X", Scale = 0.1f, Path = "<Mouse>/X" }
            };
            _tempBindings["LookY"] = new List<InputSettings.KeyBindingData>
            {
                new() { Type = "MouseAxis", MouseAxis = "Y", Scale = 0.1f, Path = "<Mouse>/Y" }
            };
        }
        
        /// <summary>
        /// Détecte tous les conflits dans les bindings actuels
        /// </summary>
        private static void DetectConflicts()
        {
            _conflicts.Clear();
            var actions = new List<string>();
            
            foreach (var actionName in _tempBindings.Keys)
            {
                actions.Add(actionName);
            }
            
            for (int i = 0; i < actions.Count; i++)
            {
                for (int j = i + 1; j < actions.Count; j++)
                {
                    var action1 = actions[i];
                    var action2 = actions[j];
                    var bindings1 = _tempBindings[action1];
                    var bindings2 = _tempBindings[action2];
                    
                    foreach (var binding1 in bindings1)
                    {
                        foreach (var binding2 in bindings2)
                        {
                            if (AreBindingsConflicting(binding1, binding2))
                            {
                                _conflicts.Add(new InputConflict
                                {
                                    Action1 = action1,
                                    Action2 = action2,
                                    Binding1 = binding1,
                                    Binding2 = binding2
                                });
                            }
                        }
                    }
                }
            }
        }
        
        private static bool AreBindingsConflicting(InputSettings.KeyBindingData binding1, InputSettings.KeyBindingData binding2)
        {
            if (binding1.Type != binding2.Type) return false;
            
            return binding1.Type switch
            {
                "Key" => binding1.Key == binding2.Key,
                "MouseButton" => binding1.MouseButton == binding2.MouseButton,
                _ => false
            };
        }
        
        private static void InitializeCategories()
        {
            _categories.Clear();
            _categories["Movement"] = new InputActionCategory
            {
                Name = "Movement",
                Description = "Player movement controls",
                Actions = new List<string> { "MoveForward", "MoveBackward", "MoveLeft", "MoveRight", "Jump" }
            };
            _categories["Camera"] = new InputActionCategory
            {
                Name = "Camera", 
                Description = "Camera and look controls",
                Actions = new List<string> { "LookX", "LookY" }
            };
            _categories["General"] = new InputActionCategory
            {
                Name = "General",
                Description = "General game actions",
                Actions = new List<string>()
            };
            
            // Assure que toutes les catégories sont étendues par défaut
            foreach (var category in _categories.Keys)
            {
                if (!_categoryExpanded.ContainsKey(category))
                    _categoryExpanded[category] = true;
            }
        }
        
        private static string GetActionCategory(string actionName)
        {
            foreach (var category in _categories.Values)
            {
                if (category.Actions.Contains(actionName))
                    return category.Name;
            }
            return "General";
        }
        
        private static bool ActionPassesFilter(string actionName)
        {
            if (string.IsNullOrEmpty(_searchFilter)) 
                return true;
                
            return actionName.Contains(_searchFilter, StringComparison.OrdinalIgnoreCase);
        }
        
        private static bool ActionHasConflicts(string actionName)
        {
            return _conflicts.Any(c => c.Action1 == actionName || c.Action2 == actionName);
        }

        private static void DrawNewActionDialog()
        {
            if (!_isCreatingNewAction) return;

            var viewport = ImGui.GetMainViewport();
            ImGui.SetNextWindowPos(viewport.WorkPos + viewport.WorkSize / 2 - new Vector2(200, 100));
            ImGui.SetNextWindowSize(new Vector2(400, 200));
            
            ImGui.PushStyleColor(ImGuiCol.WindowBg, new Vector4(0.12f, 0.12f, 0.15f, 0.98f));
            ImGui.PushStyleColor(ImGuiCol.Border, new Vector4(0.3f, 0.7f, 0.3f, 1f));
            ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, 8f);
            
            if (ImGui.Begin("➕ Create New Action", ref _isCreatingNewAction, ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoCollapse))
            {
                ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.7f, 0.9f, 1.0f, 1f));
                ImGui.Text($"Action Map: {_selectedActionMap}");
                ImGui.PopStyleColor();
                ImGui.Separator();
                ImGui.Spacing();

                // Action name input
                ImGui.AlignTextToFramePadding();
                ImGui.Text("Action Name:");
                ImGui.SameLine(120);
                ImGui.SetNextItemWidth(250);
                ImGui.InputText("##actionName", ref _newActionName, 100);
                
                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip("Enter a unique name for the action (e.g., 'Fire', 'Reload', 'Crouch')");
                }

                ImGui.Spacing();

                // Category selector
                ImGui.AlignTextToFramePadding();
                ImGui.Text("Category:");
                ImGui.SameLine(120);
                ImGui.SetNextItemWidth(250);
                var categories = _categories.Keys.ToArray();
                var catIndex = Array.IndexOf(categories, _newActionCategory);
                if (catIndex < 0) catIndex = 0;
                if (ImGui.Combo("##category", ref catIndex, categories, categories.Length))
                {
                    _newActionCategory = categories[catIndex];
                }
                
                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip("Choose the category to organize this action");
                }

                ImGui.Spacing();
                ImGui.Separator();
                ImGui.Spacing();

                // Validation message
                var isValid = !string.IsNullOrWhiteSpace(_newActionName);
                var alreadyExists = _tempBindings.ContainsKey(_newActionName);
                
                if (!isValid)
                {
                    ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1f, 0.5f, 0.2f, 1f));
                    ImGui.Text("⚠️ Please enter an action name");
                    ImGui.PopStyleColor();
                }
                else if (alreadyExists)
                {
                    ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1f, 0.3f, 0.3f, 1f));
                    ImGui.Text($"⚠️ Action '{_newActionName}' already exists!");
                    ImGui.PopStyleColor();
                }
                else
                {
                    ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.5f, 1f, 0.5f, 1f));
                    ImGui.Text($"✓ Ready to create '{_newActionName}'");
                    ImGui.PopStyleColor();
                }

                ImGui.Spacing();
                
                // Action buttons
                var buttonWidth = 120f;
                var spacing = ImGui.GetStyle().ItemSpacing.X;
                var totalWidth = buttonWidth * 2 + spacing;
                ImGui.SetCursorPosX((ImGui.GetWindowWidth() - totalWidth) * 0.5f);
                
                var canCreate = isValid && !alreadyExists;
                
                if (!canCreate)
                {
                    ImGui.PushStyleVar(ImGuiStyleVar.Alpha, 0.5f);
                }
                
                ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.2f, 0.7f, 0.2f, 0.8f));
                ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.3f, 0.8f, 0.3f, 1.0f));
                ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(0.4f, 0.9f, 0.4f, 1.0f));
                
                if (ImGui.Button("✓ Create", new Vector2(buttonWidth, 35)) && canCreate)
                {
                    CreateNewAction();
                    _isCreatingNewAction = false;
                }
                ImGui.PopStyleColor(3);
                
                if (!canCreate)
                {
                    ImGui.PopStyleVar();
                }
                
                ImGui.SameLine();
                
                ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.6f, 0.2f, 0.2f, 0.8f));
                ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.7f, 0.3f, 0.3f, 1.0f));
                ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(0.8f, 0.4f, 0.4f, 1.0f));
                if (ImGui.Button("✗ Cancel", new Vector2(buttonWidth, 35)))
                {
                    _isCreatingNewAction = false;
                }
                ImGui.PopStyleColor(3);
            }
            ImGui.End();
            
            ImGui.PopStyleVar();
            ImGui.PopStyleColor(2);
        }

        private static void CreateNewAction()
        {
            if (string.IsNullOrWhiteSpace(_newActionName) || _tempBindings.ContainsKey(_newActionName))
                return;

            // Create new action with empty binding list
            _tempBindings[_newActionName] = new List<InputSettings.KeyBindingData>();
            
            // Add to category if not General
            if (_newActionCategory != "General" && _categories.TryGetValue(_newActionCategory, out var category))
            {
                if (!category.Actions.Contains(_newActionName))
                {
                    category.Actions.Add(_newActionName);
                }
            }
            
            // Sync to action maps
            SyncBindingsToActionMaps();
            
            // Immediately open editor to add first binding
            var newIndex = _tempBindings[_newActionName].Count;
            _tempBindings[_newActionName].Add(new InputSettings.KeyBindingData { Type = "Key", Key = "Unassigned" });
            StartInlineEdit(_newActionName, newIndex);
        }
    }
    
    public class InputActionCategory
    {
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        public List<string> Actions { get; set; } = new();
    }
    
    public class InputConflict
    {
        public string Action1 { get; set; } = "";
        public string Action2 { get; set; } = "";
        public InputSettings.KeyBindingData Binding1 { get; set; } = new();
        public InputSettings.KeyBindingData Binding2 { get; set; } = new();
        
        public string GetDescription()
        {
            var bindingText = Binding1.Type switch
            {
                "Key" => Binding1.Key,
                "MouseButton" => Binding1.MouseButton,
                _ => "Unknown"
            };
            return $"'{Action1}' and '{Action2}' both use '{bindingText}'";
        }
    }
}