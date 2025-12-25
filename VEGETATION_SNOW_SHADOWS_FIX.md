# Fix : Snow Displacement & Shadows pour VegetationForward

## Problèmes identifiés

1. ❌ **Pas de snow displacement** sur VegetationForward.vert
2. ❌ **Pas d'ombres** sur la végétation (ni sur directional light, ni sur ambient IBL)

## Corrections appliquées

### 1. VegetationForward.vert - Snow Displacement

**Ajouté** :
- Uniforms `u_SnowAccumulation`, `u_SnowDisplacement`, `u_SnowSlopeMin`, `u_SnowSlopeMax`
- Fonction `calculateSnowPlacement()` (identique à TerrainForward et ForwardBase)
- Calcul du displacement vertical après les effets de vent
- Normal smoothing pour bords arrondis

**Code ajouté** (lignes 203-221) :
```glsl
// === SNOW DISPLACEMENT ===
float snowPlacement = calculateSnowPlacement(worldNormal, u_SnowSlopeMin, u_SnowSlopeMax);
float snowAmount = u_SnowAccumulation * snowPlacement;

// Vertical displacement
float displacementAmount = (1.0 - exp(-snowAmount * 0.8)) * u_SnowDisplacement;
worldPos.y += displacementAmount;

// Smooth normals for rounded snow edges
float normalSmoothFactor = clamp(snowAmount * 0.3, 0.0, 0.7);
vec3 smoothedNormal = normalize(mix(worldNormal, vec3(0, 1, 0), normalSmoothFactor));
```

---

### 2. VegetationForward.frag - Ombres

**Ajouté** :
- `#include "../Includes/Shadows.glsl"` (ligne 7)
- Calcul de `shadowFactor` avec `calculateShadowWithNL()` (ligne 414)
- Application de `shadowFactor` à la directional light (ligne 415)
- **CRITIQUE** : Application de `shadowFactor` à l'ambient IBL (ligne 444)

**Code ajouté/modifié** (lignes 410-444) :
```glsl
// Directional light with shadows
vec3 dirLighting = calculateDirectionalLight(N, V, material);
vec3 viewPos = vWorldPos - uCameraPos;
vec3 L = normalize(-uDirLightDirection);
float shadowFactor = calculateShadowWithNL(vWorldPos, viewPos, N, L);
Lo += dirLighting * shadowFactor;  // ← Ombres appliquées !

// ... Point lights, Spot lights ...

// Ambient lighting + AO
ambient = calculateAmbientLighting(material, vWorldPos);
// ... occlusion texture ...

// CRITICAL FIX: Apply shadows to ambient IBL too!
ambient *= mix(0.3, 1.0, shadowFactor);  // ← 30% ambient en ombre, 100% en lumière
```

---

## Résultat

✅ **La végétation reçoit maintenant** :
- Épaisseur de neige visuelle (vertex displacement)
- Bords arrondis (normal smoothing)
- Ombres correctes de la directional light
- Ombres sur l'ambient IBL (comme le terrain et les meshes)

✅ **Uniformité** :
Tous les shaders forward (TerrainForward, ForwardBase, VegetationForward) ont maintenant :
- Le même système de snow displacement
- Les mêmes calculs d'ombres sur ambient IBL

---

## Testing

Pour vérifier :
1. Augmente `SnowAccumulation` à 2.0
2. Vérifie que les arbres/plantes se soulèvent (~38cm)
3. Place un objet en ombre → la neige doit être plus sombre (70% plus sombre)
4. Augmente `Directional Light Intensity` → la neige doit réagir

---

**Date** : 24 décembre 2024
**Shaders modifiés** : VegetationForward.vert, VegetationForward.frag
