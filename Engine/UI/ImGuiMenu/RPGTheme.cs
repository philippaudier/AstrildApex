using System;
using System.Numerics;
using ImGuiNET;

namespace Engine.UI.ImGuiMenu
{
    /// <summary>
    /// Thème RPG custom pour ImGui avec couleurs dark fantasy et accents rouge
    /// </summary>
    public static class RPGTheme
    {
        // Color palette (dark fantasy theme with red accents)
        public static readonly Vector4 DarkBackground = new Vector4(0.08f, 0.08f, 0.10f, 0.95f);
        public static readonly Vector4 WindowBackground = new Vector4(0.12f, 0.12f, 0.15f, 0.98f);
        public static readonly Vector4 Border = new Vector4(0.91f, 0.27f, 0.38f, 0.8f); // #E94560 red
        public static readonly Vector4 BorderGlow = new Vector4(0.91f, 0.27f, 0.38f, 0.4f);
        public static readonly Vector4 TabActive = new Vector4(0.91f, 0.27f, 0.38f, 1.0f);
        public static readonly Vector4 TabInactive = new Vector4(0.2f, 0.2f, 0.25f, 1.0f);
        public static readonly Vector4 TabHover = new Vector4(0.75f, 0.22f, 0.31f, 1.0f);
        public static readonly Vector4 ButtonNormal = new Vector4(0.25f, 0.25f, 0.3f, 1.0f);
        public static readonly Vector4 ButtonHover = new Vector4(0.91f, 0.27f, 0.38f, 0.8f);
        public static readonly Vector4 ButtonActive = new Vector4(0.75f, 0.22f, 0.31f, 1.0f);
        public static readonly Vector4 TextPrimary = new Vector4(0.95f, 0.95f, 0.98f, 1.0f);
        public static readonly Vector4 TextSecondary = new Vector4(0.7f, 0.7f, 0.75f, 1.0f);
        public static readonly Vector4 TextDisabled = new Vector4(0.5f, 0.5f, 0.55f, 1.0f);
        public static readonly Vector4 FrameBg = new Vector4(0.15f, 0.15f, 0.18f, 1.0f);
        public static readonly Vector4 FrameBgHover = new Vector4(0.2f, 0.2f, 0.25f, 1.0f);
        public static readonly Vector4 FrameBgActive = new Vector4(0.25f, 0.25f, 0.3f, 1.0f);
        public static readonly Vector4 ScrollbarBg = new Vector4(0.1f, 0.1f, 0.12f, 1.0f);
        public static readonly Vector4 ScrollbarGrab = new Vector4(0.3f, 0.3f, 0.35f, 1.0f);
        public static readonly Vector4 ScrollbarGrabHover = new Vector4(0.4f, 0.4f, 0.45f, 1.0f);
        public static readonly Vector4 ScrollbarGrabActive = new Vector4(0.5f, 0.5f, 0.55f, 1.0f);
        public static readonly Vector4 Header = new Vector4(0.91f, 0.27f, 0.38f, 0.6f);
        public static readonly Vector4 HeaderHover = new Vector4(0.91f, 0.27f, 0.38f, 0.8f);
        public static readonly Vector4 HeaderActive = new Vector4(0.91f, 0.27f, 0.38f, 1.0f);
        
        // Rarity colors for items
        public static readonly Vector4 RarityCommon = new Vector4(0.7f, 0.7f, 0.7f, 1.0f);
        public static readonly Vector4 RarityUncommon = new Vector4(0.4f, 0.8f, 0.4f, 1.0f);
        public static readonly Vector4 RarityRare = new Vector4(0.4f, 0.6f, 1.0f, 1.0f);
        public static readonly Vector4 RarityEpic = new Vector4(0.8f, 0.4f, 1.0f, 1.0f);
        public static readonly Vector4 RarityLegendary = new Vector4(1.0f, 0.6f, 0.2f, 1.0f);
        
        /// <summary>
        /// Push le style RPG sur la stack ImGui
        /// </summary>
        public static void PushStyle()
        {
            // Remove guard - ImGui stack is per-frame, safe to push multiple times
            // Each PushStyle MUST be paired with PopStyle in same scope
            
            // Colors
            ImGui.PushStyleColor(ImGuiCol.WindowBg, WindowBackground);
            ImGui.PushStyleColor(ImGuiCol.Border, Border);
            ImGui.PushStyleColor(ImGuiCol.BorderShadow, BorderGlow);
            ImGui.PushStyleColor(ImGuiCol.Button, ButtonNormal);
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, ButtonHover);
            ImGui.PushStyleColor(ImGuiCol.ButtonActive, ButtonActive);
            ImGui.PushStyleColor(ImGuiCol.Text, TextPrimary);
            ImGui.PushStyleColor(ImGuiCol.TextDisabled, TextDisabled);
            ImGui.PushStyleColor(ImGuiCol.FrameBg, FrameBg);
            ImGui.PushStyleColor(ImGuiCol.FrameBgHovered, FrameBgHover);
            ImGui.PushStyleColor(ImGuiCol.FrameBgActive, FrameBgActive);
            ImGui.PushStyleColor(ImGuiCol.ScrollbarBg, ScrollbarBg);
            ImGui.PushStyleColor(ImGuiCol.ScrollbarGrab, ScrollbarGrab);
            ImGui.PushStyleColor(ImGuiCol.ScrollbarGrabHovered, ScrollbarGrabHover);
            ImGui.PushStyleColor(ImGuiCol.ScrollbarGrabActive, ScrollbarGrabActive);
            ImGui.PushStyleColor(ImGuiCol.Header, Header);
            ImGui.PushStyleColor(ImGuiCol.HeaderHovered, HeaderHover);
            ImGui.PushStyleColor(ImGuiCol.HeaderActive, HeaderActive);
            ImGui.PushStyleColor(ImGuiCol.Tab, TabInactive);
            ImGui.PushStyleColor(ImGuiCol.TabHovered, TabHover);
            ImGui.PushStyleColor(ImGuiCol.TabSelected, TabActive);
            ImGui.PushStyleColor(ImGuiCol.Separator, Border);
            
            // Vars
            ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, 8f);         // 1
            ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 2f);     // 2
            ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 4f);        // 3
            ImGui.PushStyleVar(ImGuiStyleVar.FrameBorderSize, 1f);      // 4
            ImGui.PushStyleVar(ImGuiStyleVar.ScrollbarRounding, 4f);    // 5
            ImGui.PushStyleVar(ImGuiStyleVar.ScrollbarSize, 12f);       // 6
            ImGui.PushStyleVar(ImGuiStyleVar.GrabRounding, 3f);         // 7
            ImGui.PushStyleVar(ImGuiStyleVar.TabRounding, 4f);          // 8
            ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(15f, 15f));      // 9
            ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(8f, 6f));         // 10
            ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(12f, 8f));         // 11
            ImGui.PushStyleVar(ImGuiStyleVar.ItemInnerSpacing, new Vector2(8f, 6f));     // 12
        }
        
        /// <summary>
        /// Pop le style RPG de la stack ImGui
        /// </summary>
        public static void PopStyle()
        {
            // Pop in reverse order of push
            ImGui.PopStyleVar(12);   // 12 vars pushed
            ImGui.PopStyleColor(22); // 22 colors pushed
        }
        
        /// <summary>
        /// Bouton de tab custom avec style
        /// </summary>
        public static bool TabButton(string label, bool isActive)
        {
            if (isActive)
            {
                ImGui.PushStyleColor(ImGuiCol.Button, TabActive);
                ImGui.PushStyleColor(ImGuiCol.ButtonHovered, TabActive);
                ImGui.PushStyleColor(ImGuiCol.ButtonActive, TabActive);
            }
            else
            {
                ImGui.PushStyleColor(ImGuiCol.Button, TabInactive);
                ImGui.PushStyleColor(ImGuiCol.ButtonHovered, TabHover);
                ImGui.PushStyleColor(ImGuiCol.ButtonActive, ButtonActive);
            }
            
            bool clicked = ImGui.Button(label, new Vector2(150, 40));
            
            ImGui.PopStyleColor(3);
            
            return clicked;
        }
        
        /// <summary>
        /// Texte avec icône emoji
        /// </summary>
        public static void TextWithIcon(string icon, string text)
        {
            ImGui.Text(icon);
            ImGui.SameLine();
            ImGui.Text(text);
        }
        
        /// <summary>
        /// Header section avec background coloré
        /// </summary>
        public static bool SectionHeader(string title, bool defaultOpen = true)
        {
            ImGui.PushStyleColor(ImGuiCol.Header, Header);
            ImGui.PushStyleColor(ImGuiCol.HeaderHovered, HeaderHover);
            ImGui.PushStyleColor(ImGuiCol.HeaderActive, HeaderActive);
            
            bool open = ImGui.CollapsingHeader(title, defaultOpen ? ImGuiTreeNodeFlags.DefaultOpen : ImGuiTreeNodeFlags.None);
            
            ImGui.PopStyleColor(3);
            
            return open;
        }
        
        /// <summary>
        /// Item card (pour inventaire avec rareté)
        /// </summary>
        public static bool ItemCard(string name, string description, ItemRarity rarity, bool isSelected = false)
        {
            Vector4 rarityColor = GetRarityColor(rarity);
            
            ImGui.PushID(name);
            ImGui.PushStyleColor(ImGuiCol.Border, rarityColor);
            ImGui.PushStyleVar(ImGuiStyleVar.FrameBorderSize, 2f);
            
            if (isSelected)
            {
                ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(rarityColor.X * 0.3f, rarityColor.Y * 0.3f, rarityColor.Z * 0.3f, 0.5f));
            }
            
            bool clicked = ImGui.Button($"{name}\n{description}", new Vector2(120, 80));
            
            if (isSelected)
            {
                ImGui.PopStyleColor();
            }
            
            ImGui.PopStyleVar();
            ImGui.PopStyleColor();
            ImGui.PopID();
            
            return clicked;
        }
        
        public enum ItemRarity
        {
            Common,
            Uncommon,
            Rare,
            Epic,
            Legendary
        }
        
        private static Vector4 GetRarityColor(ItemRarity rarity)
        {
            return rarity switch
            {
                ItemRarity.Common => RarityCommon,
                ItemRarity.Uncommon => RarityUncommon,
                ItemRarity.Rare => RarityRare,
                ItemRarity.Epic => RarityEpic,
                ItemRarity.Legendary => RarityLegendary,
                _ => RarityCommon
            };
        }
        
        /// <summary>
        /// Stat bar (health, mana, etc.)
        /// </summary>
        public static void StatBar(string label, float current, float max, Vector4 color)
        {
            ImGui.Text($"{label}: {current:F0} / {max:F0}");
            
            float fraction = max > 0 ? current / max : 0f;
            
            ImGui.PushStyleColor(ImGuiCol.PlotHistogram, color);
            ImGui.ProgressBar(fraction, new Vector2(-1, 20), "");
            ImGui.PopStyleColor();
        }
        
        /// <summary>
        /// Tooltip custom avec style
        /// </summary>
        public static void Tooltip(string text)
        {
            if (ImGui.IsItemHovered())
            {
                ImGui.BeginTooltip();
                ImGui.PushTextWrapPos(300f);
                ImGui.TextUnformatted(text);
                ImGui.PopTextWrapPos();
                ImGui.EndTooltip();
            }
        }
    }
}
