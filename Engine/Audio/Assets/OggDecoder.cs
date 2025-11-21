using System;
using System.IO;
using NVorbis;
using Serilog;

namespace Engine.Audio.Assets
{
    /// <summary>
    /// Décodeur OGG Vorbis utilisant NVorbis
    /// </summary>
    public sealed class OggDecoder : IAudioDecoder
    {
        private readonly string _filePath;
        private VorbisReader? _vorbisReader;
        private float[]? _floatBuffer;

        public int SampleRate { get; private set; }
        public int Channels { get; private set; }
        public float TotalTime { get; private set; }
        public bool IsLooping { get; set; }

        public OggDecoder(string filePath)
        {
            _filePath = filePath;

            try
            {
                _vorbisReader = new VorbisReader(filePath);
                SampleRate = _vorbisReader.SampleRate;
                Channels = _vorbisReader.Channels;
                TotalTime = (float)_vorbisReader.TotalTime.TotalSeconds;

                Log.Information($"[OggDecoder] Loaded OGG: {Path.GetFileName(filePath)} - {TotalTime:F2}s, {SampleRate}Hz, {Channels}ch");
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"[OggDecoder] Failed to load: {filePath}");
                throw;
            }
        }

        public int ReadSamples(short[] buffer, int offset, int count)
        {
            if (_vorbisReader == null)
                return 0;

            try
            {
                // NVorbis retourne des floats, on doit convertir en short (PCM 16-bit)
                if (_floatBuffer == null || _floatBuffer.Length < count)
                {
                    _floatBuffer = new float[count];
                }

                int samplesRead = _vorbisReader.ReadSamples(_floatBuffer, 0, count);

                // Convertir float [-1.0, 1.0] vers short [-32768, 32767]
                for (int i = 0; i < samplesRead; i++)
                {
                    float sample = Math.Clamp(_floatBuffer[i], -1f, 1f);
                    buffer[offset + i] = (short)(sample * 32767f);
                }

                return samplesRead;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[OggDecoder] Error reading samples");
                return 0;
            }
        }

        public void Reset()
        {
            if (_vorbisReader == null)
                return;

            try
            {
                // Revenir au début du stream
                _vorbisReader.SeekTo(0);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[OggDecoder] Failed to reset");
            }
        }

        public void Dispose()
        {
            _vorbisReader?.Dispose();
            _vorbisReader = null;
            _floatBuffer = null;
        }
    }
}
