# 🎨 AstrildUI Production Guide

## Vue d'ensemble

**AstrildUI** est un système UI déclaratif fluide basé sur ImGui.NET, conçu pour créer rapidement des interfaces utilisateur dans Astrild Engine. Il combine la puissance d'ImGui avec une API élégante et facile à utiliser.

## ✅ Production Ready

L'API AstrildUI est **totalement prête pour la production** et peut être utilisée dès maintenant pour créer :
- HUD de jeu (RPG, FPS, RTS)
- Menus et interfaces de dialogue
- Panneaux de debug et outils d'édition
- Interfaces d'inventaire et crafting
- Systèmes de quêtes et notifications

---

## 📦 Installation

Aucune installation nécessaire ! AstrildUI est intégré au moteur.

```csharp
using Engine.UI.AstrildUI;
using Engine.Scripting;
using ImGuiNET;
```

---

## 🚀 Démarrage Rapide

### Créer un script UI

```csharp
public class MyUI : MonoBehaviour
{
    private UIBuilder _ui;

    public override void Start()
    {
        _ui = new UIBuilder();
    }

    public override void Update(float dt)
    {
        RenderUI();
    }

    private void RenderUI()
    {
        ImGui.SetNextWindowPos(new Vector2(20, 20));
        ImGui.SetNextWindowSize(new Vector2(300, 200));
        
        if (ImGui.Begin("My Window"))
        {
            _ui.Text("Hello Astrild!");
            _ui.Button("Click Me", () => Console.WriteLine("Clicked!"));
        }
        ImGui.End();
    }
}
```

---

## 📚 API Reference

### Fenêtres

```csharp
// Fenêtre simple avec callback
_ui.Window("Title", () => {
    _ui.Text("Content");
});

// Fenêtre avec options
_ui.Window("Title", () => {
    // Contenu
}, new UIWindowOptions {
    Size = new Vector2(400, 300),
    Position = new Vector2(100, 100),
    BackgroundAlpha = 0.9f,
    NoMove = true,
    NoResize = true
});
```

### Panels (Child Windows)

```csharp
_ui.Panel("PanelID", () => {
    _ui.Text("Panel content");
}, new UIPanelOptions {
    Size = new Vector2(200, 100),
    HasBorder = true
});
```

### Boutons

```csharp
// Bouton simple
_ui.Button("Click", () => DoSomething());

// Boutons stylisés
_ui.Button("Primary", onClick, UIButtonStyle.Primary);
_ui.Button("Danger", onClick, UIButtonStyle.Danger);
_ui.Button("Success", onClick, UIButtonStyle.Success);

// Bouton avec taille personnalisée
_ui.Button("Large", onClick, size: new Vector2(200, 50));
```

### Texte

```csharp
_ui.Text("Normal text");
_ui.Text("Disabled", UITextStyle.Disabled);
_ui.Text("Colored", UITextStyle.Colored);
_ui.Text("Warning", UITextStyle.Warning);
_ui.Text("Error", UITextStyle.Error);
```

### Inputs

```csharp
string text = "";
_ui.InputText("Name", ref text);

float value = 0.5f;
_ui.SliderFloat("Volume", ref value, 0f, 1f);

bool enabled = true;
_ui.Checkbox("Enable", ref enabled, (val) => {
    Console.WriteLine($"Checkbox: {val}");
});

int selected = 0;
string[] items = { "Option 1", "Option 2", "Option 3" };
_ui.Combo("Choose", ref selected, items, (idx) => {
    Console.WriteLine($"Selected: {items[idx]}");
});
```

### Layout

```csharp
// Horizontal layout (same line)
_ui.Button("Button 1")
   .SameLine()
   .Button("Button 2");

// Spacing
_ui.Spacing(3); // 3 lignes d'espacement

// Séparateur
_ui.Separator();

// Indentation
_ui.Indent()
   .Text("Indented text")
   .Unindent();

// Alignement
_ui.AlignRight(100) // Aligner à droite avec largeur 100
   .Button("Right");

_ui.CenterHorizontal(150) // Centrer avec largeur 150
   .Button("Centered");

// Dummy (espace personnalisé)
_ui.Dummy(new Vector2(0, 20));
```

### Headers & Trees

```csharp
// Collapsing header
_ui.CollapsingHeader("Section", () => {
    _ui.Text("Content inside");
}, defaultOpen: true);

// Tree node
_ui.TreeNode("Node", () => {
    _ui.Text("Child content");
}, defaultOpen: false);
```

### Progress & Stats

```csharp
// Progress bar simple
_ui.ProgressBar(0.75f);

// Progress bar avec couleur et texte
_ui.ProgressBar(0.5f, 
    size: new Vector2(200, 20), 
    overlay: "50%",
    color: new Vector4(0.2f, 0.8f, 0.3f, 1f));
```

### Tooltips & Images

```csharp
// Tooltip sur le dernier item
_ui.Button("Hover me")
   .Tooltip("This is a helpful tooltip!");

// Image button
_ui.ImageButton("btnId", texturePtr, new Vector2(64, 64), 
    onClick: () => DoAction(),
    tooltip: "Click to activate");
```

### Color Picker

```csharp
Vector4 color = new Vector4(1, 0, 0, 1);
_ui.ColorPicker("Color", ref color, (newColor) => {
    Console.WriteLine($"New color: {newColor}");
});
```

### Custom Drawing

```csharp
_ui.CustomDraw((drawList) => {
    var pos = ImGui.GetCursorScreenPos();
    drawList.AddCircleFilled(pos + new Vector2(50, 50), 30f, 
        ImGui.ColorConvertFloat4ToU32(new Vector4(1, 0, 0, 1)));
});
```

---

## 🎮 Exemple Complet : HUD RPG

Voir `Editor/Assets/Scripts/RPGHudController.cs` pour un exemple complet avec :

✅ Barres de stats (HP, Mana, Stamina) avec gradients et animations  
✅ Système de buffs avec timers circulaires  
✅ Quick slots avec sélection et keybinds  
✅ Compass rotatif animé  
✅ Quest tracker avec progress bars  
✅ XP bar avec effets de shimmer  
✅ Damage flash overlay  
✅ Headers stylisés avec bordures fancy  

---

## 🎨 Composants High-Level

### UIComponents Library

```csharp
using Engine.UI.AstrildUI;

// Card cliquable
bool clicked = UIComponents.Card(
    "Title", 
    "Description", 
    icon: "🎮", 
    selected: false
);

// Item card (inventaire)
bool clicked = UIComponents.ItemCard(
    "Sword of Destiny", 
    ItemRarity.Legendary, 
    quantity: 1
);

// Stat bar
UIComponents.StatBar(
    "Health", 
    current: 850f, 
    max: 1000f, 
    color: new Vector4(0.8f, 0.2f, 0.2f, 1f)
);

// Progress ring (circulaire)
UIComponents.ProgressRing(
    progress: 0.75f, 
    radius: 30f, 
    color: new Vector4(0.3f, 0.5f, 0.9f, 1f)
);
```

---

## 🛠️ Styles & Themes

### UIStyleSheet

```csharp
// Utiliser un stylesheet personnalisé
var customStyle = new UIStyleSheet {
    PrimaryColor = new Vector4(0.3f, 0.7f, 1.0f, 1f),
    PrimaryHoverColor = new Vector4(0.4f, 0.8f, 1.0f, 1f),
    BackgroundColor = new Vector4(0.1f, 0.1f, 0.12f, 0.95f),
    WindowRounding = 8f,
    FrameRounding = 4f,
    Padding = new Vector2(10, 10)
};

var ui = new UIBuilder(customStyle);
```

### Styles prédéfinis

```csharp
UIStyleSheet.Default  // Style par défaut
// Ajoutez vos propres styles dans UIStyleSheet.cs
```

---

## 📋 Bonnes Pratiques

### 1. Créer un composant UI réutilisable

```csharp
public class HealthBar : MonoBehaviour
{
    public float Health = 100f;
    public float MaxHealth = 100f;
    
    private UIBuilder _ui;
    
    public override void Start()
    {
        _ui = new UIBuilder();
    }
    
    public override void Update(float dt)
    {
        ImGui.SetNextWindowPos(new Vector2(20, 20));
        
        if (ImGui.Begin("##HealthBar", ImGuiWindowFlags.NoTitleBar))
        {
            float fraction = Health / MaxHealth;
            _ui.ProgressBar(fraction, 
                size: new Vector2(200, 25),
                overlay: $"{Health:F0} / {MaxHealth:F0}",
                color: new Vector4(0.8f, 0.2f, 0.2f, 1f));
        }
        ImGui.End();
    }
}
```

### 2. Utiliser le pattern Builder pour la lisibilité

```csharp
// ✅ Bon - Fluent et lisible
_ui.Text("Player Stats")
   .Separator()
   .Text($"Level: {level}")
   .Spacing()
   .Button("Level Up", OnLevelUp);

// ❌ Moins bon - Répétitif
_ui.Text("Player Stats");
_ui.Separator();
_ui.Text($"Level: {level}");
_ui.Spacing();
_ui.Button("Level Up", OnLevelUp);
```

### 3. Organiser votre UI

```csharp
private void RenderUI()
{
    RenderTopBar();
    RenderSidePanel();
    RenderBottomHUD();
}

private void RenderTopBar()
{
    ImGui.SetNextWindowPos(new Vector2(0, 0));
    // ...
}
```

### 4. Gérer l'état UI

```csharp
// État local
private bool _showInventory = false;
private int _selectedTab = 0;

// Dans Update
if (Input.GetKeyDown(KeyCode.I))
{
    _showInventory = !_showInventory;
}

if (_showInventory)
{
    RenderInventory();
}
```

---

## ⚡ Performance

### Tips d'optimisation

1. **Évitez les allocations dans Update**
```csharp
// ❌ Mauvais - Crée un nouveau UIBuilder chaque frame
public override void Update(float dt)
{
    var ui = new UIBuilder(); // ALLOCATION!
}

// ✅ Bon - Réutilise l'instance
private UIBuilder _ui;
public override void Start()
{
    _ui = new UIBuilder();
}
```

2. **Cachez les fenêtres invisibles**
```csharp
if (_showWindow)
{
    if (ImGui.Begin("Window"))
    {
        // Render only when visible
    }
    ImGui.End();
}
```

3. **Utilisez ImGui.IsItemVisible() pour le culling**
```csharp
if (ImGui.BeginChild("List"))
{
    for (int i = 0; i < items.Length; i++)
    {
        ImGui.Text(items[i]);
        if (!ImGui.IsItemVisible()) continue;
        // Render complex content only if visible
    }
}
ImGui.EndChild();
```

---

## 🔧 Extensions Possibles

### Ajouter vos propres composants

Éditez `Engine/UI/AstrildUI/UIComponents.cs` :

```csharp
public static class UIComponents
{
    /// <summary>
    /// Votre composant personnalisé
    /// </summary>
    public static void MyCustomComponent(string label, Action onClick)
    {
        // Implémentation
    }
}
```

### Ajouter des méthodes à UIBuilder

Éditez `Engine/UI/AstrildUI/UIBuilder.cs` :

```csharp
public class UIBuilder
{
    /// <summary>
    /// Votre méthode helper
    /// </summary>
    public UIBuilder MyHelper(string text)
    {
        ImGui.Text(text);
        return this;
    }
}
```

---

## 📝 Exemples d'utilisation

### Menu principal

```csharp
public class MainMenu : MonoBehaviour
{
    private UIBuilder _ui;
    
    public override void Start()
    {
        _ui = new UIBuilder();
    }
    
    public override void Update(float dt)
    {
        var screenSize = ImGui.GetIO().DisplaySize;
        var windowSize = new Vector2(400, 300);
        var pos = (screenSize - windowSize) * 0.5f;
        
        ImGui.SetNextWindowPos(pos);
        ImGui.SetNextWindowSize(windowSize);
        
        if (ImGui.Begin("Main Menu", ImGuiWindowFlags.NoResize))
        {
            _ui.Spacing(2)
               .CenterHorizontal(200)
               .Button("New Game", OnNewGame, UIButtonStyle.Primary, new Vector2(200, 50))
               .Spacing()
               .CenterHorizontal(200)
               .Button("Load Game", OnLoadGame, size: new Vector2(200, 50))
               .Spacing()
               .CenterHorizontal(200)
               .Button("Settings", OnSettings, size: new Vector2(200, 50))
               .Spacing()
               .CenterHorizontal(200)
               .Button("Quit", OnQuit, UIButtonStyle.Danger, new Vector2(200, 50));
        }
        ImGui.End();
    }
    
    private void OnNewGame() => Console.WriteLine("New Game");
    private void OnLoadGame() => Console.WriteLine("Load Game");
    private void OnSettings() => Console.WriteLine("Settings");
    private void OnQuit() => Application.Quit();
}
```

### Inventaire

```csharp
public class Inventory : MonoBehaviour
{
    private UIBuilder _ui;
    private string[] _items = { "Sword", "Shield", "Potion", "Key" };
    private int _selectedItem = -1;
    
    public override void Start()
    {
        _ui = new UIBuilder();
    }
    
    public override void Update(float dt)
    {
        ImGui.SetNextWindowPos(new Vector2(50, 50));
        ImGui.SetNextWindowSize(new Vector2(400, 500));
        
        if (ImGui.Begin("Inventory"))
        {
            _ui.Text("Your Items", UITextStyle.Colored)
               .Separator()
               .Spacing();
            
            for (int i = 0; i < _items.Length; i++)
            {
                bool selected = i == _selectedItem;
                
                if (UIComponents.ItemCard(_items[i], ItemRarity.Common, 1, selected))
                {
                    _selectedItem = i;
                }
                
                if (i % 3 < 2) _ui.SameLine();
            }
        }
        ImGui.End();
    }
}
```

---

## 🎯 Résumé

**AstrildUI est production-ready** et offre :

✅ API fluide et intuitive  
✅ Composants réutilisables (Cards, StatBars, etc.)  
✅ Système de styles extensible  
✅ Performance optimale (pas d'allocations inutiles)  
✅ Documentation complète  
✅ Exemples complets (HUD RPG)  
✅ Intégration native avec ImGui.NET  

**Tu peux l'utiliser dès maintenant pour créer n'importe quelle UI dans ton jeu !** 🚀

---

## 📞 Support

Pour des questions ou suggestions :
- Consulte les exemples dans `Editor/Assets/Scripts/`
- Lis la documentation ImGui : https://github.com/ocornut/imgui
- Explore `Engine/UI/AstrildUI/` pour voir l'implémentation

---

*Créé pour Astrild Engine - Ton moteur, ton UI !* ⚔️✨
