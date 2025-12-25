# UI Modernization Guide - AstrildApex Engine

## 🎨 Vision
Créer une UI moderne, cohérente et facilement tweakable pour l'ensemble du moteur AstrildApex.

## 🏗️ Architecture Unifiée

### Système Centralisé: `ThemeManager.UI`

**UN SEUL endroit pour contrôler TOUT le look du moteur:**

```csharp
using Editor.Themes;

// Accès au thème unifié
var theme = ThemeManager.UI;

// Couleurs sémantiques
theme.Primary        // Bleu accent pour actions principales
theme.Success        // Vert pour états valides
theme.Warning        // Jaune pour avertissements
theme.Error          // Rouge pour erreurs
theme.Info           // Bleu clair pour informations

// Couleurs de texte
theme.TextNormal     // Texte standard
theme.TextDisabled   // Texte grisé
theme.TextLabel      // Labels dans inspecteurs

// Espacements (tweakable facilement!)
theme.SpacingSmall   // 4px
theme.SpacingMedium  // 8px
theme.SpacingLarge   // 16px

// Tailles de contrôles
theme.ButtonHeightSmall    // 20px
theme.ButtonHeightMedium   // 25px
theme.ControlWidthAuto     // -80 (offset from right)

// Toolbar moderne
theme.ToolbarBg           // Fond glassmorphism
theme.ToolbarButton       // Bouton semi-transparent
theme.GradientStart/End   // Gradient pour boutons actifs
```

### Widgets Unifiés: `EditorWidgets`

**Widgets professionnels prêts à l'emploi:**

```csharp
using Editor.UI;

// Asset field avec drag-drop + click-to-select
Guid? material = EditorWidgets.AssetField(
    "Material", 
    currentMaterial, 
    "Material",
    description: "PBR material for rendering",
    showPreview: true);

// Section collapsible
if (EditorWidgets.Section("Rendering Settings"))
{
    // ... contenu de la section ...
    EditorWidgets.EndSection();
}

// Boutons typés
if (EditorWidgets.PrimaryButton("Apply"))    // Bleu
if (EditorWidgets.DangerButton("Delete"))    // Rouge
if (EditorWidgets.SuccessButton("Save"))     // Vert

// Message boxes
EditorWidgets.InfoBox("Scene loaded successfully");
EditorWidgets.WarningBox("Missing textures detected");
EditorWidgets.ErrorBox("Failed to compile shader");

// Champs de saisie
EditorWidgets.TextField("Name", ref name, placeholder: "Enter name...");
EditorWidgets.FloatSlider("Intensity", ref intensity, 0f, 10f);
EditorWidgets.ColorPicker("Tint", ref color, showAlpha: true);
EditorWidgets.EnumCombo("Quality", ref quality);
```

### UI Moderne (Glassmorphism): `ModernUIHelpers`

**Pour les toolbars et overlays:**

```csharp
using Editor.UI;

// Toolbar avec effet glassmorphism
ModernUIHelpers.BeginToolbarGroup();
if (ModernUIHelpers.ToolButton("Move", isActive: isMoveTool, "Move Tool (W)"))
    currentTool = Tool.Move;
ModernUIHelpers.ToolbarSeparator();
if (ModernUIHelpers.ToolButton("Rotate", isActive: isRotateTool, "Rotate Tool (E)"))
    currentTool = Tool.Rotate;
ModernUIHelpers.EndToolbarGroup();
```

## 📋 Plan de Migration

### Phase 1: Inspecteurs de Components ✅ EN COURS

**Priorité haute** (plus utilisés):
- ✅ TerrainInspector - COMPLÉTÉ (proof of concept)
- 🔄 MeshRendererInspector
- 🔄 MaterialAssetInspector
- 🔄 LightInspector
- 🔄 CameraInspector

**Priorité moyenne**:
- AudioSourceInspector
- ParticleSystemInspector
- BoxColliderInspector / SphereColliderInspector / CapsuleColliderInspector

**Priorité basse**:
- Tous les autres inspecteurs de components

### Phase 2: Asset Inspectors

- TextureInspector
- MaterialAssetInspector
- MeshAssetInspector
- AudioClipInspector
- FontAssetInspector
- HDRTextureInspector

### Phase 3: Panels Principaux

- InspectorPanel (header + layout)
- AssetsPanel (thumbnails + drag-drop)
- HierarchyPanel (tree view + icons)
- ConsolePanel (log levels + colors)
- EnvironmentPanel (skybox + lighting)

### Phase 4: Fenêtres de Settings

- RenderingSettingsPanel
- InputSettingsPanel
- AudioMixerPanel
- PreferencesWindow

## 🔧 Pattern de Migration

### Avant (ancien code):
```csharp
using System.Numerics;
using ImGuiNET;

// Couleurs hardcodées
ImGui.TextColored(new Vector4(0.4f, 1f, 0.4f, 1f), "✓ Valid");

// Drag-drop manuel (50+ lignes)
if (ImGui.BeginDragDropTarget())
{
    unsafe
    {
        var payload = ImGui.AcceptDragDropPayload("ASSET_MULTI");
        // ... 40 lignes de code boilerplate ...
    }
    ImGui.EndDragDropTarget();
}

// Espacement inconsistant
ImGui.Dummy(new Vector2(0, 8));
```

### Après (nouveau code):
```csharp
using Editor.UI;
using Editor.Themes;

// Couleur du thème unifié
var theme = ThemeManager.UI;
ImGui.TextColored(theme.Success, "✓ Valid");

// Widget unifié (3 lignes)
material = EditorWidgets.AssetField(
    "Material", material, "Material");

// Espacement du thème
ImGui.Dummy(new Vector2(0, theme.SpacingMedium));
```

## ✨ Avantages du Système Unifié

### 1. **Tweaking Ultra-Rapide**
Modifiez une valeur dans `UITheme.cs`, toute l'UI se met à jour instantanément:
```csharp
// Dans UITheme.cs
public Vector4 Primary { get; set; } = new(0.4f, 0.6f, 0.9f, 1f); // Tweakez ici!
public float SpacingMedium { get; set; } = 8f; // Ou ici!
```

### 2. **Cohérence Totale**
Tous les inspecteurs/panels utilisent les mêmes couleurs, espacements, widgets → look unifié

### 3. **Productivité**
- 10x moins de code pour asset fields
- Widgets réutilisables
- Pas de copier-coller

### 4. **Maintenance**
- UN fichier à modifier pour changer un style
- Pas de couleurs magiques éparpillées
- Code clean et lisible

### 5. **Thèmes Multiples** (futur)
Facile d'ajouter Dark/Light/Custom themes en changeant les valeurs de UITheme

## 🎯 Checklist par Fichier

Pour migrer un inspecteur:

- [ ] Ajouter `using Editor.Themes;` et `using Editor.UI;`
- [ ] Remplacer asset drag-drop par `EditorWidgets.AssetField()`
- [ ] Remplacer `CollapsingHeader` par `EditorWidgets.Section()`
- [ ] Remplacer `Vector4` hardcodés par `ThemeManager.UI.Primary/Success/Error/etc.`
- [ ] Remplacer espacements magic numbers par `ThemeManager.UI.SpacingSmall/Medium/Large`
- [ ] Utiliser `EditorWidgets.InfoBox/WarningBox/ErrorBox` pour messages
- [ ] Utiliser `EditorWidgets.PrimaryButton/DangerButton` pour actions importantes
- [ ] Tester l'inspecteur dans l'éditeur

## 📊 Métriques de Succès

**Avant migration:**
- 150+ lignes pour asset field avec drag-drop
- Couleurs différentes dans chaque fichier
- Espacements inconsistants
- Duplication de code massive

**Après migration:**
- 3 lignes pour asset field
- Toutes les couleurs depuis UITheme
- Espacements cohérents partout
- Code réutilisable et maintenable

## 🚀 Prochaines Étapes

1. **Migrer les 5 inspecteurs prioritaires**
2. **Implémenter système de Ping** dans AssetsPanel
3. **Migrer les panels principaux**
4. **Remplacer toutes les couleurs hardcodées**
5. **Tester et polir l'UI complète**
6. **Documentation utilisateur des widgets**

---

**L'objectif: Un moteur avec un UI moderne, cohérent, professionnel et facilement tweakable! 🎨✨**
