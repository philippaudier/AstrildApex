# Terrain Layer System - Material-Based Upgrade

## 🎯 Objectif

Remplacer le système de textures individuelles (Albedo, Normal) par un système basé sur **Materials complets** pour chaque layer du terrain.

## ✅ Avantages

### Avant (Textures individuelles)
```csharp
public class TerrainLayer
{
    public Guid? AlbedoTexture { get; set; }
    public Guid? NormalTexture { get; set; }
    public float Metallic { get; set; }
    public float Smoothness { get; set; }
}
```

**Problèmes :**
- ❌ Propriétés PBR limitées (pas de Roughness, AO, Emission, etc.)
- ❌ Configuration fastidieuse (assigner chaque texture séparément)
- ❌ Pas de réutilisation (chaque layer doit tout redéfinir)
- ❌ Difficile à maintenir

### Après (Material-Based)
```csharp
public class TerrainLayer
{
    public Guid? Material { get; set; }  // Référence à un Material complet
    public float[] Tiling { get; set; }  // UV transform spécifique au layer
    public float[] Offset { get; set; }
}
```

**Avantages :**
- ✅ Propriétés PBR complètes (Albedo, Normal, Metallic, Roughness, AO, Emission, etc.)
- ✅ Configuration simple (drag & drop d'un Material)
- ✅ Réutilisation (même Material pour plusieurs layers)
- ✅ Facile à maintenir (modifier le Material met à jour tous les layers qui l'utilisent)
- ✅ Cohérence avec le reste du moteur

## 📝 Changements effectués

### 1. **TerrainLayer.cs** - Ajout de la propriété Material

```csharp
// Nouvelle propriété principale
[Editable]
public Guid? Material { get; set; }

// UV Transform (indépendant du Material)
public float[] Tiling { get; set; } = new float[] { 1f, 1f };
public float[] Offset { get; set; } = new float[] { 0f, 0f };

// DEPRECATED: Propriétés legacy (rétrocompatibilité)
[Obsolete("Use Material property instead")]
public Guid? AlbedoTexture { get; set; }

[Obsolete("Use Material property instead")]
public Guid? NormalTexture { get; set; }
```

**Rétrocompatibilité :** Les anciennes propriétés sont marquées `[Obsolete]` mais conservées pour ne pas casser les terrains existants.

### 2. **TerrainLayersUI.cs** - Nouvelle UI pour gérer les layers

Nouvelle classe helper qui fournit :
- Liste des layers avec TreeNode collapsibles
- Drag & Drop de Materials
- Édition des propriétés de blending (Height, Slope)
- Édition des UV Transform (Tiling, Offset)
- Boutons Add/Delete pour gérer les layers

### 3. **TerrainInspector.cs** - Intégration de l'UI

```csharp
// Ajout d'une section "Terrain Layers" dans l'inspecteur
TerrainLayersUI.DrawTerrainLayers(terrain);
```

## 🎨 Utilisation

### Workflow

1. **Créer un Material standard** (PBR)
   - Albedo, Normal, Metallic, Roughness, AO, etc.
   - Configurer toutes les propriétés souhaitées

2. **Assigner un Material TerrainForward au Terrain**
   - Ce Material contient les layers

3. **Ajouter des Layers dans l'inspecteur**
   - Click "Add Layer"
   - Drag & Drop un Material dans chaque layer

4. **Configurer le blending**
   - Height Range : Plage d'altitude (ex: 0-10m pour herbe)
   - Slope Range : Plage de pente (ex: 0-30° pour herbe, 30-90° pour roche)
   - Tiling/Offset : Ajuster la répétition de la texture
   - Strength : Intensité du layer
   - Priority : Ordre de rendu

### Exemple

```
Layer 0: Grass
- Material: Grass.material (albedo, normal, roughness)
- Height: 0-15m
- Slope: 0-35°
- Tiling: (10, 10)

Layer 1: Rock
- Material: Rock.material (albedo, normal, roughness, AO)
- Height: 5-100m
- Slope: 35-90°
- Tiling: (5, 5)

Layer 2: Snow
- Material: Snow.material (albedo, normal)
- Height: 50-100m
- Slope: 0-60°
- Tiling: (8, 8)
```

## 🔧 Migration (TODO)

### Terrain Renderer

Le `TerrainRenderer` doit être mis à jour pour :

1. **Charger les Materials** depuis les layers
2. **Extraire les textures** de chaque Material (Albedo, Normal, etc.)
3. **Bind les textures** dans le shader TerrainForward
4. **Appliquer les UV Transform** (Tiling/Offset) par layer

### Exemple de code (à implémenter)

```csharp
// Dans TerrainRenderer
foreach (var layer in terrain.Layers)
{
    if (!layer.Material.HasValue) continue;
    
    // Charger le Material
    var material = LoadMaterial(layer.Material.Value);
    
    // Extraire les textures
    int albedoTex = material.AlbedoTexture;
    int normalTex = material.NormalTexture;
    int metallicTex = material.MetallicTexture;
    int roughnessTex = material.RoughnessTexture;
    
    // Bind dans le shader
    GL.ActiveTexture(TextureUnit.Texture0 + layerIndex * 4 + 0);
    GL.BindTexture(TextureTarget.Texture2D, albedoTex);
    
    GL.ActiveTexture(TextureUnit.Texture0 + layerIndex * 4 + 1);
    GL.BindTexture(TextureTarget.Texture2D, normalTex);
    
    // ...
    
    // Upload UV transform
    shader.SetVector2($"u_Layer{layerIndex}Tiling", layer.Tiling);
    shader.SetVector2($"u_Layer{layerIndex}Offset", layer.Offset);
}
```

## 🚀 Prochaines étapes

1. ✅ Modifier TerrainLayer pour utiliser Material
2. ✅ Créer l'UI pour éditer les layers
3. ⏳ **Mettre à jour TerrainRenderer** pour charger les Materials
4. ⏳ **Mettre à jour le shader TerrainForward** pour supporter les nouvelles textures
5. ⏳ Tester avec un terrain réel
6. ⏳ Documenter les performances

## 📚 Références

- `Engine/Assets/TerrainLayer.cs` - Définition du layer
- `Editor/Inspector/TerrainLayersUI.cs` - UI pour éditer les layers
- `Editor/Inspector/TerrainInspector.cs` - Inspecteur du terrain
- `Engine/Rendering/Terrain/TerrainRenderer.cs` - Rendu du terrain (à mettre à jour)
- `Engine/Rendering/Shaders/Terrain/TerrainForward.frag` - Shader (à mettre à jour)
