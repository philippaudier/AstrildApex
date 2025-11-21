using System;
using System.IO;
using Serilog;

namespace Engine.Audio.Assets
{
    /// <summary>
    /// Décodeur WAV pour le streaming (fichiers WAV longs)
    /// </summary>
    public sealed class WavDecoder : IAudioDecoder
    {
        private readonly string _filePath;
        private FileStream? _fileStream;
        private BinaryReader? _reader;
        private long _dataStartPosition;
        private long _dataLength;

        public int SampleRate { get; private set; }
        public int Channels { get; private set; }
        public float TotalTime { get; private set; }
        public bool IsLooping { get; set; }

        private int _bitsPerSample;
        private int _bytesPerSample;

        public WavDecoder(string filePath)
        {
            _filePath = filePath;

            try
            {
                _fileStream = File.OpenRead(filePath);
                _reader = new BinaryReader(_fileStream);

                // Lire l'en-tête WAV
                ParseWavHeader();

                _bytesPerSample = _bitsPerSample / 8;

                // Calculer la durée
                long totalSamples = _dataLength / (_bytesPerSample * Channels);
                TotalTime = (float)totalSamples / SampleRate;

                Log.Information($"[WavDecoder] Loaded WAV: {Path.GetFileName(filePath)} - {TotalTime:F2}s, {SampleRate}Hz, {Channels}ch");
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"[WavDecoder] Failed to load: {filePath}");
                throw;
            }
        }

        private void ParseWavHeader()
        {
            if (_reader == null)
                throw new Exception("Reader not initialized");

            // Lire l'en-tête RIFF
            string signature = new string(_reader.ReadChars(4));
            if (signature != "RIFF")
                throw new Exception("Invalid WAV file - missing RIFF signature");

            _reader.ReadInt32(); // ChunkSize
            string format = new string(_reader.ReadChars(4));
            if (format != "WAVE")
                throw new Exception("Invalid WAV file - missing WAVE format");

            // Lire le chunk fmt
            string fmtSignature = new string(_reader.ReadChars(4));
            if (fmtSignature != "fmt ")
                throw new Exception("Invalid WAV file - missing fmt chunk");

            int fmtChunkSize = _reader.ReadInt32();
            int audioFormat = _reader.ReadInt16(); // 1 = PCM
            Channels = _reader.ReadInt16();
            SampleRate = _reader.ReadInt32();
            _reader.ReadInt32(); // ByteRate
            _reader.ReadInt16(); // BlockAlign
            _bitsPerSample = _reader.ReadInt16();

            if (audioFormat != 1)
                throw new Exception($"Unsupported WAV format: {audioFormat} (only PCM is supported)");

            // Skip extra fmt bytes if any
            if (fmtChunkSize > 16)
                _reader.ReadBytes(fmtChunkSize - 16);

            // Trouver le chunk data
            while (true)
            {
                string dataSignature = new string(_reader.ReadChars(4));
                int dataSize = _reader.ReadInt32();

                if (dataSignature == "data")
                {
                    _dataStartPosition = _fileStream!.Position;
                    _dataLength = dataSize;
                    break;
                }

                // Skip unknown chunks
                _reader.ReadBytes(dataSize);
            }
        }

        public int ReadSamples(short[] buffer, int offset, int count)
        {
            if (_reader == null || _fileStream == null)
                return 0;

            try
            {
                int samplesRead = 0;

                // Vérifier si on a atteint la fin
                if (_fileStream.Position >= _dataStartPosition + _dataLength)
                    return 0;

                // Lire les samples
                if (_bitsPerSample == 16)
                {
                    // Déjà en 16-bit, lecture directe
                    for (int i = 0; i < count && _fileStream.Position < _dataStartPosition + _dataLength; i++)
                    {
                        buffer[offset + i] = _reader.ReadInt16();
                        samplesRead++;
                    }
                }
                else if (_bitsPerSample == 8)
                {
                    // Convertir 8-bit unsigned vers 16-bit signed
                    for (int i = 0; i < count && _fileStream.Position < _dataStartPosition + _dataLength; i++)
                    {
                        byte sample8 = _reader.ReadByte();
                        buffer[offset + i] = (short)((sample8 - 128) * 256);
                        samplesRead++;
                    }
                }

                return samplesRead;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[WavDecoder] Error reading samples");
                return 0;
            }
        }

        public void Reset()
        {
            if (_fileStream == null)
                return;

            try
            {
                _fileStream.Seek(_dataStartPosition, SeekOrigin.Begin);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[WavDecoder] Failed to reset");
            }
        }

        public void Dispose()
        {
            _reader?.Dispose();
            _reader = null;
            _fileStream?.Dispose();
            _fileStream = null;
        }
    }
}
