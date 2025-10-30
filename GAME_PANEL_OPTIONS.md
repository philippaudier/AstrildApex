# 🎮 Game Panel Options - Unity-Style Settings

## 📋 Vue d'ensemble

Le Game Panel dispose maintenant d'un menu d'options complet (bouton ⚙) à côté du sélecteur de caméra, offrant des options de configuration avancées similaires à Unity.

## 🎛️ Options Disponibles

### 🎬 Play Mode Behavior

**Focus on Play**
- ✅ Activé par défaut
- Focus automatique sur le Game Panel lors de l'entrée en Play Mode
- Permet de commencer à jouer immédiatement sans cliquer

**Maximize on Play**
- ❌ Désactivé par défaut
- Maximise le Game Panel en plein écran lors de l'entrée en Play Mode
- Idéal pour les tests de jeu immersifs

### 🖼️ Display Options

**Mute Audio**
- ❌ Désactivé par défaut
- Coupe tout l'audio du Game Panel
- Utile pour travailler en silence ou tester visuellement

**Show Stats**
- ✅ Activé par défaut
- Affiche l'overlay de performance (FPS, frame time, etc.)
- Synchronisé avec l'ancien overlay de performance

**Show Gizmos**
- ❌ Désactivé par défaut
- Affiche les gizmos dans la vue Game (normalement seulement dans Scene)
- Utile pour débugger les positions/orientations en jeu

### 📐 Aspect Ratio

Options d'aspect ratio disponibles :
- **Free Aspect** - Aucune contrainte (par défaut)
- **16:9** - Widescreen standard (1920x1080, etc.)
- **16:10** - Widescreen classique (1920x1200, etc.)
- **4:3** - Ratio classique (1024x768, etc.)
- **5:4** - Ratio carré élargi (1280x1024, etc.)
- **1:1 (Square)** - Ratio carré parfait
- **Custom** - Ratio personnalisé avec slider

**Fonctionnement :**
- Le viewport du Game Panel est automatiquement redimensionné pour respecter le ratio
- Letterboxing/pillarboxing automatique si nécessaire
- Utile pour tester différentes résolutions d'écran

### ⚡ Quality & Performance

**Resolution Scale**
- Plage : 0.25x à 2.0x
- Défaut : 1.0x (natif)
- **0.5x** : Rendu à demi-résolution (boost de perf ~4x)
- **1.0x** : Résolution native
- **2.0x** : Supersampling (anti-aliasing amélioré, coût perf ~4x)
- Appliqué en temps réel via `ViewportRenderer.RenderScale`

**VSync**
- ✅ Activé par défaut
- Synchronisation verticale pour éviter le tearing
- Peut limiter les FPS au taux de rafraîchissement de l'écran

**Target FPS**
- Défaut : 0 (illimité)
- Limite le frame rate cible
- Utile pour tester les jeux à 30 FPS, 60 FPS, etc.
- 0 = pas de limite

## 🎨 Interface Utilisateur

### Accès aux Options

```
Game Panel Header:
┌─────────────────────────────────────────┐
│ Camera: [Main Camera ▼]  [⚙]           │
│                           └─> Options   │
└─────────────────────────────────────────┘
```

### Menu Popup

Cliquer sur le bouton ⚙ ouvre un popup avec toutes les options organisées par catégories :

```
╔══════════════════════════════════════╗
║ Play Mode Behavior                   ║
╠══════════════════════════════════════╣
║ ☑ Focus on Play                      ║
║ ☐ Maximize on Play                   ║
╠══════════════════════════════════════╣
║ Display Options                      ║
╠══════════════════════════════════════╣
║ ☐ Mute Audio                         ║
║ ☑ Show Stats                         ║
║ ☐ Show Gizmos                        ║
╠══════════════════════════════════════╣
║ Aspect Ratio                         ║
╠══════════════════════════════════════╣
║ [Free Aspect ▼]                      ║
╠══════════════════════════════════════╣
║ Quality & Performance                ║
╠══════════════════════════════════════╣
║ Resolution Scale: [━━●━━] 1.00x      ║
║ ☑ VSync                              ║
║ Target FPS: [  60  ]                 ║
╚══════════════════════════════════════╝
```

## 🔧 Implémentation Technique

### Architecture

**Fichiers créés :**
- `Editor/Panels/GamePanelOptions.cs` - Classe de données pour les options
- Enum `AspectRatioMode` - Modes d'aspect ratio

**Modifications :**
- `Editor/Panels/GamePanel.cs` - Ajout du menu et application des options

### Classe GamePanelOptions

```csharp
public class GamePanelOptions
{
    public bool FocusOnPlay { get; set; } = true;
    public bool MaximizeOnPlay { get; set; } = false;
    public bool MuteAudio { get; set; } = false;
    public bool ShowStats { get; set; } = true;
    public bool ShowGizmos { get; set; } = false;
    public AspectRatioMode AspectMode { get; set; } = AspectRatioMode.Free;
    public float CustomAspectRatio { get; set; } = 16f / 9f;
    public float ResolutionScale { get; set; } = 1.0f;
    public bool VSync { get; set; } = true;
    public int TargetFrameRate { get; set; } = 0;
}
```

### Application des Options

**Aspect Ratio :**
```csharp
float targetAspect = GetTargetAspectRatio();
if (targetAspect > 0)
{
    float currentAspect = (float)w / h;
    if (currentAspect > targetAspect)
        w = (int)(h * targetAspect);  // Letterbox
    else if (currentAspect < targetAspect)
        h = (int)(w / targetAspect);  // Pillarbox
}
```

**Resolution Scale :**
```csharp
_gameRenderer.RenderScale = _options.ResolutionScale;
```

## 📊 Cas d'Usage

### Test de Performance
```
Resolution Scale: 0.5x
Target FPS: 60
VSync: OFF
→ Test de performance en basse résolution
```

### Test de Qualité Visuelle
```
Resolution Scale: 2.0x
Aspect Ratio: 16:9
VSync: ON
→ Supersampling anti-aliasing pour screenshots
```

### Test Mobile
```
Aspect Ratio: 16:9
Resolution Scale: 0.75x
Target FPS: 30
→ Simulation de device mobile
```

### Développement Silencieux
```
Mute Audio: ON
Show Stats: ON
Show Gizmos: ON
→ Debug visuel sans son
```

## 🎯 Workflow Unity-like

1. **Setup** : Configurer les options avant le Play Mode
2. **Play** : Les options s'appliquent automatiquement
3. **Tweak** : Modifier les options en temps réel pendant le jeu
4. **Test** : Les changements sont appliqués immédiatement

## 🔮 Améliorations Futures Possibles

- [ ] Sauvegarder les options dans EditorSettings.json
- [ ] Presets d'options (Mobile, Desktop, Console, etc.)
- [ ] Option "Low Latency Mode" pour réduire l'input lag
- [ ] Support multi-display pour le Game Panel
- [ ] Aspect ratio presets pour consoles spécifiques (Switch, PS5, etc.)
- [ ] Screenshot avec aspect ratio forcé
- [ ] Enregistrement vidéo avec résolution/FPS cible

## ✅ Status

**Version** : 1.0  
**Date** : 18 octobre 2025  
**Build** : Compilé sans erreurs  
**Test** : Interface fonctionnelle, options appliquées en temps réel  
**Documentation** : Complète

---

*Inspiré par Unity Editor Game View Options* 🎮
