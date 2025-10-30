# Guide de Test - Corrections Shadow Mapping

## Étapes de Test

### 1. Mode Legacy (Sans CSM) - Test d'ombres basiques

**Configuration à utiliser:**
- Aller dans le panel "Rendering Settings"
- Cocher "Shadows" → "Enabled"
- Décocher "Cascaded Shadow Maps" → "Use Cascaded Shadows"
- Shadow Bias: 0.005f
- Shadow Map Size: 2048
- PCF Radius: 1.0

**Résultats attendus:**
- ✅ Le terrain doit avoir des ombres douces et naturelles
- ✅ Pas d'ombres uniformes (complètement noir)
- ✅ Les shadows ne doivent pas "nager" quand la caméra bouge
- ❌ Ne pas voir: Terrain complètement noir ou sans ombres du tout

**En cas de problème:**
- Si Peter-panning (ombres détachées): Augmenter Shadow Bias à 0.01f
- Si acné de shadow (taches): Réduire Shadow Bias à 0.002f
- Si ombres peu visibles: Augmenter PCF Radius à 1.5 ou 2.0

---

### 2. Mode CSM (Cascaded Shadow Maps) - Test hautes et basses résolutions

**Configuration à utiliser:**
- Cocher "Cascaded Shadow Maps" → "Use Cascaded Shadows"
- Cascade Count CSM: 4
- Shadow Map Size: 4096
- PCF Radius: 1.0
- Cascade Lambda: 0.5f

**Résultats attendus:**
- ✅ Les zones proches de la caméra (haute résolution) : ombres précises
- ✅ Les zones loin (basse résolution) : ombres moins nettes mais cohérentes
- ✅ Transitions lisses entre cascades
- ✅ Pas de "fantômes" d'ombres

**En cas de problème:**
- Si ombres manquantes: Vérifier que Shadow Distance (800f) couvre le terrain
- Si ombres décalées entre cascades: Augmenter Lambda à 0.75f (plus log, moins linear)
- Si ombres imprécises près de caméra: Augmenter Shadow Map Size à 4096 ou 8192

---

### 3. Debug Visuel - Vérifier les Cascades

**Mode Debug CSM:**
1. Cocher "Debug" → "Show Shadow Map" dans les settings
2. Une texture colorée doit apparaître dans le viewport (coin bas-droit)
3. Vous devriez voir 4 tuiles (2x2 pour CSM 4 cascades) avec:
   - Rouge: Cascade 0 (très proche caméra)
   - Vert: Cascade 1
   - Bleu: Cascade 2
   - Jaune: Cascade 3 (très loin caméra)

**Résultats attendus:**
- ✅ 4 tuiles visibles avec les bonnes couleurs
- ✅ Chaque tuile a de la profondeur (pas uniforme)
- ✅ Les transitions de couleur nettes (pas de mélange)

---

### 4. Performance et Qualité

**Test de performance:**
- Activer Debug Stats dans le viewport
- Noter le temps de rendu des shadows (devrait être <2ms en mode CSM)
- Augmenter Shadow Map Size et vérifier l'impact sur les FPS

**Test de qualité:**
- Observer le bord des ombres (Shadow boundaries)
- PCF Radius=1.0: Ombres nettes, pas lisses
- PCF Radius=2.0: Ombres très lisses (Peter-panning possible)
- PCF Radius=1.5: Équilibre optimal

---

## Changements Effectués dans le Code

### Fichier 1: ShadowManager.cs
**Change:** Désactivé `CompareRefToTexture` mode
```csharp
// AVANT: GL.TexParameter(..., TextureCompareMode, CompareRefToTexture);
// APRÈS: Ligne supprimée (pas de CompareRefToTexture)
```
**Raison:** Ce mode causait une double application de la comparaison de profondeur.

### Fichier 2: Shadows.glsl
**Change:** Logique PCF corrigée
```glsl
// AVANT: shadow += (compareDepth > shadowDepth + bias) ? 0.0 : 1.0;
// APRÈS: shadow += (compareDepth - bias <= shadowDepth) ? 1.0 : 0.0;
```
**Raison:** Logique inversée causant tout l'écran en ombre.

### Fichier 3: ViewportRenderer.cs (Mode Legacy)
**Change 1:** Ordre de matrices
```csharp
// AVANT: var lightSpace = lightView * ortho;
// APRÈS: var lightSpace = ortho * lightView;
```
**Raison:** OpenTK row-major convention.

**Change 2:** Remplacé LookAtLH par basis vectors
```csharp
// AVANT: var lightView = LookAtLH(lightPos, lightTarget, Vector3.UnitY) * ZFlip;
// APRÈS: Calcul manual de right, up, forward vectors
```
**Raison:** Stabilité et cohérence avec CSM.

### Fichier 4: ViewportRenderer.cs (Mode CSM)
**Change:** Correction offset Atlas Y
```csharp
// AVANT: float offsetY = tileYFlipped * scaleY;
// APRÈS: float offsetY = tileY * scaleY;
//        int tileYFlipped = (tilesY - 1) - tileY; // Pour viewport seulement
```
**Raison:** TextureSpace vs ScreenSpace confusion.

---

## Statistiques Esperées

| Métrique | Sans CSM | Avec CSM (4 cascades) |
|----------|----------|----------------------|
| Shadow Render Time | ~0.5ms | ~1.5ms |
| Shadow Quality (proche) | Bonne | Excellente |
| Shadow Quality (loin) | Mauvaise | Bonne |
| Couverte maximale | ~800 units | ~3200 units (4x800) |
| Résolution effective | 2048x2048 | 1024x1024 par cascade |

---

## Dépannage Rapide

| Symptôme | Solution |
|----------|----------|
| Tout noir | Vérifier u_UseShadows == 1, Shadow Bias trop haut |
| Aucune ombre | Vérifier cascade splits, lightspace matrix |
| Ombres dupliquées | Vérifier atlas transform offsets |
| Peter-panning | Réduire Shadow Bias |
| Acné de shadow | Augmenter Shadow Bias |
| Ombres imprécises (CSM) | Augmenter Shadow Map Size |
| Banding artifacts | Réduire PCF Radius |

