using System;
using System.Numerics;
using ImGuiNET;
using Editor.UI;
using Engine.Physics;

namespace Editor.Panels
{
    /// <summary>
    /// Panel pour configurer les collision layers et leur matrice de collision (style Unity).
    /// Permet de nommer les layers et définir quels layers peuvent collisionner entre eux.
    /// </summary>
    public static class CollisionLayersPanel
    {
        private static bool _isOpen = false;
        private static string[] _tempLayerNames = new string[32];
        private static bool[,] _tempCollisionMatrix = new bool[32, 32];
        private static int _selectedLayer = -1;
        private static string _editingLayerName = "";
        private static bool _isEditingName = false;
        private static int _editingLayerIndex = -1;

        public static void Open()
        {
            _isOpen = true;

            // Charger les noms actuels des layers
            for (int i = 0; i < 32; i++)
            {
                _tempLayerNames[i] = CollisionLayers.GetLayerName(i);
            }

            // Charger la matrice de collision actuelle
            for (int i = 0; i < 32; i++)
            {
                for (int j = 0; j < 32; j++)
                {
                    _tempCollisionMatrix[i, j] = CollisionLayers.CanLayersCollide(i, j);
                }
            }

            _selectedLayer = -1;
        }

        public static void Draw()
        {
            if (!_isOpen) return;

            var viewport = ImGui.GetMainViewport();
            ImGui.SetNextWindowPos(viewport.WorkPos + new Vector2(100, 100), ImGuiCond.FirstUseEver);
            ImGui.SetNextWindowSize(new Vector2(900, 700), ImGuiCond.FirstUseEver);

            if (ImGui.Begin("Collision Layers Settings", ref _isOpen, ImGuiWindowFlags.NoCollapse))
            {
                DrawHeader();
                ImGui.Separator();

                // Layout en 2 colonnes
                if (ImGui.BeginTable("CollisionLayersLayout", 2, ImGuiTableFlags.Resizable))
                {
                    ImGui.TableSetupColumn("Layers", ImGuiTableColumnFlags.WidthFixed, 300);
                    ImGui.TableSetupColumn("Collision Matrix", ImGuiTableColumnFlags.WidthStretch);

                    ImGui.TableNextRow();
                    ImGui.TableNextColumn();
                    DrawLayersList();

                    ImGui.TableNextColumn();
                    DrawCollisionMatrix();

                    ImGui.EndTable();
                }

                ImGui.Separator();
                DrawFooter();
            }
            ImGui.End();
        }

        private static void DrawHeader()
        {
            ImGui.SetWindowFontScale(1.2f);
            ImGui.Text("Collision Layers Configuration");
            ImGui.SetWindowFontScale(1.0f);
            ImGui.TextWrapped("Configure layer names and define which layers can collide with each other.");
            ImGui.Spacing();
        }

        private static void DrawLayersList()
        {
            ImGui.Text("Layers (32 max)");
            ImGui.Separator();

            ImGui.BeginChild("LayersListChild", new Vector2(0, -40), ImGuiChildFlags.Borders);

            for (int i = 0; i < 32; i++)
            {
                ImGui.PushID(i);

                bool isSelected = _selectedLayer == i;

                // Highlight du layer sélectionné
                if (isSelected)
                {
                    ImGui.PushStyleColor(ImGuiCol.ChildBg, new Vector4(0.3f, 0.5f, 0.8f, 0.2f));
                }

                ImGui.BeginChild($"Layer{i}", new Vector2(0, 30), ImGuiChildFlags.Borders, ImGuiWindowFlags.NoScrollbar);

                // Index du layer
                ImGui.Text($"{i:D2}");
                ImGui.SameLine();

                // Nom du layer (éditable)
                if (_isEditingName && _editingLayerIndex == i)
                {
                    ImGui.SetKeyboardFocusHere();
                    if (ImGui.InputText("##LayerName", ref _editingLayerName, 64, ImGuiInputTextFlags.EnterReturnsTrue))
                    {
                        _tempLayerNames[i] = _editingLayerName;
                        _isEditingName = false;
                        _editingLayerIndex = -1;
                    }

                    if (ImGui.IsKeyPressed(ImGuiKey.Escape))
                    {
                        _isEditingName = false;
                        _editingLayerIndex = -1;
                    }
                }
                else
                {
                    if (ImGui.Selectable(_tempLayerNames[i], isSelected, ImGuiSelectableFlags.None, new Vector2(200, 0)))
                    {
                        _selectedLayer = i;
                    }

                    // Double-click pour éditer
                    if (ImGui.IsItemHovered() && ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left))
                    {
                        _isEditingName = true;
                        _editingLayerIndex = i;
                        _editingLayerName = _tempLayerNames[i];
                    }
                }

                ImGui.EndChild();

                if (isSelected)
                {
                    ImGui.PopStyleColor();
                }

                ImGui.PopID();
            }

            ImGui.EndChild();

            ImGui.TextDisabled("Double-click a layer to rename it");
        }

        private static void DrawCollisionMatrix()
        {
            ImGui.Text("Collision Matrix");
            ImGui.Separator();

            if (_selectedLayer == -1)
            {
                ImGui.TextWrapped("Select a layer on the left to configure its collisions.");
                return;
            }

            ImGui.Text($"Layer {_selectedLayer}: {_tempLayerNames[_selectedLayer]}");
            ImGui.Separator();
            ImGui.TextWrapped("Check the layers that this layer can collide with:");
            ImGui.Spacing();

            ImGui.BeginChild("MatrixChild", new Vector2(0, -40), ImGuiChildFlags.Borders);

            // Afficher les checkboxes pour chaque layer
            for (int i = 0; i < 32; i++)
            {
                ImGui.PushID($"Collision_{_selectedLayer}_{i}");

                bool canCollide = _tempCollisionMatrix[_selectedLayer, i];

                if (ImGui.Checkbox($"[{i:D2}] {_tempLayerNames[i]}", ref canCollide))
                {
                    // Matrice symétrique
                    _tempCollisionMatrix[_selectedLayer, i] = canCollide;
                    _tempCollisionMatrix[i, _selectedLayer] = canCollide;
                }

                ImGui.PopID();
            }

            ImGui.EndChild();

            // Boutons rapides
            if (ImGui.Button("Enable All"))
            {
                for (int i = 0; i < 32; i++)
                {
                    _tempCollisionMatrix[_selectedLayer, i] = true;
                    _tempCollisionMatrix[i, _selectedLayer] = true;
                }
            }
            ImGui.SameLine();
            if (ImGui.Button("Disable All"))
            {
                for (int i = 0; i < 32; i++)
                {
                    _tempCollisionMatrix[_selectedLayer, i] = false;
                    _tempCollisionMatrix[i, _selectedLayer] = false;
                }
            }
        }

        private static void DrawFooter()
        {
            ImGui.Spacing();

            if (ImGui.Button("Apply", new Vector2(100, 0)))
            {
                ApplyChanges();
            }
            ImGui.SameLine();
            if (ImGui.Button("Apply & Close", new Vector2(120, 0)))
            {
                ApplyChanges();
                _isOpen = false;
            }
            ImGui.SameLine();
            if (ImGui.Button("Cancel", new Vector2(100, 0)))
            {
                _isOpen = false;
            }
            ImGui.SameLine();
            if (ImGui.Button("Reset to Default", new Vector2(140, 0)))
            {
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("Reset all layers and collision matrix to default values");

                CollisionLayers.Reset();
                Open(); // Recharger
            }
        }

        private static void ApplyChanges()
        {
            // Appliquer les noms de layers
            for (int i = 0; i < 32; i++)
            {
                CollisionLayers.SetLayerName(i, _tempLayerNames[i]);
            }

            // Appliquer la matrice de collision
            for (int i = 0; i < 32; i++)
            {
                for (int j = i; j < 32; j++) // Seulement la moitié supérieure (matrice symétrique)
                {
                    CollisionLayers.SetLayerCollision(i, j, _tempCollisionMatrix[i, j]);
                }
            }

            Console.WriteLine("[CollisionLayersPanel] Settings applied successfully.");
        }

        public static bool IsOpen => _isOpen;
    }
}
