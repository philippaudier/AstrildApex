# 🎮 Maximize on Play - Implementation Plan

## 📋 Objectif

Implémenter la fonctionnalité "Maximize on Play" comme dans Unity :
- Quand on entre en Play Mode avec l'option activée → Game Panel fullscreen
- Presser ESC ou arrêter le Play Mode → Restaurer la vue normale
- L'implémentation doit être propre et ne pas casser le code existant

## 🔧 Approche Simple (RECOMMENDED)

### Étape 1 : Ajouter le Flag de Maximisation

```csharp
// Dans GamePanel.cs
private static bool _isMaximized = false; // ✅ DÉJÀ AJOUTÉ
```

### Étape 2 : Modifier Draw() pour supporter le Mode Maximisé

**AVANT** :
```csharp
public static void Draw()
{
    bool visible = ImGui.Begin("Game");
    if (!visible) { ImGui.End(); return; }
    
    // ... tout le contenu du panel ...
    
    ImGui.End();
}
```

**APRÈS** :
```csharp
public static void Draw()
{
    // Si maximisé, créer une fenêtre fullscreen au lieu du panel docké
    if (_isMaximized)
    {
        var viewport = ImGui.GetMainViewport();
        ImGui.SetNextWindowPos(viewport.Pos);
        ImGui.SetNextWindowSize(viewport.Size);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, Vector2.Zero);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, 0.0f);
        
        var flags = ImGuiWindowFlags.NoDecoration | 
                    ImGuiWindowFlags.NoMove | 
                    ImGuiWindowFlags.NoResize;
        
        bool visible = ImGui.Begin("Game (Maximized)", flags);
        ImGui.PopStyleVar(2);
        
        if (!visible) { ImGui.End(); return; }
        
        // ESC pour sortir du mode maximisé
        if (ImGui.IsKeyPressed(ImGuiKey.Escape))
        {
            _isMaximized = false;
        }
        
        // Hint dans le coin
        var hint = "Press ESC to exit fullscreen";
        var textSize = ImGui.CalcTextSize(hint);
        var windowSize = ImGui.GetWindowSize();
        ImGui.SetCursorPos(new Vector2(windowSize.X - textSize.X - 10, 10));
        ImGui.TextColored(new Vector4(1, 1, 1, 0.7f), hint);
        ImGui.SetCursorPos(Vector2.Zero); // Reset pour le contenu
    }
    else
    {
        // Mode normal
        bool visible = ImGui.Begin("Game");
        if (!visible) { ImGui.End(); return; }
    }
    
    // ... tout le contenu du panel (IDENTIQUE dans les 2 modes) ...
    
    ImGui.End();
}
```

### Étape 3 : Exposer les Méthodes Publiques

```csharp
/// <summary>
/// Maximize or unmaximize the Game Panel (Unity-style)
/// </summary>
public static void SetMaximized(bool maximized)
{
    _isMaximized = maximized;
}

/// <summary>
/// Check if Game Panel is currently maximized
/// </summary>
public static bool IsMaximized => _isMaximized;
```

### Étape 4 : Connecter à PlayMode

Dans `PlayMode.cs` :

```csharp
public static void Play()
{
    if (_state != PlayState.Edit) return;
    // ... code existant ...
    
    // Maximize Game Panel if option is enabled
    if (Panels.GamePanel.Options.MaximizeOnPlay)
    {
        Panels.GamePanel.SetMaximized(true);
    }
    
    _state = PlayState.Playing;
}

public static void Stop()
{
    // ... code existant ...
    
    // Always exit maximized mode when stopping
    Panels.GamePanel.SetMaximized(false);
    
    _state = PlayState.Edit;
}
```

### Étape 5 : Exposer les Options Publiquement

Dans `GamePanel.cs` :

```csharp
/// <summary>
/// Access to Game Panel options (Unity-style settings)
/// </summary>
public static GamePanelOptions Options => _options;
```

## ✅ Avantages de cette Approche

1. **Simple** : Pas de refactoring massif, juste un `if` au début de `Draw()`
2. **Propre** : Le contenu du panel reste identique dans les 2 modes
3. **Réversible** : ESC ou Stop Play Mode restaure instantanément
4. **Unity-like** : Comportement exact de Unity

## 🚫 Problèmes à Éviter

### ❌ NE PAS refactoriser tout le contenu dans des méthodes séparées
- Trop complexe
- Risque de casser le code existant
- Duplication de logique

### ❌ NE PAS essayer de cacher/montrer le panel docké
- ImGui ne supporte pas ça facilement
- Peut casser le docking layout

### ✅ DO : Juste wrapper le Begin() avec des conditions
- Simple
- Fonctionne immédiatement
- Pas de duplication de code

## 📝 Code Status

**Actuellement** :
- `_isMaximized` field ✅ AJOUTÉ
- `SetMaximized()` method ✅ AJOUTÉ  
- `IsMaximized` property ✅ AJOUTÉ
- `Dispose()` reset ✅ AJOUTÉ
- Code de `Draw()` ❌ INCOMPLET (méthodes `DrawGameContent()` et `DrawPostGameContent()` jamais finalisées)

**TODO** :
1. Corriger `Draw()` avec l'approche simple ci-dessus
2. Supprimer `DrawGameContent()` et `DrawPostGameContent()` (non utilisées)
3. Exposer `Options` property
4. Connecter à `PlayMode.Play()` et `PlayMode.Stop()`
5. Tester

## 🎯 Implémentation Recommandée

Utiliser l'approche simple décrite ci-dessus. Pas de refactoring massif.
Le code doit rester lisible et maintenable.

---

*Date : 18 octobre 2025*
