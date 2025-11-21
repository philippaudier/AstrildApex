# Audio EFX - Corrections et Notes

## ✅ Corrections Apportées

### 1. Détection EFX Corrigée

**Problème**: EFX n'était pas détecté même avec OpenAL Soft installé.

**Cause**: Utilisation de la mauvaise méthode de vérification.
- ❌ `AL.IsExtensionPresent("AL_EXT_EFX")` → Toujours FALSE
- ✅ `ALC.IsExtensionPresent(device, "ALC_EXT_EFX")` → Correct

**Raison**: EFX est une extension de **contexte** (ALC), pas une extension de **source** (AL).

**Fichiers modifiés**:
- `Engine/Audio/Effects/EFXManager.cs` - Ligne 37
- `Engine/Audio/Effects/AudioEfxBackend.cs` - Ligne 220
- `Engine/Audio/Core/OpenALVersionChecker.cs` - Ligne 30

### 2. P/Invoke pour alSource3i

**Problème**: Erreur de compilation - `AL.Source()` n'a pas de surcharge pour 3 entiers.

**Solution**: Ajout de P/Invoke direct pour `alSource3i()` nécessaire pour attacher les auxiliary effect slots.

**Fichiers modifiés**:
- `Engine/Audio/Effects/EFXInterop.cs` - Ligne 146

### 3. ReverbZone dans le Menu Add Component

**Problème**: ReverbZone n'apparaissait pas dans `Add Component → Audio`.

**Solution**: Ajout du menu item et du case dans l'inspecteur.

**Fichiers modifiés**:
- `Editor/Panels/InspectorPanel.cs` - Lignes 791-793, 924-928

### 4. Bug Stop() ne Remet pas à Zéro

**Problème**: Après `Stop()`, la lecture ne reprend pas du début.

**Solution**: Ajout de `AL.Source(_sourceId, ALSourcef.SecOffset, 0f)` dans `Stop()`.

**Fichiers modifiés**:
- `Engine/Audio/Components/AudioSource.cs` - Ligne 463

### 5. Glitches Audio Pendant la Lecture

**Problème**: La lecture audio glitche et saute.

**Cause**: `RefreshProperties()` était appelé **chaque frame** pour toutes les sources, réappliquant tous les paramètres OpenAL.

**Solution**: Suppression de l'appel automatique à `RefreshProperties()` dans `AudioEngine.Update()`.

**Fichiers modifiés**:
- `Engine/Audio/Core/AudioEngine.cs` - Lignes 108-109

**Note**: Les propriétés sont maintenant mises à jour uniquement quand elles changent (via l'inspecteur ou le code).

---

## 📝 Notes Importantes

### Utilisation des Filtres vs Effets

Il y a actuellement **deux sections d'effets** dans l'inspecteur AudioSource:

1. **Audio Effects** (anciens):
   - Echo, Distortion, Chorus, etc.
   - ⚠️ **NE FONCTIONNENT PAS** avec le nouveau système EFX
   - Code legacy avec TODOs
   - À NE PAS utiliser pour l'instant

2. **Audio Filters** (nouveaux - EFX):
   - Low-Pass Filter, High-Pass Filter
   - ✅ **FONCTIONNENT** avec EFX
   - Utilisent `AudioEfxBackend`
   - **À utiliser** pour les effets par source

### Comment Appliquer des Filtres

**Via Code**:
```csharp
// Ajouter un filtre low-pass
var lowPass = audioSource.AddLowPassFilter(cutoffFrequency: 2000f);

// Modifier les paramètres
if (lowPass.Settings is LowPassSettings settings)
{
    settings.GainHF = 0.3f; // Atténuer les hautes fréquences
    lowPass.UpdateFilter(); // Appliquer
}

// Activer/désactiver
lowPass.Enabled = false;
```

**Via Inspecteur**:
1. Sélectionner une entité avec AudioSource
2. Scroller jusqu'à "Audio Filters" (PAS "Audio Effects")
3. Cliquer "Add Filter" → Choisir Low-Pass ou High-Pass
4. Ajuster les paramètres
5. Toggle "Enabled" pour activer/désactiver

### Mixer Group Effects

Les effets sur les groupes de mixage fonctionnent correctement:

1. Ouvrir AudioMixerPanel
2. Cliquer sur "FX" pour un groupe (Master, Music, SFX, etc.)
3. Ajouter Reverb, Echo, ou filtres
4. Les effets s'appliquent à toutes les sources du groupe

### Reverb Zones

Les zones de reverb 3D fonctionnent:

1. Add Component → Audio → Reverb Zone
2. Configurer Inner/Outer Radius
3. Choisir un preset (Cathedral, Cave, Room, etc.)
4. Les AudioSource 3D dans la zone auront automatiquement de la reverb

---

## 🐛 Bugs Connus

### 1. Anciens Effets Audio Non Fonctionnels

Les anciens effets (Echo, Distortion, Chorus) dans la section "Audio Effects" de l'inspecteur ne sont **pas implémentés** avec EFX.

**Statut**: À implémenter ultérieurement ou à supprimer.

**Workaround**: Utiliser les nouveaux filtres dans la section "Audio Filters" à la place.

### 2. Streaming Audio Peut Encore Glitcher

Si le streaming audio a toujours des glitches, c'est probablement lié au thread de streaming.

**Debug**: Vérifier les logs pour des erreurs OpenAL pendant la lecture.

**Solution potentielle**: Augmenter la taille des buffers de streaming dans `StreamingAudioClip`.

---

## 🔍 Vérification

Pour vérifier que tout fonctionne:

1. **Logs au démarrage** doivent contenir:
   ```
   [AudioEfxBackend] ✓ ALC_EXT_EFX extension detected and enabled
   [EFXManager] ✓ ALC_EXT_EFX Supported - Max Auxiliary Sends: 4
   ```

2. **Dans l'inspecteur AudioSource**:
   - Section "Audio Filters" doit dire "EFX supported"
   - Pouvoir ajouter des filtres Low-Pass/High-Pass

3. **Dans AudioMixerPanel**:
   - Bouton "FX" doit être cliquable
   - Pouvoir ajouter des effets aux groupes

4. **Add Component**:
   - Audio → Reverb Zone doit être présent

---

## 📚 Documentation

- **Guide complet**: `AUDIO_EFX_GUIDE.md`
- **Setup OpenAL**: `OPENAL_SOFT_SETUP.md`

---

## 🔧 TODO Futur

1. ⬜ Implémenter les anciens effets (Echo, Distortion, etc.) avec EFX ou les supprimer
2. ⬜ Optimiser le streaming audio pour éviter les glitches
3. ⬜ Ajouter plus de presets de reverb
4. ⬜ Ajouter un visualiseur de waveform en temps réel
5. ⬜ Implémenter l'occlusion audio (murs bloquent le son)
6. ⬜ Ajouter un système de ducking (baisse automatique de volume)

---

## 🎯 Résumé

✅ EFX fonctionne maintenant correctement
✅ Filtres par source disponibles (Low-Pass, High-Pass)
✅ Effets par groupe de mixage disponibles
✅ Reverb zones 3D fonctionnelles
✅ Stop() remet à zéro la position
✅ Glitches audio réduits (plus d'appel chaque frame)

⚠️ Utiliser "Audio Filters" (nouveaux), PAS "Audio Effects" (anciens)
⚠️ Nécessite OpenAL Soft installé (voir OPENAL_SOFT_SETUP.md)
