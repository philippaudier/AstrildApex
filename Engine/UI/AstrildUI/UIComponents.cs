using System;
using System.Numerics;
using ImGuiNET;

namespace Engine.UI.AstrildUI
{
    /// <summary>
    /// Bibliothèque de composants UI high-level réutilisables
    /// </summary>
    public static class UIComponents
    {
        // ============================================
        // Cards
        // ============================================
        
        /// <summary>
        /// Card cliquable avec icône, titre et description
        /// </summary>
        public static bool Card(string title, string description, string? icon = null, bool selected = false, Vector2? size = null)
        {
            var cardSize = size ?? new Vector2(200, 100);
            
            // Style
            if (selected)
            {
                ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.3f, 0.5f, 0.9f, 0.3f));
                ImGui.PushStyleColor(ImGuiCol.Border, new Vector4(0.3f, 0.5f, 0.9f, 1.0f));
            }
            
            ImGui.PushStyleVar(ImGuiStyleVar.FrameBorderSize, 2f);
            
            bool clicked = ImGui.Button($"##{title}_card", cardSize);
            
            // Draw content on top
            var drawList = ImGui.GetWindowDrawList();
            var pos = ImGui.GetItemRectMin();
            var textColor = ImGui.ColorConvertFloat4ToU32(new Vector4(0.95f, 0.95f, 0.98f, 1.0f));
            
            // Icon (emoji centered at top)
            if (!string.IsNullOrEmpty(icon))
            {
                var iconSize = ImGui.CalcTextSize(icon);
                var iconPos = new Vector2(pos.X + (cardSize.X - iconSize.X) * 0.5f, pos.Y + 15);
                drawList.AddText(iconPos, textColor, icon);
            }
            
            // Title
            var titleSize = ImGui.CalcTextSize(title);
            var titlePos = new Vector2(pos.X + (cardSize.X - titleSize.X) * 0.5f, pos.Y + 45);
            drawList.AddText(titlePos, textColor, title);
            
            // Description
            var descColor = ImGui.ColorConvertFloat4ToU32(new Vector4(0.7f, 0.7f, 0.75f, 1.0f));
            var descSize = ImGui.CalcTextSize(description);
            var descPos = new Vector2(pos.X + (cardSize.X - descSize.X) * 0.5f, pos.Y + 70);
            drawList.AddText(descPos, descColor, description);
            
            ImGui.PopStyleVar();
            if (selected)
                ImGui.PopStyleColor(2);
            
            return clicked;
        }
        
        /// <summary>
        /// Item card avec rareté (pour inventaire)
        /// </summary>
        public static bool ItemCard(string name, ItemRarity rarity, int quantity = 1, bool selected = false)
        {
            var rarityColor = GetRarityColor(rarity);
            
            ImGui.PushStyleColor(ImGuiCol.Border, rarityColor);
            ImGui.PushStyleVar(ImGuiStyleVar.FrameBorderSize, 2f);
            
            if (selected)
            {
                ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(rarityColor.X * 0.3f, rarityColor.Y * 0.3f, rarityColor.Z * 0.3f, 0.5f));
            }
            
            bool clicked = ImGui.Button($"{name}##item_{name}", new Vector2(120, 80));
            
            // Quantity badge
            if (quantity > 1)
            {
                var drawList = ImGui.GetWindowDrawList();
                var pos = ImGui.GetItemRectMax();
                var badgePos = new Vector2(pos.X - 25, pos.Y - 20);
                var badgeText = $"x{quantity}";
                var textSize = ImGui.CalcTextSize(badgeText);
                
                drawList.AddRectFilled(
                    badgePos - new Vector2(5, 2),
                    badgePos + textSize + new Vector2(5, 2),
                    ImGui.ColorConvertFloat4ToU32(new Vector4(0, 0, 0, 0.8f)),
                    3f
                );
                drawList.AddText(badgePos, ImGui.ColorConvertFloat4ToU32(new Vector4(1, 1, 1, 1)), badgeText);
            }
            
            if (selected)
                ImGui.PopStyleColor();
            
            ImGui.PopStyleVar();
            ImGui.PopStyleColor();
            
            return clicked;
        }
        
        // ============================================
        // Stat Bars
        // ============================================
        
        /// <summary>
        /// Barre de stat colorée (HP, Mana, etc.)
        /// </summary>
        public static void StatBar(string label, float current, float max, Vector4 color, Vector2? size = null)
        {
            var barSize = size ?? new Vector2(-1, 20);
            
            ImGui.Text($"{label}: {current:F0} / {max:F0}");
            
            float fraction = max > 0 ? current / max : 0f;
            
            ImGui.PushStyleColor(ImGuiCol.PlotHistogram, color);
            ImGui.ProgressBar(fraction, barSize, "");
            ImGui.PopStyleColor();
        }
        
        /// <summary>
        /// Progress ring (circular)
        /// </summary>
        public static void ProgressRing(float progress, float radius = 30f, Vector4? color = null)
        {
            var drawList = ImGui.GetWindowDrawList();
            var center = ImGui.GetCursorScreenPos() + new Vector2(radius, radius);
            var ringColor = color ?? new Vector4(0.3f, 0.5f, 0.9f, 1.0f);
            
            // Background circle
            drawList.AddCircle(center, radius, ImGui.ColorConvertFloat4ToU32(new Vector4(0.2f, 0.2f, 0.25f, 1.0f)), 32, 3f);
            
            // Progress arc
            int segments = (int)(progress * 32);
            for (int i = 0; i < segments; i++)
            {
                float angle1 = (float)i / 32 * MathF.PI * 2 - MathF.PI / 2;
                float angle2 = (float)(i + 1) / 32 * MathF.PI * 2 - MathF.PI / 2;
                
                var p1 = center + new Vector2(MathF.Cos(angle1), MathF.Sin(angle1)) * radius;
                var p2 = center + new Vector2(MathF.Cos(angle2), MathF.Sin(angle2)) * radius;
                
                drawList.AddLine(p1, p2, ImGui.ColorConvertFloat4ToU32(ringColor), 3f);
            }
            
            // Percentage text
            var text = $"{progress * 100:F0}%";
            var textSize = ImGui.CalcTextSize(text);
            drawList.AddText(center - textSize * 0.5f, ImGui.ColorConvertFloat4ToU32(new Vector4(1, 1, 1, 1)), text);
            
            // Dummy to reserve space
            ImGui.Dummy(new Vector2(radius * 2, radius * 2));
        }
        
        // ============================================
        // Notifications
        // ============================================
        
        /// <summary>
        /// Toast notification temporaire
        /// </summary>
        public static void Toast(string message, ToastType type = ToastType.Info, float duration = 3f)
        {
            // TODO: Implement toast queue system
            var color = type switch
            {
                ToastType.Success => new Vector4(0.2f, 0.8f, 0.2f, 1),
                ToastType.Warning => new Vector4(1f, 0.8f, 0f, 1),
                ToastType.Error => new Vector4(0.8f, 0.2f, 0.2f, 1),
                _ => new Vector4(0.3f, 0.5f, 0.9f, 1)
            };
            
            ImGui.PushStyleColor(ImGuiCol.WindowBg, color);
            ImGui.SetNextWindowPos(new Vector2(ImGui.GetIO().DisplaySize.X - 320, 50), ImGuiCond.Always);
            ImGui.SetNextWindowSize(new Vector2(300, 0));
            
            if (ImGui.Begin("##Toast", ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoResize))
            {
                ImGui.TextWrapped(message);
            }
            ImGui.End();
            ImGui.PopStyleColor();
        }
        
        /// <summary>
        /// Modal dialog avec titre, message et boutons
        /// </summary>
        public static bool Modal(string id, string title, string message, out ModalResult result)
        {
            result = ModalResult.None;
            bool open = true;
            ModalResult localResult = ModalResult.None;
            
            ImGui.SetNextWindowSize(new Vector2(400, 200));
            if (ImGui.BeginPopupModal(id, ref open, ImGuiWindowFlags.NoResize))
            {
                ImGui.TextWrapped(message);
                ImGui.Spacing();
                ImGui.Separator();
                ImGui.Spacing();
                
                // Buttons centered
                float buttonWidth = 100f;
                float spacing = 20f;
                float totalWidth = buttonWidth * 2 + spacing;
                ImGui.SetCursorPosX((ImGui.GetContentRegionAvail().X - totalWidth) * 0.5f);
                
                if (ImGui.Button("OK", new Vector2(buttonWidth, 40)))
                {
                    localResult = ModalResult.OK;
                    ImGui.CloseCurrentPopup();
                }
                
                ImGui.SameLine();
                ImGui.Dummy(new Vector2(spacing, 0));
                ImGui.SameLine();
                
                if (ImGui.Button("Cancel", new Vector2(buttonWidth, 40)))
                {
                    localResult = ModalResult.Cancel;
                    ImGui.CloseCurrentPopup();
                }
                
                ImGui.EndPopup();
                result = localResult;
                return true;
            }
            
            return false;
        }
        
        // ============================================
        // Helpers
        // ============================================
        
        public enum ItemRarity
        {
            Common,
            Uncommon,
            Rare,
            Epic,
            Legendary
        }
        
        public enum ToastType
        {
            Info,
            Success,
            Warning,
            Error
        }
        
        public enum ModalResult
        {
            None,
            OK,
            Cancel,
            Yes,
            No
        }
        
        private static Vector4 GetRarityColor(ItemRarity rarity)
        {
            return rarity switch
            {
                ItemRarity.Common => new Vector4(0.7f, 0.7f, 0.7f, 1.0f),
                ItemRarity.Uncommon => new Vector4(0.4f, 0.8f, 0.4f, 1.0f),
                ItemRarity.Rare => new Vector4(0.4f, 0.6f, 1.0f, 1.0f),
                ItemRarity.Epic => new Vector4(0.8f, 0.4f, 1.0f, 1.0f),
                ItemRarity.Legendary => new Vector4(1.0f, 0.6f, 0.2f, 1.0f),
                _ => new Vector4(0.7f, 0.7f, 0.7f, 1.0f)
            };
        }
    }
}
