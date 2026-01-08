# Système de Fog Amélioré avec Depth Buffer

## 🌫️ Vue d'ensemble

Le système de fog a été considérablement amélioré pour donner une vraie impression de relief en utilisant le depth buffer. Le fog remplit maintenant les creux et vallées, créant un effet atmosphérique réaliste où la brume s'accumule naturellement dans les dépressions du terrain.

## ✨ Nouvelles Fonctionnalités

### 1. **Valley Detection (Détection de Vallées)**
Le système analyse le depth buffer local pour détecter automatiquement les zones en creux :
- Échantillonnage 5x5 autour de chaque pixel
- Calcul de la profondeur relative par rapport aux pixels environnants
- Plus de fog dans les dépressions, moins sur les sommets

### 2. **Relative Height Analysis (Analyse de Hauteur Relative)**
- Calcule la position verticale par rapport à la géométrie environnante
- Détecte si un point est dans une vallée (négatif) ou sur un pic (positif)
- Boost exponentiel du fog pour les zones basses : jusqu'à 3x plus dense

### 3. **Height-Based Accumulation (Accumulation Basée sur la Hauteur)**
- Le fog est plus dense en-dessous de `BaseHeight`
- Diminue exponentiellement avec l'altitude
- Boost additionnel pour les zones très basses (cavernes, vallées profondes)

### 4. **Forward Rendering Enhancement**
Le fog du forward rendering (`Fog.glsl`) a également été amélioré :
- Calcul de hauteur basique pour cohérence visuelle
- Facteur de falloff exponentiel avec l'altitude
- 50% de densité supplémentaire au niveau du sol

## 🎮 Utilisation

### Activation du Fog Volumétrique

1. **Ajouter un WeatherComponent** à votre scène :
   ```
   Add Component → Environment → Weather
   ```

2. **Activer le Fog** dans le Weather Component :
   - `FogEnabled = true`
   - `FogDensity = 0.1` (ajuster au goût)
   - `FogColor = (0.7, 0.7, 0.8)` (gris-bleu typique)
   - `FogStart = 10.0`
   - `FogEnd = 300.0`

3. **Ajouter un VolumetricFogEffect** (Post-Process) :
   - Ouvrir le panneau Global Effects
   - Ajouter "Volumetric Fog"
   
4. **Configurer les paramètres** :

### Paramètres Principaux

#### Fog de Base
- **Source** : `Global` (utilise WeatherComponent), `Local`, ou `Blend`
- **UseExponential** : `true` pour fog exponentiel (plus naturel)
- **Density** : `0.01 - 0.1` (contrôle l'épaisseur)
- **DepthStart/End** : `0 - 500` (distance du fog)

#### Height-Based Fog (Relief)
- **UseHeightFog** : `true` ⭐ **ACTIVER POUR L'EFFET DE RELIEF**
- **BaseHeight** : `0.0` (altitude où le fog est le plus dense)
- **MaxHeight** : `100.0` (altitude où le fog disparaît)
- **HeightFalloff** : `0.1` (vitesse de dissipation avec l'altitude)

#### Scattering & Atmosphère
- **UseSunScattering** : `true` (diffusion de lumière solaire)
- **ScatteringIntensity** : `0.5` (intensité de la diffusion)
- **SunScatteringColor** : `(1.0, 0.9, 0.7)` (teinte dorée)

#### Noise (Détails)
- **UseNoise** : `true` (ajoute des variations)
- **NoiseScale** : `0.05` (taille des variations)
- **NoiseStrength** : `0.2` (intensité)

## 🎨 Exemples de Configuration

### Fog de Vallée Matinale
```
UseHeightFog: true
BaseHeight: -5.0
HeightFalloff: 0.15
Density: 0.05
FogColor: (0.8, 0.85, 0.9) // Blanc-bleuté
UseSunScattering: true
```

### Brume Épaisse de Marais
```
UseHeightFog: true
BaseHeight: 0.0
HeightFalloff: 0.2
Density: 0.08
FogColor: (0.6, 0.65, 0.6) // Verdâtre
UseNoise: true
NoiseStrength: 0.3
```

### Fog Atmosphérique Léger
```
UseHeightFog: true
BaseHeight: 10.0
HeightFalloff: 0.05
Density: 0.02
FogColor: (0.7, 0.75, 0.85) // Bleu clair
UseExponential: true
```

### Fog de Montagne (Creux Profonds)
```
UseHeightFog: true
BaseHeight: -10.0
HeightFalloff: 0.1
Density: 0.06
FogColor: (0.65, 0.7, 0.8)
UseSunScattering: true
ScatteringIntensity: 0.7
```

## 🔧 Détails Techniques

### Algorithme de Détection de Vallées

```glsl
// 1. Échantillonnage du depth buffer (5x5)
for (int x = -2; x <= 2; x++) {
    for (int y = -2; y <= 2; y++) {
        float sampleDepth = texture(depthBuffer, uv + offset);
        
        // 2. Si les pixels voisins sont plus proches (depth < center)
        //    => Ce pixel est dans un creux
        if (sampleDepth < centerDepth) {
            valleyFactor += abs(depthDiff);
        }
    }
}

// 3. Amplification x50 pour visibilité
valleyFactor = clamp(valleyFactor * 50.0, 0.0, 1.0);
```

### Calcul de Hauteur Relative

```glsl
// 1. Reconstruire les positions world des pixels voisins
vec3 avgWorldPos = sampleNeighborPositions();

// 2. Comparer avec position actuelle
float relativeHeight = avgWorldPos.y - currentPos.y;

// 3. Si négatif => dans une vallée => boost fog
if (relativeHeight < 0.0) {
    depressionBoost = 1.0 + abs(relativeHeight) * 0.5;
    // Clamp entre 1.0 et 3.0 (jusqu'à 3x plus de fog)
}
```

### Performance

L'algorithme de détection de vallées nécessite :
- **24 samples** du depth buffer par pixel (5x5 - centre)
- **8 samples** pour la hauteur relative (coins de la grille 3x3)
- **Coût** : ~30 texture reads par pixel

**Optimisations possibles** :
- Réduire le rayon d'échantillonnage (3x3 au lieu de 5x5)
- Downsampler le depth buffer pour l'analyse
- Cacher les résultats dans un buffer temporaire

## 📊 Comparaison Avant/Après

### Avant (Fog Simple)
- Distance-based uniquement
- Fog uniforme à toutes les altitudes
- Pas de distinction terrain/creux

### Après (Fog avec Depth Buffer)
- ✅ Detection automatique des vallées
- ✅ Accumulation dans les creux
- ✅ Relief visible et naturel
- ✅ Fog s'adapte à la topologie
- ✅ Cohérence forward + post-process

## 🐛 Troubleshooting

### Le fog ne remplit pas les creux
- Vérifiez que `UseHeightFog = true`
- Augmentez `HeightFalloff` (0.1 → 0.2)
- Diminuez `BaseHeight` si votre terrain est bas
- Vérifiez que le depth buffer est bien bindé

### Trop de fog partout
- Diminuez `Density` (0.1 → 0.03)
- Augmentez `BaseHeight` pour réserver le fog aux zones basses
- Réduisez `HeightFalloff` pour une transition plus douce

### Artefacts/scintillement
- Peut être causé par le noise : réduire `NoiseStrength`
- Augmenter `NoiseScale` pour des variations plus douces
- Vérifier la précision du depth buffer (24-bit minimum)

### Performance faible
- Désactiver temporairement `UseNoise`
- Utiliser fog exponentiel (`UseExponential = true`) plutôt que linéaire
- Réduire la résolution du post-process si possible

## 🚀 Améliorations Futures Possibles

1. **Multi-layer Fog** : Plusieurs couches de fog à différentes altitudes
2. **Wind Animation** : Déplacement directionnel du fog
3. **Color Gradient** : Variation de couleur selon la hauteur
4. **Distance-based Quality** : LOD pour l'échantillonnage
5. **Temporal Stability** : Anti-aliasing temporel pour réduire le scintillement
6. **Dynamic Weather Integration** : Transition douce entre états météo

## 📝 Notes

- Le système utilise le **depth buffer** du main render pass
- Compatible avec le **deferred rendering** et **forward rendering**
- S'intègre avec le **WeatherComponent** pour cohérence globale
- Les deux systèmes (forward + post-process) sont maintenant harmonisés

---

**Créé le** : 8 janvier 2026
**Version** : 1.0
**Auteur** : Philippe / GitHub Copilot
