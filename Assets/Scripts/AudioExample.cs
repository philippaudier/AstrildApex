using Engine.Scripting;
using Engine.Audio.Components;
using Engine.Audio.Assets;
using OpenTK.Mathematics;

/// <summary>
/// Exemple d'utilisation du système audio - Script de démonstration
/// Attachez ce script à une entité pour tester le système audio
/// </summary>
public class AudioExample : MonoBehaviour
{
    private AudioSource? audioSource;
    private AudioClip? footstepClip;
    private AudioClip? jumpClip;
    private AudioClip? ambientClip;

    private float walkTimer = 0f;
    private const float WalkSoundInterval = 0.5f; // Un pas toutes les 0.5 secondes

    public override void Start()
    {
        // Récupérer ou ajouter un composant AudioSource
        audioSource = Entity?.GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = Entity?.AddComponent<AudioSource>();
        }

        if (audioSource != null)
        {
            // Configuration de base
            audioSource.Volume = 0.8f;
            audioSource.SpatialBlend = 1.0f; // Son 3D complet
            audioSource.MinDistance = 1.0f;
            audioSource.MaxDistance = 50.0f;
            audioSource.Category = AudioCategory.SFX;
        }

        // Charger des clips audio (exemple - vous devrez avoir des fichiers WAV dans Assets/Audio/)
        // footstepClip = AudioImporter.LoadClip("Assets/Audio/footstep.wav");
        // jumpClip = AudioImporter.LoadClip("Assets/Audio/jump.wav");
        // ambientClip = AudioImporter.LoadClip("Assets/Audio/ambient_music.wav");

        Console.WriteLine("[AudioExample] Initialized - Audio system ready");
    }

    public override void Update(float dt)
    {
        if (audioSource == null) return;

        // Exemple : Jouer un son de pas pendant le déplacement
        // (Vous pouvez détecter le mouvement en vérifiant la vélocité du CharacterController)

        walkTimer += dt;

        // Simuler des pas pendant le mouvement
        if (walkTimer >= WalkSoundInterval)
        {
            walkTimer = 0f;

            // Jouer un son one-shot (ne nécessite pas de stopper les autres sons)
            if (footstepClip != null)
            {
                audioSource.PlayOneShot(footstepClip, 0.6f);
            }
        }
    }

    /// <summary>
    /// Exemple : Jouer un son de saut (appelable depuis d'autres scripts)
    /// </summary>
    public void PlayJumpSound()
    {
        if (audioSource != null && jumpClip != null)
        {
            audioSource.PlayOneShot(jumpClip, 1.0f);
        }
    }

    /// <summary>
    /// Exemple : Démarrer la musique d'ambiance en boucle
    /// </summary>
    public void PlayAmbientMusic()
    {
        if (audioSource != null && ambientClip != null)
        {
            audioSource.Clip = ambientClip;
            audioSource.Loop = true;
            audioSource.Volume = 0.3f; // Musique plus douce
            audioSource.SpatialBlend = 0.0f; // Son 2D pour la musique
            audioSource.Play();
        }
    }

    /// <summary>
    /// Exemple : Arrêter tous les sons
    /// </summary>
    public void StopAllSounds()
    {
        audioSource?.Stop();
    }
}
