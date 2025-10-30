using System;
using System.Numerics;
using ImGuiNET;

namespace Engine.UI.AstrildUI
{
    /// <summary>
    /// Helpers pour layouts communs (Grid, Stack, Split, Tabs)
    /// </summary>
    public static class UILayout
    {
        /// <summary>
        /// Layout en colonnes avec ratios personnalisables
        /// Exemple: Split(0.3f, leftContent, rightContent) → 30% / 70%
        /// </summary>
        public static void Split(float ratio, Action leftContent, Action rightContent, bool border = true)
        {
            ImGui.Columns(2, "split", border);
            
            // Set column width based on ratio
            var totalWidth = ImGui.GetContentRegionAvail().X;
            ImGui.SetColumnWidth(0, totalWidth * ratio);
            
            // Left
            leftContent?.Invoke();
            ImGui.NextColumn();
            
            // Right
            rightContent?.Invoke();
            
            ImGui.Columns(1);
        }
        
        /// <summary>
        /// Grid layout avec nombre de colonnes fixe
        /// Exemple: Grid(3, () => { Button(...); Button(...); Button(...); })
        /// </summary>
        public static void Grid(int columns, Action content)
        {
            ImGui.Columns(columns, $"grid_{columns}", false);
            
            content?.Invoke();
            
            ImGui.Columns(1);
        }
        
        /// <summary>
        /// Stack vertical avec espacement automatique
        /// </summary>
        public static void VStack(Action content, float spacing = 8f)
        {
            ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(0, spacing));
            content?.Invoke();
            ImGui.PopStyleVar();
        }
        
        /// <summary>
        /// Stack horizontal avec espacement automatique
        /// </summary>
        public static void HStack(Action content, float spacing = 8f)
        {
            ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(spacing, 0));
            content?.Invoke();
            ImGui.PopStyleVar();
        }
        
        /// <summary>
        /// Centrer horizontalement un contenu de taille connue
        /// </summary>
        public static void CenterH(float width, Action content)
        {
            var availWidth = ImGui.GetContentRegionAvail().X;
            var offsetX = (availWidth - width) * 0.5f;
            if (offsetX > 0)
                ImGui.SetCursorPosX(ImGui.GetCursorPosX() + offsetX);
            
            content?.Invoke();
        }
        
        /// <summary>
        /// Centrer verticalement un contenu de taille connue
        /// </summary>
        public static void CenterV(float height, Action content)
        {
            var availHeight = ImGui.GetContentRegionAvail().Y;
            var offsetY = (availHeight - height) * 0.5f;
            if (offsetY > 0)
                ImGui.SetCursorPosY(ImGui.GetCursorPosY() + offsetY);
            
            content?.Invoke();
        }
        
        /// <summary>
        /// Tabs système (BeginTabBar)
        /// </summary>
        public static void Tabs(string id, params (string label, Action content)[] tabs)
        {
            if (ImGui.BeginTabBar(id))
            {
                foreach (var (label, content) in tabs)
                {
                    if (ImGui.BeginTabItem(label))
                    {
                        ImGui.Spacing();
                        content?.Invoke();
                        ImGui.EndTabItem();
                    }
                }
                ImGui.EndTabBar();
            }
        }
        
        /// <summary>
        /// Scroll area avec taille fixe
        /// </summary>
        public static void ScrollArea(string id, Vector2 size, Action content, bool border = true)
        {
            var flags = border ? ImGuiChildFlags.Borders : ImGuiChildFlags.None;
            
            if (ImGui.BeginChild(id, size, flags))
            {
                content?.Invoke();
            }
            ImGui.EndChild();
        }
        
        /// <summary>
        /// Groupe inline (éléments sur la même ligne sans espacement)
        /// </summary>
        public static void Inline(params Action[] items)
        {
            for (int i = 0; i < items.Length; i++)
            {
                items[i]?.Invoke();
                if (i < items.Length - 1)
                    ImGui.SameLine();
            }
        }
        
        /// <summary>
        /// Padding autour d'un contenu
        /// </summary>
        public static void Padding(float all, Action content)
        {
            Padding(all, all, content);
        }
        
        public static void Padding(float horizontal, float vertical, Action content)
        {
            ImGui.SetCursorPos(ImGui.GetCursorPos() + new Vector2(horizontal, vertical));
            content?.Invoke();
        }
    }
}
