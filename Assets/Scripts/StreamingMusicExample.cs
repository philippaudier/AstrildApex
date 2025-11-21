using System;
using Engine.Scripting;
using Engine.Audio.Assets;
using Engine.Audio.Components;
using OpenTK.Audio.OpenAL;
using Serilog;

/// <summary>
/// Exemple d'utilisation du streaming audio pour un fichier MP3 long
/// Testez ce script avec votre fichier MP3 !
/// </summary>
public class StreamingMusicExample : MonoBehaviour
{
    private StreamingAudioClip? streamingClip;
    private int audioSourceId = -1;
    private bool isPlaying = false;

    // Configurez le chemin vers votre fichier MP3 ici
    private string musicFilePath = "Assets/Audio/Music/your_long_song.mp3";

    public override void Start()
    {
        Console.WriteLine("[StreamingMusicExample] Initializing...");

        // Charger le clip en streaming
        streamingClip = AudioImporter.LoadStreamingClip(musicFilePath);

        if (streamingClip == null)
        {
            Log.Error($"[StreamingMusicExample] Failed to load: {musicFilePath}");
            Log.Information("[StreamingMusicExample] Make sure the file exists and is a valid MP3/OGG/WAV file");
            return;
        }

        // Créer une source OpenAL
        audioSourceId = AL.GenSource();

        // Configurer la source (musique 2D)
        AL.Source(audioSourceId, ALSourceb.SourceRelative, true); // 2D
        AL.Source(audioSourceId, ALSource3f.Position, 0f, 0f, 0f);
        AL.Source(audioSourceId, ALSourcef.Gain, 0.5f); // Volume à 50%
        AL.Source(audioSourceId, ALSourceb.Looping, false); // Le streaming gère le loop lui-même

        Console.WriteLine($"[StreamingMusicExample] Loaded: {streamingClip.Name}");
        Console.WriteLine($"[StreamingMusicExample] Duration: {streamingClip.Length:F2}s");
        Console.WriteLine($"[StreamingMusicExample] Format: {streamingClip.Format}, {streamingClip.Frequency}Hz, {streamingClip.Channels}ch");
        Console.WriteLine("[StreamingMusicExample] Ready to play!");
    }

    public override void Update(float dt)
    {
        // Démarrer automatiquement la lecture
        if (!isPlaying && streamingClip != null && audioSourceId != -1)
        {
            Play();
        }

        // Afficher le statut périodiquement
        if (isPlaying && Time.FrameCount % 300 == 0) // Toutes les ~5 secondes à 60 FPS
        {
            AL.GetSource(audioSourceId, ALGetSourcei.SourceState, out int state);
            string stateStr = state switch
            {
                (int)ALSourceState.Playing => "Playing",
                (int)ALSourceState.Paused => "Paused",
                (int)ALSourceState.Stopped => "Stopped",
                (int)ALSourceState.Initial => "Initial",
                _ => "Unknown"
            };

            AL.GetSource(audioSourceId, ALGetSourcei.BuffersQueued, out int queued);
            AL.GetSource(audioSourceId, ALGetSourcei.BuffersProcessed, out int processed);

            Console.WriteLine($"[StreamingMusicExample] State: {stateStr}, Queued: {queued}, Processed: {processed}");
        }
    }

    /// <summary>
    /// Démarre la lecture
    /// </summary>
    public void Play()
    {
        if (streamingClip == null || audioSourceId == -1)
        {
            Console.WriteLine("[StreamingMusicExample] Cannot play - not initialized");
            return;
        }

        if (isPlaying)
        {
            Console.WriteLine("[StreamingMusicExample] Already playing");
            return;
        }

        Console.WriteLine("[StreamingMusicExample] Starting playback...");

        // Démarrer le streaming
        streamingClip.StartStreaming(audioSourceId);

        // Démarrer la lecture
        AL.SourcePlay(audioSourceId);

        isPlaying = true;
        Console.WriteLine("[StreamingMusicExample] Playback started! 🎵");
    }

    /// <summary>
    /// Arrête la lecture
    /// </summary>
    public void Stop()
    {
        if (!isPlaying || streamingClip == null)
            return;

        Console.WriteLine("[StreamingMusicExample] Stopping...");

        streamingClip.StopStreaming();
        AL.SourceStop(audioSourceId);

        isPlaying = false;
        Console.WriteLine("[StreamingMusicExample] Stopped");
    }

    /// <summary>
    /// Pause/Resume
    /// </summary>
    public void TogglePause()
    {
        if (audioSourceId == -1)
            return;

        AL.GetSource(audioSourceId, ALGetSourcei.SourceState, out int state);

        if (state == (int)ALSourceState.Playing)
        {
            AL.SourcePause(audioSourceId);
            Console.WriteLine("[StreamingMusicExample] Paused");
        }
        else if (state == (int)ALSourceState.Paused)
        {
            AL.SourcePlay(audioSourceId);
            Console.WriteLine("[StreamingMusicExample] Resumed");
        }
    }

    /// <summary>
    /// Change le volume
    /// </summary>
    public void SetVolume(float volume)
    {
        if (audioSourceId == -1)
            return;

        AL.Source(audioSourceId, ALSourcef.Gain, Math.Clamp(volume, 0f, 1f));
        Console.WriteLine($"[StreamingMusicExample] Volume set to {volume:F2}");
    }

    public override void OnDestroy()
    {
        Stop();

        if (audioSourceId != -1)
        {
            AL.DeleteSource(audioSourceId);
            audioSourceId = -1;
        }

        streamingClip?.Dispose();
        streamingClip = null;

        Console.WriteLine("[StreamingMusicExample] Cleaned up");
    }
}

// HELPER: Classe Time factice si elle n'existe pas encore dans votre moteur
public static class Time
{
    public static int FrameCount { get; set; } = 0;
}
