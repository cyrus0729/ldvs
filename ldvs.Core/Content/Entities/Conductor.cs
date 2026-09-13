using System.Collections;
using System.Collections.Generic;
using Microsoft.Xna.Framework.Media;
using System;
using System.Diagnostics;
using System.IO;
using Microsoft.Xna.Framework;

namespace ldvs.Core.Content.Entities;

public interface IConductor
{
    double VS_OffsetMs { get; }
    double SongPositionMs { get; }
    double BaseBPM { get; }
    double CurrentBPM { get; }
    double CurrentBeat { get; }
}

// bpm changes!!
public sealed record TempoChange(double beat, double bpm);

public sealed class TempoMap : IEnumerable<TempoChange>
{
    public readonly List<TempoChange> _changes = [];

    public TempoMap(double initialBpm)
    {
        Add(0, initialBpm);
    }

    public void Add(double beat, double bpm)
    {
        if (bpm <= 0)
            throw new ArgumentOutOfRangeException(nameof(bpm));

        _changes.Add(new TempoChange(beat, bpm));
        _changes.Sort(static (a, b) => a.beat.CompareTo(b.beat));
    }

    public double BEATtoMS(double targetBeat)
    {
        double timeMs = 0;

        for (int i = 0; i < _changes.Count; i++)
        {
            TempoChange current = _changes[i];

            double nextBeat =
                i + 1 < _changes.Count
                    ? _changes[i + 1].beat
                    : double.PositiveInfinity;

            double segmentEndBeat =
                Math.Min(targetBeat, nextBeat);

            if (segmentEndBeat <= current.beat)
                break;

            double beatsInSegment =
                segmentEndBeat - current.beat;

            timeMs += beatsInSegment * 60_000.0 / current.bpm;

            if (targetBeat < nextBeat)
                break;
        }

        return timeMs;
    }

    public double BPMatBEAT(double beat)
    {
        TempoChange current = _changes[0];

        foreach (var change in _changes)
        {
            if (change.beat > beat)
                break;

            current = change;
        }

        return current.bpm;
    }

    public double BPMatMS(double songTimeMs)
    {
        double accumulatedMs = 0;

        for (int i = 0; i < _changes.Count; i++)
        {
            TempoChange current = _changes[i];

            double nextBeat =
                i + 1 < _changes.Count
                    ? _changes[i + 1].beat
                    : double.PositiveInfinity;

            double segmentMs =
                double.IsPositiveInfinity(nextBeat)
                    ? double.PositiveInfinity
                    : (nextBeat - current.beat)
                        * 60_000.0
                        / current.bpm;

            if (songTimeMs < accumulatedMs + segmentMs)
                return current.bpm;

            accumulatedMs += segmentMs;
        }

        return _changes[^1].bpm;
    }

    public IEnumerator<TempoChange> GetEnumerator()
    {
        return _changes.GetEnumerator();
    }

    public double MStoBEAT(double songTimeMs)
    {
        double accumulatedMs = 0;

        for (int i = 0; i < _changes.Count; i++)
        {
            TempoChange current = _changes[i];

            double nextBeat =
                i + 1 < _changes.Count
                    ? _changes[i + 1].beat
                    : double.PositiveInfinity;

            double nextSegmentMs =
                double.IsPositiveInfinity(nextBeat)
                    ? double.PositiveInfinity
                    : (nextBeat - current.beat)
                        * 60_000.0
                        / current.bpm;

            if (songTimeMs <= accumulatedMs + nextSegmentMs)
            {
                double localMs = songTimeMs - accumulatedMs;

                return current.beat +
                    localMs * current.bpm / 60_000.0;
            }

            accumulatedMs += nextSegmentMs;
        }

        return 0;
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}

// one conductor for all simultaneous playfields
public sealed class Conductor : IConductor
{
    private long _startTimestamp;
    private long _pauseTimestamp;

    private double _clockStartMs = -2000.0;
    private bool _audioStarted;
    private bool _paused; // todo make global
    private Song? _currentBGM;
    private TempoMap _tempoMap = new(120);

    public double SongOffset { get; private set; }

    public double SongPositionMs { get; private set; }
    public double CurrentBPM { get; private set; } = 120;
    public double BaseBPM { get; private set; } = 120;
    public double CurrentBeat { get; private set; }
    public double VS_OffsetMs { get; private set; }

    public void Pause()
    {
        if (_paused)
            return;

        _pauseTimestamp = Stopwatch.GetTimestamp();
        _paused = true;

        if (_audioStarted)
            MediaPlayer.Pause();
    }

    public void Resume()
    {
        if (!_paused)
            return;

        long now = Stopwatch.GetTimestamp();
        long pausedDuration = now - _pauseTimestamp;
        _startTimestamp += pausedDuration;
        _paused = false;

        if (_audioStarted)
            MediaPlayer.Resume();
    }

    public void Start(BeatmapSet set, Beatmap map)
    {
        Stop();
        SongOffset = map.General.AudioLeadIn;

        _tempoMap = new TempoMap(120);
        foreach (TempoChange point in map.BPMList)
            _tempoMap.Add(point.beat, point.bpm);

        string audioPath = Path.Combine(set.FolderPath, map.General.AudioFilename);
        _currentBGM = Song.FromUri("mapsong", new Uri(StupidFuckingOgg(audioPath)));

        _clockStartMs = -2000.0; // yes its a cheap trick but who gives a damn

        _startTimestamp = Stopwatch.GetTimestamp();
        _audioStarted = false;
        _paused = false;

        SongPositionMs = -2000.0;
        CurrentBeat = 0;
        BaseBPM = CurrentBPM = _tempoMap._changes[0].bpm;
        VS_OffsetMs = SongOffset - SongPositionMs;
    }

    public void Stop()
    {
        MediaPlayer.Stop();

        _currentBGM?.Dispose();
        _currentBGM = null;

        _audioStarted = false;
        _paused = false;

        _clockStartMs = -2000.0;
        _startTimestamp = 0;

        SongPositionMs = -2000.0;
        CurrentBeat = 0;
        CurrentBPM = _tempoMap.BPMatMS(0);
        VS_OffsetMs = SongOffset - SongPositionMs;
    }

    public static string StupidFuckingOgg(string inputPath) // im ngl this sucks ass
    {
        if (File.Exists(Path.ChangeExtension(inputPath, ".ogg")))
            return Path.ChangeExtension(inputPath, ".ogg");
        if (!File.Exists(inputPath))
            throw new FileNotFoundException("Audio file not found.", inputPath);

        if (string.Equals(Path.GetExtension(inputPath), ".ogg", StringComparison.OrdinalIgnoreCase))
            return inputPath;

        string outputPath = Path.Combine(
            Path.GetDirectoryName(inputPath)!,
            Path.GetFileNameWithoutExtension(inputPath) + ".ogg");

        var startInfo = new ProcessStartInfo
        {
            FileName = "ffmpeg",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            Arguments =
                $"-y -i \"{inputPath}\" -vn -c:a libvorbis -q:a 5 \"{outputPath}\""
        };

        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Could not start FFmpeg.");

        string errors = process.StandardError.ReadToEnd();
        process.WaitForExit();

        if (process.ExitCode != 0 || !File.Exists(outputPath))
            throw new InvalidOperationException($"FFmpeg conversion failed:\n{errors}");

        return outputPath;
    }

    public void Update()
    {
        if (_paused)
            return;

        long now = Stopwatch.GetTimestamp();
        double elapsedMs = TimestampToMilliseconds(now - _startTimestamp);

        SongPositionMs = _clockStartMs + elapsedMs;
        if (!_audioStarted && SongPositionMs >= 0.0)
        {
            _audioStarted = true;
            MediaPlayer.Play(_currentBGM);
        }

        double musicMs = Math.Max(0.0, SongPositionMs);
        CurrentBeat = _tempoMap.MStoBEAT(musicMs);
        CurrentBPM = _tempoMap.BPMatMS(musicMs);
        VS_OffsetMs = SongOffset - SongPositionMs;
    }

    private static double TimestampToMilliseconds(long timestamp)
    {
        return timestamp * 1000.0 / Stopwatch.Frequency;
    }
}