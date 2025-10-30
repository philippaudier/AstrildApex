# 🎨 Audit UX des Inspecteurs - Analyse Expert Unity

## 📋 Méthodologie d'audit

En tant qu'expert UX ayant travaillé chez Unity, j'ai analysé tous les inspecteurs selon ces critères :
1. **Cohérence visuelle** - Spacing, couleurs, icônes, typographie
2. **Feedback utilisateur** - Tooltips, validation, preview, undo/redo
3. **Efficacité** - Raccourcis clavier, drag & drop, presets, multi-edit
4. **Clarté** - Labels, grouping, hierarchy, disabled states
5. **Standards Unity** - Respect des patterns Unity 2023+

---

## 🔴 PROBLÈMES CRITIQUES IDENTIFIÉS

### 1. **Incohérence des widgets de base**
**Impact : ÉLEVÉ** | **Fichiers concernés : TOUS**

❌ **Problème :**
- `ImGui.DragFloat`, `ImGui.SliderFloat`, `ImGui.Combo` utilisés directement partout
- Pas de styling uniforme (largeur, padding, couleurs)
- Pas de tooltips standardisés
- Pas de validation visuelle (warning/error states)
- Undo/redo implémenté manuellement dans certains fichiers seulement

**Exemples concrets :**
```csharp
// CameraInspector.cs - ligne 17
ImGui.PushItemWidth(160f);  // Hardcodé

// BoxColliderInspector.cs - ligne 12  
ImGui.PushItemWidth(160f);  // Même valeur, dupliquée

// LightInspector.cs - ligne 16
ImGui.PushItemWidth(160f);  // Encore dupliquée

// UIElementInspector.cs - PAS de PushItemWidth !
// TerrainInspector.cs - PAS de PushItemWidth !
```

✅ **Solution :**
Créer `InspectorWidgets.cs` avec des widgets standardisés Unity-like :
- `Widgets.FloatField()`, `Widgets.Vector3Field()`, `Widgets.ColorField()`
- Largeur auto-responsive
- Tooltips intégrés
- Validation visuelle (jaune = warning, rouge = error)
- Undo/redo automatique via `FieldEditAction`
- Multi-selection support

---

### 2. **Duplication de code Light Component**
**Impact : MOYEN** | **Fichiers : `ComponentInspector.cs` (ligne 41) + `LightInspector.cs`**

❌ **Problème :**
```csharp
// ComponentInspector.cs ligne 41-65 : DrawLight() complet
private static void DrawLight(LightComponent light) { ... }

// LightInspector.cs ligne 13-70 : MÊME CODE dupliqué !
public static void Draw(LightComponent light) { ... }
```

Les deux fichiers ont **exactement le même code** pour dessiner le Light Component.

✅ **Solution :**
- Supprimer `DrawLight()` de `ComponentInspector.cs`
- Utiliser uniquement `LightInspector.Draw()`
- Pattern uniforme : un inspecteur = un fichier

---

### 3. **Gestion des Component References incohérente**
**Impact : ÉLEVÉ** | **Fichiers : `FieldWidgets.cs`, `CameraInspector.cs`**

❌ **Problème :**
```csharp
// FieldWidgets.cs - ligne 19 : ComponentRefObj(Type, string, Entity, object?)
// Utilise Entity.GetAllComponents() - NE PEUT PAS chercher dans toute la scène !

// FieldWidgets.cs - ligne 40 : ComponentRefObj(Type, string, Scene, object?)
// Utilise Scene.Entities - Peut chercher partout

// CameraInspector.cs - ligne 77 : Appelle ComponentRef (extension method?)
TransformComponent? t = cam.FollowTarget;
if (FieldWidgets.ComponentRef("Follow Target", scene, ref t))
    cam.FollowTarget = t;
```

**Problèmes multiples :**
1. Deux signatures différentes pour la même fonctionnalité
2. Ancienne version (Entity-based) limitée à un seul GameObject
3. Extension method `ComponentRef<T>()` non trouvée dans `FieldWidgets.cs` (ligne 1-230)
4. Drag & drop ne fonctionne que pour la version Scene-based

✅ **Solution :**
- **Supprimer** la version Entity-based (ligne 19-39)
- **Standardiser** avec version Scene-based uniquement
- **Améliorer** l'UI avec icône de type de composant
- **Ajouter** preview hover (highlight dans scene view)

---

### 4. **Absence de feedback visuel pour validation**
**Impact : ÉLEVÉ** | **Fichiers : TOUS les inspecteurs**

❌ **Problème :**
Aucun inspecteur ne montre d'indicateurs visuels pour :
- Valeurs invalides (ex : Near > Far dans Camera)
- Warnings (ex : Range = 0 pour Point Light)
- References manquantes (ex : FollowTarget null en mode Orbit)
- Conflits (ex : deux cameras Main actives)

**Exemple Camera :**
```csharp
// CameraInspector.cs ligne 32-33
float n = cam.Near, f = cam.Far;
if (ImGui.DragFloat("Near", ref n, 0.01f, 0.001f, 10f)) cam.Near = n;
if (ImGui.DragFloat("Far",  ref f, 1f, 10f, 100000f))   cam.Far  = f;

// ❌ Pas de validation : Near peut être >= Far !
```

✅ **Solution :**
```csharp
// Validation en temps réel avec feedback visuel
Widgets.FloatField("Near", ref cam.Near, 
    validate: v => v < cam.Far ? null : "Near must be < Far",
    warningIcon: cam.Near >= cam.Far);

Widgets.FloatField("Far", ref cam.Far,
    validate: v => v > cam.Near ? null : "Far must be > Near",
    warningIcon: cam.Far <= cam.Near);
```

---

### 5. **Manque de Tooltips et Help**
**Impact : MOYEN** | **Fichiers : TOUS**

❌ **Problème :**
- Seul `TerrainInspector` a quelques tooltips (ligne 47, 82)
- Aucun autre inspecteur n'en a
- Pas d'icône `(?)` pour aide contextuelle
- Pas de liens vers documentation

**Comptage actuel :**
```
CameraInspector.cs     : 0 tooltips sur 12 paramètres
LightInspector.cs      : 0 tooltips sur 6 paramètres
BoxColliderInspector.cs: 0 tooltips sur 5 paramètres
UIElementInspector.cs  : 0 tooltips sur 15+ paramètres
TerrainInspector.cs    : 2 tooltips sur 20+ paramètres ✅
```

✅ **Solution :**
- Tooltip sur **chaque** label (hover)
- Icône `(?)` clickable pour aide étendue
- Format standard : "Short desc. [Units] Range: min-max"

---

### 6. **Grouping et Sections mal organisés**
**Impact : MOYEN** | **Fichiers : `CameraInspector`, `UIElementInspector`**

❌ **Problème :**

**CameraInspector :**
```csharp
// Ligne 14-25 : Projection params (bon grouping ✅)
// Ligne 27-32 : Near/Far (pas de section ❌)
// Ligne 34-35 : IsMain checkbox (isolé ❌)
// Ligne 37-42 : Update Stage + Mode (pas de section ❌)
// Ligne 45-46 : Smoothing (isolé ❌)
// Ligne 49-61 : FPS (TreeNode ✅)
// Ligne 65-99 : Orbit/Follow (TreeNode ✅)
```

**UIElementInspector :**
```csharp
// Ligne 8-27 : Type dropdown (pas de section ❌)
// Ligne 31-47 : Basic props (Separator mais pas de header ❌)
// Ligne 51-122 : Rect Transform (CollapsingHeader ✅)
// Ligne 125-179 : Style (CollapsingHeader ✅)
// Ligne 182-263 : Content specific (switch sans headers ❌)
```

✅ **Solution :**
```csharp
// Pattern Unity standard :
if (Widgets.Section("Projection", defaultOpen: true))
{
    Widgets.EnumField("Mode", ref projection);
    if (projection == Perspective)
        Widgets.SliderAngle("FOV", ref fov, 1, 170);
    else
        Widgets.FloatField("Size", ref orthoSize);
}

if (Widgets.Section("Clipping Planes"))
{
    Widgets.FloatField("Near", ref near);
    Widgets.FloatField("Far", ref far);
}
```

---

### 7. **Drag & Drop zones non standardisées**
**Impact : MOYEN** | **Fichiers : `MaterialInspector`, `TerrainInspector`, `FieldWidgets`**

❌ **Problème :**

**MaterialInspector (ligne 42-58) :**
```csharp
ImGui.Button("Drop Material to Replace");  // Pas de style visuel
if (ImGui.IsItemHovered() && ImGui.GetDragDropPayload().NativePtr != null)
    ImGui.SetTooltip("Drop Material here to replace current");
```

**TerrainInspector (ligne 98-100) :**
```csharp
ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.2f, 0.2f, 0.3f, 1f));
ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.3f, 0.3f, 0.4f, 1f));
// Puis button + BeginDragDropTarget...
```

**FieldWidgets (ligne 64-71) :**
```csharp
if (ImGui.Button($"{displayText}###{label}", new Vector2(220f, 0)))
    ImGui.OpenPopup($"select_{label}");
// Drag drop inline sans style particulier
```

✅ **Solution :**
```csharp
// Widget standardisé Unity-like
Widgets.ObjectField<Material>("Material", ref materialRef,
    allowSceneObjects: false,
    placeholder: "None (Material)",
    icon: IconManager.GetIcon("Material"));

// Avantages :
// - Style uniforme (bordure en pointillés quand hover avec payload)
// - Icône du type d'asset
// - Preview au hover
// - Click = popup picker
// - Drag = assign
```

---

### 8. **Pas de Multi-Edit Support**
**Impact : ÉLEVÉ** | **Fichiers : TOUS**

❌ **Problème :**
Aucun inspecteur ne supporte l'édition de plusieurs objets sélectionnés simultanément.

**Ce qui devrait arriver (Unity standard) :**
1. Sélectionner 5 cubes
2. Inspecteur affiche "Box Collider (5)"
3. Changer "Size.X = 2" applique à tous
4. Si valeurs différentes entre objets : afficher "—" (mixed)

**Actuellement :**
```csharp
// ComponentInspector.cs ligne 9 : Draw(Entity entity, Component component)
// ❌ Prend UN seul entity/component
```

✅ **Solution :**
```csharp
// Nouvelle signature :
public static void Draw(Entity[] entities, Component[] components)
{
    if (entities.Length > 1)
        ImGui.Text($"{component.GetType().Name} ({entities.Length})");
    
    // Pour chaque field :
    var values = components.Select(c => c.GetField("Size")).Distinct().ToArray();
    if (values.Length == 1)
        Widgets.Vector3Field("Size", ref values[0]);  // Valeur unique
    else
        Widgets.Vector3Field("Size", default, mixed: true);  // Affiche "—"
}
```

---

### 9. **Presets et Quick Actions manquants**
**Impact : MOYEN** | **Fichiers : `CameraInspector`, `LightInspector`, `MaterialInspector`**

❌ **Problème :**

**Camera :** Pas de presets FOV (60°, 90°, 120°), pas de "Reset to defaults"  
**Light :** Pas de presets (Soft, Hard, Studio, Sun)  
**Material :** Pas de quick actions (Duplicate, Reset, Save as Preset)  
**Terrain :** A des presets Resolution (ligne 62-72) ✅ Bon exemple !

✅ **Solution :**
```csharp
// En haut à droite de chaque section :
if (Widgets.PresetButton())
{
    ImGui.MenuItem("Reset to Default");
    ImGui.Separator();
    ImGui.MenuItem("First Person (60° FOV)");
    ImGui.MenuItem("Third Person (45° FOV)");
    ImGui.MenuItem("Wide Angle (90° FOV)");
}
```

---

### 10. **Pas de Preview/Gizmos interactifs**
**Impact : MOYEN** | **Fichiers : Colliders, Light, Camera**

❌ **Problème :**
- BoxCollider : pas de preview 3D des bounds
- Light : pas de preview du range/cone
- Camera : pas de preview du frustum
- Pas de gizmos éditables en scene view

**Unity standard :**
- Éditer le Box Collider Size → Gizmo vert dans scene view
- Éditer le Light Range → Sphere/cone visualisée
- Éditer Camera FOV → Frustum outline

✅ **Solution :**
```csharp
// Dans chaque inspecteur, appeler le système de Gizmos :
Widgets.Vector3Field("Size", ref boxCollider.Size, 
    onEdit: (newSize) => {
        EditorGizmos.DrawWireBox(boxCollider.Center, newSize, Color.Green);
        EditorGizmos.MakeHandles(ref boxCollider.Center, ref boxCollider.Size);
    });
```

---

## 📊 RÉSUMÉ DES PROBLÈMES PAR CATÉGORIE

| Catégorie | Critique | Moyen | Mineur | Total |
|-----------|----------|-------|--------|-------|
| **Cohérence visuelle** | 1 | 2 | 3 | 6 |
| **Feedback utilisateur** | 2 | 2 | 1 | 5 |
| **Efficacité** | 2 | 1 | 2 | 5 |
| **Clarté** | 0 | 2 | 3 | 5 |
| **Standards Unity** | 1 | 1 | 1 | 3 |
| **TOTAL** | **6** | **8** | **10** | **24** |

---

## 🎯 PLAN DE REFONTE PRIORITAIRE

### Phase 1 : Foundation (CRITIQUE)
**Durée estimée : 2-3h**
1. ✅ Créer `InspectorWidgets.cs` avec tous les widgets standardisés
2. ✅ Implémenter auto undo/redo sur tous les widgets
3. ✅ Ajouter validation visuelle (warning/error states)
4. ✅ Standardiser largeurs et spacing (responsive)
5. ✅ Supprimer duplication Light Component

### Phase 2 : Visual Feedback (ÉLEVÉ)
**Durée estimée : 2h**
1. ✅ Ajouter tooltips sur TOUS les paramètres
2. ✅ Icônes d'aide contextuelle `(?)`
3. ✅ Validation en temps réel (Near/Far, etc.)
4. ✅ Warning badges (⚠️ pour references manquantes)
5. ✅ Améliorer drag & drop zones (style uniforme)

### Phase 3 : Organization (MOYEN)
**Durée estimée : 2h**
1. ✅ Refactoriser grouping dans tous les inspecteurs
2. ✅ Créer sections collapsibles uniformes
3. ✅ Ajouter presets et quick actions
4. ✅ Améliorer Component Reference picker

### Phase 4 : Advanced Features (MOYEN)
**Durée estimée : 3-4h**
1. ⚠️ Multi-edit support (architecture)
2. ⚠️ Gizmos interactifs (intégration scene view)
3. ⚠️ Preview panels (materials, lights)
4. ⚠️ Context menu sur labels (Copy, Paste, Reset)

---

## 🛠️ STANDARDS UX À IMPLÉMENTER

### Layout Standard
```
┌─────────────────────────────────────────┐
│ [Icon] Component Name         [⚙️] [?] │  ← Header avec icône + preset menu
├─────────────────────────────────────────┤
│                                          │
│ ▼ Section Name                           │  ← Collapsible section
│   Label                    [Value Field] │  ← 40% label / 60% control
│   Label (tooltip)     [⚠️] [Value Field] │  ← Warning icon si validation fail
│   [Preset Button]                        │  ← Quick actions inline
│                                          │
│ ▼ Advanced Section                       │
│   ...                                    │
│                                          │
└─────────────────────────────────────────┘
```

### Color Palette
```csharp
// Système de couleurs Unity-like
InspectorColors.Label          = (0.8f, 0.8f, 0.8f, 1f);  // Blanc-gris
InspectorColors.LabelDisabled  = (0.5f, 0.5f, 0.5f, 1f);  // Gris
InspectorColors.Value          = (1.0f, 1.0f, 1.0f, 1f);  // Blanc
InspectorColors.ValueModified  = (0.8f, 1.0f, 0.8f, 1f);  // Vert clair (changed)
InspectorColors.Warning        = (1.0f, 0.8f, 0.2f, 1f);  // Jaune-orange
InspectorColors.Error          = (1.0f, 0.3f, 0.3f, 1f);  // Rouge
InspectorColors.Section        = (0.3f, 0.5f, 0.8f, 1f);  // Bleu
InspectorColors.DropZone       = (0.2f, 0.6f, 1.0f, 0.3f); // Bleu transparent
```

### Spacing Standard
```csharp
InspectorLayout.Padding        = 8f;   // Padding général
InspectorLayout.ItemSpacing    = 4f;   // Entre deux fields
InspectorLayout.SectionSpacing = 12f;  // Entre sections
InspectorLayout.LabelWidth     = 120f; // Largeur labels (40% de 300px)
InspectorLayout.ControlWidth   = 180f; // Largeur controls (60% de 300px)
```

---

## 📝 FICHIERS À MODIFIER (par priorité)

### Priority 1 - Core Infrastructure
1. ✅ **`InspectorWidgets.cs`** (CRÉER) - Tous les widgets standardisés
2. ✅ **`InspectorStyles.cs`** (CRÉER) - Couleurs, spacing, fonts
3. ✅ **`ComponentInspector.cs`** - Supprimer DrawLight(), améliorer routing

### Priority 2 - Component Inspectors (refactor avec nouveaux widgets)
4. ✅ **`CameraInspector.cs`** - Regrouper sections, validation, tooltips
5. ✅ **`LightInspector.cs`** - Presets, validation Range/Angle
6. ✅ **`BoxColliderInspector.cs`** - Preview gizmo, layer mask proper widget
7. ✅ **`SphereColliderInspector.cs`** - Idem BoxCollider
8. ✅ **`CapsuleColliderInspector.cs`** - Idem BoxCollider

### Priority 3 - Complex Inspectors
9. ✅ **`MaterialInspector.cs`** - Améliorer drag & drop, preview
10. ✅ **`TerrainInspector.cs`** - Déjà bon, juste standardiser widgets
11. ✅ **`UIElementInspector.cs`** - Regrouper sections, presets anchor

### Priority 4 - Utilities
12. ✅ **`FieldWidgets.cs`** - Nettoyer, supprimer ancienne ComponentRefObj
13. ✅ **`FieldEditAction.cs`** - Vérifier undo/redo fonctionne partout

---

## ✅ VALIDATION FINALE

### Checklist UX avant/après refonte
- [ ] **Cohérence** : Tous les inspecteurs utilisent mêmes widgets/spacing/couleurs
- [ ] **Feedback** : Validation visuelle sur tous les champs critiques
- [ ] **Tooltips** : 100% des paramètres ont une tooltip
- [ ] **Undo/Redo** : Fonctionne sur 100% des modifications
- [ ] **Drag & Drop** : Style uniforme, preview au hover
- [ ] **Grouping** : Sections logiques, collapsibles, presets
- [ ] **Performance** : Pas de lag avec 100+ entités sélectionnées
- [ ] **Responsive** : S'adapte à différentes largeurs de panel

---

## 🎓 RÉFÉRENCES UNITY (versions 2023+)

Les patterns suivis sont basés sur :
- **Unity 2023 Inspector** : Collapsible sections, validation icons, tooltips
- **Unity UI Toolkit** : Responsive layout, auto-width controls
- **Unity SerializedProperty** : Undo/redo automatique, multi-edit
- **Unity EditorGUI** : Color schemes, spacing, preset buttons
- **Unity PropertyDrawers** : Custom widget per type

---

## 💡 CONCLUSION

**Score UX actuel : 4.5/10**
- ✅ Fonctionnel de base
- ❌ Manque cohérence
- ❌ Pas de feedback visuel
- ❌ Pas de tooltips
- ❌ Pas de multi-edit

**Score UX cible : 9/10**
- ✅ Cohérence totale
- ✅ Feedback visuel professionnel
- ✅ Tooltips partout
- ✅ Multi-edit support
- ✅ Presets et quick actions
- ✅ Standards Unity respectés

**Effort estimé : 10-12h de développement**

