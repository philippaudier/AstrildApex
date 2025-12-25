# 🔍 Terrain Debug - Diagnostic

## Problèmes Identifiés

### 1. ❌ Terrain Invisible
- **Symptôme:** Le terrain n'apparaît plus après l'intégration du système de neige
- **Cause possible:** Erreur de compilation du shader TerrainForward.frag
- **Uniforms suspects:** u_SnowSlopeMin, u_SnowSlopeMax, u_SnowSparkle, u_SnowDisplacement

### 2. ❌ Coverage Suit Intensity
- **Symptôme:** Quand Intensity descend à 0, Coverage passe immédiatement à 0
- **Cause:** Clamp(Coverage, 0, Intensity) force Coverage = 0 quand Intensity = 0
- **✅ CORRIGÉ:** Le clamp n'est plus appliqué quand on est en mode fonte

### 3. ❌ Advanced Snow Settings Non Utilisés
- **Symptôme:** SlopeMin/Max, Sparkle, Displacement semblent sans effet
- **Cause possible:** Uniforms non envoyés ou shader ne les utilise pas

### 4. ❌ SnowMaterial Non Utilisé
- **Symptôme:** Le material assigné dans l'UI n'est pas utilisé
- **Cause:** On n'a jamais implémenté le sampling des textures du material

---

## 🔧 Actions de Correction

### Action 1: Simplifier Temporairement le Shader ✅

Pour isoler le problème, on va créer une version minimale du shader qui fonctionne, puis ajouter les features progressivement.

**Test à faire:**
1. Commenter temporairement les fonctions calculateSnowPlacement() et calculateSnowSparkle()
2. Revenir à la version basique de la neige
3. Vérifier si le terrain réapparaît

### Action 2: Vérifier les Logs de Compilation

Le shader peut échouer à compiler. Vérifier dans la console/logs de l'éditeur :
- Messages d'erreur GLSL
- Warnings de compilation
- Erreurs de linkage

### Action 3: Tester avec Snow Désactivé

Mettre `u_SnowCoverage = 0` et vérifier si le terrain apparaît.

---

## 📋 Checklist de Diagnostic

### Uniforms Requis (TerrainForward.frag)

Vérifier que TOUS ces uniforms sont initialisés dans ViewportRenderer:

**Matrices:**
- [x] u_Model
- [x] u_View
- [x] u_Projection
- [x] u_NormalMat

**Lighting:**
- [x] u_ViewPos / uCameraPos
- [x] u_LightDir
- [x] u_LightColor

**PBR/Material:**
- [x] u_LayerCount
- [x] u_LayerAlbedo[0-7]
- [x] u_LayerNormal[0-7]
- [x] ... (tous les layer uniforms)

**Weather (basique):**
- [x] u_RainIntensity
- [x] u_SnowCoverage
- [x] u_Wetness

**Weather (avancé - NOUVEAUX):**
- [?] u_SnowSlopeMin
- [?] u_SnowSlopeMax
- [?] u_SnowSparkle
- [?] u_SnowDisplacement

**Shadows:**
- [x] u_ShadowMap
- [x] u_UseShadows
- [x] ... (shadow uniforms)

**IBL:**
- [x] u_IrradianceMap
- [x] u_PrefilteredEnvMap
- [x] u_BRDFLUT

---

## 🧪 Test Plan

### Test 1: Version Minimale (Sans Neige Avancée)

**Objectif:** Vérifier que le terrain est visible sans les nouvelles fonctionnalités

**Modifications temporaires:**
1. Commenter `calculateSnowPlacement()`
2. Commenter `calculateSnowSparkle()`
3. Revenir au code de neige basique
4. Rebuild et test

**Résultat attendu:** Terrain visible

### Test 2: Neige Basique

**Objectif:** Vérifier que la neige basique fonctionne

**Config:**
- SnowCoverage = 0.5
- Vérifier visuellement la neige sur surfaces horizontales

### Test 3: Advanced Snow (Progressive)

**Objectif:** Ajouter les features une par une

**Étapes:**
1. Ajouter calculateSnowPlacement() seulement
   - Test: Neige respecte les angles ?
2. Ajouter calculateSnowSparkle()
   - Test: Scintillement visible ?

---

## 🛠️ Solution Temporaire: Version Safe du Shader

Si le problème persiste, utiliser cette version "safe" du code de neige qui fonctionne à coup sûr:

```glsl
// === SAFE SNOW SYSTEM (version basique garantie fonctionnelle) ===
if (u_SnowCoverage > 0.0)
{
    vec3 up = vec3(0, 1, 0);
    float upFacing = max(0.0, dot(material.normal, up));
    float snowAmount = pow(upFacing, 2.0) * u_SnowCoverage;

    // Respect SlopeMax (simple version)
    float angleRad = acos(clamp(dot(normalize(material.normal), up), -1.0, 1.0));
    float angleDeg = degrees(angleRad);
    float slopeFactor = smoothstep(u_SnowSlopeMax + 5.0, u_SnowSlopeMax - 5.0, angleDeg);
    snowAmount *= slopeFactor;

    if (snowAmount > 0.01)
    {
        vec3 snowColor = vec3(0.95, 0.96, 1.0);
        material.baseColor = mix(material.baseColor, snowColor, snowAmount);
        material.roughness = mix(material.roughness, 0.3, snowAmount);
        material.metallic = mix(material.metallic, 0.0, snowAmount);
    }
}
```

Cette version:
- ✅ Pas de fonction complexe
- ✅ Respect de SlopeMax avec smoothstep simple
- ✅ Pas de sparkle (pour éviter les bugs)
- ✅ Code inline (pas d'appel de fonction)

---

## 💡 Hypothèses sur le Problème du Terrain Invisible

### Hypothèse 1: Erreur de Compilation GLSL ⚠️

**Problème possible:**
- La fonction `degrees()` n'existe peut-être pas en GLSL 420
- Ou une autre erreur de syntaxe

**Solution:**
- Utiliser `angleRad * 180.0 / 3.14159265` au lieu de `degrees(angleRad)`
- Vérifier les logs de compilation

### Hypothèse 2: Uniforms Non Initialisés ⚠️

**Problème:**
- Si un uniform requis n'est pas initialisé, le shader peut échouer

**Solution:**
- Ajouter des valeurs par défaut pour TOUS les uniforms
- Wrap dans des try/catch

### Hypothèse 3: Ordre d'Initialisation ⚠️

**Problème:**
- Les uniforms de neige sont peut-être envoyés APRÈS le rendu du terrain

**Solution:**
- Vérifier l'ordre des appels dans ViewportRenderer
- S'assurer que les uniforms sont envoyés AVANT terrain.Render()

---

## 🎯 Recommandation Immédiate

**OPTION A: Revenir Temporairement à la Version Basique**

Simplifier le shader pour isoler le problème:
1. Utiliser la version "safe" du code de neige (ci-dessus)
2. Rebuild
3. Vérifier si le terrain réapparaît

**OPTION B: Debug Mode Complet**

Ajouter des logs pour tracer le problème:
1. Log de compilation du shader
2. Log des valeurs des uniforms
3. Log GL errors

**OPTION C: Désactiver Temporairement la Neige**

Mettre `if (false && u_SnowCoverage > 0.0)` pour désactiver complètement la neige et vérifier si c'est elle qui cause le problème.

---

## 🔍 Commandes de Debug

### Dans l'éditeur:

1. **Vérifier les logs de shader:**
   - Chercher "TerrainForward" dans la console
   - Chercher "ERROR", "WARNING", "failed to compile"

2. **Vérifier les uniforms:**
   - Ajouter des logs temporaires dans ViewportRenderer
   - Vérifier les valeurs envoyées

3. **Test avec shader de debug:**
   - Utiliser `TERRAIN_DEBUG_SHADER=1` pour activer TerrainDebug shader
   - Vérifier si le problème persiste

---

## ✅ Next Steps

1. **Essayer la version SAFE du shader** (remplacer le code de neige avancé)
2. **Vérifier les logs de compilation** pour erreurs GLSL
3. **Tester avec SnowCoverage = 0** pour éliminer la neige comme cause
4. **Si ça marche, réintroduire les features progressivement**

---

**Date:** 2025-12-22
**Status:** 🔴 En Investigation
