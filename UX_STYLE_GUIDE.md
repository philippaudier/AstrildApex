# AstrildApex Editor UX Style Guide

## 📐 Design Philosophy

L'éditeur AstrildApex suit une approche moderne et professionnelle inspirée des meilleurs éditeurs (Unity, Unreal, Godot) avec une attention particulière à :
- **Cohérence** : Toutes les interfaces utilisent les mêmes widgets et couleurs
- **Efficacité** : Actions rapides avec drag & drop et popups de sélection
- **Clarté** : Indicateurs visuels clairs (couleurs, icônes)
- **Accessibilité** : Multiples façons d'accomplir une tâche

## 🎨 Color Palette

### Primary Colors
- **Primary** : `Vector4(0.4f, 0.6f, 0.9f, 1f)` - Actions principales, headers
- **Primary Hovered** : `Vector4(0.5f, 0.7f, 1.0f, 1f)` - Hover state
- **Primary Active** : `Vector4(0.3f, 0.5f, 0.8f, 1f)` - Pressed state

### Status Colors
- **Success** : `Vector4(0.4f, 1f, 0.4f, 1f)` - ✓ Opérations réussies, assets assignés
- **Warning** : `Vector4(1f, 0.8f, 0.2f, 1f)` - ⚠ Avertissements
- **Error** : `Vector4(1f, 0.4f, 0.4f, 1f)` - ✗ Erreurs, assets manquants
- **Info** : `Vector4(0.4f, 0.8f, 1f, 1f)` - ℹ Informations

### UI Elements
- **Background** : `Vector4(0.2f, 0.2f, 0.3f, 1f)` - Zones de drag & drop
- **Separator** : `Vector4(0.4f, 0.4f, 0.5f, 0.6f)` - Lignes de séparation
- **Text Disabled** : `Vector4(0.5f, 0.5f, 0.6f, 1f)` - Texte désactivé/hints

## 📦 Widget System

### EditorWidgets.AssetField()

Widget unifié pour tous les champs d'assets avec :
- **Drag & Drop** : Zone claire pour glisser-déposer
- **Click to Select** : Popup avec liste filtrée des assets compatibles
- **Preview** : Aperçu pour les textures (optionnel)
- **Actions** : Boutons Select (ouvrir dans inspector) / Ping (highlight) / Clear

```csharp
var newTexture = EditorWidgets.AssetField(
    "Albedo Texture",              // Label
    material.AlbedoTexture,        // Current GUID
    "Texture2D",                   // Asset type filter
    "Main diffuse texture",        // Description/tooltip
    showPreview: true,             // Show texture preview
    dragDropHeight: 60f            // Height of drag zone
);
```

### Sections

```csharp
if (EditorWidgets.Section("Materials", defaultOpen: true, tooltip: "Material properties"))
{
    // Content...
    EditorWidgets.EndSection();
}
```

### Buttons

```csharp
if (EditorWidgets.PrimaryButton("Apply"))     // Action principale
if (EditorWidgets.SuccessButton("Save"))      // Action positive
if (EditorWidgets.DangerButton("Delete"))     // Action destructive
```

### Info Boxes

```csharp
EditorWidgets.InfoBox("Information message");
EditorWidgets.WarningBox("Warning message");
EditorWidgets.ErrorBox("Error message");
EditorWidgets.SuccessBox("Success message");
```

## 🎯 Usage Examples

### Material Assignment (comme MeshRenderer)

```csharp
// AVANT (ancien code verbeux)
ImGui.Text("Material:");
if (materialGuid.HasValue)
{
    var name = AssetDatabase.GetName(materialGuid.Value);
    ImGui.TextColored(green, $"✓ {name}");
    if (ImGui.Button("Clear")) materialGuid = null;
}
else
{
    ImGui.Button("Drag & Drop Material Here");
    if (ImGui.BeginDragDropTarget())
    {
        // 20 lignes de code...
    }
}

// APRÈS (nouveau système propre)
var newMaterial = EditorWidgets.AssetField(
    "Material",
    materialGuid,
    "Material",
    "Visual properties of the mesh"
);
if (newMaterial != materialGuid)
{
    materialGuid = newMaterial;
}
```

### Texture avec Preview (comme Terrain)

```csharp
var newHeightmap = EditorWidgets.AssetField(
    "Heightmap Texture",
    terrain.HeightmapTextureGuid,
    "Texture2D",
    "16-bit grayscale PNG recommended",
    showPreview: true,
    dragDropHeight: EditorWidgets.Layout.DragDropLargeHeight
);
```

## 📝 Migration Guide

### 1. Asset Fields
Remplacer tous les patterns de drag & drop manuels par `EditorWidgets.AssetField()`

### 2. Colors
Utiliser `EditorWidgets.Colors.*` au lieu de hardcoder les couleurs

### 3. Sections
Utiliser `EditorWidgets.Section()` au lieu de `InspectorWidgets.Section()`

### 4. Buttons
Utiliser les boutons typés (`PrimaryButton`, `DangerButton`, etc.) pour la cohérence

## ✨ Features

### Asset Field Avancé
- ✅ Drag & Drop depuis Assets panel
- ✅ Click pour ouvrir popup de sélection
- ✅ Recherche dans le popup
- ✅ Preview pour textures
- ✅ Bouton "Select" (ouvre dans inspector)
- ✅ Bouton "Ping" (highlight dans Assets panel)
- ✅ Bouton "Clear" (remove assignment)
- ✅ Validation du type d'asset
- ✅ Affichage d'erreur si asset manquant

### Popups de Sélection
- Liste tous les assets du type demandé
- Recherche en temps réel
- Tri alphabétique
- Tooltip avec chemin complet
- Scroll si beaucoup d'assets

## 🔄 Consistency Rules

1. **Toujours** utiliser `EditorWidgets.AssetField()` pour les assets
2. **Toujours** utiliser les couleurs de `EditorWidgets.Colors`
3. **Toujours** fournir des tooltips/descriptions
4. **Toujours** utiliser les constantes de `EditorWidgets.Layout`
5. **Jamais** hardcoder les couleurs ou tailles
6. **Jamais** dupliquer le code de drag & drop

## 🚀 Future Improvements

- [ ] Ping dans Assets panel (highlight asset)
- [ ] Recent assets dans les popups
- [ ] Favorites system
- [ ] Multi-asset drag & drop
- [ ] Asset preview plus large (zoom)
- [ ] Undo/Redo pour asset assignments
- [ ] Context menu sur assets assignés (copy path, etc.)
- [ ] Custom validators pour assets (ex: "Doit être 16-bit")

## 📚 Complete API Reference

Voir `Editor/UI/EditorWidgets.cs` pour la documentation complète de tous les widgets disponibles.
