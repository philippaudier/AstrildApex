# Shore Foam System - Guide Complet

## Vue d'ensemble

Le système de Shore Foam (écume du rivage) ajoute un effet réaliste d'écume blanche dans les zones d'eau peu profonde, près des plages et des rivages. Cette écume apparaît naturellement dans les zones où l'eau rencontre le terrain.

## Caractéristiques principales

- **Écume basée sur la profondeur**: Apparaît automatiquement dans l'eau peu profonde
- **Texture ou procédural**: Utilise la texture de foam si assignée, sinon génère une écume procédurale
- **Animation réaliste**: Double couche de défilement avec différentes vitesses
- **Contrôle artistique**: Tous les paramètres exposés dans l'inspecteur
- **Intégration seamless**: Se combine avec la crest foam existante

## Architecture

### Composants modifiés

1. **WaterPlaneComponent.cs**: 8 nouveaux paramètres shore foam
2. **WaterOcean.frag**: Fonction `calculateShoreFoam()` et uniforms
3. **WaterPlaneRenderer.cs**: Bindings des uniforms shore foam
4. **WaterPlaneInspector.cs**: Section UI "Shore Foam" collapsable

### Fichiers impactés

```
Engine/
  Components/WaterPlaneComponent.cs         (8 propriétés ajoutées)
  Rendering/WaterPlaneRenderer.cs           (8 bindings ajoutés)
  Rendering/Shaders/Forward/WaterOcean.frag (fonction calculateShoreFoam)
  
Editor/
  Inspector/WaterPlaneInspector.cs          (section UI Shore Foam)
```

## Paramètres

### Enable Shore Foam
- **Type**: Checkbox
- **Description**: Active/désactive le système de shore foam
- **Défaut**: `false`

### Shore Foam Depth
- **Type**: Float (0.1 - 10.0)
- **Défaut**: `2.0`
- **Description**: Profondeur maximale en mètres où la shore foam apparaît
- **Usage**: Augmentez pour avoir de l'écume plus loin du rivage

### Shore Foam Intensity
- **Type**: Float (0.0 - 3.0)
- **Défaut**: `1.5`
- **Description**: Intensité globale de l'écume
- **Usage**: 
  - 0.0 = invisible
  - 1.0 = intensité normale
  - 2.0+ = écume très visible

### Shore Foam Color
- **Type**: Vector4 (RGBA)
- **Défaut**: `(1.0, 1.0, 1.0, 1.0)` - blanc pur
- **Description**: Couleur de l'écume avec transparence
- **Usage**: Modifiez l'alpha pour fondre l'écume avec l'eau

### Shore Foam Scale
- **Type**: Float (0.1 - 50.0)
- **Défaut**: `8.0`
- **Description**: Échelle de la texture/pattern de foam
- **Usage**: 
  - Petites valeurs = foam fine et détaillée
  - Grandes valeurs = foam large et éparse

### Shore Foam Speed
- **Type**: Float (0.0 - 0.5)
- **Défaut**: `0.05`
- **Description**: Vitesse d'animation de l'écume
- **Usage**: Ralentissez pour une écume plus stable, accélérez pour plus de mouvement

### Shore Foam Fade
- **Type**: Float (0.1 - 2.0)
- **Défaut**: `0.5`
- **Description**: Courbe de fade basée sur la profondeur
- **Usage**:
  - < 1.0 = transition douce et progressive
  - 1.0 = transition linéaire
  - > 1.0 = transition abrupte et nette

### Edge Sharpness
- **Type**: Float (0.5 - 5.0)
- **Défaut**: `2.0`
- **Description**: Accentue l'écume dans les zones très peu profondes
- **Usage**: Augmentez pour un bord de plage plus marqué

## Algorithme technique

### Fonction calculateShoreFoam()

```glsl
vec3 calculateShoreFoam(float waterDepth, vec2 worldPos, vec2 uv)
```

#### 1. Test de profondeur
```glsl
if (u_ShoreFoamEnabled == 0 || waterDepth > u_ShoreFoamDepth) 
    return vec3(0.0);
```
Si désactivé ou trop profond, pas d'écume.

#### 2. Calcul du facteur de profondeur
```glsl
float depthFactor = 1.0 - saturate(waterDepth / u_ShoreFoamDepth);
depthFactor = pow(depthFactor, u_ShoreFoamFade);
depthFactor = pow(depthFactor, 1.0 / u_ShoreFoamEdgeSharpness);
```
- Fade de 100% à profondeur=0 vers 0% à profondeur=max
- Application de courbes pour contrôle artistique

#### 3. UVs animés (dual-layer)
```glsl
vec2 foamUV1 = worldPos * u_ShoreFoamScale + u_Time * u_ShoreFoamSpeed * vec2(0.3, 0.7);
vec2 foamUV2 = worldPos * u_ShoreFoamScale * 1.3 + u_Time * u_ShoreFoamSpeed * vec2(-0.5, 0.4);
```
Deux couches avec directions différentes pour organicité.

#### 4. Pattern de foam

**Avec texture:**
```glsl
float foam1 = texture(u_FoamTex, foamUV1).r;
float foam2 = texture(u_FoamTex, foamUV2).r;
foamPattern = foam1 * 0.6 + foam2 * 0.4;
```

**Procédural (fallback):**
- Layer 1: Larges patches de foam (hash noise + 3 octaves)
- Layer 2: Détails fins (hash noise + 2 octaves)
- Combinaison: `foamNoise1 * 0.6 + foamNoise2 * 0.4`
- Influence des vagues: modulation sinusoïdale basée sur worldPos

#### 5. Beach edge enhancement
```glsl
float beachEdge = smoothstep(0.5, 0.0, waterDepth);
foamPattern = mix(foamPattern, 1.0, beachEdge * 0.5);
```
Augmente l'écume dans les zones très peu profondes (<0.5m).

#### 6. Résultat final
```glsl
return u_ShoreFoamColor.rgb * depthFactor * u_ShoreFoamIntensity * foamPattern * u_ShoreFoamColor.a;
```

### Intégration dans le shader principal

```glsl
// Calcul
vec3 shoreFoam = calculateShoreFoam(depthDiff, vWorldPos.xz, vUV);

// Application additive après crest foam
float shoreFoamMask = length(shoreFoam);
finalColor = mix(finalColor, finalColor + shoreFoam, saturate(shoreFoamMask));
```

## Utilisation dans l'inspecteur

### Activation rapide

1. Sélectionnez votre WaterPlane dans la scène
2. Dans l'inspecteur, trouvez la section **"Shore Foam"**
3. Cochez **"Enable Shore Foam"**
4. L'écume apparaît avec les valeurs par défaut

### Tweaking artistique

#### Pour une plage tropicale claire:
```
Shore Foam Depth: 3.0
Shore Foam Intensity: 1.8
Shore Foam Color: (1.0, 1.0, 0.95, 0.9) - blanc légèrement chaud
Shore Foam Scale: 6.0
Shore Foam Speed: 0.03
Shore Foam Fade: 0.4 - transition douce
Edge Sharpness: 1.5
```

#### Pour des vagues agitées sur rochers:
```
Shore Foam Depth: 1.5
Shore Foam Intensity: 2.5
Shore Foam Color: (1.0, 1.0, 1.0, 1.0)
Shore Foam Scale: 12.0
Shore Foam Speed: 0.08
Shore Foam Fade: 0.8
Edge Sharpness: 3.0 - bords nets
```

#### Pour un lac calme:
```
Shore Foam Depth: 1.0
Shore Foam Intensity: 0.8
Shore Foam Color: (0.95, 0.98, 1.0, 0.7) - légèrement bleuté
Shore Foam Scale: 4.0
Shore Foam Speed: 0.02
Shore Foam Fade: 0.3 - très doux
Edge Sharpness: 1.0
```

## Texture de foam

### Utilisation de texture
La shore foam utilise automatiquement la **même texture** que la crest foam si assignée dans la section "Crest Foam" → "Foam Texture".

#### Spécifications recommandées:
- **Format**: Texture R (grayscale) ou RGB
- **Résolution**: 512x512 ou 1024x1024
- **Contenu**: Pattern d'écume organique, haute fréquence
- **Canal utilisé**: Red channel (`.r`)
- **Tileable**: Oui, absolument nécessaire

### Fallback procédural
Si aucune texture n'est assignée, le shader génère automatiquement une écume procédurale avec:
- Multi-layer hash noise (5 octaves total)
- Animation indépendante par couche
- Influence des vagues pour plus de réalisme

## Intégration avec crest foam

Les deux systèmes cohabitent parfaitement:

### Différences clés

| Aspect | Crest Foam | Shore Foam |
|--------|------------|------------|
| **Trigger** | Hauteur de vague (`vWaveHeight`) | Profondeur d'eau (`depthDiff`) |
| **Localisation** | Sommets de vagues (dynamique) | Zones peu profondes (statique par rapport au terrain) |
| **Mouvement** | Suit les vagues | Fixe au sol avec animation texture |
| **Usage** | Océan ouvert, vagues | Plages, rivages, bas-fonds |
| **Intensité** | Dépend de l'amplitude des vagues | Dépend de la profondeur |

### Blending
```glsl
// Crest foam appliquée d'abord
finalColor = mix(finalColor, finalColor + crestFoam, saturate(foamMask));

// Shore foam appliquée ensuite (additive avec crest)
finalColor = mix(finalColor, finalColor + shoreFoam, saturate(shoreFoamMask));
```

Les deux écumes s'additionnent naturellement dans les zones de transition.

## Performance

### Impact GPU

- **Coût**: ~15-20 instructions shader supplémentaires
- **Texture samples**: 
  - Avec texture: 2 samples (foamUV1 + foamUV2)
  - Procédural: 0 samples, ~25 instructions de hash
- **Recommandation**: Procédural souvent plus rapide que texture

### Optimisation

#### Early-out
```glsl
if (u_ShoreFoamEnabled == 0 || waterDepth > u_ShoreFoamDepth) 
    return vec3(0.0);
```
Si désactivé ou hors zone, coût = 0.

#### Distance LOD (optionnel)
Pour optimiser davantage, vous pourriez ajouter:
```glsl
float distToCamera = length(uCameraPos - vWorldPos);
if (distToCamera > 100.0) return vec3(0.0); // Pas de shore foam au loin
```

## Troubleshooting

### L'écume n'apparaît pas

**Causes possibles:**
1. ✅ **Enable Shore Foam** décoché
2. ✅ Profondeur d'eau > **Shore Foam Depth** partout
3. ✅ **Shore Foam Intensity** à 0
4. ✅ **Shore Foam Color** alpha à 0

**Solutions:**
- Augmentez Shore Foam Depth (essayez 5.0)
- Augmentez Shore Foam Intensity (essayez 2.0)
- Vérifiez que le terrain est bien sous l'eau (depthDiff > 0)

### L'écume est trop uniforme

**Cause:** Scale trop petite ou speed trop lente

**Solutions:**
- Augmentez Shore Foam Scale (10-15)
- Augmentez Shore Foam Speed (0.1)
- Vérifiez que u_Time est bien passé au shader

### L'écume scintille/flickering

**Cause:** Scale trop grande créant du aliasing

**Solutions:**
- Diminuez Shore Foam Scale (4-6)
- Ajoutez une texture de foam pour plus de stabilité
- Augmentez Edge Sharpness pour réduire les transitions fines

### L'écume est trop dure/nette

**Cause:** Fade et Sharpness trop élevés

**Solutions:**
- Diminuez Shore Foam Fade (0.3-0.5)
- Diminuez Edge Sharpness (1.0-1.5)

## Évolutions futures possibles

### V1.1 - Distance fade
```glsl
float distanceFade = saturate(1.0 - distToCamera / 200.0);
foamPattern *= distanceFade;
```

### V1.2 - Wind direction influence
```glsl
vec2 windOffset = normalize(u_WindDirection) * u_WindStrength * 0.1;
foamUV1 += windOffset;
```

### V1.3 - Parallax depth
```glsl
float height = texture(u_FoamTex, foamUV1).r;
vec2 parallaxUV = foamUV1 - V.xy * height * 0.02;
```

### V1.4 - Foam texture channel packing
- R: Foam pattern
- G: Foam detail/noise
- B: Foam movement mask
- A: Foam thickness

## Conclusion

Le système Shore Foam ajoute un réalisme significatif aux zones de transition eau/terre. Il est:

✅ **Performant**: Early-out et options procédurales
✅ **Flexible**: 8 paramètres tweakables + texture optionnelle  
✅ **Réaliste**: Dual-layer animation, depth-based, beach edge enhancement
✅ **Intégré**: Cohabite avec crest foam, utilise la même texture

**Prochaine étape recommandée**: Ajoutez une texture de foam de qualité pour encore plus de réalisme!
