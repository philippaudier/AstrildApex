using System;
using System.Numerics;
using ImGuiNET;

namespace Engine.UI.ImGuiMenu
{
    /// <summary>
    /// Panel Inventaire - Grid d'items avec détails
    /// </summary>
    public class InventoryPanel
    {
        private int _selectedItemIndex = -1;
        private readonly InventoryItem[] _testItems;
        
        public InventoryPanel()
        {
            // Test data
            _testItems = new[]
            {
                new InventoryItem { Name = "Épée en fer", Description = "Une épée basique en fer forgé", Rarity = RPGTheme.ItemRarity.Common, Quantity = 1 },
                new InventoryItem { Name = "Potion de vie", Description = "Restaure 50 PV", Rarity = RPGTheme.ItemRarity.Uncommon, Quantity = 5 },
                new InventoryItem { Name = "Armure runique", Description = "Armure légendaire gravée de runes anciennes", Rarity = RPGTheme.ItemRarity.Epic, Quantity = 1 },
                new InventoryItem { Name = "Gemme de mana", Description = "Pierre précieuse imprégnée de magie", Rarity = RPGTheme.ItemRarity.Rare, Quantity = 3 },
                new InventoryItem { Name = "Parchemin de téléportation", Description = "Usage unique - Téléporte au point de sauvegarde", Rarity = RPGTheme.ItemRarity.Rare, Quantity = 2 },
                new InventoryItem { Name = "Lame du destin", Description = "Arme légendaire forgée par les anciens dieux", Rarity = RPGTheme.ItemRarity.Legendary, Quantity = 1 },
                new InventoryItem { Name = "Viande séchée", Description = "Nourriture basique", Rarity = RPGTheme.ItemRarity.Common, Quantity = 12 },
                new InventoryItem { Name = "Arc elfique", Description = "Arc enchanté des forêts anciennes", Rarity = RPGTheme.ItemRarity.Epic, Quantity = 1 },
            };
        }
        
        public void Render()
        {
            // Two columns: Grid + Details
            ImGui.Columns(2, "InventoryColumns", true);
            
            // LEFT: Item grid
            ImGui.BeginChild("##ItemGrid", new Vector2(0, 0), ImGuiChildFlags.Borders);
            
            RPGTheme.SectionHeader("🎒 Inventaire (8/40)");
            
            ImGui.Spacing();
            
            // Grid layout (4 items per row)
            int itemsPerRow = 4;
            for (int i = 0; i < _testItems.Length; i++)
            {
                var item = _testItems[i];
                
                // Item card
                if (RPGTheme.ItemCard(
                    $"{item.Name} x{item.Quantity}",
                    GetRarityText(item.Rarity),
                    item.Rarity,
                    _selectedItemIndex == i))
                {
                    _selectedItemIndex = i;
                }
                
                // Tooltip
                if (ImGui.IsItemHovered())
                {
                    ImGui.BeginTooltip();
                    ImGui.Text(item.Name);
                    ImGui.TextColored(GetRarityColorVec4(item.Rarity), GetRarityText(item.Rarity));
                    ImGui.Separator();
                    ImGui.TextWrapped(item.Description);
                    ImGui.Text($"Quantité: {item.Quantity}");
                    ImGui.EndTooltip();
                }
                
                // New row
                if ((i + 1) % itemsPerRow != 0 && i < _testItems.Length - 1)
                {
                    ImGui.SameLine();
                }
            }
            
            ImGui.EndChild();
            
            ImGui.NextColumn();
            
            // RIGHT: Item details
            ImGui.BeginChild("##ItemDetails", new Vector2(0, 0), ImGuiChildFlags.Borders);
            
            if (_selectedItemIndex >= 0 && _selectedItemIndex < _testItems.Length)
            {
                var item = _testItems[_selectedItemIndex];
                
                RPGTheme.SectionHeader("📋 Détails");
                
                ImGui.Spacing();
                
                // Name
                ImGui.PushFont(ImGui.GetFont()); // TODO: Larger font
                ImGui.TextColored(GetRarityColorVec4(item.Rarity), item.Name);
                ImGui.PopFont();
                
                ImGui.Spacing();
                
                // Rarity badge
                ImGui.TextColored(GetRarityColorVec4(item.Rarity), $"✦ {GetRarityText(item.Rarity)}");
                
                ImGui.Spacing();
                ImGui.Separator();
                ImGui.Spacing();
                
                // Description
                ImGui.TextWrapped(item.Description);
                
                ImGui.Spacing();
                ImGui.Separator();
                ImGui.Spacing();
                
                // Stats
                ImGui.Text($"Quantité: {item.Quantity}");
                ImGui.Text($"Poids: {item.Quantity * 0.5f:F1} kg");
                
                ImGui.Spacing();
                ImGui.Separator();
                ImGui.Spacing();
                
                // Actions
                if (ImGui.Button("Utiliser", new Vector2(-1, 40)))
                {
                    Console.WriteLine($"[InventoryPanel] Use item: {item.Name}");
                }
                
                if (ImGui.Button("Équiper", new Vector2(-1, 40)))
                {
                    Console.WriteLine($"[InventoryPanel] Equip item: {item.Name}");
                }
                
                if (ImGui.Button("Jeter", new Vector2(-1, 40)))
                {
                    Console.WriteLine($"[InventoryPanel] Drop item: {item.Name}");
                }
            }
            else
            {
                ImGui.TextDisabled("Sélectionnez un objet pour voir les détails");
            }
            
            ImGui.EndChild();
            
            ImGui.Columns(1);
        }
        
        private string GetRarityText(RPGTheme.ItemRarity rarity)
        {
            return rarity switch
            {
                RPGTheme.ItemRarity.Common => "Commun",
                RPGTheme.ItemRarity.Uncommon => "Peu commun",
                RPGTheme.ItemRarity.Rare => "Rare",
                RPGTheme.ItemRarity.Epic => "Épique",
                RPGTheme.ItemRarity.Legendary => "Légendaire",
                _ => "Inconnu"
            };
        }
        
        private Vector4 GetRarityColorVec4(RPGTheme.ItemRarity rarity)
        {
            return rarity switch
            {
                RPGTheme.ItemRarity.Common => RPGTheme.RarityCommon,
                RPGTheme.ItemRarity.Uncommon => RPGTheme.RarityUncommon,
                RPGTheme.ItemRarity.Rare => RPGTheme.RarityRare,
                RPGTheme.ItemRarity.Epic => RPGTheme.RarityEpic,
                RPGTheme.ItemRarity.Legendary => RPGTheme.RarityLegendary,
                _ => RPGTheme.RarityCommon
            };
        }
        
        private class InventoryItem
        {
            public string Name { get; set; } = "";
            public string Description { get; set; } = "";
            public RPGTheme.ItemRarity Rarity { get; set; }
            public int Quantity { get; set; }
        }
    }
}
