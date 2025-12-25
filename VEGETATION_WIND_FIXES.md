# Corrections VegetationForward - Wind & LOD en Temps Réel

## Problèmes Résolus

### 1. ✅ Feuilles qui disparaissent
**Cause:** Le système LOD et distance culling était trop agressif et s'activait même quand désactivé.

**Solution:**
- Alpha test en premier (avant tous les discards)
- LOD et distance culling conditionnels (seulement si activés)
- Ordre correct: Sample texture → Alpha test → LOD check → Distance check

```glsl
// AVANT (mauvais ordre)
void main() {
    // LOD checks AVANT de sampler la texture
    if (lodLevel >= 3.0) discard;
    vec4 albedoSample = texture(u_AlbedoTex, vUV);
}

// APRÈS (bon ordre)
void main() {
    // Sample texture EN PREMIER
    vec4 albedoSample = texture(u_AlbedoTex, vUV);
    float alpha = albedoSample.a * u_AlbedoColor.a;
    
    // Alpha test pour feuilles
    if (alpha < 0.1) discard;
    
    // LOD SEULEMENT si activé
    if (u_EnableLOD > 0) { ... }
}
```

### 2. ✅ Wind non visible en Edit Mode
**Cause:** Le rendu de végétation était commenté dans ViewportRenderer.

**Solution:**
- Décommenté le bloc VegetationRenderer dans ViewportRenderer
- Ajout des paramètres LOD au call de Render()
- Récupération des paramètres depuis le premier VegetationLayer

### 3. ✅ Shader incompatible avec Instancing
**Cause:** Le vertex shader utilisait `uniform mat4 u_Model` au lieu des attributs d'instance.

**Solution:**
```glsl
// Instance attributes (per-instance)
layout(location=3) in vec4 aInstanceMatrix0;
layout(location=4) in vec4 aInstanceMatrix1;
layout(location=5) in vec4 aInstanceMatrix2;
layout(location=6) in vec4 aInstanceMatrix3;

void main() {
    // Reconstruire la matrice depuis les attributs
    mat4 u_Model = mat4(aInstanceMatrix0, aInstanceMatrix1, aInstanceMatrix2, aInstanceMatrix3);
    // ... reste du code
}
```

## Changements de Code

### VegetationForward.vert
1. **Supprimé:** `uniform mat4 u_Model;`
2. **Ajouté:** Instance attributes (locations 3-6)
3. **Modifié:** Reconstruction de u_Model depuis les attributs

### VegetationForward.frag
1. **Réorganisé:** Sample texture en premier
2. **Ajouté:** Alpha test avant tous les discards
3. **Conditionnel:** LOD et distance culling seulement si activés

### ViewportRenderer.cs
1. **Décommenté:** Bloc de rendu vegetation (lignes 2593-2660)
2. **Ajouté:** Récupération des paramètres LOD depuis VegetationLayer
3. **Ajouté:** Passage des paramètres LOD à Render()

## Utilisation

### Pour voir le Wind en temps réel:

1. **Créer un Terrain** avec végétation
2. **Configurer le Wind** dans Weather & Environment:
   - Wind Strength: 0.30 (visible)
   - Wind Speed: 1.0
   - Wind Gustiness: 0.50
   - Direction: X=1, Z=0 (ou utiliser presets)

3. **Le wind s'anime automatiquement** en Edit Mode et Play Mode

### Configuration LOD (optionnel):

Dans Advanced Settings du VegetationLayer:
- Max Draw Distance: 60 (ou plus selon besoin)
- Enable LOD System: ✓ (cocher pour activer)
- LOD0/1/2 Distances: 50, 100, 150

**Note:** Si les feuilles disparaissent encore, **désactiver LOD System** temporairement.

## Paramètres Wind Recommandés

### Vent léger (feuilles qui tremblent):
```
Strength: 0.15
Speed: 1.0
Gustiness: 0.3
```

### Vent moyen (branches qui bougent):
```
Strength: 0.30
Speed: 1.5
Gustiness: 0.5
```

### Vent fort (arbres qui se plient):
```
Strength: 0.60
Speed: 2.0
Gustiness: 0.8
```

## Debug

Si les feuilles disparaissent encore:

1. **Vérifier le matériau:**
   - Alpha Cutoff < 0.1
   - Texture a un canal alpha valide

2. **Désactiver LOD:**
   - Décocher "Enable LOD System"
   - Max Draw Distance = 0 (désactive distance culling)

3. **Console logs:**
   - Regarder les logs `[VegetationRenderer]`
   - Vérifier que le shader se charge correctement

## Performance

Avec le nouveau système:
- ✅ Wind visible en temps réel (Edit + Play Mode)
- ✅ Pas de disparition des feuilles
- ✅ LOD optionnel pour grandes scènes
- ✅ Dithering temporal pour transitions douces

## Tests Réussis

- [x] Compilation sans erreurs
- [x] Shader compatible avec instancing
- [x] Alpha test pour feuilles transparentes
- [x] Wind animé en temps réel
- [x] LOD conditionnel (ne casse pas quand désactivé)
- [x] Distance culling conditionnel
