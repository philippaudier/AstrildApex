using System;
using System.Numerics;
using ImGuiNET;

namespace Engine.UI.ImGuiMenu
{
    /// <summary>
    /// Panel Personnage - Stats, équipement, compétences
    /// </summary>
    public class CharacterPanel
    {
        // Test character data
        private readonly CharacterStats _stats = new CharacterStats
        {
            Name = "Héros",
            Level = 12,
            Class = "Guerrier",
            Health = 450f,
            MaxHealth = 500f,
            Mana = 120f,
            MaxMana = 150f,
            Stamina = 80f,
            MaxStamina = 100f,
            Strength = 25,
            Dexterity = 18,
            Intelligence = 12,
            Vitality = 22,
            Experience = 3450,
            NextLevelExp = 5000
        };
        
        public void Render()
        {
            // Two columns: Stats + Equipment
            ImGui.Columns(2, "CharacterColumns", true);
            
            // LEFT: Character stats
            ImGui.BeginChild("##CharacterStats", new Vector2(0, 0), ImGuiChildFlags.Borders);
            
            RenderCharacterInfo();
            ImGui.Spacing();
            RenderVitalStats();
            ImGui.Spacing();
            RenderAttributes();
            ImGui.Spacing();
            RenderExperience();
            
            ImGui.EndChild();
            
            ImGui.NextColumn();
            
            // RIGHT: Equipment
            ImGui.BeginChild("##Equipment", new Vector2(0, 0), ImGuiChildFlags.Borders);
            
            RenderEquipment();
            
            ImGui.EndChild();
            
            ImGui.Columns(1);
        }
        
        private void RenderCharacterInfo()
        {
            if (RPGTheme.SectionHeader("👤 Informations"))
            {
                ImGui.Indent();
                
                ImGui.Text($"Nom: {_stats.Name}");
                ImGui.Text($"Classe: {_stats.Class}");
                ImGui.Text($"Niveau: {_stats.Level}");
                
                ImGui.Unindent();
            }
        }
        
        private void RenderVitalStats()
        {
            if (RPGTheme.SectionHeader("❤️ Statistiques vitales"))
            {
                ImGui.Indent();
                
                // Health
                RPGTheme.StatBar("Vie", _stats.Health, _stats.MaxHealth, new Vector4(0.8f, 0.2f, 0.2f, 1.0f));
                
                ImGui.Spacing();
                
                // Mana
                RPGTheme.StatBar("Mana", _stats.Mana, _stats.MaxMana, new Vector4(0.2f, 0.4f, 1.0f, 1.0f));
                
                ImGui.Spacing();
                
                // Stamina
                RPGTheme.StatBar("Endurance", _stats.Stamina, _stats.MaxStamina, new Vector4(0.2f, 0.8f, 0.2f, 1.0f));
                
                ImGui.Unindent();
            }
        }
        
        private void RenderAttributes()
        {
            if (RPGTheme.SectionHeader("⚔️ Attributs"))
            {
                ImGui.Indent();
                
                ImGui.Columns(2, "AttributesColumns", false);
                
                // Left column
                ImGui.Text("Force");
                ImGui.Text("Dextérité");
                
                ImGui.NextColumn();
                
                // Right column
                ImGui.Text($"{_stats.Strength}");
                ImGui.Text($"{_stats.Dexterity}");
                
                ImGui.NextColumn();
                
                // Left column
                ImGui.Text("Intelligence");
                ImGui.Text("Vitalité");
                
                ImGui.NextColumn();
                
                // Right column
                ImGui.Text($"{_stats.Intelligence}");
                ImGui.Text($"{_stats.Vitality}");
                
                ImGui.Columns(1);
                
                ImGui.Unindent();
            }
        }
        
        private void RenderExperience()
        {
            if (RPGTheme.SectionHeader("⭐ Expérience"))
            {
                ImGui.Indent();
                
                RPGTheme.StatBar("XP", _stats.Experience, _stats.NextLevelExp, new Vector4(1.0f, 0.84f, 0.0f, 1.0f));
                
                ImGui.Text($"Prochain niveau: {_stats.NextLevelExp - _stats.Experience} XP");
                
                ImGui.Unindent();
            }
        }
        
        private void RenderEquipment()
        {
            if (RPGTheme.SectionHeader("🛡️ Équipement"))
            {
                ImGui.Indent();
                
                RenderEquipmentSlot("Arme principale", "Épée en fer");
                RenderEquipmentSlot("Arme secondaire", "Bouclier en bois");
                
                ImGui.Spacing();
                
                RenderEquipmentSlot("Casque", "Casque de cuir");
                RenderEquipmentSlot("Armure", "Armure runique");
                RenderEquipmentSlot("Gants", "Gants en fer");
                RenderEquipmentSlot("Bottes", "Bottes de voyageur");
                
                ImGui.Spacing();
                
                RenderEquipmentSlot("Anneau 1", "Anneau de force +2");
                RenderEquipmentSlot("Anneau 2", "Vide");
                RenderEquipmentSlot("Amulette", "Amulette de protection");
                
                ImGui.Unindent();
            }
            
            ImGui.Spacing();
            
            if (RPGTheme.SectionHeader("✨ Compétences"))
            {
                ImGui.Indent();
                
                RenderSkill("Coup puissant", "Dégâts +50%", 5);
                RenderSkill("Parade", "Bloque 30% des dégâts", 3);
                RenderSkill("Charge", "Fonce vers l'ennemi", 8);
                RenderSkill("Cri de guerre", "Augmente les dégâts de l'équipe", 12);
                
                ImGui.Unindent();
            }
        }
        
        private void RenderEquipmentSlot(string slotName, string itemName)
        {
            ImGui.Text($"{slotName}:");
            ImGui.SameLine(150);
            
            if (itemName == "Vide")
            {
                ImGui.TextDisabled(itemName);
            }
            else
            {
                ImGui.TextColored(RPGTheme.RarityUncommon, itemName);
            }
        }
        
        private void RenderSkill(string name, string description, int level)
        {
            ImGui.Text($"{name} (Nv.{level})");
            if (ImGui.IsItemHovered())
            {
                ImGui.BeginTooltip();
                ImGui.Text(name);
                ImGui.Separator();
                ImGui.TextWrapped(description);
                ImGui.Text($"Niveau: {level}");
                ImGui.EndTooltip();
            }
        }
        
        private class CharacterStats
        {
            public string Name { get; set; } = "";
            public int Level { get; set; }
            public string Class { get; set; } = "";
            public float Health { get; set; }
            public float MaxHealth { get; set; }
            public float Mana { get; set; }
            public float MaxMana { get; set; }
            public float Stamina { get; set; }
            public float MaxStamina { get; set; }
            public int Strength { get; set; }
            public int Dexterity { get; set; }
            public int Intelligence { get; set; }
            public int Vitality { get; set; }
            public int Experience { get; set; }
            public int NextLevelExp { get; set; }
        }
    }
}
