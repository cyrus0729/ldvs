using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace ldvs.Core.Content.Entities;

public sealed class _osu : ISongParser
{
    private const string GeneralSection = "General";
    private const string MetadataSection = "Metadata";
    private const string DifficultySection = "Difficulty";
    private const string TimingPointsSection = "TimingPoints";
    private const string HitObjectsSection = "HitObjects";
    private const string EventsSection = "Events";

    public static Beatmap Parse(string text, BeatmapSet beatmapSet)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(beatmapSet);

        var map = new Beatmap();
        string? section = null;

        foreach (var rawLine in text.Split(
                     ["\r\n", "\n"],
                     StringSplitOptions.None))
        {
            var line = rawLine.Trim();

            if (IsIgnorableLine(line))
                continue;

            if (TryParseSection(line, out var parsedSection))
            {
                section = parsedSection;
                continue;
            }

            if (section is not null)
                ParseLine(section, line, map, beatmapSet);
        }

        return map;
    }

    private static bool IsIgnorableLine(string line) =>
        string.IsNullOrWhiteSpace(line) ||
        line.StartsWith("//", StringComparison.Ordinal);

    private static void ParseBackgroundEvent(string[] parts, List<Mod> mods)
    {
        if (parts.Length < 3 || !SPHelper.TryParseInt(parts[1], out var startTime))
        { return; }

        SPHelper.TryParseInt(parts[3], out var x);
        SPHelper.TryParseInt(parts[4], out var y);
        var extra = 0;
        if (parts.Length>5) { SPHelper.TryParseInt(parts[5], out extra); }

        mods.Add(new ModImplementations.BGImage(startTime,parts[2],x,y,extra));
    }

    private static void ParseDifficultyLine(
        string line,
        BMPData metadata)
    {
        if (!SPHelper.TryParseKeyValue(line, out var key, out var value))
            return;

        if (key == "CircleSize" &&
            SPHelper.TryParseInt(value, out var keyCount))
        {
            metadata.OMKeyCount = keyCount;
        }
    }

    private static void ParseEventLine(string line, Beatmap map)
    {
        var parts = SPHelper.SplitCSVLine(line);

        if (parts.Length == 0)
            return;

        switch (parts[0])
        {
            case "0":
                ParseBackgroundEvent(parts, map.Mods);
                break;

            case "1":
                ParseVideoEvent(parts, map.Mods);
                break;

            case "3":
                ParseSampleEvent(parts, map.Mods);
                break;
        }
    }

    private static void ParseGeneralLine(
        string line,
        General general)
    {
        if (!SPHelper.TryParseKeyValue(line, out var key, out var value))
            return;

        switch (key)
        {
            case "AudioFilename":
                general.AudioFilename = value;
                break;

            case "AudioLeadIn":
                if (SPHelper.TryParseInt(value, out var leadIn))
                    general.AudioLeadIn = leadIn;
                break;

            case "PreviewTime":
                if (SPHelper.TryParseInt(value, out var previewTime))
                    general.PreviewPoint = previewTime;
                break;
        }
    }

    private static void ParseHitObjectLine(string line, Beatmap map)
    {
        var parts = SPHelper.SplitCSVLine(line);

        // x, y, time, type, hitSound (used as note type in this)
        if (parts.Length < 6 || !SPHelper.TryParseInt(parts[0], out var x) ||
            !SPHelper.TryParseInt(parts[2], out var time) ||
            !SPHelper.TryParseInt(parts[3], out var LNVar))
        {
            return;
        }

        if (!TryGetLane(x, map.BMPData.OMKeyCount, out var lane))
            return;

        var isHoldNote = (LNVar & 128) != 0;

        int? endTime = null;
        int hitSound = 0;

        var parameters = parts[5].Split(':');

        if (parameters.Length > 0 &&
            SPHelper.TryParseInt(parameters[0], out var parsed))
        {
            if (isHoldNote)
                endTime = parsed;
            else
                hitSound = parsed;
        }
        map.HitObjects.Add(
            new HitObject
            {
                Lane = lane,
                Time = time,
                EndTime = endTime,
                Type = hitSound
            });
    }

    private static void ParseLine(string section, string line, Beatmap map, BeatmapSet beatmapSet)
    {
        switch (section)
        {
            case GeneralSection:
                ParseGeneralLine(line, map.General);
                break;

            case MetadataSection:
                ParseMetadataLine(line, map.BMPData, beatmapSet.Metadata);
                break;

            case DifficultySection:
                ParseDifficultyLine(line, map.BMPData);
                break;

            case TimingPointsSection:
                ParseTimingPointLine(line, map);
                break;

            case HitObjectsSection:
                ParseHitObjectLine(line, map);
                break;

            case EventsSection:
                ParseEventLine(line, map);
                break;
        }
    }

    private static void ParseMetadataLine(string line, BMPData mapMetadata,BMSData songMetadata)
    {
        if (!SPHelper.TryParseKeyValue(line, out var key, out var value))
            return;

        switch (key)
        {
            case "Title":
                songMetadata.Title = value;
                break;

            case "TitleUnicode":
                songMetadata.TitleUnicode = value;
                break;

            case "Artist":
                songMetadata.Artist = value;
                break;

            case "ArtistUnicode":
                songMetadata.ArtistUnicode = value;
                break;

            case "Creator":
                mapMetadata.Creator = value;
                break;

            case "CreatorUnicode":
                mapMetadata.CreatorUnicode = value;
                break;

            case "Source":
                songMetadata.Source = value;
                break;

            case "Tags":
                songMetadata.Tags = value
                    .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                    .ToList();
                break;

            case "Version":
                ParseVersion(value, mapMetadata);
                break;
        }
    }

    private static void ParseSampleEvent(string[] parts, List<Mod> mods)
    {
        if (parts.Length < 4 || !SPHelper.TryParseInt(parts[1], out var time))
        { return; }

        SPHelper.TryParseInt(parts[2], out var layer);
        SPHelper.TryParseInt(parts[4], out var volume);

        mods.Add(new ModImplementations.Sample(time,parts[3],layer,volume));
    }

    private static void ParseTimingPointLine(string line, Beatmap map)
    {
        var parts = SPHelper.SplitCSVLine(line);

        if (parts.Length < 7 ||
            !SPHelper.TryParseDouble(parts[0], out var offset) ||
            !SPHelper.TryParseDouble(parts[1], out var beatLength) ||
            !SPHelper.TryParseInt(parts[2], out var meter) ||
            !SPHelper.TryParseInt(parts[6], out var uninheritedValue))
        { return; }

        if (uninheritedValue==1)
        {
            map.BPMList.Add(offset, beatLength);
        }
        else
        {
            map.Mods.Add(ModRegistry.Create("globalscrollspeed", offset, double.PositiveInfinity, (float)(100.0 / Math.Abs(beatLength))));
        }
    }

    private static void ParseVersion(
        string value,
        BMPData metadata)
    {
        var parts = value.Split(
            ' ',
            StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length == 0)
            return;

        if (SPHelper.TryParseInt(parts[0], out var difficulty))
        {
            metadata.Difficulty =
                difficulty.ToString(CultureInfo.InvariantCulture);

            metadata.DifficultyName = string.Join(' ', parts.Skip(1));
        }
        else
        {
            metadata.Difficulty = "0";
            metadata.DifficultyName = value;
        }
    }

    private static void ParseVideoEvent(string[] parts, List<Mod> mods)
    {
        if (parts.Length < 3 || !SPHelper.TryParseInt(parts[1], out var startTime))
        { return; }

        mods.Add(new ModImplementations.Video(startTime, parts[2]));
    }

    private static bool TryGetLane(int x,
        int laneCount,
        out int lane)
    {
        lane = -1;

        if (laneCount <= 0 || x is < 0 or >= 512)
            return false;

        lane = Math.Clamp(
            x * laneCount / 512,
            0,
            laneCount - 1);

        return true;
    }

    private static bool TryParseSection(
        string line,
        out string section)
    {
        section = string.Empty;

        if (line.Length < 3 ||
            line[0] != '[' ||
            line[^1] != ']')
        {
            return false;
        }

        section = line[1..^1].Trim();
        return section.Length > 0;
    }
}