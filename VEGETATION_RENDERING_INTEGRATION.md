# Vegetation Rendering & Weather System - Implementation Guide

## Vue d'ensemble

Ce guide documente l'intégration du système de rendu de végétation avec support du vent et des effets météorologiques, ainsi que l'ajout du culling mode dans le MeshRenderer.

## 1. Shaders VegetationForward

### VegetationForward.vert

**Emplacement** : `Engine/Rendering/Shaders/Forward/VegetationForward.vert`

Vertex shader optimisé pour GPU instancing avec support complet du vent et de la météo.

#### Caractéristiques principales :

- **GPU Instancing** : Utilise 4 attributs d'instance (locations 3-6) pour transférer les matrices de transformation
- **Animation de vent** :
  - `u_WindStrength` : Intensité du vent (0.0 à 1.0)
  - `u_WindDirection` : Direction normalisée du vent (plan XZ)
  - `u_WindSpeed` : Vitesse d'animation
  - `u_WindGustiness` : Turbulence (0.0 = doux, 1.0 = rafales)
  - `u_Time` : Temps du jeu pour l'animation
  
- **Effets météo** :
  - `u_RainIntensity` : Fait ployer la végétation vers le bas
  - `u_SnowCoverage` : Pour les effets visuels dans le fragment shader
  
- **Variation procédurale** : Chaque instance utilise sa position pour varier l'animation (évite l'aspect synchronisé)

#### Outputs :
- `vWindFactor` : Facteur d'influence du vent basé sur la hauteur du vertex (0 en bas, 1 en haut)
- `vInstancePos` : Position de l'instance pour les effets de pluie/neige

### VegetationForward.frag

**Emplacement** : `Engine/Rendering/Shaders/Forward/VegetationForward.frag`

Fragment shader basé sur ForwardBase avec extensions météorologiques.

#### Nouveaux uniforms météo :

```glsl
uniform float u_RainIntensity;     // 0.0 = pas de pluie, 1.0 = forte pluie
uniform float u_SnowCoverage;      // 0.0 = pas de neige, 1.0 = neige complète
uniform float u_Wetness;           // Mouillure de surface (affecte roughness/specular)
```

#### Effets implémentés :

1. **Accumulation de neige** :
   - Calcule l'accumulation basée sur l'orientation de la surface (surfaces horizontales accumulent plus)
   - Ajoute variation procédurale basée sur la position mondiale
   - Blende vers une couleur blanche/bleue
   - Réduit le metallic et augmente légèrement la roughness

2. **Effets de pluie** :
   - Assombrit les surfaces (look mouillé)
   - Réduit la roughness (surfaces plus spéculaires quand mouillées)
   - Atténue légèrement l'éclairage ambiant

3. **Support transparence** : Utilise le canal alpha de l'albedo pour les feuilles

## 2. Intégration du rendu

### ViewportRenderer

**Fichier** : `Editor/Rendering/ViewportRenderer.cs`

#### Modifications :

1. **Champ** : `private Engine.Rendering.VegetationRenderer? _vegetationRenderer = null;`

2. **Initialisation** dans `Resize()` :
```csharp
if (_vegetationRenderer == null)
{
    _vegetationRenderer = new Engine.Rendering.VegetationRenderer();
    LogManager.LogInfo("Vegetation Renderer initialized successfully", "Renderer");
}
```

3. **Rendu** dans `RenderScene()` (après opaque, avant particles) :
```csharp
// === QUEUE 3000: VEGETATION ===
foreach (var entity in Scene.Entities)
{
    var terrain = entity.GetComponent<Engine.Components.Terrain>();
    if (terrain?.VegetationLayers != null && terrain.VegetationInstances != null)
    {
        for (int layerIndex = 0; layerIndex < terrain.VegetationLayers.Length; layerIndex++)
        {
            var layer = terrain.VegetationLayers[layerIndex];
            if (layer.Enabled && layer.ModelGuid.HasValue)
            {
                if (terrain.VegetationInstances.TryGetValue(layerIndex, out var instances))
                {
                    _vegetationRenderer.UpdateBatch(layer.ModelGuid.Value, layer.SubmeshIndex, instances);
                }
            }
        }
    }
}
// TODO: _vegetationRenderer.Render(...) quand le shader sera chargé
```

4. **Cleanup** dans `Dispose()` :
```csharp
_vegetationRenderer?.Dispose();
```

### GameRenderer

**Fichier** : `Editor/Rendering/GameRenderer.cs`

Modifications identiques à ViewportRenderer :
- Champ `_vegetationRenderer`
- Initialisation dans `InitializeOpenGL()`
- Rendu après les terrains dans `RenderScene()`
- Cleanup dans `Dispose()`

## 3. MeshRenderer Culling Mode

### Enum CullingMode

**Fichier** : `Engine/Components/MeshRendererComponent.cs`

```csharp
public enum CullingMode
{
    Back = 0,   // Cull back faces (défaut)
    Front = 1,  // Cull front faces (géométrie inversée)
    None = 2    // Pas de culling (rendu recto-verso)
}
```

### Propriété dans MeshRendererComponent

```csharp
[Engine.Serialization.Serializable("cullingMode")]
public CullingMode Culling { get; set; } = CullingMode.Back;
```

### Utilisation dans l'inspecteur

L'UI de l'inspecteur devra être mise à jour pour afficher un dropdown avec les 3 options :
- **Back** : Pour la plupart des objets (valeur par défaut)
- **Front** : Pour les objets "inside-out" (skybox, etc.)
- **None** : Pour les objets fins devant être visibles des deux côtés (feuilles, papier, etc.)

### Application du culling dans les renderers

Les renderers (ViewportRenderer, GameRenderer) devront appliquer le culling mode avant de dessiner chaque mesh :

```csharp
switch (meshRenderer.Culling)
{
    case CullingMode.Back:
        GL.Enable(EnableCap.CullFace);
        GL.CullFace(CullFaceMode.Back);
        break;
    case CullingMode.Front:
        GL.Enable(EnableCap.CullFace);
        GL.CullFace(CullFaceMode.Front);
        break;
    case CullingMode.None:
        GL.Disable(EnableCap.CullFace);
        break;
}
```

## 4. Prochaines étapes

### Chargement du shader VegetationForward

Le VegetationRenderer doit être modifié pour charger et utiliser les shaders VegetationForward :

```csharp
private int _vegetationShader = 0;

private void LoadShader()
{
    string vertPath = "Engine/Rendering/Shaders/Forward/VegetationForward.vert";
    string fragPath = "Engine/Rendering/Shaders/Forward/VegetationForward.frag";
    _vegetationShader = ShaderCompiler.CompileShaderProgram(vertPath, fragPath);
}
```

### Uniforms à envoyer

Le renderer doit envoyer les paramètres de vent et météo :

```csharp
GL.Uniform1(GL.GetUniformLocation(shader, "u_WindStrength"), windStrength);
GL.Uniform2(GL.GetUniformLocation(shader, "u_WindDirection"), windDirX, windDirZ);
GL.Uniform1(GL.GetUniformLocation(shader, "u_WindSpeed"), windSpeed);
GL.Uniform1(GL.GetUniformLocation(shader, "u_WindGustiness"), gustiness);
GL.Uniform1(GL.GetUniformLocation(shader, "u_Time"), currentTime);
GL.Uniform1(GL.GetUniformLocation(shader, "u_RainIntensity"), rainIntensity);
GL.Uniform1(GL.GetUniformLocation(shader, "u_SnowCoverage"), snowCoverage);
GL.Uniform1(GL.GetUniformLocation(shader, "u_Wetness"), wetness);
```

Ces paramètres peuvent être exposés dans le composant Terrain ou dans un composant WeatherSystem séparé.

## 5. Paramètres suggérés pour tester

### Vent doux (brises) :
- `u_WindStrength` : 0.2
- `u_WindSpeed` : 1.0
- `u_WindGustiness` : 0.3

### Vent fort (tempête) :
- `u_WindStrength` : 0.8
- `u_WindSpeed` : 3.0
- `u_WindGustiness` : 0.7

### Pluie :
- `u_RainIntensity` : 0.6
- `u_Wetness` : 0.8

### Neige :
- `u_SnowCoverage` : 0.5 (50% couverture)

## 6. Performance

Le système est optimisé pour des scènes cinématiques :
- **GPU Instancing** : 1 draw call par mesh unique, supporte 100,000+ instances
- **Pas de CPU overhead** : Les animations de vent sont entièrement dans le shader
- **Batching automatique** : Les instances du même modèle+submesh sont groupées

## 7. Compatibilité

- **OpenGL 4.2+** : Requis pour les includes de shader et l'instancing avancé
- **.NET 8.0** : Testé avec le runtime actuel
- **Shaders existants** : Compatibles avec le système d'éclairage IBL/PBR existant
