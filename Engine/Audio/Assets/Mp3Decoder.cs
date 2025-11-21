using System;
using System.IO;
using NLayer;
using Serilog;

namespace Engine.Audio.Assets
{
    /// <summary>
    /// Décodeur MP3 utilisant NLayer
    /// </summary>
    public sealed class Mp3Decoder : IAudioDecoder
    {
        private readonly string _filePath;
        private MpegFile? _mpegFile;
        private float[]? _floatBuffer;

        public int SampleRate { get; private set; }
        public int Channels { get; private set; }
        public float TotalTime { get; private set; }
        public bool IsLooping { get; set; }

        public Mp3Decoder(string filePath)
        {
            _filePath = filePath;

            try
            {
                _mpegFile = new MpegFile(filePath);
                SampleRate = _mpegFile.SampleRate;
                Channels = _mpegFile.Channels;

                // Calculer la durée totale
                long totalSamples = _mpegFile.Length / sizeof(float) / Channels;
                TotalTime = (float)totalSamples / SampleRate;

                Log.Information($"[Mp3Decoder] Loaded MP3: {Path.GetFileName(filePath)} - {TotalTime:F2}s, {SampleRate}Hz, {Channels}ch");
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"[Mp3Decoder] Failed to load: {filePath}");
                throw;
            }
        }

        public int ReadSamples(short[] buffer, int offset, int count)
        {
            if (_mpegFile == null)
                return 0;

            try
            {
                // NLayer retourne des floats, on doit convertir en short (PCM 16-bit)
                if (_floatBuffer == null || _floatBuffer.Length < count)
                {
                    _floatBuffer = new float[count];
                }

                int samplesRead = _mpegFile.ReadSamples(_floatBuffer, 0, count);

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
                Log.Error(ex, "[Mp3Decoder] Error reading samples");
                return 0;
            }
        }

        public void Reset()
        {
            if (_mpegFile == null)
                return;

            try
            {
                // Recréer le fichier MPEG pour revenir au début
                _mpegFile.Dispose();
                _mpegFile = new MpegFile(_filePath);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[Mp3Decoder] Failed to reset");
            }
        }

        public void Dispose()
        {
            _mpegFile?.Dispose();
            _mpegFile = null;
            _floatBuffer = null;
        }
    }
}
