Appendix — Contenu fusionné des anciens guides

Les guides et notes de conception dispersés dans le dépôt ont été consolidés dans ce fichier. Il contient des résumés et extraits des anciens fichiers `.md` afin de préserver le contenu technique utile tout en réduisant le nombre total de fichiers markdown dans le dépôt.

## AstrildUI — Guide (extrait)

*(contenu importé depuis `ASTRILD_UI_GUIDE.md`)*

AstrildUI est un système de UI déclaratif construit au-dessus d'ImGui.NET avec une API fluide et des composants réutilisables. Exemples, API et patterns : Window, Panel, Layout (Grid/Split/Tabs), UIBuilder, UIStyleSheet, composants (ItemCard, StatBar, Toast), thèmes prédéfinis (RPG, SciFi, Minimal).

Quick start (extrait) :

```csharp
using Engine.UI.AstrildUI;

var ui = new UIBuilder(UIStyleSheet.Default);
ui.Window("Inventory", () =>
{
    ui.Text("Welcome to your inventory!", UITextStyle.Colored);
    ui.Separator();
    if (ui.Button("Open Chest", style: UIButtonStyle.Primary)) { /* ... */ }
});
```

---

## AstrildUI — Production Guide (extrait)

*(contenu importé depuis `ASTRILD_UI_PRODUCTION_GUIDE.md`)*

Le guide de production détaille l'usage en contexte produit : intégration dans MonoBehaviour, conseils d'optimisation, patterns pour menus et HUD, et exemples d'usage pour panels, boutons stylisés, capture et layout. L'API est prête pour la production et permet la création de HUD, menus, panneaux debug et interfaces d'inventaire.

Extrait :

```csharp
public class MyUI : MonoBehaviour
{
    private UIBuilder _ui;
    public override void Start() { _ui = new UIBuilder(); }
    public override void Update(float dt) { RenderUI(); }
    private void RenderUI() { /* ImGui + UIBuilder code */ }
}
```

---

## Terrain Material System (Résumé)

*(contenu importé depuis `TERRAIN_MATERIAL_SYSTEM.md`)*

Le système de layers pour le terrain a été ré-architecturé pour référencer des `Material` complets par layer (PBR properties) au lieu d'uniquement textures (albedo/normal). Avantages : PBR complet, réutilisation, configuration simplifiée.

Migration clé : `TerrainLayer.Material` (GUID) + UV transform (tiling/offset). Le `TerrainRenderer` doit charger les materials des layers et binder leurs textures dans le shader `TerrainForward`.

---

## Shadow mapping — résumés & guide de test

*(contenu importé depuis `SHADOWMAP_FIX_SUMMARY.md` et `SHADOWMAP_TEST_GUIDE.md`)*

Résumé des corrections :
- Éviter la double-compare (CompareRefToTexture + PCF manual) — choisir une méthode
- Corriger la logique de comparaison dans `Shadows.glsl`
- Matrices : respecter l'ordre (ortho * view) avec OpenTK
- Atlas Y inversion fix pour CSM

Tests recommandés :
- Mode Legacy : Shadow Bias 0.005, ShadowMap 2048, PCF radius 1.0
- Mode CSM : Cascade count 4, ShadowMap 4096, PCF radius 1.0

---

## SSAO — notes d'implémentation

*(contenu importé depuis `SSAO_IMPLEMENTATION_NOTES.md`)*

Points importants :
- Le bruit SSAO doit être en espace écran (normal)
- Utiliser un noise texture 4x4 et un blur bilatéral fort pour rendre le pattern invisible
- Réglages conseillés : BlurRadius ~6, SampleCount 64 (ou ajuster selon perf)

---

## Material Hot-Reload (Résumé)

*(contenu importé depuis `MATERIAL_HOTRELOAD_SYSTEM.md`)*

Design : `AssetDatabase` émet `MaterialSaved` events; renderers (TerrainRenderer, ViewportRenderer) écoutent cet événement, invalidant leur cache pour recharger le material au prochain frame. Résultat : modifications de matériau visibles immédiatement.

---

## Input System (Résumé)

*(contenu importé depuis `INPUT_SYSTEM_COMPLETE.md`)*

Le système d'input fournit : KeyCode enum, InputManager (GetKeyDown/Up/GetKey), InputAction / InputActionMap avec queries par nom, un panneau d'édition complet dans l'éditeur (binding capture, conflict detection, friendly names). API runtime simple et action-based recommandée.

---

## Inspector Widgets & UX (Résumé)

*(contenu importé depuis `INSPECTOR_WIDGETS_GUIDE.md`)*

Le guide décrit le système d'inspecteurs : widgets standardisés, validation temps réel, Undo/Redo, sections collapsibles, presets, champs numériques, vecteurs, couleurs, enum dropdowns, etc. Utiliser `InspectorWidgets` pour créer des inspecteurs cohérents et accessibles.

---

## Theme System (Résumé)

*(contenu importé depuis `THEME_SYSTEM_GUIDE.md`)*

Thèmes intégrés (Purple Dream, Cyber Blue, Mint Fresh, Dark Unity), ThemeManager API et Preview realtime. Les thèmes persistent dans `ProjectSettings/EditorSettings.json`.

---

_Remarque_ : tout le contenu complet des fichiers originaux a été sauvegardé dans l'historique Git avant suppression. Si tu veux que je restaure une page complète (par ex. le guide UI complet), je peux la réexposer séparément ou la convertir en section dédiée plus détaillée.
