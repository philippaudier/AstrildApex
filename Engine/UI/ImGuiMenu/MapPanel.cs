using System;
using System.Numerics;
using ImGuiNET;

namespace Engine.UI.ImGuiMenu
{
    /// <summary>
    /// Panel Carte - Mini-map et navigation
    /// </summary>
    public class MapPanel
    {
        private Vector2 _mapCenter = new Vector2(0, 0);
        private float _mapZoom = 1.0f;
        
        // Test locations
        private readonly MapLocation[] _locations = new[]
        {
            new MapLocation { Name = "Village de départ", X = 100, Y = 150, Icon = "🏘️", Visited = true },
            new MapLocation { Name = "Forêt sombre", X = 250, Y = 180, Icon = "🌲", Visited = true },
            new MapLocation { Name = "Donjon ancien", X = 400, Y = 220, Icon = "⚔️", Visited = false },
            new MapLocation { Name = "Montagne glacée", X = 300, Y = 50, Icon = "🏔️", Visited = false },
            new MapLocation { Name = "Port de la mer", X = 500, Y = 300, Icon = "⚓", Visited = true },
        };
        
        public void Render()
        {
            // Two columns: Map + Legend
            ImGui.Columns(2, "MapColumns", true);
            
            // LEFT: Map canvas
            ImGui.BeginChild("##MapCanvas", new Vector2(0, 0), ImGuiChildFlags.Borders);
            
            RenderMapCanvas();
            
            ImGui.EndChild();
            
            ImGui.NextColumn();
            
            // RIGHT: Legend and controls
            ImGui.BeginChild("##MapLegend", new Vector2(0, 0), ImGuiChildFlags.Borders);
            
            RenderMapControls();
            ImGui.Spacing();
            RenderLocationList();
            
            ImGui.EndChild();
            
            ImGui.Columns(1);
        }
        
        private void RenderMapCanvas()
        {
            RPGTheme.SectionHeader("🗺️ Carte du monde");
            
            ImGui.Spacing();
            
            // Get canvas area
            var canvasPos = ImGui.GetCursorScreenPos();
            var canvasSize = ImGui.GetContentRegionAvail();
            
            // Draw map background
            var drawList = ImGui.GetWindowDrawList();
            
            // Background
            uint bgColor = ImGui.ColorConvertFloat4ToU32(new Vector4(0.1f, 0.12f, 0.15f, 1.0f));
            drawList.AddRectFilled(canvasPos, canvasPos + canvasSize, bgColor);
            
            // Grid
            uint gridColor = ImGui.ColorConvertFloat4ToU32(new Vector4(0.2f, 0.22f, 0.25f, 0.5f));
            int gridStep = 50;
            for (int x = 0; x < canvasSize.X; x += gridStep)
            {
                drawList.AddLine(
                    canvasPos + new Vector2(x, 0),
                    canvasPos + new Vector2(x, canvasSize.Y),
                    gridColor);
            }
            for (int y = 0; y < canvasSize.Y; y += gridStep)
            {
                drawList.AddLine(
                    canvasPos + new Vector2(0, y),
                    canvasPos + new Vector2(canvasSize.X, y),
                    gridColor);
            }
            
            // Draw locations
            foreach (var loc in _locations)
            {
                Vector2 screenPos = canvasPos + new Vector2(loc.X * _mapZoom, loc.Y * _mapZoom) + _mapCenter;
                
                // Skip if out of bounds
                if (screenPos.X < canvasPos.X || screenPos.X > canvasPos.X + canvasSize.X ||
                    screenPos.Y < canvasPos.Y || screenPos.Y > canvasPos.Y + canvasSize.Y)
                {
                    continue;
                }
                
                // Draw location marker
                uint markerColor = loc.Visited
                    ? ImGui.ColorConvertFloat4ToU32(RPGTheme.TabActive)
                    : ImGui.ColorConvertFloat4ToU32(RPGTheme.TextDisabled);
                
                drawList.AddCircleFilled(screenPos, 8f * _mapZoom, markerColor, 16);
                drawList.AddCircle(screenPos, 8f * _mapZoom, ImGui.ColorConvertFloat4ToU32(RPGTheme.Border), 16, 2f);
                
                // Draw icon (simplified - actual implementation would use icon texture)
                var textSize = ImGui.CalcTextSize(loc.Icon);
                drawList.AddText(screenPos - textSize * 0.5f, ImGui.ColorConvertFloat4ToU32(RPGTheme.TextPrimary), loc.Icon);
            }
            
            // Invisible button for interaction
            ImGui.SetCursorScreenPos(canvasPos);
            ImGui.InvisibleButton("##MapCanvas", canvasSize);
            
            // Handle dragging
            if (ImGui.IsItemActive() && ImGui.IsMouseDragging(ImGuiMouseButton.Left))
            {
                var delta = ImGui.GetMouseDragDelta(ImGuiMouseButton.Left);
                _mapCenter += delta;
                ImGui.ResetMouseDragDelta(ImGuiMouseButton.Left);
            }
            
            // Handle zoom with mouse wheel
            if (ImGui.IsItemHovered())
            {
                float wheel = ImGui.GetIO().MouseWheel;
                if (wheel != 0)
                {
                    _mapZoom = Math.Clamp(_mapZoom + wheel * 0.1f, 0.5f, 3.0f);
                }
            }
        }
        
        private void RenderMapControls()
        {
            if (RPGTheme.SectionHeader("🎮 Contrôles"))
            {
                ImGui.Indent();
                
                ImGui.Text($"Zoom: {_mapZoom:F1}x");
                
                if (ImGui.Button("Zoom +", new Vector2(-1, 30)))
                {
                    _mapZoom = Math.Clamp(_mapZoom + 0.2f, 0.5f, 3.0f);
                }
                
                if (ImGui.Button("Zoom -", new Vector2(-1, 30)))
                {
                    _mapZoom = Math.Clamp(_mapZoom - 0.2f, 0.5f, 3.0f);
                }
                
                if (ImGui.Button("Réinitialiser", new Vector2(-1, 30)))
                {
                    _mapCenter = new Vector2(0, 0);
                    _mapZoom = 1.0f;
                }
                
                ImGui.Unindent();
                
                ImGui.Spacing();
                ImGui.TextDisabled("💡 Faites glisser pour déplacer la carte");
                ImGui.TextDisabled("💡 Molette pour zoomer");
            }
        }
        
        private void RenderLocationList()
        {
            if (RPGTheme.SectionHeader("📍 Lieux découverts"))
            {
                ImGui.Indent();
                
                foreach (var loc in _locations)
                {
                    if (loc.Visited)
                    {
                        ImGui.TextColored(RPGTheme.TabActive, $"{loc.Icon} {loc.Name}");
                        
                        if (ImGui.IsItemHovered())
                        {
                            ImGui.BeginTooltip();
                            ImGui.Text(loc.Name);
                            ImGui.Separator();
                            ImGui.Text($"Position: ({loc.X}, {loc.Y})");
                            ImGui.EndTooltip();
                        }
                        
                        ImGui.SameLine(ImGui.GetContentRegionAvail().X - 80);
                        if (ImGui.SmallButton($"Aller##loc_{loc.Name}"))
                        {
                            Console.WriteLine($"[MapPanel] Navigate to: {loc.Name}");
                            // Center map on location
                            var canvasSize = ImGui.GetContentRegionAvail();
                            _mapCenter = new Vector2(canvasSize.X * 0.5f - loc.X * _mapZoom, canvasSize.Y * 0.5f - loc.Y * _mapZoom);
                        }
                    }
                    else
                    {
                        ImGui.TextDisabled($"??? Lieu inconnu");
                    }
                }
                
                ImGui.Unindent();
            }
        }
        
        private class MapLocation
        {
            public string Name { get; set; } = "";
            public float X { get; set; }
            public float Y { get; set; }
            public string Icon { get; set; } = "";
            public bool Visited { get; set; }
        }
    }
}
