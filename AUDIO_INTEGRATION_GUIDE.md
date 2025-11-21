# Guide d'Intégration du Système Audio

## Étape 1 : Restaurer les packages NuGet

Après avoir intégré le système audio, restaurez les packages :

```bash
cd Engine
dotnet restore
```

Le package `OpenTK.Audio.OpenAL` version 4.9.4 sera installé automatiquement.

## Étape 2 : Initialiser le moteur audio

Dans votre fichier `Program.cs` ou `Editor/Program.cs`, ajoutez l'initialisation du moteur audio :

```csharp
using Engine.Audio.Core;

// Au démarrage de l'application (après l'initialisation OpenGL)
try
{
    AudioEngine.Instance.Initialize();
    Console.WriteLine("Audio engine initialized successfully");
}
catch (Exception ex)
{
    Console.WriteLine($"Failed to initialize audio: {ex.Message}");
}
```

## Étape 3 : Mettre à jour chaque frame

Dans votre boucle de jeu principale, ajoutez :

```csharp
// Dans la méthode Update ou la boucle de rendu
AudioEngine.Instance.Update(deltaTime);
```

## Étape 4 : Ajouter un AudioListener à la caméra

Lorsque vous créez votre caméra principale, ajoutez le composant AudioListener :

```csharp
using Engine.Audio.Components;

// Sur votre entité caméra
var camera = scene.CreateEntity("Main Camera");
camera.AddComponent<CameraComponent>();
camera.AddComponent<AudioListenerComponent>(); // <-- Ajoutez ceci
```

## Étape 5 : Intégrer les inspecteurs dans l'éditeur

Dans `Editor/Panels/InspectorPanel.cs`, ajoutez les inspecteurs audio :

```csharp
using Engine.Audio.Components;
using Editor.Inspector;

// Dans la méthode DrawComponent() ou équivalent
if (component is AudioSource audioSource)
{
    AudioSourceInspector.Draw(audioSource);
}
else if (component is AudioListenerComponent audioListener)
{
    AudioListenerInspector.Draw(audioListener);
}
```

## Étape 6 : Préparer vos assets audio

Créez un dossier pour vos fichiers audio :

```
AstrildApex/
└── Assets/
    └── Audio/
        ├── Music/
        │   ├── menu_theme.wav
        │   └── gameplay_theme.wav
        └── SFX/
            ├── footstep.wav
            ├── jump.wav
            └── explosion.wav
```

### Formats recommandés :

- **SFX courts** : WAV 16-bit, 44100 Hz, Mono
- **Musique** : WAV 16-bit, 44100 Hz, Stéréo (OGG Vorbis à venir)

### Conversion rapide avec FFmpeg :

```bash
# Convertir en WAV mono 44100Hz (pour SFX)
ffmpeg -i input.mp3 -ar 44100 -ac 1 output.wav

# Convertir en WAV stéréo 44100Hz (pour musique)
ffmpeg -i input.mp3 -ar 44100 -ac 2 output.wav
```

## Étape 7 : Tester avec un exemple simple

Créez un script de test :

```csharp
using Engine.Scripting;
using Engine.Audio.Components;
using Engine.Audio.Assets;

public class AudioTest : MonoBehaviour
{
    private AudioSource? audioSource;

    public override void Start()
    {
        // Ajouter AudioSource
        audioSource = Entity?.AddComponent<AudioSource>();

        // Charger et jouer un clip
        var clip = AudioImporter.LoadClip("Assets/Audio/SFX/test.wav");
        if (clip != null && audioSource != null)
        {
            audioSource.Clip = clip;
            audioSource.Play();
            Console.WriteLine("Playing audio!");
        }
    }
}
```

## Étape 8 : Nettoyage à la fermeture

Dans votre code de shutdown :

```csharp
// Avant de fermer l'application
AudioEngine.Instance.Dispose();
AudioImporter.UnloadAll();
```

## Exemple complet : Intégration dans le moteur

Voici un exemple de modification de votre fichier principal :

```csharp
// Editor/Program.cs ou équivalent
using Engine.Audio.Core;
using Engine.Audio.Components;

class Program
{
    static void Main(string[] args)
    {
        // Initialisation OpenGL, fenêtre, etc.
        // ...

        // Initialiser le système audio
        try
        {
            AudioEngine.Instance.Initialize();
            AudioEngine.Instance.MasterVolume = 0.8f;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Audio init failed: {ex.Message}");
        }

        // Créer la scène
        var scene = new Scene();

        // Créer la caméra avec listener
        var camera = scene.CreateEntity("Main Camera");
        camera.AddComponent<CameraComponent>();
        camera.AddComponent<AudioListenerComponent>();

        // Boucle de jeu
        while (!ShouldClose())
        {
            float deltaTime = CalculateDeltaTime();

            // Mise à jour du moteur audio
            AudioEngine.Instance.Update(deltaTime);

            // Mise à jour de la scène, rendu, etc.
            scene.Update(deltaTime);
            Render();
        }

        // Nettoyage
        AudioEngine.Instance.Dispose();
        AudioImporter.UnloadAll();
    }
}
```

## Debugging

### Vérifier si OpenAL est disponible

```csharp
using OpenTK.Audio.OpenAL;

try
{
    var device = ALC.OpenDevice(null);
    if (device != ALDevice.Null)
    {
        Console.WriteLine("OpenAL device: " + ALC.GetString(device, AlcGetString.DeviceSpecifier));
        ALC.CloseDevice(device);
    }
    else
    {
        Console.WriteLine("ERROR: No OpenAL device found!");
    }
}
catch (Exception ex)
{
    Console.WriteLine($"OpenAL not available: {ex.Message}");
}
```

### Logs audio

Activez les logs Serilog pour voir les messages du système audio :

```csharp
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .WriteTo.Console()
    .CreateLogger();
```

Vous verrez alors :
```
[AudioEngine] Initialized successfully
[AudioEngine] Device: OpenAL Soft
[AudioClip] Loaded: footstep (0.25s, 44100Hz, 1ch)
```

## Exemples d'utilisation

### 1. Son de pas du joueur

```csharp
public class PlayerFootsteps : MonoBehaviour
{
    private AudioSource audioSource;
    private AudioClip footstepClip;
    private float timer = 0f;

    public override void Start()
    {
        audioSource = Entity.AddComponent<AudioSource>();
        footstepClip = AudioImporter.LoadClip("Assets/Audio/SFX/footstep.wav");
    }

    public override void Update(float dt)
    {
        var controller = Entity.GetComponent<CharacterController>();
        if (controller != null && controller.IsGrounded && controller.Velocity.Length > 0.1f)
        {
            timer += dt;
            if (timer >= 0.5f)
            {
                timer = 0f;
                audioSource?.PlayOneShot(footstepClip, 0.7f);
            }
        }
    }
}
```

### 2. Musique d'ambiance

```csharp
public class AmbientMusic : MonoBehaviour
{
    public override void Start()
    {
        var audioSource = Entity.AddComponent<AudioSource>();
        var musicClip = AudioImporter.LoadClip("Assets/Audio/Music/ambient.wav");

        audioSource.Clip = musicClip;
        audioSource.Loop = true;
        audioSource.Volume = 0.3f;
        audioSource.SpatialBlend = 0.0f; // 2D
        audioSource.Category = AudioCategory.Music;
        audioSource.Play();
    }
}
```

### 3. Explosion 3D

```csharp
public class Explosion : MonoBehaviour
{
    public override void Start()
    {
        var audioSource = Entity.AddComponent<AudioSource>();
        var explosionClip = AudioImporter.LoadClip("Assets/Audio/SFX/explosion.wav");

        audioSource.Clip = explosionClip;
        audioSource.Volume = 1.0f;
        audioSource.SpatialBlend = 1.0f; // 3D complet
        audioSource.MinDistance = 5.0f;
        audioSource.MaxDistance = 100.0f;
        audioSource.Play();

        // Détruire l'entité après le son
        // Destroy(Entity, explosionClip.Length);
    }
}
```

## Troubleshooting

### Pas de son ?

1. Vérifiez que `AudioEngine.Instance.Initialize()` a été appelé
2. Vérifiez que le volume n'est pas à 0
3. Vérifiez que le listener est présent dans la scène
4. Vérifiez les logs pour les erreurs OpenAL

### Audio déformé ?

- Vérifiez que les clips sont en 44100 Hz
- Évitez d'avoir trop de sources simultanées (limite : 64 par défaut)
- Vérifiez que le pitch n'est pas trop élevé/bas

### Performances

- Préchargez les clips au démarrage avec `AudioImporter.PreloadDirectory()`
- Utilisez des clips courts en mémoire, streamez les longs (à venir)
- Limitez le nombre de sources 3D actives

## Prochaines étapes

1. **Ajouter du support OGG/MP3** pour compresser la musique
2. **Implémenter les effets EFX** (reverb, chorus, etc.)
3. **Créer un éditeur de mixage audio** dans l'inspecteur
4. **Ajouter le streaming** pour les fichiers audio longs
5. **Intégrer HRTF** pour un meilleur audio 3D

Bon développement sonore ! 🎵
