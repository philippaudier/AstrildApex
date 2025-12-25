# Système LOD de Végétation - Documentation Complète

## Vue d'ensemble

Le système LOD (Level of Detail) pour la végétation offre des performances optimales avec des transitions visuelles fluides grâce au temporal dithering. Ce système remplace l'ancien système de Face Culling Override qui était inapproprié pour la gestion de la végétation.

## Architecture

### 1. Suppression du Face Culling Override

**Avant**: Le Face Culling Override permettait de forcer un mode de culling pour toute la végétation d'un layer, ce qui était problématique pour les modèles multi-submesh (ex: arbres avec tronc + feuilles).

**Après**: Le culling est maintenant géré **uniquement par les matériaux**. Chaque submesh utilise le mode de culling défini dans son matériau respectif, permettant par exemple:
- Tronc d'arbre: `Back` culling (solide, performance optimale)
- Feuillage: `None` culling (double-sided pour feuilles fines)

### 2. Système LOD à 3 Niveaux

Le système offre 3 niveaux de détail avec transitions fluides:

- **LOD0** (0-33% de la distance max): Détail maximum
- **LOD1** (33-66% de la distance max): Détail réduit
- **LOD2** (66-100% de la distance max): Billboard/Impostor

### 3. Temporal Dithering

Utilisation d'une matrice de Bayer 4x4 avec variation temporelle pour:
- Transitions LOD sans pop visuel
- Fade-out progressif à la distance maximale
- Pattern de dithering animé pour mouvement fluide

## Modifications des Fichiers

### VegetationLayer.cs

**Propriétés supprimées:**
```csharp
CullingOverride
VegetationCullingMode enum
```

**Nouvelles propriétés LOD:**
```csharp
public float MaxDrawDistance { get; set; } = 200f;
public bool EnableLOD { get; set; } = false;
public float LOD0Distance { get; set; } = 50f;  // 0-33%
public float LOD1Distance { get; set; } = 100f; // 33-66%
public float LOD2Distance { get; set; } = 150f; // 66-100%
```

### TerrainVegetationUI.cs

**Interface mise à jour:**
- Suppression de la section "Face Culling Override"
- Ajout des contrôles LOD avec sliders intuitifs
- Tooltips informatifs pour chaque paramètre
- Validation des distances (LOD1 > LOD0, LOD2 > LOD1, etc.)

### VegetationForward Shaders

**Vertex Shader (VegetationForward.vert):**
```glsl
// Nouveaux uniforms
uniform float u_MaxDrawDistance;
uniform int u_EnableLOD;
uniform float u_LOD0Distance;
uniform float u_LOD1Distance;
uniform float u_LOD2Distance;

// Nouvelle sortie
out float vDistanceToCamera;
```

**Fragment Shader (VegetationForward.frag):**
```glsl
// Matrice de Bayer 4x4 pour dithering
const mat4 BAYER_MATRIX = mat4(...);

// Fonction de dithering temporal
float getDitherThreshold(vec2 screenPos, float time);

// Calcul du niveau LOD avec transition
vec2 calculateLOD(float distance);

// Application du distance culling et LOD
```

### VegetationRenderer.cs

**Signature de Render mise à jour:**
```csharp
public void Render(
    Matrix4 view, Matrix4 projection, float time,
    // ... paramètres existants ...
    float maxDrawDistance = 200f, 
    bool enableLOD = false,
    float lod0Distance = 50f, 
    float lod1Distance = 100f, 
    float lod2Distance = 150f
)
```

**Transmission des uniforms au shader:**
```csharp
_vegetationShader.SetFloat("u_MaxDrawDistance", maxDrawDistance);
_vegetationShader.SetInt("u_EnableLOD", enableLOD ? 1 : 0);
_vegetationShader.SetFloat("u_LOD0Distance", lod0Distance);
_vegetationShader.SetFloat("u_LOD1Distance", lod1Distance);
_vegetationShader.SetFloat("u_LOD2Distance", lod2Distance);
```

### Terrain.cs

**Fonction simplifiée:**
```csharp
// Ancien: GetCullingModeForSubmesh(layer, materialGuid)
// Nouveau: GetCullingModeFromMaterial(materialGuid)
private CullingMode GetCullingModeFromMaterial(Guid? materialGuid)
{
    // Lit directement le culling du matériau
    // Pas d'override par layer
}
```

## Utilisation

### Configuration dans l'Inspector

1. **Activer le LOD:**
   - Cocher "Enable LOD System" dans Advanced Settings
   
2. **Configurer les distances:**
   - `LOD0 Distance`: Distance pour le détail maximum (recommandé: 25-33% de MaxDrawDistance)
   - `LOD1 Distance`: Distance pour le détail moyen (recommandé: 50-66% de MaxDrawDistance)
   - `LOD2 Distance`: Distance pour le billboard (recommandé: 75-90% de MaxDrawDistance)
   - `Max Draw Distance`: Distance maximale de rendu avec fade-out dithered

3. **Exemple de configuration:**
   ```
   Max Draw Distance: 200
   LOD0 Distance: 50  (0-25%)
   LOD1 Distance: 100 (25-50%)
   LOD2 Distance: 150 (50-75%)
   Fade-out: 150-200 (75-100%)
   ```

### Culling des Matériaux

Pour configurer le culling approprié:

1. **Pour les troncs d'arbres:**
   - Material Culling Mode: `Back`
   - Raison: Solide, jamais vu de l'intérieur

2. **Pour le feuillage:**
   - Material Culling Mode: `None` (Both Sides)
   - Raison: Feuilles fines visibles des deux côtés

3. **Pour les rochers:**
   - Material Culling Mode: `Back`
   - Raison: Solides, meilleures performances

## Avantages du Nouveau Système

### Performance
- ✅ Réduction progressive de la charge GPU avec la distance
- ✅ Culling automatique au-delà de MaxDrawDistance
- ✅ Contrôle précis de la densité de rendu

### Qualité Visuelle
- ✅ Transitions LOD invisibles grâce au temporal dithering
- ✅ Pas de "pop" lors du changement de LOD
- ✅ Fade-out progressif et naturel
- ✅ Culling approprié par submesh (tronc vs feuilles)

### Workflow
- ✅ Configuration par matériau (réutilisable)
- ✅ Pas de duplication de paramètres
- ✅ Interface claire et intuitive
- ✅ Validation automatique des distances

## Détails Techniques

### Temporal Dithering

Le dithering temporal utilise une matrice de Bayer 4x4 avec variation temporelle:

```glsl
float temporal = fract(time * 0.5);
float threshold = fract(staticPattern + temporal);
```

Avantages:
- Pattern animé évite les artefacts statiques
- Smooth blending entre LOD levels
- Compatible TAA (Temporal Anti-Aliasing)

### Distance Fade-out

Le fade-out commence à 85% de MaxDrawDistance:

```glsl
float fadeStart = u_MaxDrawDistance * 0.85;
float fadeFactor = 1.0 - clamp((distance - fadeStart) / fadeRange, 0.0, 1.0);

if (fadeFactor < ditherThreshold) {
    discard; // Temporal dithering
}
```

### LOD Transitions

Chaque transition LOD dispose d'une plage de 5 unités:

```glsl
float transitionRange = 5.0;
float t = (distance - lodThreshold) / transitionRange;
```

## Tests et Validation

### Scénarios de Test

1. **Test de densité:**
   - Générer 1000+ instances
   - Vérifier le framerate avec/sans LOD
   
2. **Test de transition:**
   - Déplacer la caméra à travers les seuils LOD
   - Vérifier l'absence de pop visuel
   
3. **Test de culling:**
   - Vérifier que troncs et feuilles ont des culling différents
   - Tester avec différents angles de caméra

### Métriques de Performance

À mesurer:
- FPS avec LOD activé vs désactivé
- Draw calls par frame
- Instances rendues par distance
- Temps de rendu GPU

## Migration depuis l'Ancien Système

Si vous aviez configuré `CullingOverride` sur vos layers:

1. **CullingOverride = Auto:**
   - Rien à faire, c'est le comportement par défaut maintenant

2. **CullingOverride = ForceBack:**
   - Configurer tous les matériaux du modèle avec `CullingMode = Back`

3. **CullingOverride = ForceNone:**
   - Configurer tous les matériaux du modèle avec `CullingMode = None`

## Conclusion

Le nouveau système LOD offre un pipeline propre, performant et robuste pour le rendu de végétation dense. Le passage du culling au niveau du matériau permet une flexibilité maximale tout en maintenant des performances optimales.

**Points clés:**
- ✅ Culling géré par les matériaux (approprié)
- ✅ LOD à 3 niveaux avec dithering
- ✅ Transitions invisibles
- ✅ Configuration intuitive
- ✅ Pipeline robuste et maintenable
