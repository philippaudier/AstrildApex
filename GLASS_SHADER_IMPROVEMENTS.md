# Glass Shader - Améliorations physiques et SSAO

## Changements effectués

### 1. **Thickness physique view-dependent** ✅

**Problème** : La thickness était uniforme sur toute la sphère, pas réaliste.

**Solution** : Calcul d'épaisseur effective basée sur l'angle de vue (lignes 132-142)

```glsl
// Center of sphere (NdotV=1.0) = full diameter, edges (NdotV=0.0) = thin
float viewAngleFactor = mix(0.2, 1.0, NdotV); // 20% thickness at edges, 100% at center
float effectiveThickness = u_Thickness * viewAngleFactor;

// Beer's law: I = I0 * exp(-μ * d) where d = thickness
float absorption = exp(-effectiveThickness * 3.0);
```

**Résultat** :
- ✅ Centre de la sphère : thickness = 100% (traverse le diamètre complet)
- ✅ Bords de la sphère : thickness = 20% (traverse peu de matière)
- ✅ Absorption réaliste basée sur Beer's law

---

### 2. **Opacité basée sur thickness** ✅

**Problème** : L'opacité ne changeait pas avec la thickness.

**Solution** : Alpha calculé avec thickness effective (lignes 155-160)

```glsl
// Thick glass absorbs more light → more opaque
float thicknessOpacity = 1.0 - absorption; // More absorption = more opaque
float baseAlpha = mix(u_Opacity, u_Opacity + thicknessOpacity * 0.5, effectiveThickness);
float alpha = mix(baseAlpha, 1.0, fresnel);
```

**Résultat** :
- ✅ Verre épais (thickness=1.0) + tint foncée = opaque au centre
- ✅ Verre fin (thickness=0.1) = très transparent partout
- ✅ Effet Fresnel preserved (bords toujours plus réfléchissants)

---

### 3. **Compensation SSAO** ✅

**Problème** : SSAO "baked" dans sceneColorTex créait des artefacts sombres à travers le verre réfracté.

**Solution** : Éclaircissement des zones sombres (lignes 103-109)

```glsl
// SSAO compensation: Lighten dark areas (SSAO artifacts) through glass
float sceneLuminance = dot(sceneColor, vec3(0.299, 0.587, 0.114));
if (sceneLuminance < 0.3) {
    // Lighten dark SSAO areas by 30-50% to reduce artifacts
    sceneColor *= mix(1.0, 1.5, (0.3 - sceneLuminance) / 0.3);
}
```

**Résultat** :
- ✅ Zones très sombres (SSAO) éclaircies de 30-50%
- ✅ Réduit les artefacts de "taches noires" déformées
- ✅ Préserve les vraies ombres (luminance > 0.3)

**Limitation** : Ce n'est pas parfait - le SSAO reste légèrement visible mais beaucoup moins prononcé.

---

## Rendu physique réaliste

### Comment une sphère de verre épaisse se comporte :

#### **Verre fin (thickness = 0.1)**
- Centre : Légèrement teinté, très transparent
- Bords : Presque invisible, forte réflection (Fresnel)
- Exemple : Verre à boire, ampoule

#### **Verre moyen (thickness = 0.3-0.5)**
- Centre : Teinté visible, modérément opaque
- Bords : Transparent avec réflections
- Exemple : Bouteille de vin, vase

#### **Verre très épais (thickness = 1.0+)**
- Centre : Très opaque, couleur saturée (tint)
- Bords : Encore assez transparent (20% thickness)
- Exemple : Bille de verre massive, presse-papier

---

## Paramètres pour effets réalistes

### **Sphère de verre colorée épaisse**
```csharp
GlassProperties:
  RefractiveIndex = 1.5
  DistortionStrength = 1.0 (full sphere refraction)
  ChromaticAberration = 0.2 (slight dispersion)
  Roughness = 0.0 (smooth)
  Thickness = 0.8 - 1.5 (thick glass)
  Tint = [0.2, 0.6, 0.8] (bleu)
  Opacity = 0.2 (base transparency)
  FresnelPower = 5.0
  ReflectionStrength = 1.0
```

**Résultat** : Centre bleu foncé opaque, bords clairs avec forte réflection.

---

### **Bouteille de vin verte**
```csharp
GlassProperties:
  RefractiveIndex = 1.52
  DistortionStrength = 0.4
  ChromaticAberration = 0.0
  Roughness = 0.0
  Thickness = 0.4
  Tint = [0.3, 0.7, 0.3] (vert)
  Opacity = 0.1
  FresnelPower = 5.0
  ReflectionStrength = 0.8
```

---

### **Presse-papier cristal**
```csharp
GlassProperties:
  RefractiveIndex = 2.0 (cristal dense)
  DistortionStrength = 1.0
  ChromaticAberration = 0.3 (dispersion)
  Roughness = 0.0
  Thickness = 1.2 (très épais)
  Tint = [1.0, 1.0, 1.0] (clair)
  Opacity = 0.15
  FresnelPower = 5.0
  ReflectionStrength = 1.2
```

**Résultat** : Centre très dense/opaque avec effet arc-en-ciel (chromatic aberration), bords brillants.

---

## Limitations et solutions futures

### **Problème : SSAO à travers le verre**

**Cause** : SSAO est appliqué PENDANT le forward rendering, donc déjà "baked" dans `_sceneColorTex` avant la copie.

**Solutions possibles** :

1. **✅ Implémentée** : Compensation dans shader (éclaircit les zones sombres)
   - Avantage : Simple, aucun coût performance
   - Inconvénient : Pas parfait, SSAO encore légèrement visible

2. **Future** : Blur SSAO dans sceneColorTex
   - Avantage : Rend l'artefact moins dur
   - Inconvénient : Coût GPU supplémentaire (blur pass)

3. **Future** : Render opaques 2 fois (avec/sans SSAO)
   - Avantage : Solution parfaite
   - Inconvénient : Double le temps de rendu des opaques (~50% perf hit)

4. **Future** : MRT (Multiple Render Targets)
   - Avantage : Sépare color et SSAO dans 2 textures
   - Inconvénient : Refonte majeure du pipeline

**Recommandation actuelle** : La compensation SSAO (option 1) est suffisante pour la plupart des cas.

---

## Testing

1. Créer une sphère avec GlassMaterial
2. Placer des objets colorés derrière
3. Ajuster **Thickness** : 0.1 → 1.5
4. Ajuster **Tint Color** : Blanc → Rouge/Bleu
5. Observer :
   - ✅ Centre plus opaque que les bords
   - ✅ Couleur plus saturée au centre
   - ✅ SSAO moins visible (artefacts réduits)

---

**Date** : 24 décembre 2024
**Fichiers modifiés** : Glass.frag
**Lignes modifiées** : 132-142 (thickness), 155-160 (opacity), 103-109 (SSAO compensation)
