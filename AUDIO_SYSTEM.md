# Système Audio - Documentation

## Vue d'ensemble

Le moteur AstrildApex intègre maintenant un système audio complet basé sur **OpenAL Soft**, similaire à Unity. Le système supporte :

- **Audio 3D spatial** avec atténuation de distance et effet Doppler
- **Composants ECS** (AudioSource, AudioListenerComponent)
- **Gestion de clips audio** (AudioClip, AudioImporter)
- **Système de mixage** (AudioMixer, AudioMixerGroup)
- **Catégories audio** (Master, Music, SFX, Voice, Ambient)
- **Effets audio** (Reverb, Chorus, Distortion - via OpenAL EFX)

## Architecture

```
Engine/Audio/
├── Core/
│   ├── AudioEngine.cs          # Singleton - Moteur audio principal
│   ├── AudioSettings.cs        # Configuration globale
│   └── AudioListener.cs        # Listener OpenAL (oreille du joueur)
├── Components/
│   ├── AudioSource.cs          # Component - Source sonore sur entité
│   └── AudioListenerComponent.cs  # Component - Listener sur caméra
├── Assets/
│   ├── AudioClip.cs           # Asset - Clip audio chargé
│   └── AudioImporter.cs       # Importation et cache des clips
├── Mixing/
│   ├── AudioMixer.cs          # Mixer pour groupes audio
│   └── AudioMixerGroup.cs     # Groupe de mixage (ex: Music, SFX)
└── Effects/
    ├── AudioEffect.cs         # Base pour effets audio
    └── ReverbEffect.cs        # Effet de réverbération
```

## Utilisation

### 1. Initialisation

Le système audio doit être initialisé au démarrage du moteur :

```csharp
using Engine.Audio.Core;

// Dans votre code de démarrage (Program.cs ou équivalent)
AudioEngine.Instance.Initialize();
```

### 2. Configuration du Listener

Le listener représente l'oreille du joueur. Attachez-le à votre caméra principale :

```csharp
using Engine.Audio.Components;

// Sur votre entité caméra
var listener = camera.AddComponent<AudioListenerComponent>();
```

### 3. Jouer un son 3D

```csharp
using Engine.Audio.Assets;
using Engine.Audio.Components;

// Charger un clip audio
var clip = AudioImporter.LoadClip("Assets/Audio/explosion.wav");

// Sur une entité (ex: explosion, ennemi, etc.)
var audioSource = entity.AddComponent<AudioSource>();
audioSource.Clip = clip;
audioSource.Volume = 1.0f;
audioSource.SpatialBlend = 1.0f; // 1.0 = 3D complet, 0.0 = 2D
audioSource.MinDistance = 1.0f;
audioSource.MaxDistance = 50.0f;
audioSource.Play();
```

### 4. Jouer un son one-shot (sans arrêter les autres)

```csharp
var footstepClip = AudioImporter.LoadClip("Assets/Audio/footstep.wav");
audioSource.PlayOneShot(footstepClip, 0.8f); // volume scale
```

### 5. Musique en boucle (2D)

```csharp
var musicClip = AudioImporter.LoadClip("Assets/Audio/background_music.wav");

var musicSource = musicEntity.AddComponent<AudioSource>();
musicSource.Clip = musicClip;
musicSource.Loop = true;
musicSource.SpatialBlend = 0.0f; // Son 2D (pas d'atténuation)
musicSource.Category = AudioCategory.Music;
musicSource.Volume = 0.5f;
musicSource.Play();
```

### 6. Contrôle global du volume

```csharp
// Volume principal
AudioEngine.Instance.MasterVolume = 0.8f;

// Volumes par catégorie
AudioEngine.Instance.Settings.MusicVolume = 0.6f;
AudioEngine.Instance.Settings.SFXVolume = 1.0f;
AudioEngine.Instance.Settings.VoiceVolume = 0.9f;
```

### 7. Pause/Resume/Stop

```csharp
// Pause individuelle
audioSource.Pause();
audioSource.UnPause();
audioSource.Stop();

// Pause globale (toutes les sources)
AudioEngine.Instance.PauseAll();
AudioEngine.Instance.ResumeAll();
AudioEngine.Instance.StopAll();
```

## Formats supportés

### Actuellement implémenté
- ✅ **WAV** (PCM) - Format recommandé pour les SFX courts

### À venir (TODO)
- ⏳ **OGG Vorbis** - Recommandé pour la musique (compression avec perte)
- ⏳ **MP3** - Support optionnel

## Propriétés AudioSource

| Propriété | Description | Valeurs |
|-----------|-------------|---------|
| `Volume` | Volume de la source | 0.0 - 1.0 |
| `Pitch` | Hauteur du son (pitch shift) | 0.5 - 2.0 |
| `Loop` | Boucle le clip | true/false |
| `PlayOnAwake` | Joue automatiquement au démarrage | true/false |
| `SpatialBlend` | Mélange 2D/3D | 0.0 (2D) - 1.0 (3D) |
| `MinDistance` | Distance minimale (volume max) | > 0.0 |
| `MaxDistance` | Distance maximale (volume min) | > MinDistance |
| `RolloffFactor` | Facteur d'atténuation | 0.0 - 10.0 |
| `DopplerLevel` | Intensité de l'effet Doppler | 0.0 - 5.0 |
| `Category` | Catégorie de mixage | Master, Music, SFX, Voice, Ambient |
| `Priority` | Priorité (pour la gestion des limites) | 0 (élevée) - 256 (basse) |
| `Mute` | Mute la source | true/false |

## Configuration globale (AudioSettings)

```csharp
var settings = AudioEngine.Instance.Settings;

// Volumes par catégorie
settings.MasterVolume = 1.0f;
settings.MusicVolume = 0.8f;
settings.SFXVolume = 1.0f;
settings.VoiceVolume = 1.0f;

// Effet Doppler
settings.DopplerFactor = 1.0f; // 0 = désactivé
settings.SpeedOfSound = 343.3f; // m/s

// Modèle d'atténuation
settings.DistanceModel = ALDistanceModel.InverseDistanceClamped;

// Limite de sources simultanées
settings.MaxAudioSources = 64;
```

## Modèles d'atténuation (Distance Models)

- `InverseDistanceClamped` - Atténuation réaliste (recommandé)
- `LinearDistanceClamped` - Atténuation linéaire
- `ExponentDistanceClamped` - Atténuation exponentielle
- `None` - Pas d'atténuation

## Système de Mixage

Créez des groupes de mixage pour contrôler plusieurs sources :

```csharp
using Engine.Audio.Mixing;

var mixer = new AudioMixer("MainMixer");

// Créer des groupes
var sfxGroup = mixer.CreateGroup("SFX");
var musicGroup = mixer.CreateGroup("Music");
var voiceGroup = mixer.CreateGroup("Voice");

// Contrôler les volumes
mixer.SetGroupVolume("Music", 0.6f);
mixer.SetGroupMute("SFX", true); // Mute tous les SFX
```

## Effets Audio (EFX)

```csharp
using Engine.Audio.Effects;

// Créer un effet de réverbération
var reverb = new ReverbEffect();
reverb.Preset = ReverbEffect.ReverbPreset.Cathedral;
reverb.DecayTime = 5.0f;
reverb.Density = 1.0f;

// Appliquer à une source (TODO: implémentation complète)
reverb.Apply(audioSource);
```

### Presets de Reverb disponibles
- `Generic` - Réverb générique
- `Room` - Petite pièce
- `LivingRoom` - Salon
- `Hall` - Grande salle
- `Cathedral` - Cathédrale (longue réverbération)
- `Cave` - Grotte
- `Arena` - Stade/Arène
- `Hangar` - Hangar
- `Underwater` - Sous l'eau

## Exemple complet : Script de joueur avec sons

```csharp
using Engine.Scripting;
using Engine.Audio.Components;
using Engine.Audio.Assets;

public class PlayerAudio : MonoBehaviour
{
    private AudioSource audioSource;
    private AudioClip footstepClip;
    private AudioClip jumpClip;
    private AudioClip landClip;

    private float footstepTimer = 0f;
    private const float FootstepInterval = 0.4f;

    public override void Start()
    {
        // Ajouter AudioSource
        audioSource = Entity.AddComponent<AudioSource>();
        audioSource.SpatialBlend = 1.0f; // 3D
        audioSource.MinDistance = 1.0f;
        audioSource.MaxDistance = 30.0f;
        audioSource.Category = AudioCategory.SFX;

        // Charger les clips
        footstepClip = AudioImporter.LoadClip("Assets/Audio/Player/footstep.wav");
        jumpClip = AudioImporter.LoadClip("Assets/Audio/Player/jump.wav");
        landClip = AudioImporter.LoadClip("Assets/Audio/Player/land.wav");
    }

    public override void Update(float dt)
    {
        // Récupérer le CharacterController
        var controller = Entity.GetComponent<CharacterController>();
        if (controller == null) return;

        // Sons de pas pendant le mouvement au sol
        if (controller.IsGrounded && controller.Velocity.LengthSquared > 0.1f)
        {
            footstepTimer += dt;
            if (footstepTimer >= FootstepInterval)
            {
                footstepTimer = 0f;
                audioSource.PlayOneShot(footstepClip, 0.7f);
            }
        }
        else
        {
            footstepTimer = 0f;
        }
    }

    public void OnJump()
    {
        audioSource.PlayOneShot(jumpClip, 1.0f);
    }

    public void OnLand()
    {
        audioSource.PlayOneShot(landClip, 0.9f);
    }
}
```

## Mise à jour du moteur

N'oubliez pas d'appeler `Update()` chaque frame :

```csharp
// Dans votre boucle de jeu principale
AudioEngine.Instance.Update(deltaTime);
```

## Nettoyage

```csharp
// À la fin de votre application
AudioEngine.Instance.Dispose();
AudioImporter.UnloadAll();
```

## Limitations actuelles

1. **Formats audio** : Seul WAV est supporté. OGG et MP3 arrivent prochainement.
2. **Effets EFX** : L'implémentation des effets nécessite OpenAL-EFX (en cours).
3. **Streaming** : Les clips longs sont chargés entièrement en mémoire. Le streaming arrive bientôt.

## Performances

- **Recommandations** :
  - Utilisez WAV pour les SFX courts (< 5 secondes)
  - Utilisez OGG Vorbis pour la musique longue (compression ~10:1)
  - Limitez le nombre de sources simultanées (MaxAudioSources = 64 par défaut)
  - Préchargez les clips fréquemment utilisés au démarrage
  - Déchargez les clips inutilisés avec `AudioImporter.UnloadClip()`

## TODO / Roadmap

- [ ] Support OGG Vorbis (via NVorbis)
- [ ] Support MP3 (via NLayer)
- [ ] Streaming audio pour fichiers longs
- [ ] Implémentation complète EFX (effets audio)
- [ ] Filtres audio (low-pass, high-pass, band-pass)
- [ ] Courbes d'atténuation personnalisées
- [ ] Spatialisation HRTF (audio 3D avancé)
- [ ] Occlusion/obstruction audio
- [ ] Audio dans l'éditeur (preview, waveform viewer)
- [ ] Inspecteur AudioSource dans l'éditeur
- [ ] Sérialisation des AudioClip dans les scènes

## Références

- [OpenAL Soft Documentation](https://openal-soft.org/)
- [OpenAL 1.1 Specification](https://www.openal.org/documentation/openal-1.1-specification.pdf)
- [OpenAL EFX Extension](https://openal-soft.org/openal-extensions/SOFT_effect_target.txt)
