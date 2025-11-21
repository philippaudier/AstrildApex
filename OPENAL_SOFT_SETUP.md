# OpenAL Soft Setup - Installation EFX

Pour que les effets audio EFX fonctionnent, vous devez utiliser **OpenAL Soft** au lieu de la version de Creative Labs.

## Problème

Si vous voyez "EFX not supported" dans l'éditeur, c'est que:
- Vous utilisez l'ancienne DLL OpenAL32.dll de Creative Labs (qui ne supporte pas EFX)
- Ou OpenAL Soft n'est pas correctement installé

## Solution: Installer OpenAL Soft

### Option 1: Téléchargement Direct (Recommandé)

1. Téléchargez OpenAL Soft depuis:
   - https://www.openal-soft.org/
   - Ou directement: https://github.com/kcat/openal-soft/releases

2. Téléchargez la version Windows (par exemple `openal-soft-1.23.1-bin.zip`)

3. Extrayez l'archive

4. Copiez `OpenAL32.dll` depuis le dossier **`bin\Win64`** (pour 64-bit)

5. Collez-le dans les dossiers suivants de votre projet:
   ```
   Editor\bin\Debug\net8.0-windows\OpenAL32.dll
   Editor\bin\Release\net8.0-windows\OpenAL32.dll
   Sandbox\bin\Debug\net8.0-windows\OpenAL32.dll
   ```

### Option 2: Package NuGet (Alternative)

Il n'y a pas de package NuGet officiel pour OpenAL Soft, donc l'Option 1 est préférable.

### Option 3: Installation Système

1. Installez OpenAL Soft system-wide via l'installeur Windows
2. Redémarrez votre application

## Vérification

Pour vérifier que OpenAL Soft est bien utilisé:

1. Lancez votre éditeur
2. Ouvrez la console/logs
3. Cherchez le message:
   ```
   [EFXManager] EFX Supported - Max Auxiliary Sends: 4 (default)
   [AudioEfxBackend] EFX extension detected and enabled
   ```

4. Si vous voyez à la place:
   ```
   [EFXManager] OpenAL EFX extension not supported
   ```
   C'est que vous utilisez toujours l'ancienne DLL.

## Vérifier Quelle DLL est Utilisée

Sous Windows, vous pouvez utiliser **Dependency Walker** ou **Process Explorer** pour voir quelle OpenAL32.dll est chargée.

Ou simplement vérifier la date de modification du fichier:
- **OpenAL Soft**: Date récente (2020+)
- **Creative OpenAL**: Date ancienne (avant 2010)

## Structure de Fichiers Après Installation

```
AstrildApex/
├── Editor/
│   └── bin/
│       └── Debug/
│           └── net8.0-windows/
│               ├── Editor.exe
│               ├── OpenAL32.dll  ← OpenAL Soft (requis pour EFX)
│               └── ...
├── Engine/
│   └── ...
└── ...
```

## Fonctionnalités EFX Disponibles Après Installation

Une fois OpenAL Soft installé, vous aurez accès à:

✅ **Audio Filters (dans AudioSource Inspector)**
- Low-Pass Filter (effet étouffé/sous l'eau)
- High-Pass Filter (effet téléphone/radio)

✅ **Mixer Group Effects (dans AudioMixerPanel → bouton FX)**
- Reverb (sur groupes Master, Music, SFX, etc.)
- Echo
- Filtres globaux (Low-Pass, High-Pass)

✅ **Reverb Zones (Add Component → Audio → Reverb Zone)**
- Reverb spatial 3D
- Presets: Generic, Room, Cathedral, Cave, etc.

## Dépannage

### "DllNotFoundException: Unable to load DLL 'OpenAL32.dll'"

**Solution**: OpenAL32.dll n'est pas dans le dossier de l'exécutable.
- Copiez OpenAL32.dll dans le même dossier que Editor.exe

### "EFX not supported" même avec OpenAL Soft

**Causes possibles**:
1. Vous avez copié la mauvaise version (32-bit au lieu de 64-bit)
   - Solution: Utilisez `bin\Win64\OpenAL32.dll` pour x64
2. Votre driver audio ne supporte pas OpenAL
   - Solution: Mettez à jour vos drivers audio
3. La DLL est bloquée par Windows
   - Solution: Clic droit → Propriétés → Décocher "Débloquer"

### Conflits avec l'Installation Système

Si vous avez déjà OpenAL installé système (via un jeu par exemple):
- L'application pourrait charger celle du système plutôt que la locale
- Solution: Assurez-vous que OpenAL32.dll est bien dans le dossier de l'exe

## Liens Utiles

- OpenAL Soft Website: https://www.openal-soft.org/
- GitHub Repository: https://github.com/kcat/openal-soft
- Documentation EFX: https://github.com/kcat/openal-soft/blob/master/docs/effects/
- OpenAL EFX Specification: https://www.openal.org/documentation/OpenAL_Programmers_Guide.pdf

## Note pour le Développement

Si vous distribuez votre jeu/application:
- Incluez OpenAL32.dll (OpenAL Soft) dans le package de distribution
- Ajoutez une note dans le README mentionnant l'utilisation d'OpenAL Soft (licence LGPL)
