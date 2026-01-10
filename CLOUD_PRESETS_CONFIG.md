# ☁️ Cloud System - Preset Configurations

## 🎯 Configurations Optimales par Type

### CIRRUS (Type 0) - Nuages Hauts, Fins et Wispy

**Apparence**: Filaments fins et déchirés en haute altitude  
**Référence**: https://weather.metoffice.gov.uk/.../cirrus-clouds.jpg

```csharp
// Base Parameters
CloudType = CloudType.Cirrus
CloudCoverage = 0.15f - 0.35f
CloudDensity = 0.3f - 0.5f
CloudScattering = 0.7f
CloudAmbient = 0.4f

// Animation
CloudSpeed = 1.5f
CloudMorphSpeed = 0.9f
CloudDetailSpeed = 0.7f
CloudNoiseScale = 1.2f

// Dual-Layer Scrolling
NoiseLayer1Speed = 1.5f
NoiseLayer1DirectionX = 1.0f
NoiseLayer1DirectionY = 0.1f
NoiseLayer1Scale = 1.2f

NoiseLayer2Speed = 2.5f
NoiseLayer2DirectionX = 0.8f
NoiseLayer2DirectionY = -0.6f
NoiseLayer2Scale = 4.0f

// FBM Configuration
FBMOctaves = 5
FBMLacunarity = 2.5f  // High pour détails fins
FBMGain = 0.45f
FBMStrength = 0.6f
WorleyWeight = 0.3f   // Plus Perlin pour streaks
Erosion = 0.5f        // Très fragmenté
Sharpness = 0.8f      // Edges nets

// Fine-Tune
CloudEdgeSoftness = 0.3f
CloudBillowiness = 0.2f
CloudDetailStrength = 0.7f
```

**Caractéristiques Clés**:
- Très fragmenté (Erosion élevée)
- Détails fins (Layer2 Scale élevée)
- Rapide (speeds élevés)
- Edges nets (Sharpness haute)

---

### CUMULUS (Type 1) - Nuages Cotonneux et Puffy

**Apparence**: Nuages blancs cotonneux avec edges bien définis  
**Référence**: Images fournies (cumulus clouds)

```csharp
// Base Parameters
CloudType = CloudType.Cumulus
CloudCoverage = 0.4f - 0.6f
CloudDensity = 0.75f - 0.85f
CloudScattering = 0.6f
CloudAmbient = 0.3f

// Animation
CloudSpeed = 1.0f
CloudMorphSpeed = 0.7f
CloudDetailSpeed = 0.5f
CloudNoiseScale = 1.0f

// Dual-Layer Scrolling
NoiseLayer1Speed = 1.0f
NoiseLayer1DirectionX = 1.0f
NoiseLayer1DirectionY = 0.5f
NoiseLayer1Scale = 1.0f

NoiseLayer2Speed = 1.5f
NoiseLayer2DirectionX = -0.5f
NoiseLayer2DirectionY = 1.0f
NoiseLayer2Scale = 2.5f

// FBM Configuration
FBMOctaves = 4
FBMLacunarity = 2.0f  // Standard
FBMGain = 0.5f
FBMStrength = 0.7f
WorleyWeight = 0.7f   // High pour billowy appearance
Erosion = 0.25f       // Légère fragmentation
Sharpness = 0.6f      // Edges définis mais pas trop sharp

// Fine-Tune
CloudEdgeSoftness = 0.4f
CloudBillowiness = 0.7f  // Maximum pour cotton look
CloudDetailStrength = 0.6f
```

**Caractéristiques Clés**:
- Très billowy (Worley Weight élevé)
- Erosion modérée (breaks naturels)
- Balance entre smooth et defined
- Vitesses modérées pour aspect stable

---

### STRATUS (Type 2) - Couche Uniforme et Continue

**Apparence**: Couche grise uniforme, peu de variation

```csharp
// Base Parameters
CloudType = CloudType.Stratus
CloudCoverage = 0.7f - 0.9f
CloudDensity = 0.6f - 0.8f
CloudScattering = 0.4f
CloudAmbient = 0.25f

// Animation
CloudSpeed = 0.5f     // Lent
CloudMorphSpeed = 0.3f
CloudDetailSpeed = 0.3f
CloudNoiseScale = 0.8f

// Dual-Layer Scrolling
NoiseLayer1Speed = 0.5f
NoiseLayer1DirectionX = 1.0f
NoiseLayer1DirectionY = 0.2f
NoiseLayer1Scale = 0.8f

NoiseLayer2Speed = 0.8f
NoiseLayer2DirectionX = 0.5f
NoiseLayer2DirectionY = 0.8f
NoiseLayer2Scale = 1.5f

// FBM Configuration
FBMOctaves = 3        // Peu de détails
FBMLacunarity = 1.8f  // Transitions douces
FBMGain = 0.4f
FBMStrength = 0.4f
WorleyWeight = 0.2f   // Très smooth (Perlin dominant)
Erosion = 0.1f        // Presque continu
Sharpness = 0.3f      // Edges très soft

// Fine-Tune
CloudEdgeSoftness = 0.7f  // Maximum softness
CloudBillowiness = 0.2f
CloudDetailStrength = 0.3f
```

**Caractéristiques Clés**:
- Très continu (Erosion minimale)
- Edges très soft
- Peu de détails (octaves bas)
- Vitesses lentes pour stabilité

---

### STORM / CUMULONIMBUS (Type 3) - Nuages d'Orage Dramatiques

**Apparence**: Énormes structures verticales avec tops anvil-shaped  
**Référence**: https://www.angleofattack.com/.../Cumulonimbus-Clouds-scaled.jpg

```csharp
// Base Parameters
CloudType = CloudType.Storm
CloudCoverage = 0.85f - 0.95f
CloudDensity = 0.9f - 1.0f
CloudScattering = 0.3f  // Peu de scattering (sombre)
CloudAmbient = 0.2f

// Animation
CloudSpeed = 1.5f      // Rapide et chaotique
CloudMorphSpeed = 1.0f
CloudDetailSpeed = 0.8f
CloudNoiseScale = 0.9f

// Dual-Layer Scrolling
NoiseLayer1Speed = 0.8f
NoiseLayer1DirectionX = 1.0f
NoiseLayer1DirectionY = 0.3f
NoiseLayer1Scale = 0.8f

NoiseLayer2Speed = 1.8f  // Beaucoup plus rapide
NoiseLayer2DirectionX = -0.7f
NoiseLayer2DirectionY = 0.7f
NoiseLayer2Scale = 3.5f  // Détails très fins

// FBM Configuration
FBMOctaves = 6        // Maximum de détails
FBMLacunarity = 2.2f
FBMGain = 0.55f
FBMStrength = 0.85f   // Très strong
WorleyWeight = 0.5f   // Balance
Erosion = 0.6f        // Fortement déchiré
Sharpness = 0.7f      // Edges nets et dramatiques

// Fine-Tune
CloudEdgeSoftness = 0.3f
CloudBillowiness = 0.5f
CloudDetailStrength = 0.8f
```

**Caractéristiques Clés**:
- Maximum de détails (octaves élevés)
- Fortement érodé (breaks dramatiques)
- Vitesses rapides et chaotiques
- Très dense et sombre

---

## 🎨 Presets Spéciaux

### CUMULUS SUNSET (Golden Hour)

**Usage**: Cumulus au coucher du soleil avec couleurs chaudes

```csharp
// Base Cumulus settings +
CloudScattering = 0.8f  // Plus de scattering pour golden light
CloudAmbient = 0.4f     // Plus d'ambient pour warm glow
CloudDensity = 0.7f     // Légèrement transparent pour let light through

// Colors viennent du système de lighting (Golden Hour Blend)
```

### CIRRUS MORNING (Matin Clair)

**Usage**: Cirrus légers au lever du soleil

```csharp
// Base Cirrus settings +
CloudCoverage = 0.15f   // Très peu de couverture
CloudDensity = 0.3f     // Très transparent
Erosion = 0.6f          // Très fragmenté
Layer2Speed = 3.0f      // Très rapide (wind swept)
```

### STORM BUILDUP (Orage qui Approche)

**Usage**: Transition entre Cumulus et Storm

```csharp
CloudType = CloudType.Cumulus  // Start as Cumulus
CloudCoverage = 0.7f   // Increasing
CloudDensity = 0.85f
FBMOctaves = 5
Erosion = 0.4f
Sharpness = 0.65f

// Transition graduelle vers Storm avec TransitionToPreset()
```

---

## 🔧 Formules de Calcul

### Vitesse de Scroll Effective
```
EffectiveSpeed = WindSpeed × CloudSpeed × LayerSpeed × 0.1
```

### Coverage Remapping
```
threshold = 1.0 - coverage
remapped = smoothstep(threshold - 0.3, threshold + 0.1, rawNoise)
```

### Dual-Layer Composition
```
if (erosion < 0.5):
    result = primary × (0.7 + secondary × 0.3)  // Additive
else:
    result = primary × secondary  // Multiplicative (erosion)
```

---

## 📊 Tableau Comparatif

| Type | Octaves | Lacunarity | Worley | Erosion | Sharpness | Layer2 Scale |
|------|---------|------------|--------|---------|-----------|--------------|
| Cirrus | 5 | 2.5 | 0.3 | 0.5 | 0.8 | 4.0 |
| Cumulus | 4 | 2.0 | 0.7 | 0.25 | 0.6 | 2.5 |
| Stratus | 3 | 1.8 | 0.2 | 0.1 | 0.3 | 1.5 |
| Storm | 6 | 2.2 | 0.5 | 0.6 | 0.7 | 3.5 |

---

## 🎯 Performance Guidelines

### Low-End (30 FPS target)
```
FBMOctaves = 3
Layer2Scale = 2.0
CloudDetailStrength = 0.4
```

### Mid-Range (60 FPS target)
```
FBMOctaves = 4
Layer2Scale = 2.5
CloudDetailStrength = 0.6
```

### High-End (60+ FPS target)
```
FBMOctaves = 6
Layer2Scale = 4.0
CloudDetailStrength = 0.8
```

---

**Date**: Janvier 2025  
**Version**: 2.0
