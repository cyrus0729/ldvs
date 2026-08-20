using System.Diagnostics;
using MonoSound;
using MonoSound.Streaming;
using System;
using System.IO;

namespace ldvs.Core.Content.Entities;

public static class AudioTypeDetector
{
    public static AudioType Detect(Stream stream)
    {
        if (stream == null)
            throw new ArgumentNullException(nameof(stream));

        if (!stream.CanRead)
            throw new ArgumentException("strean not readable 3:", nameof(stream));

        if (!stream.CanSeek)
            throw new NotSupportedException(
                "stream no seek");

        long originalPosition = stream.Position;

        try
        {
            Span<byte> header = stackalloc byte[12];
            int count = stream.Read(header);

            // XNB files begin with "XNB"
            if (count >= 3 &&
                header[0] == (byte)'X' &&
                header[1] == (byte)'N' &&
                header[2] == (byte)'B')
            {
                return AudioType.XNB;
            }

            // XACT wave banks mostly begin with "WBND"
            if (count >= 4 &&
                header[0] == (byte)'W' &&
                header[1] == (byte)'B' &&
                header[2] == (byte)'N' &&
                header[3] == (byte)'D')
            {
                return AudioType.XWB;
            }

            // WAV: RIFF....WAVE
            if (count >= 12 &&
                header[0] == (byte)'R' &&
                header[1] == (byte)'I' &&
                header[2] == (byte)'F' &&
                header[3] == (byte)'F' &&
                header[8] == (byte)'W' &&
                header[9] == (byte)'A' &&
                header[10] == (byte)'V' &&
                header[11] == (byte)'E')
            {
                return AudioType.WAV;
            }

            // OGG Vorbis
            if (count >= 4 &&
                header[0] == (byte)'O' &&
                header[1] == (byte)'g' &&
                header[2] == (byte)'g' &&
                header[3] == (byte)'S')
            {
                return AudioType.OGG;
            }

            // MP3 with ID3 metadata
            if (count >= 3 &&
                header[0] == (byte)'I' &&
                header[1] == (byte)'D' &&
                header[2] == (byte)'3')
            {
                return AudioType.MP3;
            }

            // why does mp3 have to be like this
            if (count >= 2 &&
                header[0] == 0xFF &&
                (header[1] & 0xE0) == 0xE0)
            {
                return AudioType.MP3;
            }

            return AudioType.Custom;
        }
        finally
        {
            stream.Position = originalPosition;
        }
    }
}


public class Conductor : IConductor
{
    private readonly Stopwatch _songSw = new();
    private TimeSpan _songBasePos = TimeSpan.Zero;

    public double SongOffset { get; set; }

    private StreamPackage _currentBGM;

    public double SongPositionMs { get; private set; }
    public double SongPositionOffsetMs { get; private set; }

    public void Start(BeatmapSet set, Beatmap map)
    {
        var filePath = Path.Combine(set.FolderPath, map.General.AudioFilename);
        SongOffset = map.General.AudioLeadIn;

        SongPositionOffsetMs = 0;
        SongPositionMs = 0;

        byte[] audioBytes = File.ReadAllBytes(filePath);
        Stream audioStream = new MemoryStream(audioBytes);
        AudioType type = AudioTypeDetector.Detect(audioStream);
        if (type == AudioType.Custom)
        {
            throw new ArgumentOutOfRangeException($"{filePath} isnt a valid audio format right now!!! >:(");
        }
        _currentBGM = StreamLoader.GetStreamedSound(audioStream, type, looping: false);
        _songBasePos = TimeSpan.Zero;

        _songSw.Restart();
        _currentBGM.Play();
    }

    public void Update()
    {
        if (_songSw.IsRunning)
        {
            // i hate this i hate this i hate this
            var pos = _songBasePos + TimeSpan.FromSeconds(_songSw.Elapsed.TotalSeconds);

            SongPositionMs = pos.TotalMilliseconds;
            SongPositionOffsetMs = SongPositionMs + SongOffset; // something might use this so
        }
    }

    public void Stop()
    {
        _currentBGM.Stop();
        _currentBGM = null;
        _songSw.Stop();
        _songBasePos = TimeSpan.Zero;
    }
}

public interface IConductor
{
    double SongPositionMs { get; }
}