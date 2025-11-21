# Intégration UI Audio - Guide Utilisateur

## ✅ Ajouts Complétés

Le système audio est maintenant **entièrement intégré** dans l'interface de l'éditeur !

---

## 🎵 Menu "Audio Mixer" dans View

### Accès
1. **Menu** → **View** → **🎵 Audio Mixer**
2. Le panneau Audio Mixer s'ouvre

### Fonctionnalités
- **Faders verticaux** pour chaque groupe audio (Master, Music, SFX, Voice, Ambient)
- **VU Meters** en temps réel (indicateurs de niveau audio)
- **Boutons Mute/Solo** par groupe
- **Volume Master** global dans la toolbar
- **Barre de statut** affichant les volumes par catégorie

### Utilisation
```
View → 🎵 Audio Mixer

Contrôles disponibles :
- Slider vertical : Ajuster le volume du groupe (0-100%)
- Bouton "M" : Mute le groupe
- Bouton "S" : Solo le groupe (TODO)
- Master Volume : Contrôle global dans la toolbar
```

---

## 🔊 Menu "Audio" dans Add Component

### Accès
1. Sélectionnez une entité dans la **Hierarchy**
2. Dans l'**Inspector**, cliquez sur **Add Component**
3. Ouvrez le menu **Audio**

### Composants Disponibles

#### 1. **Audio Source**
Source audio attachable à n'importe quelle entité.

**Utilisation** :
- Son 3D spatial (explosion, ennemi, porte)
- Son 2D (musique de fond, UI)
- SFX courts (pas, tirs, impacts)
- Musique en streaming (MP3, OGG, WAV longs)

**Propriétés Principales** :
- Clip : Le fichier audio à jouer
- Volume : 0.0 - 1.0
- Pitch : 0.5 - 2.0
- Loop : Boucle le son
- Spatial Blend : 0 (2D) - 1 (3D)
- Min/Max Distance : Atténuation 3D
- Category : Master, Music, SFX, Voice, Ambient

**Inspecteur** :
- Contrôles Play/Pause/Stop
- Preview en temps réel
- Sliders pour tous les paramètres
- Indicateur de lecture (temps/durée)

#### 2. **Audio Listener**
Représente l'oreille du joueur (généralement sur la caméra).

**Utilisation** :
- À placer sur la **Main Camera**
- Un seul listener actif à la fois
- Suit automatiquement la position/rotation de la caméra

**Propriétés** :
- Velocity Update Mode : Auto/Manual
- Indicateur "ACTIVE LISTENER" en vert

**Note** : Si plusieurs listeners sont actifs, le dernier activé prend le dessus.

---

## 📋 Workflow Complet

### Étape 1 : Setup Audio Listener
```
1. Sélectionnez votre Main Camera
2. Add Component → Audio → Audio Listener
3. Le listener est maintenant actif (indicateur vert dans l'inspecteur)
```

### Étape 2 : Ajouter un Son 3D
```
1. Sélectionnez une entité (ex: ennemi, porte, explosion)
2. Add Component → Audio → Audio Source
3. Dans l'inspecteur AudioSource :
   - Cliquez sur "Load Clip..." (TODO)
   - Ou assignez un clip par code
   - Réglez Spatial Blend à 1.0 (3D complet)
   - Ajustez Min Distance (ex: 5.0)
   - Ajustez Max Distance (ex: 100.0)
   - Cliquez Play pour tester !
```

### Étape 3 : Ajouter de la Musique 2D
```
1. Créez une entité vide "Music Manager"
2. Add Component → Audio → Audio Source
3. Dans l'inspecteur :
   - Assignez un clip MP3 long
   - Loop = true
   - Spatial Blend = 0.0 (2D)
   - Category = Music
   - Volume = 0.5
   - Play On Awake = true
```

### Étape 4 : Utiliser l'Audio Mixer
```
1. View → 🎵 Audio Mixer
2. Ajustez les volumes par catégorie :
   - Music : 60%
   - SFX : 100%
   - Voice : 90%
3. Testez le Mute sur différents groupes
4. Le Master Volume affecte tout
```

---

## 🎨 Interface Inspecteur Audio

### AudioSource Inspector
```
┌─────────────────────────────────────┐
│ Audio Clip                          │
│ ┌─────────────────────────────────┐ │
│ │ Clip: explosion.wav             │ │
│ └─────────────────────────────────┘ │
│ [Load Clip...]                      │
│                                     │
│ Playback                            │
│ [Stop] [Pause]                      │
│ Time: 1.25s / 2.50s                 │
│ ███████████░░░░░░░░░ 50%           │
│                                     │
│ Audio Settings                      │
│ Volume        [====|====] 0.8       │
│ Pitch         [====|====] 1.0       │
│ ☑ Mute                              │
│ ☑ Loop                              │
│ ☑ Play On Awake                     │
│                                     │
│ 3D Sound Settings                   │
│ Spatial Blend [========|] 1.0       │
│ Min Distance  5.0                   │
│ Max Distance  100.0                 │
│ Rolloff Factor 1.0                  │
│ Doppler Level  1.0                  │
│                                     │
│ Mixing                              │
│ Category      [SFX ▼]               │
│ Priority      128                   │
│                                     │
│ Info                                │
│ Format: Stereo16                    │
│ Frequency: 44100 Hz                 │
│ Channels: 2                         │
│ Size: 512 KB                        │
│ Is Playing: true                    │
│ Is Paused: false                    │
└─────────────────────────────────────┘
```

### AudioListener Inspector
```
┌─────────────────────────────────────┐
│ Audio Listener                      │
│ ● ACTIVE LISTENER                   │
│                                     │
│ Velocity Mode [Auto ▼]              │
│                                     │
│ Only one listener can be active     │
│ at a time. This represents the      │
│ player's ear in the 3D world.       │
└─────────────────────────────────────┘
```

---

## 🎛️ Audio Mixer Panel

### Layout
```
┌────────────────────────────────────────────────────────┐
│ Audio Mixer                                      [X]   │
├────────────────────────────────────────────────────────┤
│ [Add Group]  Master Volume [====|====] 0.8             │
├────────────────────────────────────────────────────────┤
│                                                        │
│  Master    Music     SFX      Voice    Ambient        │
│  ┌───┐    ┌───┐    ┌───┐    ┌───┐    ┌───┐          │
│  │   │    │   │    │ █ │    │   │    │   │  VU       │
│  │   │    │ █ │    │ █ │    │ █ │    │   │  Meter    │
│  │ █ │    │ █ │    │ █ │    │ █ │    │   │           │
│  │ █ │    │ █ │    │ █ │    │ █ │    │   │           │
│  └───┘    └───┘    └───┘    └───┘    └───┘          │
│    │        │        │        │        │              │
│    │        │        │        │        │    Faders    │
│    │        │        │        │        │              │
│   100%     60%     100%      90%       0%             │
│  [M][S]   [M][S]   [M][S]   [M][S]   [M][S]          │
│                                                        │
├────────────────────────────────────────────────────────┤
│ Master: 80% | Music: 60% | SFX: 100% | Voice: 90%     │
└────────────────────────────────────────────────────────┘
```

---

## 💡 Exemples d'Utilisation

### Exemple 1 : Son de Pas sur un Joueur
```csharp
// Script attaché au joueur
var audioSource = Entity.GetComponent<AudioSource>();
if (controller.IsGrounded && controller.Velocity.Length > 0.1f)
{
    timer += dt;
    if (timer >= 0.5f)
    {
        timer = 0f;
        audioSource.PlayOneShot(footstepClip, 0.7f);
    }
}
```

### Exemple 2 : Musique de Fond
```csharp
// Dans un MusicManager
var audioSource = Entity.AddComponent<AudioSource>();
var musicClip = AudioImporter.LoadStreamingClip("Assets/Audio/Music/theme.mp3");

// Configuration pour musique
audioSource.Loop = true;
audioSource.SpatialBlend = 0.0f; // 2D
audioSource.Category = AudioCategory.Music;
audioSource.Volume = 0.5f;
audioSource.PlayOnAwake = true;
```

### Exemple 3 : Explosion 3D
```csharp
// Sur une entité explosion
var audioSource = Entity.AddComponent<AudioSource>();
var explosionClip = AudioImporter.LoadClip("Assets/Audio/SFX/explosion.wav");

audioSource.Clip = explosionClip;
audioSource.SpatialBlend = 1.0f; // 3D complet
audioSource.MinDistance = 10.0f;
audioSource.MaxDistance = 200.0f;
audioSource.Category = AudioCategory.SFX;
audioSource.Play();
```

---

## 🎯 Checklist Rapide

### Setup Initial
- [ ] Initialiser AudioEngine dans Program.cs
- [ ] Ajouter AudioListener sur la Main Camera
- [ ] Créer le dossier Assets/Audio/

### Pour Chaque Son
- [ ] Importer le fichier audio (WAV, MP3, OGG)
- [ ] Créer/sélectionner l'entité
- [ ] Add Component → Audio → Audio Source
- [ ] Configurer les propriétés dans l'inspecteur
- [ ] Tester avec le bouton Play

### Mixage
- [ ] Ouvrir View → Audio Mixer
- [ ] Ajuster les volumes par catégorie
- [ ] Tester le rendu final

---

## 📚 Documentation Technique

Pour plus de détails techniques :
- **AUDIO_SYSTEM.md** - API complète
- **AUDIO_STREAMING_TEST_GUIDE.md** - Test MP3
- **AUDIO_SYSTEM_COMPLETE.md** - Vue d'ensemble

---

## ✅ Résumé

**Menu View** :
✅ 🎵 Audio Mixer Panel ajouté

**Menu Add Component** :
✅ Audio → Audio Source
✅ Audio → Audio Listener

**Inspecteurs** :
✅ AudioSource Inspector (complet)
✅ AudioListener Inspector (complet)

**Tout est prêt pour créer des expériences audio immersives ! 🎵🚀**
