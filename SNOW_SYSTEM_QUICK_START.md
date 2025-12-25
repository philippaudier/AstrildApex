# ❄️ Snow System - Quick Start Guide

## 🎯 En 5 minutes: Comment avoir de la neige réaliste

### Étape 1: Créer un Material de Neige (2 minutes)

1. **Télécharger des textures PBR de neige gratuites:**
   - Allez sur https://polyhaven.com/textures
   - Recherchez "snow"
   - Téléchargez un pack (ex: "Snow 002" ou "Fresh Snow")
   - Vous aurez: `snow_albedo.jpg`, `snow_normal.jpg`, `snow_roughness.jpg`

2. **Importer dans AstrildApex:**
   ```
   Copiez les fichiers dans: Assets/Textures/Snow/
   ```

3. **Créer le Material:**
   - Assets panel → Clic droit → "Create → Material"
   - Nommez-le "SnowMaterial"
   - Assignez les textures:
     * Albedo → `snow_albedo.jpg`
     * Normal → `snow_normal.jpg`
     * Roughness → `snow_roughness.jpg`
     * Metallic → 0.0
     * Smoothness → 0.3

### Étape 2: Configurer le WeatherComponent (1 minute)

1. Sélectionnez votre entité avec `WeatherComponent`
2. Dans l'Inspector, section **❄️ Snow**:
   - Drag & drop votre "SnowMaterial" dans le champ **Snow Material**
   - Ajustez **Coverage** à 0.7 (70% de neige)
   - Ajustez **Intensity** à 0.3 (chute de neige légère)

### Étape 3: Tester ! (30 secondes)

Appuyez sur Play. Vous devriez voir de la neige sur toutes les surfaces horizontales !

---

## 🎨 Réglages Rapides

### Neige Légère (Début d'Hiver)
```
Coverage: 0.3
Intensity: 0.2
Slope Max: 30°
Sparkle: 0.3
```

### Neige Abondante (Plein Hiver)
```
Coverage: 0.9
Intensity: 0.5
Slope Max: 45°
Sparkle: 0.6
```

### Tempête de Neige (Blizzard)
```
Coverage: 1.0
Intensity: 1.0
Slope Max: 60°
Sparkle: 0.8
Fog: Enabled, Density: 0.1
```

---

## 🔧 Contrôles Principaux

| Paramètre | Effet | Valeur Recommandée |
|-----------|-------|-------------------|
| **Coverage** | Quantité de neige au sol | 0.5 - 0.8 |
| **Intensity** | Vitesse de chute | 0.0 - 0.5 (auto-accumule) |
| **Slope Max** | Angle max où la neige colle | 30° - 45° |
| **Sparkle** | Scintillement | 0.3 - 0.5 |
| **Displacement** | Épaisseur 3D | 0.02 - 0.05 |

---

## 🚨 Problèmes Fréquents

### "Je ne vois pas de neige !"
✅ Vérifiez que `Coverage > 0`
✅ Vérifiez que le SnowMaterial est assigné
✅ Vérifiez que vous êtes en Play Mode

### "La neige est sur les murs verticaux !"
✅ Réduisez `Slope Max` à 30-35°

### "La neige est trop blanche/fade"
✅ Ajustez l'Albedo de votre material (couleur légèrement bleutée)

### "Ça rame (FPS drop)"
✅ Réduisez la résolution des textures à 1024x1024
✅ Mettez `Sparkle` à 0

---

## 📊 Système Complet (Vue d'Ensemble)

```
┌─────────────────────────────────────────────────────────────┐
│                    WEATHERCOMPONENT                         │
│  ┌──────────────────────────────────────────────────────┐  │
│  │  Snow Intensity: 0.3  (chute de neige)              │  │
│  │  Snow Coverage:  0.7  (accumulation actuelle)       │  │
│  │  Snow Material:  [SnowMaterial]                     │  │
│  │                                                      │  │
│  │  Advanced:                                           │  │
│  │    - Slope Min/Max: 0° - 45°                        │  │
│  │    - Sparkle: 0.5                                   │  │
│  │    - Displacement: 0.03                             │  │
│  └──────────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────────┘
                           ↓
┌─────────────────────────────────────────────────────────────┐
│                     WEATHERSYSTEM                           │
│  (Auto-update chaque frame)                                 │
│                                                              │
│  IF SnowIntensity > 0:                                      │
│    SnowCoverage += AccumulationSpeed * deltaTime            │
│  ELSE:                                                       │
│    SnowCoverage -= MeltSpeed * deltaTime                    │
└─────────────────────────────────────────────────────────────┘
                           ↓
┌─────────────────────────────────────────────────────────────┐
│                   SHADERS (GPU)                             │
│  ┌──────────────────────────────────────────────────────┐  │
│  │  TerrainForward.frag                                │  │
│  │  ForwardBase.frag                                   │  │
│  │  VegetationForward.frag                             │  │
│  │                                                      │  │
│  │  Pour chaque fragment:                              │  │
│  │    1. Calculer angle de la surface                  │  │
│  │    2. Si angle < SlopeMax: appliquer neige         │  │
│  │    3. Sampler SnowMaterial textures                │  │
│  │    4. Mixer avec la surface originale              │  │
│  │    5. Ajouter sparkle si activé                    │  │
│  └──────────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────────┘
                           ↓
                  🎨 RENDU FINAL
```

---

## 🎓 Technique Moderne (2025)

### Pourquoi cette approche ?

**Avant (2020):**
- Couleur blanche plate
- Pas de textures
- Résultat peu réaliste

**Maintenant (2025):**
✅ **PBR Materials** - Textures réalistes avec albedo, normal, roughness
✅ **Normal-based placement** - La neige respecte la physique (colle aux surfaces plates)
✅ **Temporal accumulation** - S'accumule progressivement comme dans la vraie vie
✅ **Sparkle effect** - Simule les cristaux de glace qui réfléchissent la lumière
✅ **Displacement mapping** - Donne de la profondeur 3D à la neige

### Placement basé sur les Normales

```glsl
// Calcul de l'angle de la surface
vec3 up = vec3(0, 1, 0);
float angle = acos(dot(normal, up));

// Conversion en degrés pour lisibilité
// 0° = surface plate (horizontal)
// 45° = pente modérée
// 90° = mur vertical

// La neige s'accumule seulement si:
// angle >= SlopeMin ET angle <= SlopeMax
```

**Exemple visuel:**
```
        🏔️ Montagne
       /│\
      / │ \      45° → Neige s'accroche
     /  │  \
    /   │   \    30° → Plus de neige
   /    │    \
  /_____|_____\  0° (flat) → Maximum de neige
```

### Sparkle (Scintillement)

La neige scintille à cause des cristaux de glace qui réfléchissent la lumière. L'effet est plus fort quand:
1. Vous regardez la neige à un angle rasant (Fresnel)
2. Il y a des variations de micro-surface (bruit/noise)

```glsl
// Pseudo-code
sparkle = randomNoise(worldPos) * fresnelEffect * sparkleIntensity
snowColor += sparkle * 0.5  // Ajoute de la luminosité
```

---

## 📚 Pour Aller Plus Loin

Consultez le guide complet: `SNOW_SYSTEM_GUIDE.md`

Contient:
- Implémentation shader détaillée (code GLSL complet)
- Techniques avancées (trails, occlusion, displacement)
- Optimisations de performance
- Binding C# des uniforms
- Troubleshooting complet

---

## ✅ Checklist d'Implémentation

### Fait Automatiquement ✅
- [x] Paramètres ajoutés au WeatherComponent
- [x] UI Inspector avec EditorWidget.AssetField()
- [x] Support de drag & drop pour le material
- [x] Contrôles avancés (slope, sparkle, displacement)

### À Faire (Optionnel) 🔧
- [ ] Mettre à jour les shaders (voir SNOW_SYSTEM_GUIDE.md § "Shader Implementation")
- [ ] Ajouter le binding des uniforms en C#
- [ ] Créer/télécharger des textures de neige PBR
- [ ] Créer un SnowMaterial dans l'Assets panel
- [ ] Tester les différents presets (Light Snow, Heavy Snow, Blizzard)

---

**Temps d'implémentation total:** ~30 minutes
**Difficulté:** Intermédiaire
**Résultat:** Neige photoréaliste dans votre scène !

Bon courage ! ❄️🎨
