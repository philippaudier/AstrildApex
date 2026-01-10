# ☁️ Cloud System Refactor 2025 - Advanced Noise & FBM

## 📋 Overview

Le système de nuages a été complètement refactorisé pour offrir des nuages plus réalistes, détaillés et organiques avec:

- **Dual-Layer Scrolling Noise**: Deux couches de noise indépendantes qui scrollent à des vitesses et directions différentes
- **FBM Avancé (Fractal Brownian Motion)**: Contrôle complet sur les octaves, lacunarity, gain et strength pour chaque type de nuage
- **Erosion & Déchirure**: Les nuages peuvent se déchirer et se fragmenter de manière réaliste
- **Morphing Organique**: Animation temporelle fluide avec évolution naturelle des formes
- **Tous les paramètres exposés**: Contrôle total dans l'inspecteur pour tweaker l'apparence

## 🎨 Objectifs Atteints

### ✅ Formes Réalistes
- Cumulonimbus avec détails fins et structure verticale
- Cumulus cotonneux avec edges bien définis
- Cirrus filamenteux et wispy
- Stratus uniformes avec variations subtiles

### ✅ Morphing Visible
- Deux layers de noise scrollant indépendamment
- Distorsion temporelle organique
- Breathing effect (respiration naturelle)
- Évolution continue des formes

### ✅ Détails Fins
- FBM multi-octaves configurable (2-8 octaves)
- Lacunarity et Gain réglables
- Worley/Perlin mixing pour texture variée
- Sharpness control pour edges nets ou soft

### ✅ Déchirure & Erosion
- Paramètre Erosion (0-1) pour créer des trous
- Layer 2 agit comme masque d'érosion
- Breaks réalistes dans les nuages
- Fragments qui se séparent naturellement

## 🎛️ Nouveaux Paramètres

### Dual-Layer Scrolling Noise

#### **Layer 1 (Primary - Large Shapes)**
- **Speed**: Vitesse de scroll (0.0-3.0)
- **Direction**: Direction de scroll en degrés (-180° à 180°)
- **Scale**: Échelle du noise (0.1-5.0) - plus bas = formes plus grandes

#### **Layer 2 (Detail - Fine Erosion)**
- **Speed**: Vitesse de scroll indépendante (0.0-3.0)
- **Direction**: Direction de scroll indépendante (-180° à 180°)
- **Scale**: Échelle du noise détail (0.1-5.0) - plus haut = détails plus fins

**💡 Tip**: Pour un morphing visible:
- Mettez des directions opposées (ex: Layer1=90°, Layer2=-45°)
- Layer2 Speed légèrement plus rapide que Layer1
- Layer2 Scale 2-3x plus élevée que Layer1

### FBM (Fractal Brownian Motion)

#### **Octaves** (2-8)
- Nombre de couches de noise
- Plus d'octaves = plus de détails (mais plus lent)
- **Recommandé**: 4-5 pour bon équilibre

#### **Lacunarity** (1.5-3.0)
- Multiplicateur de fréquence par octave
- **2.0**: Standard, bon équilibre
- **2.5-3.0**: Détails plus rapides, plus chaotiques
- **1.5-1.8**: Transitions plus douces

#### **Gain** (0.3-0.7)
- Multiplicateur d'amplitude par octave
- **0.5**: Standard, balance entre base et détails
- **0.6-0.7**: Détails plus prononcés
- **0.3-0.4**: Base shape domine

#### **Strength** (0.0-1.0)
- Contribution globale du FBM
- **0.0**: Noise simple (rapide mais peu de détails)
- **0.5-0.7**: Bon équilibre (recommandé)
- **1.0**: Maximum de détails fractals

#### **Worley Weight** (0.0-1.0)
- Mix entre Perlin (smooth) et Worley (cellular/billowy)
- **0.0**: 100% Perlin - nuages lisses
- **0.6**: Standard - bon équilibre
- **1.0**: 100% Worley - très billowy/cotonneux

#### **Erosion** (0.0-1.0)
- Force de l'érosion (crée trous/déchirures)
- **0.0**: Nuages solides, pas de trous
- **0.3**: Légère fragmentation (recommandé)
- **0.7-1.0**: Fortement érodé, breaks dramatiques

#### **Sharpness** (0.0-1.0)
- Netteté des edges
- **0.0**: Edges très soft/flous
- **0.5**: Balance entre soft et sharp
- **1.0**: Edges très nets/définis

## 📸 Résultats Attendus

### Cumulonimbus (Type Storm)
Référence: https://www.angleofattack.com/wp-content/uploads/2023/04/Cumulonimbus-Clouds-scaled.jpg

**Réglages Recommandés:**
```
FBM Octaves: 5-6
Lacunarity: 2.2
Gain: 0.55
Strength: 0.8
Worley Weight: 0.5
Erosion: 0.4-0.6
Sharpness: 0.6
Layer1 Speed: 0.8
Layer2 Speed: 1.5
Layer2 Scale: 3.0
```

### Cumulus
Référence: Images fournies (nuages cotonneux)

**Réglages Recommandés:**
```
FBM Octaves: 4
Lacunarity: 2.0
Gain: 0.5
Strength: 0.7
Worley Weight: 0.7 (plus billowy)
Erosion: 0.2-0.3
Sharpness: 0.7
Layer1 Speed: 1.0
Layer2 Speed: 1.5
Layer2 Scale: 2.5
```

### Cirrus
Référence: https://weather.metoffice.gov.uk/binaries/.../cirrus-clouds...

**Réglages Recommandés:**
```
FBM Octaves: 5
Lacunarity: 2.5 (plus de détails fins)
Gain: 0.45
Strength: 0.6
Worley Weight: 0.3 (plus Perlin pour streaks)
Erosion: 0.5 (fragmenté)
Sharpness: 0.8
Layer1 Speed: 1.5
Layer2 Speed: 2.0
Layer2 Scale: 4.0
```

### Stratus
**Réglages Recommandés:**
```
FBM Octaves: 3
Lacunarity: 1.8
Gain: 0.4
Strength: 0.4
Worley Weight: 0.2 (très smooth)
Erosion: 0.1 (presque continu)
Sharpness: 0.3
Layer1 Speed: 0.5
Layer2 Speed: 0.8
Layer2 Scale: 1.5
```

## 🛠️ Utilisation dans l'Inspecteur

### Accéder aux Contrôles

1. Sélectionner un entity avec **WeatherComponent**
2. Dans l'inspector, section **☁️ Clouds**
3. Activer **Enabled**
4. Choisir le **Type** de nuages
5. Ajuster **Coverage** et **Density**

### Section "Advanced Cloud"
- Paramètres de base (Scattering, Ambient, Speed, etc.)

### Section "🌪️ Dual-Layer Scrolling Noise"
- Contrôle indépendant des deux layers de noise
- Tweaker pour obtenir morphing visible

### Section "🔬 FBM (Fractal Brownian Motion)"
- Paramètres avancés du système fractal
- Pour utilisateurs avancés

## 🎯 Workflow Recommandé

### Étape 1: Type de Base
1. Choisir le **Cloud Type** (Cirrus/Cumulus/Stratus/Storm)
2. Ajuster **Coverage** (0.0 = ciel clair, 1.0 = overcast)
3. Ajuster **Density** (0.0 = transparent, 1.0 = opaque)

### Étape 2: Animation Globale
1. **Animation Speed**: Vitesse générale
2. **Morph Speed**: Vitesse du morphing organique
3. **Detail Evolution**: Vitesse d'évolution des détails FBM

### Étape 3: Dual-Layer (Morphing)
1. Ouvrir section **🌪️ Dual-Layer Scrolling Noise**
2. Layer 1: Direction primaire (alignée avec le vent)
3. Layer 2: Direction différente (ex: perpendiculaire)
4. Layer 2 Speed > Layer 1 Speed pour morphing visible
5. Layer 2 Scale 2-3x Layer 1 Scale pour détails fins

### Étape 4: FBM (Détails Fins)
1. Ouvrir section **🔬 FBM**
2. Commencer avec presets ci-dessus
3. Ajuster **Octaves** (plus = plus de détails)
4. Tweaker **Erosion** jusqu'à obtenir déchirures désirées
5. Ajuster **Sharpness** pour edge quality

### Étape 5: Fine-Tuning
- **Edge Softness**: Pour adoucir transitions
- **Billowiness**: Pour aspect cotonneux (Cumulus)
- **Detail Strength**: Pour intensité globale des détails
- **Noise Scale**: Pour taille globale des patterns

## 🔧 Architecture Technique

### Fichiers Modifiés

1. **CloudNoise.glsl**
   - Nouvelle fonction `cloudShapeAdvanced()` avec struct `CloudNoiseParams`
   - Support FBM multi-octaves configurable
   - Système d'érosion intégré

2. **clouds.frag**
   - Dual-layer UV calculation avec scrolling indépendant
   - Distorsion organique sur chaque layer
   - Composition additive/multiplicative selon erosion
   - Nouveaux uniforms pour tous les paramètres

3. **WeatherComponent.cs**
   - 16 nouveaux paramètres sérialisables
   - Helpers `GetNoiseLayer1Direction()` et `GetNoiseLayer2Direction()`

4. **CloudRenderer.cs**
   - Binding de tous les nouveaux uniforms
   - Support des directions normalisées

5. **WeatherInspector.cs**
   - Nouvelles sections UI pour Dual-Layer et FBM
   - Tooltips détaillés pour chaque paramètre
   - Organisation en TreeNodes collapasables

## 🎨 Exemples de Configuration

### Configuration "Realistic Cumulus"
```csharp
// Base
CloudType = CloudType.Cumulus
CloudCoverage = 0.5f
CloudDensity = 0.8f

// Animation
CloudSpeed = 1.0f
MorphSpeed = 0.7f
DetailSpeed = 0.5f

// Dual-Layer
NoiseLayer1Speed = 1.0f
NoiseLayer1Direction = (1.0, 0.5)  // Nord-Est
NoiseLayer1Scale = 1.0f

NoiseLayer2Speed = 1.5f
NoiseLayer2Direction = (-0.5, 1.0) // Nord-Ouest
NoiseLayer2Scale = 2.5f

// FBM
FBMOctaves = 4
FBMLacunarity = 2.0f
FBMGain = 0.5f
FBMStrength = 0.7f
WorleyWeight = 0.7f
Erosion = 0.25f
Sharpness = 0.6f
```

### Configuration "Dramatic Storm"
```csharp
// Base
CloudType = CloudType.Storm
CloudCoverage = 0.9f
CloudDensity = 1.0f

// Animation
CloudSpeed = 1.5f
MorphSpeed = 1.0f
DetailSpeed = 0.8f

// Dual-Layer
NoiseLayer1Speed = 0.8f
NoiseLayer1Direction = (1.0, 0.3)
NoiseLayer1Scale = 0.8f

NoiseLayer2Speed = 1.8f
NoiseLayer2Direction = (-0.7, 0.7)
NoiseLayer2Scale = 3.5f

// FBM
FBMOctaves = 6
FBMLacunarity = 2.2f
FBMGain = 0.55f
FBMStrength = 0.85f
WorleyWeight = 0.5f
Erosion = 0.6f
Sharpness = 0.7f
```

### Configuration "Wispy Cirrus"
```csharp
// Base
CloudType = CloudType.Cirrus
CloudCoverage = 0.3f
CloudDensity = 0.4f

// Animation
CloudSpeed = 1.5f
MorphSpeed = 0.9f
DetailSpeed = 0.7f

// Dual-Layer
NoiseLayer1Speed = 1.5f
NoiseLayer1Direction = (1.0, 0.1)
NoiseLayer1Scale = 1.2f

NoiseLayer2Speed = 2.5f
NoiseLayer2Direction = (0.8, -0.6)
NoiseLayer2Scale = 4.0f

// FBM
FBMOctaves = 5
FBMLacunarity = 2.5f
FBMGain = 0.45f
FBMStrength = 0.6f
WorleyWeight = 0.3f
Erosion = 0.5f
Sharpness = 0.8f
```

## 🐛 Troubleshooting

### Nuages ne bougent pas
- Vérifier que **Cloud Speed** > 0
- Vérifier **Layer1/2 Speed** > 0
- Vérifier **Morph Speed** > 0

### Pas de morphing visible
- Augmenter différence de speed entre Layer1 et Layer2
- Mettre des directions très différentes (ex: opposées)
- Augmenter **Layer2 Scale** pour détails plus fins

### Nuages trop uniformes/plats
- Augmenter **FBM Octaves** (5-6)
- Augmenter **FBM Strength** (0.7-0.9)
- Augmenter **Detail Strength**
- Tweaker **Worley Weight** pour plus de billowiness

### Nuages disparaissent/glitchent
- Vérifier **Coverage** > 0.1
- Vérifier **Density** > 0.1
- Réduire **Erosion** si trop élevé
- Vérifier que **FBM Octaves** est entre 2-8

### Performance faible
- Réduire **FBM Octaves** (3-4)
- Réduire **Detail Strength**
- Simplifier les calculs de distorsion

## 📚 Références

- Images Cumulonimbus: https://www.angleofattack.com/.../Cumulonimbus-Clouds...
- Images Cumulus: URLs fournies par l'utilisateur
- Images Cirrus: https://weather.metoffice.gov.uk/.../cirrus-clouds...
- FBM Theory: "Texturing and Modeling: A Procedural Approach" (Perlin)
- Worley Noise: Steven Worley (1996)

## 🎓 Next Steps

### Améliorations Futures Possibles
- 3D volumetric clouds (ray marching)
- Cloud shadows sur le terrain
- Weather-driven presets (transition automatique)
- Cloud density maps (texture-based)
- Lightning effects pour Storm clouds
- God rays à travers les breaks

## ✅ Validation

Tester avec les configurations ci-dessus et vérifier:
- [x] Morphing visible avec dual-layer
- [x] Détails fins avec FBM
- [x] Déchirures réalistes avec Erosion
- [x] Formes organiques qui évoluent
- [x] Tous paramètres accessibles dans l'inspecteur
- [x] Performance acceptable (60 FPS)

---

**Date de Refactor**: Janvier 2025  
**Version**: 2.0  
**Auteur**: Philippe (avec assistance GitHub Copilot)
