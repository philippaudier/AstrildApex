# 🎨 AstrildUI - Système UI Déclaratif

**AstrildUI** est un système de UI déclaratif et intuitif pour le moteur AstrildApex, construit au-dessus d'ImGui.NET avec une API fluide et des composants high-level.

---

## 📦 Architecture

### Composants Principaux

```
Engine/UI/AstrildUI/
├── UIBuilder.cs        - API fluide pour construction déclarative
├── UIStyleSheet.cs     - Système de thèmes et styles
├── UILayout.cs         - Helpers de layout (grilles, stacks, splits)
└── UIComponents.cs     - Composants réutilisables (cards, bars, toasts)
```

### Philosophie

- **Déclaratif** : Décrivez ce que vous voulez, pas comment le construire
- **Fluent API** : Chaînage de méthodes pour un code lisible
- **Thématique** : 4 thèmes prédéfinis + customisation facile
- **Composable** : Assemblez des composants pour créer des UIs complexes

---

## 🚀 Quick Start

### Exemple Basique

```csharp
using Engine.UI.AstrildUI;

// Créer un builder avec thème RPG
var ui = new UIBuilder(UIStyleSheet.Default);

// Construire une fenêtre
ui.Window("Inventory", () =>
{
    ui.Text("Welcome to your inventory!", UITextStyle.Colored);
    ui.Separator();
    
    if (ui.Button("Open Chest", style: UIButtonStyle.Primary))
    {
        Console.WriteLine("Chest opened!");
    }
});
```

### Menu RPG Complet

```csharp
var theme = UIStyleSheet.CreateRPGTheme();
var ui = new UIBuilder(theme);

ui.Window("RPG Menu", () =>
{
    UILayout.Tabs("main_tabs", new[]
    {
        ("Inventory", () =>
        {
            UILayout.Grid(4, () =>
            {
                UIComponents.ItemCard("Iron Sword", ItemRarity.Common, 1);
                UIComponents.ItemCard("Health Potion", ItemRarity.Uncommon, 5);
                UIComponents.ItemCard("Dragon Scale", ItemRarity.Legendary, 1);
            });
        }),
        
        ("Character", () =>
        {
            UILayout.Split(0.5f,
                () => // Left: Stats
                {
                    ui.Text("Stats", UITextStyle.Colored);
                    UIComponents.StatBar("Health", 85, 100, new Vector4(0.8f, 0.2f, 0.2f, 1));
                    UIComponents.StatBar("Mana", 60, 100, new Vector4(0.2f, 0.4f, 0.9f, 1));
                    UIComponents.StatBar("Stamina", 45, 100, new Vector4(0.2f, 0.8f, 0.2f, 1));
                },
                () => // Right: Equipment
                {
                    ui.Text("Equipment", UITextStyle.Colored);
                    ui.Button("Helmet: None");
                    ui.Button("Chest: Iron Armor");
                    ui.Button("Weapon: Iron Sword");
                }
            );
        }),
        
        ("Map", () =>
        {
            ui.Text("World Map", UITextStyle.Colored);
            UIComponents.ProgressRing(0.65f, 50, new Vector4(0.91f, 0.27f, 0.38f, 1));
            ui.Text("Quest Progress: 65%");
        })
    });
}, new UIWindowOptions
{
    Size = new Vector2(800, 600)
});
```

---

## 📘 API Reference

### UIBuilder

#### Création

```csharp
var ui = new UIBuilder(UIStyleSheet.Default);
```

#### Fenêtres et Conteneurs

```csharp
// Fenêtre principale
ui.Window("Title", content, new UIWindowOptions
{
    Size = new Vector2(800, 600),
    Position = new Vector2(100, 100),
    BackgroundAlpha = 0.95f,
    Flags = ImGuiWindowFlags.NoResize
});

// Panel child
ui.Panel("panel_id", content, new UIPanelOptions
{
    Size = new Vector2(300, 400),
    HasBorder = true
});

// Collapsing header
ui.CollapsingHeader("Section", content, defaultOpen: true);
```

#### Contrôles

```csharp
// Bouton
if (ui.Button("Click Me", onClick: () => { }, style: UIButtonStyle.Primary))
{
    // Action
}

// Texte stylisé
ui.Text("Normal text");
ui.Text("Warning!", UITextStyle.Warning);
ui.Text("Error!", UITextStyle.Error);
ui.Text("Disabled", UITextStyle.Disabled);
ui.Text("Custom color", UITextStyle.Colored, new Vector4(1, 0, 0, 1));

// Input text
string value = "test";
ui.InputText("Label", ref value);

// Slider
float floatValue = 0.5f;
ui.SliderFloat("Volume", ref floatValue, 0f, 1f);

// Checkbox
bool boolValue = true;
ui.Checkbox("Enable feature", ref boolValue);

// Combo dropdown
string[] items = { "Option 1", "Option 2", "Option 3" };
int selected = 0;
ui.Combo("Select", ref selected, items);
```

#### Layout

```csharp
// Separator
ui.Separator();

// Spacing
ui.Spacing();

// Same line
ui.SameLine();
```

---

### UIStyleSheet

#### Thèmes Prédéfinis

```csharp
// RPG Theme (rouge #E94560, dark fantasy)
var rpgTheme = UIStyleSheet.CreateRPGTheme();

// SciFi Theme (cyan neon #00B3FF, sharp corners)
var sciFiTheme = UIStyleSheet.CreateSciFiTheme();

// Minimal Theme (clair, blue accents)
var minimalTheme = UIStyleSheet.CreateMinimalTheme();

// Fantasy Theme (gold #B39619, warm browns)
var fantasyTheme = UIStyleSheet.CreateFantasyTheme();
```

#### Customisation

```csharp
var customTheme = new UIStyleSheet
{
    PrimaryColor = new Vector4(1, 0, 0, 1),
    PrimaryHoverColor = new Vector4(1, 0.2f, 0.2f, 1),
    BackgroundColor = new Vector4(0.1f, 0.1f, 0.15f, 1),
    WindowBackgroundColor = new Vector4(0.15f, 0.15f, 0.2f, 0.95f),
    BorderColor = new Vector4(1, 0, 0, 1),
    TextColor = new Vector4(0.95f, 0.95f, 0.98f, 1),
    WindowRounding = 8f,
    FrameRounding = 4f,
    WindowPadding = new Vector2(16, 16),
    FramePadding = new Vector2(8, 6),
    ItemSpacing = new Vector2(12, 8)
};

// Appliquer
customTheme.Push();
// ... render UI ...
customTheme.Pop();
```

#### Usage avec UIBuilder

```csharp
var ui = new UIBuilder(customTheme);
// Le thème est appliqué automatiquement
```

---

### UILayout

#### Split (colonnes)

```csharp
UILayout.Split(0.6f,
    () => { /* Left: 60% */ },
    () => { /* Right: 40% */ }
);
```

#### Grid

```csharp
UILayout.Grid(4, () =>
{
    ui.Button("Item 1");
    ui.Button("Item 2");
    ui.Button("Item 3");
    ui.Button("Item 4");
    ui.Button("Item 5"); // Wraps to next row
});
```

#### Stacks

```csharp
// Vertical stack
UILayout.VStack(() =>
{
    ui.Text("Line 1");
    ui.Text("Line 2");
    ui.Text("Line 3");
}, spacing: 10);

// Horizontal stack
UILayout.HStack(() =>
{
    ui.Button("Button 1");
    ui.Button("Button 2");
    ui.Button("Button 3");
}, spacing: 5);
```

#### Centering

```csharp
// Center horizontally
UILayout.CenterH(200, () =>
{
    ui.Button("Centered", size: new Vector2(200, 40));
});

// Center vertically
UILayout.CenterV(100, () =>
{
    ui.Text("Vertically centered content");
});
```

#### Tabs

```csharp
UILayout.Tabs("my_tabs", new[]
{
    ("Tab 1", () => ui.Text("Content 1")),
    ("Tab 2", () => ui.Text("Content 2")),
    ("Tab 3", () => ui.Text("Content 3"))
});
```

#### Scroll Area

```csharp
UILayout.ScrollArea("scroll_id", new Vector2(300, 400), () =>
{
    for (int i = 0; i < 100; i++)
    {
        ui.Text($"Line {i}");
    }
});
```

#### Inline & Padding

```csharp
// Multiple items on same line
UILayout.Inline(() =>
{
    ui.Button("Save");
    ui.Button("Load");
    ui.Button("Cancel");
});

// Add padding
UILayout.Padding(20, 10, () =>
{
    ui.Text("Padded content");
});
```

---

### UIComponents

#### Cards

```csharp
// Card cliquable
if (UIComponents.Card("Settings", "Configure game options", "⚙️", selected: false))
{
    Console.WriteLine("Settings clicked!");
}

// Item card avec rareté
if (UIComponents.ItemCard("Dragon Sword", ItemRarity.Legendary, quantity: 1, selected: true))
{
    Console.WriteLine("Item selected!");
}
```

#### Stat Bars

```csharp
// Barre de stat horizontale
UIComponents.StatBar(
    "Health",
    current: 85,
    max: 100,
    color: new Vector4(0.8f, 0.2f, 0.2f, 1)
);

// Progress ring circulaire
UIComponents.ProgressRing(
    progress: 0.75f,
    radius: 50,
    color: new Vector4(0.3f, 0.5f, 0.9f, 1)
);
```

#### Notifications

```csharp
// Toast notification
UIComponents.Toast("Item received!", ToastType.Success, duration: 3f);
UIComponents.Toast("Warning: Low health!", ToastType.Warning);
UIComponents.Toast("Error: Connection lost", ToastType.Error);

// Modal dialog
ImGui.OpenPopup("confirm_delete");
if (UIComponents.Modal("confirm_delete", "Delete Item?", "Are you sure you want to delete this item?", out var result))
{
    if (result == ModalResult.OK)
    {
        Console.WriteLine("Item deleted!");
    }
}
```

---

## 🎮 Exemples Pratiques

### HUD de Jeu

```csharp
var theme = UIStyleSheet.CreateRPGTheme();
var ui = new UIBuilder(theme);

ui.Window("HUD", () =>
{
    // Health/Mana en haut à gauche
    UIComponents.StatBar("HP", 450, 500, new Vector4(0.8f, 0.2f, 0.2f, 1));
    UIComponents.StatBar("MP", 180, 250, new Vector4(0.2f, 0.4f, 0.9f, 1));
    
    ui.Separator();
    
    // Minimap
    ui.Text("Minimap", UITextStyle.Colored);
    UIComponents.ProgressRing(0.33f, 60);
    
    ui.Separator();
    
    // Quests
    ui.CollapsingHeader("Active Quests", () =>
    {
        ui.Text("• Defeat the Dragon");
        ui.Text("• Find 10 herbs");
        ui.Text("• Talk to NPC");
    });
    
}, new UIWindowOptions
{
    Position = new Vector2(10, 10),
    Size = new Vector2(300, 400),
    Flags = ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoResize
});
```

### Dialogue System

```csharp
ui.Window("Dialogue", () =>
{
    // NPC card
    UILayout.CenterH(150, () =>
    {
        UIComponents.Card("Merchant", "Level 25", "🧙");
    });
    
    ui.Separator();
    
    // Dialogue text
    UILayout.ScrollArea("dialogue_scroll", new Vector2(-1, 200), () =>
    {
        ui.Text("[Merchant]: Welcome, traveler! What can I do for you today?");
        ui.Spacing();
        ui.Text("[You]: I'm looking for a powerful weapon.");
        ui.Spacing();
        ui.Text("[Merchant]: Ah, I have just the thing...");
    });
    
    ui.Separator();
    
    // Response options
    if (ui.Button("1. Show me your wares", style: UIButtonStyle.Primary))
    {
        Console.WriteLine("Shop opened");
    }
    
    if (ui.Button("2. Tell me about this place"))
    {
        Console.WriteLine("Lore displayed");
    }
    
    if (ui.Button("3. Goodbye", style: UIButtonStyle.Danger))
    {
        Console.WriteLine("Dialogue closed");
    }
    
}, new UIWindowOptions
{
    Size = new Vector2(500, 500)
});
```

### Crafting UI

```csharp
ui.Window("Crafting", () =>
{
    UILayout.Split(0.4f,
        () => // Left: Recipes
        {
            ui.Text("Recipes", UITextStyle.Colored);
            ui.Separator();
            
            UILayout.ScrollArea("recipes", new Vector2(-1, -1), () =>
            {
                if (UIComponents.Card("Iron Sword", "Requires: 5 Iron", "⚔️"))
                {
                    Console.WriteLine("Recipe selected");
                }
                
                if (UIComponents.Card("Health Potion", "Requires: 2 Herbs", "🧪"))
                {
                    Console.WriteLine("Recipe selected");
                }
                
                if (UIComponents.Card("Steel Armor", "Requires: 10 Steel", "🛡️"))
                {
                    Console.WriteLine("Recipe selected");
                }
            });
        },
        () => // Right: Crafting details
        {
            ui.Text("Iron Sword", UITextStyle.Colored);
            ui.Text("A sturdy blade forged from iron.");
            ui.Separator();
            
            ui.Text("Requirements:");
            UIComponents.ItemCard("Iron Ingot", ItemRarity.Common, 5);
            
            ui.Separator();
            
            UILayout.CenterH(150, () =>
            {
                if (ui.Button("Craft", onClick: () => { }, style: UIButtonStyle.Success, size: new Vector2(150, 40)))
                {
                    UIComponents.Toast("Iron Sword crafted!", ToastType.Success);
                }
            });
        }
    );
    
}, new UIWindowOptions
{
    Size = new Vector2(800, 600)
});
```

---

## 🔄 Migration depuis ImGuiMenu

### Avant (ImGuiMenu)

```csharp
if (ImGui.BeginTabItem("Inventory"))
{
    for (int i = 0; i < items.Count; i++)
    {
        if (i % 4 != 0) ImGui.SameLine();
        
        var item = items[i];
        ImGui.PushStyleColor(ImGuiCol.Border, GetRarityColor(item.Rarity));
        
        if (ImGui.Button($"{item.Name}##{i}", new Vector2(120, 80)))
        {
            selectedItem = item;
        }
        
        ImGui.PopStyleColor();
    }
    
    ImGui.EndTabItem();
}
```

### Après (AstrildUI)

```csharp
UILayout.Tabs("main_tabs", new[]
{
    ("Inventory", () =>
    {
        UILayout.Grid(4, () =>
        {
            foreach (var item in items)
            {
                if (UIComponents.ItemCard(item.Name, item.Rarity, item.Quantity))
                {
                    selectedItem = item;
                }
            }
        });
    })
});
```

### Avantages

- ✅ **Moins de code** : Grid au lieu de SameLine() manuel
- ✅ **Plus lisible** : Déclaratif au lieu d'impératif
- ✅ **Type-safe** : Enums au lieu de strings
- ✅ **Réutilisable** : ItemCard au lieu de code dupliqué
- ✅ **Maintenable** : Style géré par UIStyleSheet

---

## 🎨 Best Practices

### 1. Utilisez le Builder Pattern

```csharp
// ✅ Bon
var ui = new UIBuilder(theme);
ui.Window("Title", () =>
{
    ui.Text("Content");
    ui.Button("Action");
});

// ❌ Mauvais
ImGui.Begin("Title");
ImGui.Text("Content");
if (ImGui.Button("Action")) { }
ImGui.End();
```

### 2. Composez des Layouts

```csharp
// ✅ Bon - Composition claire
UILayout.Split(0.5f,
    () => UILayout.VStack(() => { /* Left */ }),
    () => UILayout.VStack(() => { /* Right */ })
);

// ❌ Mauvais - Imbrication confuse
ImGui.BeginChild("left");
// ...
ImGui.EndChild();
ImGui.SameLine();
ImGui.BeginChild("right");
// ...
ImGui.EndChild();
```

### 3. Extrayez des Composants

```csharp
// ✅ Bon - Composant réutilisable
void RenderPlayerCard(Player player)
{
    UIComponents.Card(player.Name, $"Level {player.Level}", "👤");
    UIComponents.StatBar("HP", player.HP, player.MaxHP, Colors.Red);
}

// ❌ Mauvais - Code dupliqué partout
ImGui.Button($"{player.Name}##player_card");
ImGui.ProgressBar(player.HP / player.MaxHP);
```

### 4. Gérez les Thèmes Proprement

```csharp
// ✅ Bon - Thème appliqué automatiquement
var ui = new UIBuilder(UIStyleSheet.CreateRPGTheme());
ui.Window("Title", () => { /* Styled content */ });

// ❌ Mauvais - Push/Pop manuel partout
ImGui.PushStyleColor(...);
ImGui.PushStyleVar(...);
// ... code ...
ImGui.PopStyleVar();
ImGui.PopStyleColor();
```

---

## 🚧 Limitations

- **Toast System** : Pas encore de queue temporelle (TODO)
- **Modal Callbacks** : Gestion asynchrone à améliorer
- **Animations** : Pas de système d'animation intégré
- **Responsive** : Pas de système de breakpoints

---

## 📈 Roadmap

### v1.1
- [ ] Toast queue avec gestion temporelle
- [ ] Animations fluides (fade, slide, scale)
- [ ] Responsive breakpoints
- [ ] Data binding bidirectionnel

### v1.2
- [ ] Drag & drop système
- [ ] Context menus
- [ ] Keyboard navigation
- [ ] Accessibility (screen reader)

### v2.0
- [ ] Hot reload support
- [ ] Visual editor
- [ ] Component library expansion
- [ ] Performance optimizations

---

## 📚 Resources

- **ImGui.NET Documentation** : https://github.com/ImGuiNET/ImGui.NET
- **Dear ImGui Reference** : https://github.com/ocornut/imgui
- **AstrildApex Engine** : `Engine/` folder

---

## 🤝 Contributing

Pour ajouter un nouveau composant :

1. Ajoutez la méthode dans `UIComponents.cs`
2. Documentez les paramètres avec XML comments
3. Créez un exemple dans cette doc
4. Testez avec les 4 thèmes

---

**AstrildUI** - Built with ❤️ for AstrildApex Engine
