using System;
using System.Collections.Generic;
using Engine.Scripting;
using Engine.Audio.Components;
using Engine.Audio.Assets;
using Engine.Audio.Core;

/// <summary>
/// Gestionnaire de musique de fond - Exemple avancé
/// Gère le crossfade entre plusieurs pistes musicales
/// </summary>
public class MusicManager : MonoBehaviour
{
    private Dictionary<string, AudioClip> musicTracks = new();
    private AudioSource? currentMusicSource;
    private AudioSource? nextMusicSource;

    private string? currentTrack;
    private bool isCrossfading = false;
    private float crossfadeTimer = 0f;
    private float crossfadeDuration = 2.0f;

    public override void Start()
    {
        // Créer deux sources audio pour le crossfade
        currentMusicSource = Entity?.AddComponent<AudioSource>();
        nextMusicSource = Entity?.AddComponent<AudioSource>();

        if (currentMusicSource != null)
        {
            ConfigureMusicSource(currentMusicSource);
        }

        if (nextMusicSource != null)
        {
            ConfigureMusicSource(nextMusicSource);
            nextMusicSource.Volume = 0f; // Commence muet
        }

        // Charger les pistes musicales (exemple - adaptez les chemins)
        // LoadMusicTrack("menu", "Assets/Audio/Music/menu_theme.wav");
        // LoadMusicTrack("gameplay", "Assets/Audio/Music/gameplay_theme.wav");
        // LoadMusicTrack("boss", "Assets/Audio/Music/boss_theme.wav");

        Console.WriteLine("[MusicManager] Initialized");
    }

    private void ConfigureMusicSource(AudioSource source)
    {
        source.SpatialBlend = 0.0f; // Son 2D
        source.Category = AudioCategory.Music;
        source.Loop = true;
        source.PlayOnAwake = false;
    }

    /// <summary>
    /// Charge une piste musicale
    /// </summary>
    public void LoadMusicTrack(string trackName, string filePath)
    {
        var clip = AudioImporter.LoadClip(filePath);
        if (clip != null)
        {
            musicTracks[trackName] = clip;
            Console.WriteLine($"[MusicManager] Loaded track: {trackName}");
        }
    }

    /// <summary>
    /// Joue une piste immédiatement (sans crossfade)
    /// </summary>
    public void PlayImmediate(string trackName, float volume = 0.5f)
    {
        if (!musicTracks.TryGetValue(trackName, out var clip))
        {
            Console.WriteLine($"[MusicManager] Track not found: {trackName}");
            return;
        }

        StopAll();

        if (currentMusicSource != null)
        {
            currentMusicSource.Clip = clip;
            currentMusicSource.Volume = volume;
            currentMusicSource.Play();
            currentTrack = trackName;
        }
    }

    /// <summary>
    /// Transition vers une nouvelle piste avec crossfade
    /// </summary>
    public void CrossfadeTo(string trackName, float duration = 2.0f, float targetVolume = 0.5f)
    {
        if (!musicTracks.TryGetValue(trackName, out var clip))
        {
            Console.WriteLine($"[MusicManager] Track not found: {trackName}");
            return;
        }

        if (trackName == currentTrack)
        {
            Console.WriteLine("[MusicManager] Already playing this track");
            return;
        }

        // Préparer le crossfade
        if (nextMusicSource != null && currentMusicSource != null)
        {
            nextMusicSource.Clip = clip;
            nextMusicSource.Volume = 0f;
            nextMusicSource.Play();

            isCrossfading = true;
            crossfadeTimer = 0f;
            crossfadeDuration = duration;
        }

        Console.WriteLine($"[MusicManager] Crossfading to: {trackName}");
    }

    public override void Update(float dt)
    {
        if (!isCrossfading || currentMusicSource == null || nextMusicSource == null)
            return;

        crossfadeTimer += dt;
        float t = Math.Clamp(crossfadeTimer / crossfadeDuration, 0f, 1f);

        // Fade out current, fade in next
        currentMusicSource.Volume = (1f - t) * 0.5f;
        nextMusicSource.Volume = t * 0.5f;

        // Crossfade terminé
        if (t >= 1f)
        {
            currentMusicSource.Stop();

            // Swap les sources
            var temp = currentMusicSource;
            currentMusicSource = nextMusicSource;
            nextMusicSource = temp;

            isCrossfading = false;
            Console.WriteLine("[MusicManager] Crossfade complete");
        }
    }

    /// <summary>
    /// Arrête toute la musique
    /// </summary>
    public void StopAll()
    {
        currentMusicSource?.Stop();
        nextMusicSource?.Stop();
        isCrossfading = false;
        currentTrack = null;
    }

    /// <summary>
    /// Fade out progressif
    /// </summary>
    public void FadeOut(float duration = 2.0f)
    {
        // TODO: Implémenter un fade out progressif
        Console.WriteLine($"[MusicManager] Fading out over {duration}s");
    }

    /// <summary>
    /// Définit le volume de la musique
    /// </summary>
    public void SetVolume(float volume)
    {
        if (currentMusicSource != null)
            currentMusicSource.Volume = volume;
    }
}
