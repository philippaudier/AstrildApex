# Correction du système Shadow Mapping - Résumé

## Problème Identifié
- **Sans CSM** : Tout le terrain était recouvert d'ombres (shadowmap vide)
- **Avec CSM** : Les ombres n'étaient pas visibles du tout

## Root Causes Identifiés

### 1. **Double Application de CompareRefToTexture** (CRITIQUE)
**Fichier**: `Engine/Rendering/Shadows/ShadowManager.cs`

OpenGL a deux modes de shadow mapping:
- Mode 1: Utiliser `CompareRefToTexture` pour laisser le hardware faire la comparaison
- Mode 2: Faire PCF manual en lisant la profondeur brute

**Le problème**: Le code activait `CompareRefToTexture` MAIS faisait aussi un PCF manual, créant une double application et inversant la logique des ombres.

**Solution**: Désactiver `CompareRefToTexture` et utiliser uniquement PCF manual.

### 2. **Logique de Comparaison Inversée** (CRITIQUE)
**Fichier**: `Engine/Rendering/Shaders/Includes/Shadows.glsl`

Logique incorrecte:
```glsl
shadow += (compareDepth > shadowDepth + bias) ? 0.0 : 1.0;
```

C'est inversé. Devrait être:
```glsl
shadow += (compareDepth - bias <= shadowDepth) ? 1.0 : 0.0;
```

Raison: Un pixel est LIT si sa profondeur est PLUS PROCHE (<=) que la profondeur stockée.

### 3. **Ordre Incorrecte des Matrices** (Mode Legacy)
**Fichier**: `Editor/Rendering/ViewportRenderer.cs` (ligne ~2938)

OpenTK utilise la convention **row-major** (Vector4 * Matrix):
```csharp
// INCORRECT (causait une projection non-appliquée)
var lightSpace = lightView * ortho;

// CORRECT
var lightSpace = ortho * lightView;
```

### 4. **Instabilité de LookAtLH**
**Fichier**: `Editor/Rendering/ViewportRenderer.cs`

`LookAtLH` pouvait créer des singularités. Remplacé par un calcul de basis vectoriel stable:
```csharp
Vector3 right = Vector3.Normalize(Vector3.Cross(up, lightDir));
up = Vector3.Normalize(Vector3.Cross(lightDir, right));
// Construire manually la matrix de vue
```

### 5. **Inversion de Y Incorrecte dans Atlas CSM**
**Fichier**: `Editor/Rendering/ViewportRenderer.cs` (ligne ~2638)

Le Y était inversé pour le viewport (correct en screen space) mais aussi pour offsetY du transform d'atlas (incorrect en texture space).

```csharp
// ANCIEN (INCORRECT)
int tileYFlipped = (tilesY - 1) - tileY;
float offsetY = tileYFlipped * scaleY;  // ← MAUVAIS

// NOUVEAU (CORRECT)
int tileYFlipped = (tilesY - 1) - tileY;  // Pour viewport seulement
float offsetY = tileY * scaleY;  // Pas d'inversion pour l'atlas transform
```

## Tests Recommandés

1. **Mode Legacy (Sans CSM)** :
   - Désactiver CSM dans les paramètres de rendu
   - Vérifier que le terrain a des ombres normales (pas complètement noir, pas complètement blanc)
   - Ajuster le bias (0.005f par défaut) si acné de shadow ou peter-panning

2. **Mode CSM** :
   - Activer CSM
   - Vérifier que cascades 0-3 ont des ombres correctes
   - Tester le debug color mode pour voir la distribution des cascades

3. **Paramètres à Tester** :
   - Shadow Bias: 0.005f - 0.01f (augmenter si Peter-panning, réduire si acné)
   - PCF Radius: 1.0 - 2.0 (plus haut = ombres plus douces)
   - Shadow Map Size: 2048 - 4096

## Fichiers Modifiés

1. `Engine/Rendering/Shadows/ShadowManager.cs` - Désactivé CompareRefToTexture
2. `Engine/Rendering/Shaders/Includes/Shadows.glsl` - Corrigé logique PCF
3. `Editor/Rendering/ViewportRenderer.cs` - Corrigé matrices et atlas transforms
