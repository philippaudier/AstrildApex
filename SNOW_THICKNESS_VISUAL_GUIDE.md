# Guide : Épaisseur Visuelle de Neige

## Résumé des changements

✅ **Vertex Displacement** ajouté aux shaders `TerrainForward.vert` et `ForwardBase.vert`
✅ **Normal Smoothing** pour bords arrondis (effet "cushion")
✅ **Valeur par défaut** de `SnowDisplacement` augmentée : **0.02m → 0.5m** (2cm → 50cm)

---

## Comment ça fonctionne

### 1. Vertex Displacement (épaisseur 3D)
```glsl
// Dans le vertex shader
float displacementAmount = (1.0 - exp(-snowAmount * 0.8)) * u_SnowDisplacement;
worldPos.y += displacementAmount; // Soulève les vertices vers le haut
```

**Résultat** :
- Les surfaces horizontales se soulèvent (toit, sol)
- Les surfaces verticales ne bougent pas (pentes >45°)
- Courbe exponentielle = accumulation naturelle (plus tu mets de neige, plus ça monte)

### 2. Normal Smoothing (bords arrondis)
```glsl
float normalSmoothFactor = clamp(snowAmount * 0.3, 0.0, 0.7);
vec3 smoothedNormal = mix(worldNormal, vec3(0, 1, 0), normalSmoothFactor);
```

**Résultat** :
- Les normales se "lissent" vers le haut (Y+)
- Crée l'effet arrondi comme dans les photos
- Maximum 70% de blend pour garder un peu de relief

---

## Paramètres à ajuster dans l'Inspector

### **SnowDisplacement** (hauteur max de déplacement)
- **Par défaut** : `0.5` (50cm de neige épaisse)
- **Petit saupoudrage** : `0.1` (10cm)
- **Grosse tempête** : `1.0 - 2.0` (1-2 mètres !)

### **SnowAccumulation** (quantité accumulée)
- Contrôle le **pourcentage de displacement**
- `0.0` = pas de neige
- `1.0` = 100% du displacement (ex: 50cm si SnowDisplacement=0.5)
- `2.0` = 200% (ex: 100cm si SnowDisplacement=0.5)

**Formule** :
```
Hauteur réelle = (1 - exp(-SnowAccumulation * 0.8)) * SnowDisplacement
```

### Exemples :
| Accumulation | Displacement | Hauteur réelle | Effet visuel |
|--------------|--------------|----------------|--------------|
| 0.5 | 0.5m | ~16cm | Légère couche |
| 1.0 | 0.5m | ~28cm | Bonne couche |
| 2.0 | 0.5m | ~38cm | Très épais |
| 5.0 | 0.5m | ~49cm | Presque max |
| 1.0 | 1.0m | ~56cm | Tempête |
| 2.0 | 2.0m | ~1.5m | Blizzard ! |

---

## Vitesse d'accumulation

### **SnowAccumulationSpeed** (vitesse d'ajout)
- Par défaut : `0.2` (20% par seconde)
- Rapide : `0.5` (50% par seconde)
- Très rapide : `1.0` (100% par seconde)

**Exemple** : Avec `SnowIntensity = 1.0` et `SnowAccumulationSpeed = 0.2` :
- Après 5 secondes : Accumulation = 1.0 (28cm si Displacement=0.5m)
- Après 10 secondes : Accumulation = 2.0 (38cm)
- Après 20 secondes : Accumulation = 4.0 (47cm)

### **SnowMeltSpeed** (vitesse de fonte)
- Par défaut : `0.1` (lerp 10% vers 0 par seconde)
- Rapide : `0.3`
- Très rapide : `1.0` (fonte instantanée)

---

## Pour obtenir l'effet des photos

### Photo 1 : Toit enneigé (30-40cm)
```csharp
WeatherComponent:
  SnowIntensity = 1.0
  SnowAccumulation = 1.5 - 2.0
  SnowDisplacement = 0.5
  SnowSlopeMin = 0°
  SnowSlopeMax = 35° (la neige glisse sur les toits raides)
```

### Photo 2 : Neige au sol épaisse (20-30cm)
```csharp
WeatherComponent:
  SnowIntensity = 1.0
  SnowAccumulation = 1.0 - 1.5
  SnowDisplacement = 0.4
  SnowSlopeMin = 0°
  SnowSlopeMax = 45°
```

---

## Astuces visuelles

### 1. Augmenter le SnowNormalStrength (bords plus arrondis)
```csharp
SnowNormalStrength = 2.0 - 3.0  // Normal map plus prononcée
```

### 2. Ajuster SnowSparkle (scintillement)
```csharp
SnowSparkle = 0.8 - 1.0  // Plus de brillance sur neige épaisse
```

### 3. Créer une fonte progressive
```csharp
// Dans l'inspector, ou via code :
weather.SnowIntensity = 0.0f;  // Arrête la neige
weather.SnowMeltSpeed = 0.05f; // Fonte lente (20 sec pour fondre)
```

---

## Debug

Si la neige ne se soulève pas :
1. Vérifier que `SnowDisplacement > 0.1`
2. Vérifier que `SnowAccumulation > 0.5`
3. Vérifier dans l'inspector que la surface a une pente < 45°

Si les bords ne sont pas arrondis :
1. C'est normal si `SnowAccumulation < 1.0` (peu de neige)
2. Augmenter l'accumulation à 2.0+ pour voir l'effet
3. Vérifier que le mesh a assez de subdivisions (low-poly = angles durs)

---

## Prochaines étapes (optionnel)

Pour aller encore plus loin :
- **Tessellation** : Subdiviser les triangles en temps réel pour un relief ultra-détaillé
- **Parallax Occlusion Mapping** : Ajouter du détail de surface (traces, crêtes)
- **Snow Trail Map** : Texture 2D persistante pour traces de pas (voir Option 3 précédente)

---

**Enjoy your thick snow! ❄️🎿**
