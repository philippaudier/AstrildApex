# Volumetric Fog - Troubleshooting Guide

## Problème : Tout devient noir quand j'active le fog

### Causes possibles :

#### 1. **Valeurs de fog trop élevées**
- **Solution** : Réduire la `Density`
  ```
  Density: 0.01 → 0.001
  ```
- Si vous utilisez le mode `Global`, vérifiez le `WeatherComponent`:
  ```
  FogDensity: 0.1 → 0.01
  ```

#### 2. **ExtinctionFactor trop élevé**
- **Solution** : L'extinction contrôle combien de lumière est absorbée
  ```
  ExtinctionFactor: 1.0 → 0.5 ou 0.3
  ```

#### 3. **Depth buffer non bindé**
- **Symptômes** : Écran noir complet, même le skybox
- **Vérification** : Regardez les logs dans la Console
  ```
  [VolumetricFog] WARNING: Depth texture is 0!
  ```
- **Solution** : Le depth buffer doit être attaché au framebuffer principal

#### 4. **Matrices de projection/view invalides**
- **Symptômes** : Écran noir ou artefacts étranges
- **Solution** : Les matrices sont automatiquement passées par ViewportRenderer
- Si problème persiste, vérifiez que la caméra est bien configurée

#### 5. **Shader ne compile pas**
- **Vérification** : Regardez les logs
  ```
  [VolumetricFog] Shader load failed: ...
  ```
- **Solution** : Vérifiez que les fichiers existent :
  - `Engine/Rendering/Shaders/PostProcess/volumetric_fog.vert`
  - `Engine/Rendering/Shaders/PostProcess/volumetric_fog.frag`

### Mode Debug

Pour visualiser le depth buffer et vérifier qu'il fonctionne :

1. Ouvrir `volumetric_fog.frag`
2. Décommenter ces lignes dans `main()` :
   ```glsl
   // DEBUG MODE: Uncomment to visualize depth buffer
   FragColor = vec4(vec3(depth), 1.0);
   return;
   ```
3. Recompiler et lancer
4. **Résultat attendu** :
   - Blanc = proche de la caméra
   - Noir = loin de la caméra
   - Skybox = blanc pur

### Configuration Safe par défaut

Si rien ne marche, essayez ces valeurs minimales :

```
Source: Local
Density: 0.001
DepthStart: 50.0
DepthEnd: 500.0
UseExponential: false (linear)
UseHeightFog: false
ExtinctionFactor: 0.5
UseNoise: false
UseSunScattering: false
```

Activez ensuite progressivement chaque feature.

---

## Problème : Le fog ne se dessine pas sur le skybox

✅ **C'est normal et intentionnel !**

Le fog volumétrique est un effet **spatial** qui s'applique uniquement à la géométrie 3D de votre scène. Le skybox représente l'arrière-plan infini et ne devrait PAS avoir de fog dessus.

### Pourquoi ?

1. **Réalisme** : Dans la vraie vie, le brouillard affecte les objets dans l'air, pas le ciel lointain
2. **Performance** : Skip le skybox évite des calculs inutiles
3. **Esthétique** : Le skybox doit rester visible comme arrière-plan

### Si vous voulez du fog sur le skybox

Le fog sur le skybox doit être géré différemment, dans le **shader du skybox** lui-même. Cela se fait avec un fog vertical basé sur la direction du regard :

Fichier : `Engine/Rendering/Shaders/skybox.frag`

```glsl
if (uFogEnabled != 0)
{
    // Fog vertical pour le skybox
    float heightFactor = clamp((dir.y * 0.5) + 0.5, 0.0, 1.0);
    float remapped = ...
    float fogFactor = exp(-uFogDensity * 20.0 * d);
    color = mix(uFogColor, color, fogFactor);
}
```

**Note** : Le fog du skybox est déjà implémenté dans votre projet ! Activez le via le `WeatherComponent` :
- `FogEnabled = true`
- `FogDensity > 0`

---

## Problème : Fog trop uniforme / pas d'effet de relief

### Vérifications :

1. **UseHeightFog doit être activé**
   ```
   UseHeightFog: true ✓
   ```

2. **BaseHeight doit correspondre à votre terrain**
   ```
   Si votre terrain est à Y=0: BaseHeight = 0.0
   Si votre terrain est à Y=10: BaseHeight = 10.0
   ```

3. **HeightFalloff contrôle la vitesse de dissipation**
   ```
   Plus élevé = fog disparaît plus vite avec l'altitude
   Recommandé: 0.05 - 0.2
   ```

4. **Ajustez l'amplification de valley detection**
   
   Dans le shader `volumetric_fog.frag`, ligne ~133:
   ```glsl
   return clamp(avgValley * 50.0, 0.0, 1.0); // Amplify by 50x
   ```
   
   Augmentez le multiplicateur (50.0 → 100.0) pour un effet plus prononcé.

---

## Problème : Performance faible / FPS chute

Le volumetric fog avec valley detection est coûteux (5x5 + 3x3 samples).

### Solutions :

#### 1. Désactiver les features coûteuses
```
UseHeightFog: true (gardez ça)
UseNoise: false ← Désactiver
UseSunScattering: false ← Désactiver
```

#### 2. Réduire le rayon d'échantillonnage

Dans `volumetric_fog.frag`, fonction `calculateValleyFactor()` :
```glsl
const int radius = 2; // Changer à 1 pour 3x3 au lieu de 5x5
```

Dans `calculateRelativeHeight()` :
```glsl
const int radius = 3; // Changer à 2 pour moins de samples
```

#### 3. Downsampler le fog

Appliquez le fog à une résolution réduite (50% ou 25%), puis upsample. Nécessite modification du système de post-process.

---

## Problème : Artefacts / Scintillement

### Causes :

1. **Noise trop fort**
   ```
   NoiseStrength: 0.2 → 0.05
   ```

2. **Amplification de valley trop élevée**
   ```glsl
   avgValley * 50.0 → avgValley * 20.0
   ```

3. **Précision du depth buffer insuffisante**
   - Utilisez un depth buffer 24-bit minimum
   - Préférez depth buffer 32-bit si possible

---

## Problème : Fog ne change pas quand je modifie WeatherComponent

### Vérifications :

1. **Source doit être sur Global**
   ```
   VolumetricFogEffect.Source = Global ✓
   ```

2. **Un seul WeatherComponent par scène**
   - S'il y en a plusieurs, seul le premier est utilisé

3. **FogEnabled doit être true dans WeatherComponent**
   ```
   WeatherComponent.FogEnabled = true ✓
   ```

---

## Valeurs Recommandées par Scénario

### 🏔️ Brume de montagne légère
```
Source: Global
UseExponential: true
Density: 0.02
UseHeightFog: true
BaseHeight: 0.0
HeightFalloff: 0.1
ExtinctionFactor: 0.8
```

### 🌫️ Fog épais de vallée
```
Source: Local
Density: 0.05
UseExponential: true
UseHeightFog: true
BaseHeight: -5.0
HeightFalloff: 0.2
ExtinctionFactor: 1.2
```

### 🌅 Brume matinale avec diffusion solaire
```
Source: Global
Density: 0.015
UseHeightFog: true
BaseHeight: 0.0
HeightFalloff: 0.08
UseSunScattering: true
ScatteringIntensity: 0.7
SunScatteringColor: (1.0, 0.9, 0.7)
ExtinctionFactor: 0.6
```

---

## Logs de Debug Utiles

Ajoutez temporairement dans `VolumetricFogRenderer.cs` :

```csharp
// Après fog.GetEffectiveFogParameters()
try 
{ 
    Engine.Utils.DebugLogger.Log($"[VolumetricFog] Color={fogColor}, Density={density}, Start={depthStart}, End={depthEnd}");
    Engine.Utils.DebugLogger.Log($"[VolumetricFog] DepthTexture={context.DepthTexture}, Width={context.Width}, Height={context.Height}");
} 
catch { }
```

Regardez la console pour ces messages au runtime.

---

## Checklist Rapide

Quand vous activez le fog et que tout va mal :

- [ ] Density < 0.05 ?
- [ ] ExtinctionFactor <= 1.0 ?
- [ ] DepthTexture != 0 ? (check logs)
- [ ] ViewMatrix et ProjectionMatrix valides ?
- [ ] Shader compile sans erreur ? (check logs)
- [ ] Mode debug du depth buffer fonctionne ?
- [ ] WeatherComponent.FogEnabled = true (si Source=Global) ?
- [ ] Au moins un objet 3D visible dans la scène ?

Si tous les checks passent et c'est toujours noir → contactez le support 😅

---

**Créé le** : 8 janvier 2026
**Auteur** : Philippe / GitHub Copilot
