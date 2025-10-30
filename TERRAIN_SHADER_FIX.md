# 🔧 Fix: Terrain Shader Loading Issue

## Problème Identifié

### Symptômes
- ❌ Terrain invisible
- ❌ Textures blanches sur tous les objets
- ❌ Erreur console: `Shader file not found: Engine/Rendering/Shaders/Forward/TerrainForward.vert`

### Logs d'Erreur
```
[TerrainRenderer] ✗ CRITICAL: Failed to load shader 'TerrainForward': Shader file not found: Engine/Rendering/Shaders/Forward/TerrainForward.vert
[TerrainRenderer] CRITICAL: Failed to load TerrainForward shader - terrain will not render!
```

## Cause Racine

Le `TerrainRenderer.LoadTerrainShader()` chargeait le shader **directement** avec `ShaderProgram.FromFiles()` au lieu d'utiliser le **ShaderLibrary**.

### Code Problématique (AVANT)
```csharp
private Engine.Rendering.ShaderProgram? LoadTerrainShader(string shaderName)
{
    // ❌ Chargement direct - chemin relatif ne fonctionne pas
    var shader = Engine.Rendering.ShaderProgram.FromFiles(
        $"Engine/Rendering/Shaders/Forward/{shaderName}.vert",
        $"Engine/Rendering/Shaders/Forward/{shaderName}.frag");
    
    return shader;
}
```

**Pourquoi ça ne marchait pas ?**
- `ShaderPreprocessor.ProcessShaderCached()` utilise `File.Exists(shaderPath)` avec le chemin exact passé
- Les chemins relatifs (`Engine/Rendering/...`) ne fonctionnent que depuis la racine du projet
- Le working directory peut varier selon comment l'app est lancée

## Solution Appliquée

### Code Corrigé (APRÈS)
```csharp
private Engine.Rendering.ShaderProgram? LoadTerrainShader(string shaderName)
{
    // ✅ Utilisation du ShaderLibrary - résolution de chemin automatique
    var shader = Engine.Rendering.ShaderLibrary.GetShaderByName(shaderName);
    
    if (shader != null)
    {
        Console.WriteLine($"[TerrainRenderer] ✓ Loaded shader '{shaderName}' (Handle: {shader.Handle})");
    }
    else
    {
        Console.WriteLine($"[TerrainRenderer] ✗ Shader '{shaderName}' not found in ShaderLibrary");
    }
    
    return shader;
}
```

**Pourquoi ça fonctionne maintenant ?**
- ✅ `ShaderLibrary` scanne automatiquement `Engine/Rendering/Shaders/**/*.vert`
- ✅ Résolution de chemin robuste (gère working directory)
- ✅ Cache des shaders compilés
- ✅ Détection automatique des tessellation shaders (.tesc/.tese)
- ✅ Binding automatique des uniform blocks globaux

## Fichiers Modifiés

### `Engine/Rendering/Terrain/TerrainRenderer.cs`
- ✏️ Ligne 51-70: Remplacé `ShaderProgram.FromFiles()` par `ShaderLibrary.GetShaderByName()`

## Vérification

### Avant Fix
```
[TerrainRenderer] ✗ CRITICAL: Failed to load shader 'TerrainForward'
[TerrainRenderer] CRITICAL: Failed to load TerrainForward shader - terrain will not render!
```

### Après Fix (attendu)
```
[TerrainRenderer] Attempting to load shader 'TerrainForward' from ShaderLibrary...
[TerrainRenderer] ✓ Loaded shader 'TerrainForward' (Handle: 42)
```

## Problème Secondaire Potentiel: Textures Blanches

Si le terrain s'affiche mais reste blanc, vérifier :

### 1. Matériau du Terrain
```csharp
// Dans l'inspecteur Terrain
Material: Assets/Materials/Terrain.material
```

### 2. Textures du Matériau
Le matériau `Terrain.material` doit avoir :
- **Albedo Map** (texture de base)
- **Normal Map** (optionnel)
- **Heightmap** (pour displacement)

### 3. Vérifier dans l'Asset Database
```
[AssetDatabase] Material loaded: Terrain.material
[AssetDatabase] Texture loaded: terrain_albedo.png
[AssetDatabase] Texture loaded: terrain_normal.png
```

### 4. Code de Diagnostic
Ajouter des logs dans `TerrainRenderer.RenderTerrain()`:
```csharp
if (material != null)
{
    Console.WriteLine($"[Terrain] Material: {material.Name}");
    Console.WriteLine($"[Terrain] Albedo: {material.AlbedoTexture?.Path ?? "None"}");
    Console.WriteLine($"[Terrain] Normal: {material.NormalTexture?.Path ?? "None"}");
}
```

## Prochaines Étapes

Si le terrain reste blanc après ce fix :

1. **Vérifier le matériau dans l'inspecteur**
   - Ouvrir l'entité "Terrain"
   - Vérifier que "Material GUID" est rempli
   - Cliquer sur le matériau pour l'ouvrir

2. **Vérifier les textures du matériau**
   - Ouvrir `Assets/Materials/Terrain.material`
   - Assigner les textures (drag & drop depuis Assets)
   - Sauvegarder (Ctrl+S)

3. **Hot-reload du matériau**
   - Après modification du .material
   - Le terrain devrait se rafraîchir automatiquement (MaterialSaved event)

## Impact du Système de Thèmes

⚠️ **Note importante** : Le système de thèmes n'affecte **PAS** le rendu 3D !

- ✅ Thèmes = UI de l'éditeur uniquement (ImGui)
- ✅ Rendu 3D = Shaders OpenGL indépendants
- ✅ Les deux systèmes sont totalement découplés

Le problème du terrain était **préexistant** et **non causé** par les thèmes.

## Build Status

✅ **Build SUCCESS** (0 errors, 0 warnings)

```
Engine -> C:\...\Engine\bin\Debug\net8.0\win-x64\Engine.dll
La génération a réussi.
    0 Avertissement(s)
    0 Erreur(s)
```

---

**Status**: ✅ Fix appliqué, prêt à tester  
**Date**: 2024-10-18  
**Version**: Engine v0.1.0
